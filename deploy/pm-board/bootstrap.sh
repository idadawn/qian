#!/usr/bin/env bash
# 任务看板上 K3s：一次性初始化 / 状态核验。
#
#   bash deploy/pm-board/bootstrap.sh                     # 本机同时有 gh 与 kubectl
#   bash deploy/pm-board/bootstrap.sh --kube-ssh qd        # 本机只有 gh；kubectl 在 ssh 主机 qd 上（私钥在远端生成，不经过本机）
#   bash deploy/pm-board/bootstrap.sh --check [--kube-ssh qd]   # 只核验：deploy key、Secret、工作负载、看板是否与仓库 HEAD 一致
#
# 幂等：Secret 已存在则不再生成新 key；GitHub 上已有同名 deploy key 则不重复登记。
set -euo pipefail

REPO="idadawn/qian"
NS="qian-tools"
SECRET="qian-pm-board-git"
KEY_TITLE="qian-pm-board git-sync (read-only)"
HERE="$(cd "$(dirname "$0")" && pwd)"
KUBE_SSH=""; CHECK=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --kube-ssh) KUBE_SSH="$2"; shift 2 ;;
    --check) CHECK=1; shift ;;
    -h|--help) sed -n '2,10p' "$0"; exit 0 ;;
    *) echo "未知参数：$1" >&2; exit 2 ;;
  esac
done

# 远端或本地执行 kubectl / shell
rsh() { if [[ -n "$KUBE_SSH" ]]; then ssh -o BatchMode=yes "$KUBE_SSH" "$@"; else bash -c "$*"; fi; }
kc()  { rsh "kubectl $* 2>&1 | grep -v 'level=warning' || true"; }

need() { command -v "$1" >/dev/null || { echo "缺少命令：$1" >&2; exit 2; }; }
need gh; [[ -n "$KUBE_SSH" ]] && need ssh || need kubectl

echo "== 环境：kubectl 在 ${KUBE_SSH:-本机}，gh 在本机"

# ---------- 核验 ----------
check() {
  local ok=1
  echo "-- GitHub deploy key"
  if gh repo deploy-key list -R "$REPO" | grep -qF "qian-pm-board git-sync"; then echo "   ok：已登记（只读）"; else echo "   缺失"; ok=0; fi
  echo "-- Secret ${NS}/${SECRET}"
  if kc -n "$NS" get secret "$SECRET" -o name | grep -q secret/; then echo "   ok"; else echo "   缺失"; ok=0; fi
  echo "-- 工作负载"
  local ready; ready="$(kc -n "$NS" get deploy qian-pm-board -o jsonpath='{.status.readyReplicas}')"
  if [[ "$ready" == "1" ]]; then echo "   ok：1/1 就绪"; else echo "   未就绪（readyReplicas=${ready:-0}）"; ok=0; fi
  echo "-- 同步到的提交 vs origin/main"
  local synced head
  synced="$(kc -n "$NS" exec deploy/qian-pm-board -c nginx -- readlink /git/current | tr -d '\r' | sed 's#.*/##')"
  head="$(git -C "$HERE" ls-remote -q "git@github.com:${REPO}.git" refs/heads/main 2>/dev/null | cut -f1 || true)"
  if [[ -n "$synced" && "$synced" == "$head" ]]; then echo "   ok：${synced:0:7}"; else echo "   看板 ${synced:0:7} ≠ main ${head:0:7}（等下一次同步或检查 git-sync 日志）"; ok=0; fi
  echo "-- 生成看板的提交 vs 同步到的提交"
  local built
  built="$(kc -n "$NS" exec deploy/qian-pm-board -c nginx -- readlink /site/current | tr -d '\r')"
  if [[ -n "$built" && "$built" == "$synced" ]]; then echo "   ok：${built:0:7}"; else echo "   生成 ${built:0:7} ≠ 同步 ${synced:0:7}（等几秒再查，或看 builder 容器日志）"; ok=0; fi
  echo "-- 入口"
  local np lb
  np="$(kc -n "$NS" get svc qian-pm-board -o jsonpath='{.spec.ports[0].nodePort}')"
  lb="$(kc -n "$NS" get svc qian-pm-board -o jsonpath='{.status.loadBalancer.ingress[0].ip}')"
  echo "   NodePort ${np:-?}，LoadBalancer ${lb:-未分配}，Ingress $(grep -m1 'host:' "$HERE/ingress.yaml" | awk '{print $3}')"
  [[ $ok == 1 ]] && echo "== 核验通过" || { echo "== 核验未通过"; return 1; }
}

if [[ $CHECK == 1 ]]; then check; exit $?; fi

# ---------- 初始化 ----------
echo "1/4 命名空间"
kc create ns "$NS" --dry-run=client -o yaml \| kubectl apply -f - >/dev/null

echo "2/4 只读 deploy key 与 Secret"
if kc -n "$NS" get secret "$SECRET" -o name | grep -q secret/; then
  echo "   Secret 已存在，跳过生成"
else
  # 在 kubectl 所在主机生成，私钥不落本机；known_hosts 用 ssh-keyscan 取 GitHub 公钥
  rsh "set -e; D=\$(mktemp -d); umask 077; ssh-keygen -q -t ed25519 -N '' -C '$KEY_TITLE' -f \$D/ssh; \
       ssh-keyscan -t ed25519,ecdsa,rsa github.com > \$D/known_hosts 2>/dev/null; \
       kubectl -n $NS create secret generic $SECRET --from-file=ssh=\$D/ssh --from-file=known_hosts=\$D/known_hosts --dry-run=client -o yaml | kubectl apply -f - >/dev/null; \
       kubectl -n argocd create secret generic qian-repo-ssh --from-literal=type=git --from-literal=url=git@github.com:$REPO.git --from-file=sshPrivateKey=\$D/ssh --dry-run=client -o yaml \
         | kubectl label --local -f - argocd.argoproj.io/secret-type=repository -o yaml | kubectl apply -f - >/dev/null 2>&1 || true; \
       cat \$D/ssh.pub; rm -rf \$D" 2>&1 | grep -v 'level=warning' > "$HERE/.deploy-key.pub.tmp"
  if gh repo deploy-key list -R "$REPO" | grep -qF "qian-pm-board git-sync"; then
    echo "   GitHub 已有同名 deploy key，未重复登记（若 Secret 是新生成的，请先删除旧 key 再重跑）"
  else
    gh repo deploy-key add "$HERE/.deploy-key.pub.tmp" -R "$REPO" --title "$KEY_TITLE, $(date +%F)"
  fi
  rm -f "$HERE/.deploy-key.pub.tmp"
fi

echo "3/4 应用清单"
if [[ -n "$KUBE_SSH" ]]; then kubectl kustomize "$HERE" | ssh -o BatchMode=yes "$KUBE_SSH" 'kubectl apply -f -' 2>&1 | grep -v 'level=warning'
else kubectl apply -k "$HERE"; fi
kc -n "$NS" rollout status deploy/qian-pm-board --timeout=180s

echo "4/4 核验"
check || true
echo "之后交给 ArgoCD：把 argocd-application.example.yaml 放进部署仓库 env-ds/argocd/（仓库凭据 argocd/qian-repo-ssh 已随本脚本创建）。"
