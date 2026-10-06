# 任务看板上 K3s

把 `pm/index.html`（离线看板）、`pm/STATUS.md`、`pm/tasks.json` 放进集群，随时用浏览器看进度。**不构建镜像、不改 CI**：一个 Pod 里 `git-sync` 每 60 秒拉一次仓库 `main` 分支，`nginx` 直接提供 `pm/` 目录。谁提交了任务文件并 `build`，一分钟内看板就更新。

```text
GitHub idadawn/qian (main) --git-sync 每 60s--> emptyDir /git/current --nginx--> Service --Traefik Ingress--> 浏览器
```

## 文件

| 文件 | 作用 |
|---|---|
| `kustomization.yaml` | 命名空间 `qian-tools`（开发期工具，与平台本体 `qian` 分开） |
| `deployment.yaml` | `git-sync` + `nginx-unprivileged` 两个容器，非 root、只读根文件系统之外无特权 |
| `nginx-configmap.yaml` | 只暴露 `/`、`/tasks.json`、`/STATUS.md`、`/tasks/*`，禁缓存 |
| `service.yaml`、`ingress.yaml` | Traefik Ingress，域名占位 `qian-pm.deepsingularity.cn`，按实际改 |
| `networkpolicy.yaml` | 只放行入站 8080 与出站 DNS / 22 / 443 |
| `bootstrap.sh` | 一次性：生成只读 deploy key → 登记到 GitHub → 写 Secret → apply |
| `argocd-application.example.yaml` | 交给 ArgoCD 托管时用，放进部署仓库 `env-ds/argocd/` |

## 首次部署

```bash
bash deploy/pm-board/bootstrap.sh            # 默认 kube context
bash deploy/pm-board/bootstrap.sh my-ctx     # 指定 context
```

脚本会在 GitHub 仓库设置里加一把**只读** deploy key（可在仓库 Settings → Deploy keys 看到并随时吊销），其余都在集群内。公网访问需要在边缘 nginx 加一条到该域名的转发（现网 TLS 在边缘终止）；内网直连 Traefik 或 `kubectl port-forward` 不需要。

## 之后

- 交给 ArgoCD：复制 `argocd-application.example.yaml` 到部署仓库 `env-ds/argocd/` 并加入其 kustomization；ArgoCD 需要能读 `idadawn/qian` 的仓库凭据。此后清单改动随 `main` 自动同步。
- 换分支或缩短刷新间隔：改 `deployment.yaml` 的 `GITSYNC_REF`、`GITSYNC_PERIOD`。
- 不想用 SSH：把 `GITSYNC_REPO` 改为 `https://github.com/idadawn/qian.git`，Secret 改放 `GITSYNC_USERNAME` / `GITSYNC_PASSWORD`（只读 fine-grained PAT），去掉两个 SSH 环境变量。
- 文档站（S1-DOCS-001）上线后可以复用同一套方式先做预览，再决定是否走 Cloudflare Pages。
