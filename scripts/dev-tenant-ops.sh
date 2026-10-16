#!/usr/bin/env bash
# 本机租户运营手测环境（S1-QA-012）。说明见 tools/Qian.Dev.TenantOps/README.md。
# 这是开发机上的手测环境：不替代 qian dev（S1-RT-013），也不替代 K3s 部署（S1-DEP-002）。
# 兼容 macOS 自带的 bash 3.2：不用关联数组，也不在 set -u 下展开空数组。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE="${QIAN_DEV_STATE:-$ROOT/.qian-dev/tenant-ops}"
CONTAINER="${QIAN_DEV_PG_CONTAINER:-qian-dev-tenant-ops-pg}"
PG_PORT="${QIAN_DEV_PG_PORT:-55432}"
PG_IMAGE="${QIAN_DEV_PG_IMAGE:-${QIAN_TEST_IMAGE_POSTGRES:-postgres:16-alpine}}"
OPS_URL="${QIAN_DEV_OPS_URL:-http://127.0.0.1:5080}"
PROJECT="$ROOT/tools/Qian.Dev.TenantOps"
DLL="$PROJECT/bin/Debug/net10.0/Qian.Dev.TenantOps.dll"
PASSWORD_FILE="$STATE/pg-admin-password"

usage() {
  cat <<EOF
用法：scripts/dev-tenant-ops.sh <命令>

  up                    起 PostgreSQL 16 容器（只绑 127.0.0.1:$PG_PORT），重建开发库，前台启动运营宿主（$OPS_URL）
  walkthrough [--yes]   另开一个终端运行：按「建租户 → 开通步骤 → 激活 → 写设置/读配额 → 冻结/恢复 → 扫描到期」逐步调用
  api 方法 路径 [JSON]   以平台超管身份调用一次运营宿主，例如：api GET /api/tenancy/tenants
  token [选项]          打印本机开发令牌；选项 --sub、--tenant、--kind、--ttl（分钟），默认是平台超管 root
  status                查看容器与运营宿主是否在运行
  down [--purge]        删除 PostgreSQL 容器；--purge 同时删除本机开发密钥与口令（$STATE）
EOF
}

need() { command -v "$1" >/dev/null 2>&1 || { echo "缺少命令：$1（$2）" >&2; exit 1; }; }

build() {
  need dotnet "安装 .NET 10 SDK，版本见仓库根目录 global.json"
  echo "构建手测宿主 ..."
  dotnet build "$PROJECT/Qian.Dev.TenantOps.csproj" -c Debug -nologo -v quiet
}

ensure_built() { [ -f "$DLL" ] || build >&2; }

container_state() {
  local status
  if status="$(docker inspect -f '{{.State.Status}}' "$CONTAINER" 2>/dev/null)"; then echo "$status"; else echo missing; fi
}

start_postgres() {
  need docker "安装并启动 Docker Desktop 或其他 Docker 引擎"
  docker info >/dev/null 2>&1 || { echo "Docker 引擎没有运行。" >&2; exit 1; }
  mkdir -p "$STATE"; chmod 700 "$STATE"
  local state; state="$(container_state)"
  if [ "$state" != missing ] && [ ! -s "$PASSWORD_FILE" ]; then
    echo "容器 $CONTAINER 已存在但本机没有它的管理员口令，删除后重建。"
    docker rm -f "$CONTAINER" >/dev/null; state=missing
  fi
  if [ "$state" = missing ]; then
    # 管理员口令在本机随机生成，只存在仅本人可读的状态目录里（git 忽略），不进仓库。
    (umask 077; python3 -c 'import secrets; print(secrets.token_hex(24))' > "$PASSWORD_FILE")
    echo "创建 PostgreSQL 容器 $CONTAINER（$PG_IMAGE，127.0.0.1:$PG_PORT）"
    docker run -d --name "$CONTAINER" -p "127.0.0.1:$PG_PORT:5432" \
      -e POSTGRES_USER=qian -e POSTGRES_DB=postgres -e "POSTGRES_PASSWORD=$(cat "$PASSWORD_FILE")" \
      --label qian.dev=tenant-ops "$PG_IMAGE" >/dev/null
  elif [ "$state" != running ]; then
    docker start "$CONTAINER" >/dev/null
  fi
  local i
  for i in $(seq 1 60); do
    if docker exec "$CONTAINER" pg_isready -U qian -d postgres -h 127.0.0.1 >/dev/null 2>&1; then return 0; fi
    sleep 1
  done
  echo "PostgreSQL 60 秒内没有就绪，查看：docker logs $CONTAINER" >&2; exit 1
}

