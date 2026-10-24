#!/usr/bin/env bash
# 外壳认证联调环境（S1-YUE-002 / S1-YUE-012）。说明见 tools/Qian.Dev.Shell/README.md。
# 真实 qian.identity（与 CRM 参考运行时同宿主）+ 平台表面参考宿主 + 真实 Identity.Client / Qian.Gateway BFF（HTTPS 自签名，静态托管外壳与参考应用远程制品）+ 上游 OIDC 发行方 CNCF Dex（SSO 跳转回调）；
# 开发库、平台插件树与运营前置复用 tools/Qian.Dev.TenantOps。这是开发机上的联调环境：不替代 qian dev（S1-RT-013），也不替代 K3s 部署（S1-DEP-002）。
# 兼容 macOS 自带的 bash 3.2：不用关联数组，也不在 set -u 下展开空数组。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE="${QIAN_DEV_STATE:-$ROOT/.qian-dev/shell}"
CONTAINER="${QIAN_DEV_PG_CONTAINER:-qian-dev-shell-pg}"
PG_PORT="${QIAN_DEV_PG_PORT:-55433}"
PG_IMAGE="${QIAN_DEV_PG_IMAGE:-${QIAN_TEST_IMAGE_POSTGRES:-postgres:16-alpine}}"
REDIS_CONTAINER="${QIAN_DEV_REDIS_CONTAINER:-qian-dev-shell-redis}"
REDIS_PORT="${QIAN_DEV_REDIS_PORT:-55434}"
REDIS_IMAGE="${QIAN_DEV_REDIS_IMAGE:-${QIAN_TEST_IMAGE_REDIS:-redis:7-alpine}}"
API_URL="${QIAN_DEV_SHELL_API_URL:-http://127.0.0.1:5090}"
SHELL_URL="${QIAN_DEV_SHELL_URL:-https://localhost:5443}"
PROJECT="$ROOT/tools/Qian.Dev.Shell"
DLL="$PROJECT/bin/Debug/net10.0/Qian.Dev.Shell.dll"
PASSWORD_FILE="$STATE/pg-admin-password"
# 上游 OIDC 发行方（CNCF Dex）：内存存储、本地密码库里一个用户（种子 seed/dex.json）、一个机密客户端 qian-identity；issuer 与回调地址写进配置，所以每次 up 都按当前端口重建。
DEX_CONTAINER="${QIAN_DEV_DEX_CONTAINER:-qian-dev-shell-dex}"
DEX_PORT="${QIAN_DEV_DEX_PORT:-55556}"
DEX_IMAGE="${QIAN_DEV_DEX_IMAGE:-${QIAN_TEST_IMAGE_DEX:-ghcr.io/dexidp/dex:v2.41.1}}"
DEX_ISSUER="http://127.0.0.1:${DEX_PORT}/dex"
DEX_SECRET_FILE="$STATE/dex-client-secret"
DEX_CONFIG="$STATE/dex.yaml"
DEX_SEED="$PROJECT/seed/dex.json"

usage() {
  cat <<USAGE
用法：scripts/dev-shell.sh <命令>

  up          起 PostgreSQL 16 容器（只绑 127.0.0.1:${PG_PORT}）、Redis 7（127.0.0.1:${REDIS_PORT}）与上游 Dex 容器（${DEX_ISSUER}），构建前端与宿主，重建开发库，前台启动身份 / 业务宿主（${API_URL}）与网关 BFF（${SHELL_URL}）
  build-ui    只构建三个前端制品：packages/qian-identity-ui（认证页族）、samples/qian-sample-crm-ui（参考应用远程模块）与 apps/qian-shell（外壳）
  status      查看三个容器与两个宿主是否在运行；都在运行时退出码为 0，否则为 1
  e2e         真实流程验收：独立状态目录与随机端口自起一套环境，跑 Playwright，结束后清理（等同 pnpm --filter qian-shell e2e:auth）
  down [--purge]  删除 PostgreSQL、Redis 与 Dex 容器；--purge 同时删除本机状态目录（${STATE}：口令、网关密钥、Dex 客户端密钥与配置、运行时配置）

环境变量：QIAN_DEV_REDIS_CONTAINER、QIAN_DEV_REDIS_PORT、QIAN_DEV_REDIS_IMAGE、QIAN_DEV_STATE、QIAN_DEV_PG_CONTAINER、QIAN_DEV_PG_PORT、QIAN_DEV_PG_IMAGE、QIAN_DEV_DEX_CONTAINER、QIAN_DEV_DEX_PORT、QIAN_DEV_DEX_IMAGE、
          QIAN_DEV_SHELL_API_URL、QIAN_DEV_SHELL_URL、QIAN_DEV_SHELL_SKIP_UI_BUILD=1
USAGE
}

need() { command -v "$1" >/dev/null 2>&1 || { echo "缺少命令：$1（$2）" >&2; exit 1; }; }

build() {
  need dotnet "安装 .NET 10 SDK，版本见仓库根目录 global.json"
  echo "构建联调宿主 ..."
  dotnet build "$PROJECT/Qian.Dev.Shell.csproj" -c Debug -nologo -v quiet
}

