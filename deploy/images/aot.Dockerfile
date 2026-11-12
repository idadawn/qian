# profile 镜像的 NativeAOT 构建模板（S1-DEP-003 / R106），供 qian-gateway 与 AOT 自检镜像使用。
# 统一入口是 scripts/images.py build。构建阶段用带原生工具链的 SDK 镜像，按目标架构发布 Linux NativeAOT；
# 运行镜像是 distroless 的 runtime-deps chiseled，只含原生可执行文件，以非 root 用户运行，部署时根文件系统只读。
# 当前钉定 .NET 10.0.15（SDK 10.0.404）。2026-11-11 从 10.0.13 升级：GHSA-p3jp-9v83-76w9 影响 10.0.14 以前的 ILCompiler 与运行时，NuGet 审计在 AOT 发布时报 NU1903。
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble-aot@sha256:7d223c2b945173edb8c73e5f5d48a64e7cd0ca632ccf95170b857e8eea78913c
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:40eba5db69594d517c3a61e96bdacc2e3d6f8c99a44b52d55edee844d33518d0

# 前端构建产物（S1-DEP-011）：scripts/images.py build 总以 --build-context web=<目录> 覆盖这个空阶段（没给 --web 时是空目录），产物落在 /app/web。
# 平台镜像带认证页族与开通申请公开页（/app/web/identity、/app/web/signup），网关镜像带外壳与已部署组件的远程（/app/web/shell、/app/web/remotes/<组件>）。
FROM scratch AS web

FROM ${SDK_IMAGE} AS build
ARG PROJECT
ARG ASSEMBLY
ARG TARGETARCH
WORKDIR /src
COPY . .
# 项目必须在 csproj 里声明 PublishAot：命令行传 -p:PublishAot=true 会作为全局属性波及被引用的分析器项目
# （netstandard2.0），报 NETSDK1207。发布后若还有同名托管程序集，说明没走 NativeAOT，直接失败。
RUN --mount=type=cache,id=qian-nuget,target=/root/.nuget/packages \
    test -n "$PROJECT" && test -n "$ASSEMBLY" \
 && case "$TARGETARCH" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) echo "不支持的目标架构：$TARGETARCH" >&2; exit 1 ;; esac \
 && dotnet publish "$PROJECT" -c Release -r "$rid" -o /out -p:StripSymbols=true \
 && if [ -e "/out/$ASSEMBLY.dll" ]; then echo "$PROJECT 没有按 NativeAOT 发布：csproj 需声明 PublishAot" >&2; exit 1; fi \
 && rm -f /out/*.dbg /out/*.pdb "/out/$ASSEMBLY.xml" \
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
COPY profiles/ /app/qian/profiles/
COPY plugins/ /app/qian/plugins/
COPY --from=web / /app/web/
ENV QIAN_ROOT=/app/qian
USER $APP_UID
ENTRYPOINT ["/app/entrypoint"]
