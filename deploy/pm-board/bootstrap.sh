#!/usr/bin/env bash
# 一次性初始化：为看板创建只读 deploy key、写入集群 Secret、应用清单。
# 用法：bash deploy/pm-board/bootstrap.sh [kube-context]
# 需要：gh（已登录且对 idadawn/qian 有管理权限）、kubectl、ssh-keygen、ssh-keyscan
set -euo pipefail

REPO="idadawn/qian"
NS="qian-tools"
SECRET="qian-pm-board-git"
CTX="${1:-}"
KUBECTL=(kubectl); [[ -n "$CTX" ]] && KUBECTL+=(--context "$CTX")
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT

echo "1/4 生成只读 deploy key"
ssh-keygen -q -t ed25519 -N "" -C "qian-pm-board git-sync (read-only)" -f "$WORK/ssh"
ssh-keyscan -t ed25519,ecdsa,rsa github.com > "$WORK/known_hosts" 2>/dev/null

echo "2/4 登记到 GitHub（只读）"
gh repo deploy-key add "$WORK/ssh.pub" -R "$REPO" --title "qian-pm-board git-sync ($(date +%F))"

echo "3/4 写入集群 Secret ${NS}/${SECRET}"
"${KUBECTL[@]}" get ns "$NS" >/dev/null 2>&1 || "${KUBECTL[@]}" create ns "$NS"
"${KUBECTL[@]}" -n "$NS" create secret generic "$SECRET" \
  --from-file=ssh="$WORK/ssh" --from-file=known_hosts="$WORK/known_hosts" \
  --dry-run=client -o yaml | "${KUBECTL[@]}" apply -f -

echo "4/4 应用清单"
"${KUBECTL[@]}" apply -k "$HERE"
"${KUBECTL[@]}" -n "$NS" rollout status deploy/qian-pm-board --timeout=120s

HOST="$(grep -m1 'host:' "$HERE/ingress.yaml" | awk '{print $3}')"
echo "完成。访问 http://${HOST}/ （或 kubectl -n ${NS} port-forward svc/qian-pm-board 8080:80 后打开 http://localhost:8080/）"
echo "之后交给 ArgoCD 托管：把 argocd-application.example.yaml 放进部署仓库 env-ds/argocd/。"
