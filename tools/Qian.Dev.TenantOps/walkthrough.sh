#!/usr/bin/env bash
# 租户运营手测脚本（S1-QA-012）。先在另一个终端运行 scripts/dev-tenant-ops.sh up，再运行 scripts/dev-tenant-ops.sh walkthrough。
#
# 顺序：建租户 → 开通步骤 → 激活 → 写设置 / 读配额 → 冻结 / 恢复 → 扫描到期，最后是权限反例。
# 覆盖运营契约 plugins/qian.tenancy/contracts/http-v1.openapi.json 的 8 个路径、11 个操作；
# 「开通步骤」「扫描到期」没有产品端点，调用的是本机手测宿主的 /__dev/ 开发路由（见 README.md）。
# 每步先打印等价的 curl 命令（令牌写成 $TOKEN），再打印响应并核对状态码与关键字段。
# 交互终端里每步前暂停；加 --yes 或在非交互环境里一口气跑完。任一检查不符时退出码为 1。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OPS="${QIAN_DEV_OPS_URL:-http://127.0.0.1:5080}"
STATE="${QIAN_DEV_STATE:-$ROOT/.qian-dev/tenant-ops}"
DLL="$ROOT/tools/Qian.Dev.TenantOps/bin/Debug/net10.0/Qian.Dev.TenantOps.dll"
VALID_SECONDS="${QIAN_DEV_VALID_SECONDS:-30}"
AUTO=0; if [ "${1:-}" = "--yes" ] || [ ! -t 0 ]; then AUTO=1; fi
WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
PASSED=0; FAILED=0; FAILURES=""
if [ -t 1 ]; then BOLD=$'\033[1m'; GREEN=$'\033[32m'; RED=$'\033[31m'; DIM=$'\033[2m'; RESET=$'\033[0m'; else BOLD=""; GREEN=""; RED=""; DIM=""; RESET=""; fi

token() { dotnet "$DLL" token --state "$STATE" "$@"; }

# 从上一次响应（$WORK/body.json）里取字段，路径用点分隔，如 field items.0.code；取不到时输出空串，由 check 报不符。
field() {
  python3 -c '
import json, sys
try:
    value = json.load(open(sys.argv[1]))
    for part in sys.argv[2].split("."):
        value = value[int(part)] if isinstance(value, list) else value[part]
    print(json.dumps(value) if isinstance(value, (dict, list)) or value is None else value)
except Exception:
    print("")
' "$WORK/body.json" "$1"
}

stop() { printf '\n%s%s%s\n看上面的响应，以及 up 那个终端里的宿主日志。\n' "$RED" "$1" "$RESET" >&2; exit 1; }

section() {
  printf '\n%s== %s ==%s\n' "$BOLD" "$1" "$RESET"
  if [ "$AUTO" = 0 ]; then read -r -p "按回车继续，Ctrl+C 退出 " _ </dev/tty; fi
}

check() { # 名称 实际 期望
  if [ "$2" = "$3" ]; then PASSED=$((PASSED + 1)); printf '   %s✔ %s：%s%s\n' "$GREEN" "$1" "$2" "$RESET"
  else FAILED=$((FAILED + 1)); FAILURES="$FAILURES\n   - $1：期望 $3，实际 $2"; printf '   %s✘ %s：期望 %s，实际 %s%s\n' "$RED" "$1" "$3" "$2" "$RESET"; fi
}

# call 说明 方法 路径 期望状态码 [JSON 请求体] [令牌，默认平台超管；写 none 表示不带令牌]
call() {
  local title="$1" method="$2" path="$3" expected="$4" body="${5:-}" bearer="${6:-$TOKEN}" shown args
  printf '\n%s%s%s\n' "$BOLD" "$title" "$RESET"
  shown="curl -X $method '$OPS$path'"
  args="-sS -o $WORK/body.json -D $WORK/headers.txt -w %{http_code} -X $method"
  if [ "$bearer" != none ]; then shown="$shown -H \"Authorization: Bearer \$TOKEN\""; fi
  if [ -n "$body" ]; then shown="$shown -H 'Content-Type: application/json' -d '$body'"; fi
  printf '%s$ %s%s\n' "$DIM" "$shown" "$RESET"
  if [ "$bearer" = none ] && [ -z "$body" ]; then STATUS="$(curl --noproxy '*' $args "$OPS$path")"
  elif [ "$bearer" = none ]; then STATUS="$(curl --noproxy '*' $args -H 'Content-Type: application/json' --data "$body" "$OPS$path")"
  elif [ -z "$body" ]; then STATUS="$(curl --noproxy '*' $args -H "Authorization: Bearer $bearer" "$OPS$path")"
  else STATUS="$(curl --noproxy '*' $args -H "Authorization: Bearer $bearer" -H 'Content-Type: application/json' --data "$body" "$OPS$path")"; fi
  python3 -c '
import json, sys
text = open(sys.argv[1]).read()
try: print(json.dumps(json.loads(text), ensure_ascii=False, indent=2))
except ValueError: print(text) if text else None
' "$WORK/body.json" | sed 's/^/   /'
  grep -i '^location:' "$WORK/headers.txt" | tr -d '\r' | sed 's/^/   /' || true
  check "HTTP 状态码" "$STATUS" "$expected"
}

