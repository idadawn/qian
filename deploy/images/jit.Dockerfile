# profile 镜像的 JIT 构建模板（S1-DEP-003 / R106），供 qian-platform、qian-identity 与 JIT 自检镜像使用。
# 统一入口是 scripts/images.py build：由它传入项目、程序集名和构建证据，不要绕开它直接 docker build。
# 多阶段构建；运行镜像是 chiseled（无 shell、无包管理器），以非 root 用户运行，部署时根文件系统只读，
# 只有 io.qian.writable-paths 列出的目录挂临时卷。基础镜像按多架构索引摘要钉定，升级时标签与摘要一起改。
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:3ed6558056b54d5c9dfefb7ff473728e022ff2bf1d976cbf8ee07b05ec594374

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
ENV QIAN_ROOT=/app/qian
USER $APP_UID
ENTRYPOINT ["/app/entrypoint"]
