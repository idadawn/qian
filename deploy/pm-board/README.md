# 任务看板上 K3s

在集群里提供任务看板（看板页、`STATUS.md`、`tasks.json`），随时用浏览器看进度。**不构建镜像、不改 CI**：一个 Pod 里三个容器：

- `git-sync` 每 60 秒拉一次仓库的 `main` 分支；
- `builder` 每换一个提交，就用该提交自己的 `scripts/pm.py build --out` 生成看板（S1-PM-004）；
- `nginx` 提供生成结果与任务源文件。

看板只取决于 `main` 上的任务文件，任务改动合入 `main` 后，一分钟左右看板就会更新。

```text
GitHub idadawn/qian (main) --git-sync 每 60s--> /git/current --builder（pm.py build）--> /site/current --nginx--> Service --Traefik Ingress--> 浏览器
```

## 文件

| 文件 | 作用 |
|---|---|
| `kustomization.yaml` | 命名空间 `qian-tools`（开发期工具，与平台本体 `qian` 分开）。`builder.py` 生成为带哈希后缀的 ConfigMap，并带 ArgoCD 的 `IgnoreExtraneous` 注解：改脚本后留下的旧 ConfigMap 不会让应用一直显示 OutOfSync（应用没开 prune，旧的不会被自动删除） |
| `deployment.yaml` | `git-sync` + `builder`（官方 `python:3.13-alpine`）+ `nginx-unprivileged` 三个容器，非 root；builder 的根文件系统只读，只写 `/site`。首版看板生成前 Pod 不就绪。与文档站一样固定在 qd：worker 节点经镜像代理拉 `registry.k8s.io` 的 git-sync 会失败（2026-10-15 遇到 502）。滚动更新（S1-PM-005）：新 Pod 就绪后才停旧 Pod，新 Pod 起不来时旧版照常提供看板；nginx 停止前先等 5 秒（`preStop`），等各节点的转发规则摘掉旧 Pod，切换瞬间不丢请求；为此三个容器各只请求 5m CPU（实测 git-sync 4m、builder 1m、nginx 1m），新旧两个 Pod 才放得进 qd 的余量（2026-10-16 qd 已请求 7980m / 8000m） |
| `builder.py` | builder 的主循环：发现新提交就生成到 `/site/<提交>`，原子切换 `/site/current`，删掉旧版。生成失败（任务文件校验不过）时继续提供上一版，只记日志；生成途中 git-sync 已切到更新的提交（旧检出随之被删）不算失败，只记一条跳过说明，下一轮生成新提交。收到 SIGTERM 立即退出：它在容器里是 1 号进程，不处理的话 SIGTERM 会被忽略，旧 Pod 要等 30 秒宽限期满才被强杀。kustomize 把它生成为带哈希后缀的 ConfigMap，改脚本即滚动更新 |
| `test_builder.py` | builder 的自测（只用标准库）：正常生成、任务文件校验不过、生成途中切换提交、收到 SIGTERM 立即退出。`python3 -m unittest discover -s deploy/pm-board -p 'test_*.py'`，docs-and-pm 工作流也会跑 |
| `nginx-configmap.yaml` | 只暴露 `/`、`/tasks.json`、`/STATUS.md`（取自 `/site/current`）与 `/tasks/*`（取自 git 检出），禁缓存 |
| `service.yaml`、`ingress.yaml` | Service 为 LoadBalancer（MetalLB 固定 192.168.1.210）+ 固定 NodePort 30810；Traefik Ingress 域名占位 `qian-pm.deepsingularity.cn` |
| `networkpolicy.yaml` | 只放行入站 8080 与出站 DNS / 22 / 443 |
| `bootstrap.sh` | 初始化（生成只读 deploy key → 登记 GitHub → 写 Secret → apply → 核验）；支持 `--kube-ssh` 分步与 `--check` |
| `argocd-application.example.yaml` | 交给 ArgoCD 托管时用，放进部署仓库 `env-ds/argocd/` |