now_plus() { python3 -c 'import datetime, sys; print((datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(seconds=int(sys.argv[1]))).strftime("%Y-%m-%dT%H:%M:%SZ"))' "$1"; }
seconds_until() { python3 -c 'import datetime, sys, math; t = datetime.datetime.strptime(sys.argv[1], "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=datetime.timezone.utc); print(max(0, math.ceil((t - datetime.datetime.now(datetime.timezone.utc)).total_seconds())))' "$1"; }
uuid() { python3 -c 'import uuid; print(uuid.uuid4())'; }

[ -f "$DLL" ] || { echo "手测宿主还没构建：先运行 scripts/dev-tenant-ops.sh up" >&2; exit 1; }
if ! curl --noproxy '*' -s -o /dev/null --max-time 2 "$OPS/api/tenancy/meta/state-machine"; then
  echo "运营宿主 $OPS 没有响应：先在另一个终端运行 scripts/dev-tenant-ops.sh up" >&2; exit 1
fi
TOKEN="$(token)"
CODE="acme$(date +%H%M%S)"
printf '%s租户运营手测：%s，新租户编码 %s%s\n' "$BOLD" "$OPS" "$CODE" "$RESET"
printf '想自己重放下面的 curl：export TOKEN=$(scripts/dev-tenant-ops.sh token)；令牌默认 8 小时有效。\n'

section "1 建租户"
call "1.1 列表（GET /api/tenancy/tenants）" GET "/api/tenancy/tenants?skip=0&take=50" 200
call "1.2 新建（POST /api/tenancy/tenants）：开通编排同步执行，返回 pending" POST /api/tenancy/tenants 201 \
  "{\"code\":\"$CODE\",\"name\":\"Acme 手测租户\",\"administrator\":{\"membershipId\":\"$(uuid)\",\"subjectId\":\"owner-$CODE\"},\"quotas\":{\"users\":5}}"
TENANT="$(field id)"
[ -n "$TENANT" ] || stop "新建租户没有成功，后面的步骤都依赖它，走查到此停止。"
check "新租户状态" "$(field status)" pending
call "1.3 详情（GET /api/tenancy/tenants/{id}）" GET "/api/tenancy/tenants/$TENANT" 200
REVISION="$(field revision)"; REVISION="${REVISION:-0}"
call "1.4 改名（PUT /api/tenancy/tenants/{id}）：带当前修订号" PUT "/api/tenancy/tenants/$TENANT" 200 \
  "{\"name\":\"Acme 手测租户（改名）\",\"expectedRevision\":$REVISION,\"quotas\":{\"users\":5}}"
call "1.5 反例：拿旧修订号再改一次，并发保护返回 409" PUT "/api/tenancy/tenants/$TENANT" 409 \
  "{\"name\":\"过期的编辑\",\"expectedRevision\":$REVISION,\"quotas\":{\"users\":5}}"
check "错误码" "$(field code)" CONFLICT

section "2 开通步骤（开发路由）"
call "2.1 开通步骤表（GET /__dev/tenancy/tenants/{id}/provision-steps）" GET "/__dev/tenancy/tenants/$TENANT/provision-steps" 200
python3 -c '
import json, sys
items = json.load(open(sys.argv[1]))["items"]
print("   步骤                         状态        次数  原因")
for s in items: print("   %-28s %-11s %-5s %s" % (s["step"], s["status"], s["attempts"], s["reason"] or ""))
' "$WORK/body.json"
check "未成功的步骤（skipped 之外）" "$(python3 -c 'import json,sys; print(sum(1 for s in json.load(open(sys.argv[1]))["items"] if s["status"] not in ("succeeded","skipped")))' "$WORK/body.json")" 0