build_ui() {
  need pnpm "corepack enable 或按 package.json 的 packageManager 安装 pnpm"
  echo "构建认证页族、参考应用远程模块与外壳 ..."
  pnpm --filter @qian/identity-ui build
  pnpm --filter @qian/sample-crm-ui build
  pnpm --filter qian-shell build
}

container_state() {
  local status
  if status="$(docker inspect -f '{{.State.Status}}' "$CONTAINER" 2>/dev/null)"; then echo "$status"; else echo missing; fi
}

# 探测两个宿主与 Dex，输出 HTTP 状态码，没有响应时是 000。身份宿主与 Dex 的 discovery 文档应答 200；网关的匿名会话快照应答 401。
api_probe() { curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 2 "$API_URL/.well-known/openid-configuration" || true; }
shell_probe() { curl --noproxy '*' -k -s -o /dev/null -w '%{http_code}' --max-time 2 "$SHELL_URL/auth/session" || true; }
dex_probe() { curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 2 "$DEX_ISSUER/.well-known/openid-configuration" || true; }
dex_state() {
  local status
  if status="$(docker inspect -f '{{.State.Status}}' "$DEX_CONTAINER" 2>/dev/null)"; then echo "$status"; else echo missing; fi
}

# Dex 每次都重建（内存存储、无状态）：客户端密钥随机生成、只存在状态目录；用户口令与 bcrypt 哈希来自种子文件；配置经 docker cp 放进容器，不挂载宿主目录。
start_dex() {
  mkdir -p "$STATE"; chmod 700 "$STATE"
  if [ "$(dex_state)" != missing ]; then docker rm -f "$DEX_CONTAINER" >/dev/null; fi
  if [ "$(dex_probe)" != 000 ]; then echo "$DEX_ISSUER 已有服务在监听；先停掉它（或设 QIAN_DEV_DEX_PORT 换端口）。" >&2; exit 1; fi
  [ -s "$DEX_SEED" ] || { echo "缺少 Dex 种子文件：$DEX_SEED" >&2; exit 1; }
  (umask 077; python3 -c 'import secrets; print(secrets.token_hex(24))' > "$DEX_SECRET_FILE")
  # 配置文件要让容器里的非 root 用户可读（docker cp 保留宿主机权限），所以是 0644；状态目录本身仅本人可进。
  (umask 022; python3 - "$DEX_SEED" "$DEX_CONFIG" "$DEX_ISSUER" "$API_URL/connect/callback/dex" "$DEX_SECRET_FILE" <<'PY'
import json, sys
seed = json.load(open(sys.argv[1], encoding='utf-8'))
issuer, callback = sys.argv[3], sys.argv[4]
secret = open(sys.argv[5], encoding='utf-8').read().strip()
lines = [
    'issuer: ' + json.dumps(issuer),
    'storage:', '  type: memory',
    'web:', '  http: 0.0.0.0:5556',
    'oauth2:', '  skipApprovalScreen: true', '  responseTypes: ["code"]',
    'enablePasswordDB: true',
    'staticPasswords:',
    '- email: ' + json.dumps(seed['email']), '  hash: ' + json.dumps(seed['hash']),
    '  username: ' + json.dumps(seed['username']), '  userID: ' + json.dumps(seed['userID']),
    'staticClients:',
    '- id: qian-identity', '  name: "Qian identity (dev shell)"', '  secret: ' + json.dumps(secret),
    '  redirectURIs:', '  - ' + json.dumps(callback),
]
open(sys.argv[2], 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
PY
  )
  echo "创建 Dex 容器 ${DEX_CONTAINER}（${DEX_IMAGE}，${DEX_ISSUER}）"
  docker create --name "$DEX_CONTAINER" -p "127.0.0.1:$DEX_PORT:5556" --label qian.dev=shell --entrypoint /usr/local/bin/dex "$DEX_IMAGE" serve /etc/dex/qian.yaml >/dev/null
  chmod 644 "$DEX_CONFIG"
  docker cp "$DEX_CONFIG" "$DEX_CONTAINER:/etc/dex/qian.yaml"
  docker start "$DEX_CONTAINER" >/dev/null
  local i
  for i in $(seq 1 60); do
    if [ "$(dex_probe)" = 200 ]; then return 0; fi
    sleep 1
  done
  echo "Dex 60 秒内没有就绪，查看：docker logs $DEX_CONTAINER" >&2; exit 1
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
    echo "创建 PostgreSQL 容器 ${CONTAINER}（${PG_IMAGE}，127.0.0.1:${PG_PORT}）"
    docker run -d --name "$CONTAINER" -p "127.0.0.1:$PG_PORT:5432" \
      -e POSTGRES_USER=qian -e POSTGRES_DB=postgres -e "POSTGRES_PASSWORD=$(cat "$PASSWORD_FILE")" \
      --label qian.dev=shell "$PG_IMAGE" >/dev/null
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

redis_state() {
  local status
  if status="$(docker inspect -f '{{.State.Status}}' "$REDIS_CONTAINER" 2>/dev/null)"; then echo "$status"; else echo missing; fi
}

start_redis() {
  need docker "启动 Docker Desktop 或 Docker Engine"
  if [ "$(redis_state)" = missing ]; then
    docker run -d --name "$REDIS_CONTAINER" -p "127.0.0.1:$REDIS_PORT:6379" "$REDIS_IMAGE" >/dev/null
  elif [ "$(redis_state)" != running ]; then
    docker start "$REDIS_CONTAINER" >/dev/null
  fi
  if [ "$(docker port "$REDIS_CONTAINER" 6379/tcp)" != "127.0.0.1:$REDIS_PORT" ]; then
    echo "Redis 容器端口与配置不符；请核对容器名和 QIAN_DEV_REDIS_PORT。" >&2; exit 1
  fi
  local attempt=0
  while [ "$attempt" -lt 60 ]; do
    if [ "$(docker exec "$REDIS_CONTAINER" redis-cli ping 2>/dev/null || true)" = PONG ]; then return 0; fi
    attempt=$((attempt + 1)); sleep 1
  done
  echo "Redis 60 秒内没有就绪，查看：docker logs $REDIS_CONTAINER" >&2; exit 1
}

cmd_up() {
  need python3 "macOS 自带 /usr/bin/python3"
  need curl "系统自带"
  if [ "$(api_probe)" != 000 ]; then echo "$API_URL 已有服务在监听；先停掉它（或设 QIAN_DEV_SHELL_API_URL 换端口）。" >&2; exit 1; fi
  if [ "$(shell_probe)" != 000 ]; then echo "$SHELL_URL 已有服务在监听；先停掉它（或设 QIAN_DEV_SHELL_URL 换端口）。" >&2; exit 1; fi
  if [ "${QIAN_DEV_SHELL_SKIP_UI_BUILD:-0}" != 1 ]; then build_ui; fi
  start_postgres
  start_redis
  start_dex
  build
  QIAN_DEV_REDIS_PORT="$REDIS_PORT" QIAN_DEV_PG_ADMIN="Host=127.0.0.1;Port=$PG_PORT;Username=qian;Password=$(cat "$PASSWORD_FILE");Database=postgres" \
    exec dotnet "$DLL" serve --urls "$API_URL" --gateway "$SHELL_URL" --state "$STATE" --dex "$DEX_ISSUER" --dex-secret "$DEX_SECRET_FILE"
}

cmd_down() {
  if [ "$(redis_state)" != missing ]; then docker rm -f "$REDIS_CONTAINER" >/dev/null; echo "已删除容器 $REDIS_CONTAINER"; fi
  if [ "$(container_state)" != missing ]; then docker rm -f "$CONTAINER" >/dev/null; echo "已删除容器 $CONTAINER"; fi
  if [ "$(dex_state)" != missing ]; then docker rm -f "$DEX_CONTAINER" >/dev/null; echo "已删除容器 $DEX_CONTAINER"; fi
  rm -f "$PASSWORD_FILE" "$DEX_SECRET_FILE" "$DEX_CONFIG"
  if [ "${1:-}" = "--purge" ]; then rm -rf "$STATE"; echo "已删除 ${STATE}（下次 up 会重新生成网关密钥与缺省运行时配置）"; fi
}

cmd_status() {
  echo "PostgreSQL 容器 ${CONTAINER}：$(container_state)"
  echo "Dex 容器 ${DEX_CONTAINER}：$(dex_state)（探测 $(dex_probe)）"
  local api shell ok=0
  echo "Redis 容器 ${REDIS_CONTAINER}：$(redis_state)"
  if [ "$(redis_state)" != running ]; then ok=1; fi
  api="$(api_probe)"; shell="$(shell_probe)"
  case "$api" in
    200) echo "身份 / 业务宿主 ${API_URL}：运行中" ;;
    000) echo "身份 / 业务宿主 ${API_URL}：未运行"; ok=1 ;;
    *) echo "身份 / 业务宿主 ${API_URL}：有别的服务在监听（探测返回 ${api}）"; ok=1 ;;
  esac
  case "$shell" in
    401) echo "外壳 / 网关 BFF ${SHELL_URL}：运行中" ;;
    000) echo "外壳 / 网关 BFF ${SHELL_URL}：未运行"; ok=1 ;;
    *) echo "外壳 / 网关 BFF ${SHELL_URL}：有别的服务在监听（探测返回 ${shell}）"; ok=1 ;;
  esac
  return "$ok"
}

command="${1:-}"; [ $# -gt 0 ] && shift
case "$command" in
  up) cmd_up ;;
  build-ui) build_ui ;;
  down) cmd_down "$@" ;;
  status) cmd_status ;;
  e2e) need pnpm "corepack enable 或按 package.json 的 packageManager 安装 pnpm"; exec pnpm --filter qian-shell e2e:auth ;;
  *) usage; [ -z "$command" ] || exit 2 ;;
esac
