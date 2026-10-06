#!/usr/bin/env bash
# 把文档站构建产物发布到 docs-site 分支；K3s 上的 git-sync 每 60 秒拉取该分支，nginx 直接提供（见 README.md）。
#
# 用法（在仓库检出目录里运行，需要对 origin 有推送权限；CI 用 GITHUB_TOKEN）：
#   deploy/docs-site/publish.sh main <产物目录>          正式站点：替换分支根目录下除 pr-preview/ 以外的内容
#   deploy/docs-site/publish.sh preview <PR 号> <产物目录> PR 预览：替换 pr-preview/pr-<n>/
#   deploy/docs-site/publish.sh remove <PR 号>           PR 关闭后删除它的预览
#
# 分支只有一个提交：每次在当前内容上改完后作为无父提交强推，产物不在仓库里累积历史。
# 并发发布（多个 PR 同时推）用 --force-with-lease 防止互相覆盖，租约失败就重新取最新内容再来。
set -euo pipefail

BRANCH="${DOCS_BRANCH:-docs-site}"
REMOTE="${DOCS_REMOTE:-origin}"
ATTEMPTS="${DOCS_PUBLISH_ATTEMPTS:-5}"

usage() { sed -n '4,8p' "$0" >&2; exit 2; }

mode="${1:-}"
case "$mode" in
  main) [[ $# -eq 2 ]] || usage; target=""; src="$2" ;;
  preview) [[ $# -eq 3 && "$2" =~ ^[0-9]+$ ]] || usage; target="pr-preview/pr-$2"; src="$3" ;;
  remove) [[ $# -eq 2 && "$2" =~ ^[0-9]+$ ]] || usage; target="pr-preview/pr-$2"; src="" ;;
  *) usage ;;
esac
if [[ -n "$src" && ! -f "$src/index.html" ]]; then
  echo "产物目录 $src 里没有 index.html，先运行 pnpm --filter website build" >&2
  exit 1
fi

git_dir="$(git rev-parse --absolute-git-dir)"
source_sha="${DOCS_SOURCE_SHA:-${GITHUB_SHA:-$(git rev-parse HEAD)}}"
work="$(mktemp -d)"
index="$(mktemp)"
trap 'rm -rf "$work" "$index"' EXIT

export GIT_AUTHOR_NAME="${GIT_AUTHOR_NAME:-github-actions[bot]}"
export GIT_AUTHOR_EMAIL="${GIT_AUTHOR_EMAIL:-41898282+github-actions[bot]@users.noreply.github.com}"
export GIT_COMMITTER_NAME="$GIT_AUTHOR_NAME"
export GIT_COMMITTER_EMAIL="$GIT_AUTHOR_EMAIL"

for attempt in $(seq 1 "$ATTEMPTS"); do
  find "$work" -mindepth 1 -delete
  # 取分支当前内容；分支还不存在时从空目录开始，租约要求远端仍不存在
  if git fetch --quiet --depth 1 "$REMOTE" "+refs/heads/$BRANCH:refs/remotes/$REMOTE/$BRANCH" 2>/dev/null; then
    lease="$(git rev-parse "refs/remotes/$REMOTE/$BRANCH")"
    git archive "$lease" | tar -x -C "$work"
  else
    lease=""
  fi
  if [[ "$mode" == remove && ! -d "$work/$target" ]]; then
    echo "$BRANCH 上没有 $target，无需删除"
    exit 0
  fi

  if [[ -z "$target" ]]; then
    # 正式站点：保留各 PR 的预览，其余整体替换
    find "$work" -mindepth 1 -maxdepth 1 ! -name pr-preview -exec rm -rf {} +
    cp -R "$src"/. "$work"/
    dest="$work"
  else
    rm -rf "${work:?}/$target"
    dest="$work/$target"
    if [[ -n "$src" ]]; then
      mkdir -p "$dest"
      cp -R "$src"/. "$dest"/
    fi
    rmdir "$work/pr-preview" 2>/dev/null || true
  fi
  if [[ -n "$src" ]]; then
    printf '{"commit":"%s","ref":"%s","publishedAt":"%s"}\n' \
      "$source_sha" "${GITHUB_REF:-local}" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$dest/build-info.json"
  fi

  # 用临时索引在临时目录上建树，不碰当前检出的索引与工作区
  rm -f "$index"
  GIT_INDEX_FILE="$index" git -C "$work" --git-dir="$git_dir" add --all --force .
  tree="$(GIT_INDEX_FILE="$index" git --git-dir="$git_dir" write-tree)"
  previews=""
  if [[ -d "$work/pr-preview" ]]; then
    previews="$(find "$work/pr-preview" -mindepth 1 -maxdepth 1 -type d -exec basename {} \; | sort -V | tr '\n' ' ')"
  fi
  commit="$(git commit-tree "$tree" -m "docs-site: $mode ${target:-/} @ ${source_sha:0:12}" -m "当前预览：${previews:-无}")"

  if git push --quiet --force-with-lease="refs/heads/$BRANCH:$lease" "$REMOTE" "$commit:refs/heads/$BRANCH"; then
    echo "已发布到 $BRANCH（$mode ${target:-/}），提交 ${commit:0:12}；当前预览：${previews:-无}"
    exit 0
  fi
  echo "第 $attempt 次推送被拒（分支已被其他发布更新），重新取最新内容再试" >&2
  sleep $((attempt * 3))
done
echo "发布失败：$ATTEMPTS 次都没能推送到 $BRANCH" >&2
exit 1