## 当前状态（2026-10-07 已部署）

| 入口 | 地址 | 适用 |
|---|---|---|
| WireGuard 侧（Mac 直接可用） | <http://10.78.1.161:30810/> | 固定 NodePort，经节点别名 10.78.1.161 |
| 公司内网 | <http://192.168.1.210/> | MetalLB 固定地址，仅 192.168.1.0/24 内可达 |
| Traefik Host 路由 | `qian-pm.deepsingularity.cn` → 192.168.1.200 | 公网接入前置：公网 DNS 与边缘 nginx 转发尚未配置 |

已在集群创建：命名空间 `qian-tools`、Secret `qian-tools/qian-pm-board-git`（只读 deploy key + GitHub known_hosts）、ArgoCD 仓库凭据 `argocd/qian-repo-ssh`（同一把 key，供 Application 使用）。deploy key 已登记到 GitHub 仓库（Settings → Deploy keys，`qian-pm-board git-sync (read-only, 2026-10-07)`）。ArgoCD Application 通过部署仓库的 PR 加入 `env-ds/argocd/`，合并后由 `platform-apps` 自动纳管。

## 首次部署

```bash
bash deploy/pm-board/bootstrap.sh                    # 本机同时有 gh 与 kubectl
bash deploy/pm-board/bootstrap.sh --kube-ssh qd      # 本机只有 gh，kubectl 在 ssh 主机 qd（本环境的实际用法）
bash deploy/pm-board/bootstrap.sh --check --kube-ssh qd   # 只核验：deploy key、Secret、就绪、同步提交 == main、生成提交 == 同步提交、入口
```

脚本幂等：Secret 已存在不再生成 key，GitHub 已有同名 key 不重复登记。

脚本会在 GitHub 仓库设置里加一把**只读** deploy key（可在仓库 Settings → Deploy keys 看到并随时吊销），其余都在集群内。公网访问需要在边缘 nginx 加一条到该域名的转发（现网 TLS 在边缘终止）；内网直连 Traefik 或 `kubectl port-forward` 不需要。

## 之后

- 交给 ArgoCD：复制 `argocd-application.example.yaml` 到部署仓库 `env-ds/argocd/` 并加入其 kustomization；ArgoCD 需要能读 `idadawn/qian` 的仓库凭据。此后清单改动随 `main` 自动同步。
- 换分支或缩短刷新间隔：改 `deployment.yaml` 的 `GITSYNC_REF`、`GITSYNC_PERIOD`。
- 看板没更新：先看 `bootstrap.sh --check` 的两项提交对比，再看 builder 日志（`kubectl -n qian-tools logs deploy/qian-pm-board -c builder`）。日志里「生成失败」说明该提交的任务文件校验不过，看板仍是上一版；「跳过这一版」说明生成途中 git-sync 已同步到更新的提交，下一轮会生成，不用处理。
- 改了清单却一直没换上新 Pod：多半是新 Pod 停在 Pending（`kubectl -n qian-tools describe pod` 的事件里有 `Insufficient cpu`），即 qd 的 CPU 请求余量放不下第二个 Pod。旧 Pod 仍在提供看板；腾出余量后会自动继续，超过 10 分钟 Deployment 会标为 `ProgressDeadlineExceeded`。
- ArgoCD 显示 OutOfSync，差异只有 `qian-pm-board-builder-<哈希>`（待清理）：这是带 `IgnoreExtraneous` 注解之前生成的旧 ConfigMap，只被缩到 0 的旧 ReplicaSet 引用，可以 `kubectl -n qian-tools delete configmap <名字>` 删掉。
- 不想用 SSH：把 `GITSYNC_REPO` 改为 `https://github.com/idadawn/qian.git`，Secret 改放 `GITSYNC_USERNAME` / `GITSYNC_PASSWORD`（只读 fine-grained PAT），去掉两个 SSH 环境变量。
- 文档站（S1-DOCS-001）上线后可以复用同一套方式先做预览，再决定是否走 Cloudflare Pages。
