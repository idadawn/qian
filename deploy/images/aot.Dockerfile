# profile 镜像的 NativeAOT 构建模板（S1-DEP-003 / R106），供 qian-gateway 与 AOT 自检镜像使用。
# 统一入口是 scripts/images.py build。构建阶段用带原生工具链的 SDK 镜像，按目标架构发布 Linux NativeAOT；
# 运行镜像是 distroless 的 runtime-deps chiseled，只含原生可执行文件，以非 root 用户运行，部署时根文件系统只读。
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-noble-aot@sha256:62a3ebde694544ae0790b5ca2ffd56db04298af5652b7295c88fd61097752664
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled@sha256:8d23bb82cd8345ee51738b36c6da8e2f89ded7a547bc25fa52d5cc60e37f6d7a

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
ENV QIAN_ROOT=/app/qian
USER $APP_UID
ENTRYPOINT ["/app/entrypoint"]