section "3 激活"
call "3.1 状态机元数据（GET /api/tenancy/meta/state-machine）：先查合法迁移" GET /api/tenancy/meta/state-machine 200
VALID_UNTIL="$(now_plus "$VALID_SECONDS")"
call "3.2 激活（POST /api/tenancy/tenants/{id}:activate）：有效期到 ${VALID_UNTIL}（${VALID_SECONDS} 秒后，第 6 步用来演示到期）" \
  POST "/api/tenancy/tenants/$TENANT:activate" 200 "{\"trial\":false,\"validUntil\":\"$VALID_UNTIL\"}"
check "状态" "$(field status)" active

section "4 写设置 / 读配额"
call "4.1 读 qian.audit 的租户设置（GET …/settings/qian.audit）" GET "/api/tenancy/tenants/$TENANT/settings/qian.audit" 200
SETTINGS_REVISION="$(field revision)"; SETTINGS_REVISION="${SETTINGS_REVISION:-0}"
call "4.2 反例：配置不符合组件 configSchema，返回 400 并指出字段" PUT "/api/tenancy/tenants/$TENANT/settings/qian.audit" 400 \
  "{\"config\":{\"chain\":{\"store\":\"invalid\"}},\"expectedRevision\":$SETTINGS_REVISION}"
call "4.3 写租户设置（PUT …/settings/qian.audit）" PUT "/api/tenancy/tenants/$TENANT/settings/qian.audit" 200 \
  "{\"config\":{\"chain\":{\"store\":\"pg\"}},\"expectedRevision\":$SETTINGS_REVISION}"
call "4.4 再读一次设置，确认已生效" GET "/api/tenancy/tenants/$TENANT/settings/qian.audit" 200
check "chain.store" "$(field config.chain.store)" pg
call "4.5 读配额（GET …/quota）：Identity 未接入，已用人数如实为 null" GET "/api/tenancy/tenants/$TENANT/quota" 200
check "用户上限" "$(field limits.users)" 5
check "用量可得" "$(field usageAvailable)" False

section "5 冻结 / 恢复"
call "5.1 冻结（POST …:suspend）" POST "/api/tenancy/tenants/$TENANT:suspend" 200
check "状态" "$(field status)" suspended
call "5.2 恢复（POST …:resume）：回到冻结前的状态" POST "/api/tenancy/tenants/$TENANT:resume" 200
check "状态" "$(field status)" active

section "6 扫描到期（开发路由，代调度器触发）"
WAIT="$(seconds_until "$VALID_UNTIL")"
if [ "$WAIT" -gt 0 ]; then printf '\n等待 %s 秒，让有效期（%s）过去 ...\n' "$WAIT" "$VALID_UNTIL"; sleep "$((WAIT + 1))"; fi
call "6.1 扫描到期（POST /__dev/tenancy/scan-expired）" POST /__dev/tenancy/scan-expired 200
# 同一次 up 里中断过的旧租户也会在这次扫描中到期，所以只要求至少一个。
EXPIRED="$(field expired)"
check "本次进入宽限的租户数 ≥ 1" "$([ "${EXPIRED:-0}" -ge 1 ] 2>/dev/null && echo 是 || echo 否)" 是
call "6.2 详情：到期租户进入宽限" GET "/api/tenancy/tenants/$TENANT" 200
check "状态" "$(field status)" grace
call "6.3 反例：宽限中的租户不能再激活，返回 409" POST "/api/tenancy/tenants/$TENANT:activate" 409 \
  "{\"trial\":false,\"validUntil\":\"$(now_plus 86400)\"}"
check "错误码" "$(field code)" TENANCY_STATE_CONFLICT

section "7 权限反例"
call "7.1 不带令牌：401" GET /api/tenancy/tenants 401 "" none
check "错误码" "$(field code)" UNAUTHENTICATED
call "7.2 不是平台超管的用户（sub=intruder）：403；拒绝原因只进宿主日志" GET /api/tenancy/tenants 403 "" "$(token --sub intruder)"
check "错误码" "$(field code)" AUTHZ_DENIED
call "7.3 带 tenant_id 的租户主体：运营接口一律 403" GET /api/tenancy/tenants 403 "" "$(token --tenant "$TENANT")"
check "错误码" "$(field code)" AUTHZ_DENIED

printf '\n%s汇总：%s 项通过，%s 项不符%s\n' "$BOLD" "$PASSED" "$FAILED" "$RESET"
if [ "$FAILED" -gt 0 ]; then printf '%b\n' "$FAILURES"; exit 1; fi