cmd_up() {
  need python3 "macOS 自带 /usr/bin/python3"
  if curl --noproxy '*' -s -o /dev/null --max-time 2 "$OPS_URL/api/tenancy/meta/state-machine"; then
    echo "$OPS_URL 已有服务在监听；先停掉它（或设 QIAN_DEV_OPS_URL 换端口）。" >&2; exit 1
  fi
  start_postgres
  build
  QIAN_DEV_PG_ADMIN="Host=127.0.0.1;Port=$PG_PORT;Username=qian;Password=$(cat "$PASSWORD_FILE");Database=postgres" \
    exec dotnet "$DLL" serve --urls "$OPS_URL" --state "$STATE"
}

cmd_down() {
  if [ "$(container_state)" != missing ]; then docker rm -f "$CONTAINER" >/dev/null; echo "已删除容器 $CONTAINER"; fi
  rm -f "$PASSWORD_FILE"
  if [ "${1:-}" = "--purge" ]; then rm -rf "$STATE"; echo "已删除 $STATE（下次 up 会重新生成开发密钥）"; fi
}

cmd_token() { ensure_built; dotnet "$DLL" token --state "$STATE" "$@"; }

cmd_api() {
  [ $# -ge 2 ] || { echo "用法：api 方法 路径 [JSON 请求体]" >&2; exit 2; }
  local method="$1" path="$2" body="${3:-}" token
  token="$(cmd_token)"
  if [ -n "$body" ]; then
    curl --noproxy '*' -sS -X "$method" "$OPS_URL$path" -H "Authorization: Bearer $token" \
      -H 'Content-Type: application/json' --data "$body" -w '\n%{http_code}\n' | pretty
  else
    curl --noproxy '*' -sS -X "$method" "$OPS_URL$path" -H "Authorization: Bearer $token" -w '\n%{http_code}\n' | pretty
  fi
}

# 最后一行是状态码，其余是响应体；JSON 缩进输出，非 JSON 原样输出。
pretty() {
  python3 -c '
import json, sys
lines = sys.stdin.read().rstrip("\n").split("\n")
status, body = lines[-1], "\n".join(lines[:-1])
print("HTTP " + status)
try:
    print(json.dumps(json.loads(body), ensure_ascii=False, indent=2))
except ValueError:
    print(body) if body else None
'
}

cmd_status() {
  echo "PostgreSQL 容器 $CONTAINER：$(container_state)"
  local code
  code="$(curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 2 "$OPS_URL/api/tenancy/meta/state-machine" || true)"
  case "$code" in
    401) echo "运营宿主 $OPS_URL：运行中" ;;
    000|"") echo "运营宿主 $OPS_URL：未运行" ;;
    *) echo "运营宿主 $OPS_URL：有别的服务在监听（探测返回 $code）" ;;
  esac
}

command="${1:-}"; [ $# -gt 0 ] && shift
case "$command" in
  up) cmd_up ;;
  down) cmd_down "$@" ;;
  token) cmd_token "$@" ;;
  api) cmd_api "$@" ;;
  walkthrough) ensure_built; QIAN_DEV_OPS_URL="$OPS_URL" QIAN_DEV_STATE="$STATE" exec bash "$PROJECT/walkthrough.sh" "$@" ;;
  status) cmd_status ;;
  *) usage; [ -z "$command" ] || exit 2 ;;
esac
