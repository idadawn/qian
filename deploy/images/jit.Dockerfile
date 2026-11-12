# profile 镜像的 JIT 构建模板（S1-DEP-003 / R106），供 qian-platform、qian-identity 与 JIT 自检镜像使用。
# 统一入口是 scripts/images.py build：由它传入项目、程序集名和构建证据，不要绕开它直接 docker build。
# 多阶段构建；运行镜像是 chiseled（无 shell、无包管理器），以非 root 用户运行，部署时根文件系统只读，
# 只有 io.qian.writable-paths 列出的目录挂临时卷。基础镜像按多架构索引摘要钉定，升级时标签与摘要一起改。
# 当前钉定 .NET 10.0.15（SDK 10.0.404）。2026-11-11 从 10.0.13 升级：GHSA-p3jp-9v83-76w9（Brotli 解码越界写）影响 10.0.14 以前的运行时。
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:cae5b962bfb786606def03d250c6a645cc98839095c89e806e4bd8978a68115b

# 前端构建产物（S1-DEP-011）：scripts/images.py build 总以 --build-context web=<目录> 覆盖这个空阶段（没给 --web 时是空目录），产物落在 /app/web。
# 平台镜像带认证页族与开通申请公开页（/app/web/identity、/app/web/signup），网关镜像带外壳与已部署组件的远程（/app/web/shell、/app/web/remotes/<组件>）。
FROM scratch AS web

FROM ${SDK_IMAGE} AS build
ARG PROJECT
ARG ASSEMBLY
WORKDIR /src
COPY . .
# 入口固定为 /app/entrypoint：指向 apphost 的符号链接，apphost 按自身真实文件名找到同名程序集。
# /probe 是写探针目录，运行期证据用它区分「根文件系统只读」与「没有写权限」。
RUN --mount=type=cache,id=qian-nuget,target=/root/.nuget/packages \
    test -n "$PROJECT" && test -n "$ASSEMBLY" \
 && dotnet publish "$PROJECT" -c Release -o /out -p:UseAppHost=true \
 && test -x "/out/$ASSEMBLY" \
 && ln -s "$ASSEMBLY" /out/entrypoint \
 && mkdir /probe

FROM ${RUNTIME_IMAGE}
ARG SOURCE_SHA
ARG KUN_VERSION
ARG PROFILE_REVISION
ARG PLUGINS_REVISION
ARG IMAGE_ROLE=product
LABEL org.opencontainers.image.source="https://github.com/idadawn/qian" \
      org.opencontainers.image.revision="${SOURCE_SHA}" \
      io.qian.kun-version="${KUN_VERSION}" \
      io.qian.profile-revision="${PROFILE_REVISION}" \
      io.qian.plugins-revision="${PLUGINS_REVISION}" \
      io.qian.image-role="${IMAGE_ROLE}" \
      io.qian.writable-paths="/tmp"
WORKDIR /app
COPY --from=build /out/ /app/
COPY --from=build --chown=$APP_UID:$APP_UID /probe /app/.write-probe
# profile、组合包与组件清单随镜像发布（环境级 overlay 由 ConfigMap 挂载），漂移门禁在镜像内对它们执行 dump-config。
COPY profiles/ /app/qian/profiles/
COPY plugins/ /app/qian/plugins/
COPY --from=web / /app/web/
ENV QIAN_ROOT=/app/qian
USER $APP_UID
ENTRYPOINT ["/app/entrypoint"]
