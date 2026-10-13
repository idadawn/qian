#!/usr/bin/env python3
"""Qian 项目管理工具：任务即文件（pm/tasks/*.md），本脚本负责校验、生成看板与改状态。

用法：
  python3 scripts/pm.py validate                 # 校验全部任务文件
  python3 scripts/pm.py build                    # 生成 pm/index.html、pm/tasks.json、pm/STATUS.md
  python3 scripts/pm.py check-fresh              # CI：生成物是否与任务文件一致
  python3 scripts/pm.py list [--status s] [--area a] [--owner o] [--stage S1]
  python3 scripts/pm.py stats
  python3 scripts/pm.py add --by <代理名> --stage S1 --area RT --title "..." [--priority P0 --wave 1 --type feature
                            --estimate 1d --depends ID,ID --ref path#节 --ref ...]
  python3 scripts/pm.py set <id> --by <代理名> [--status doing] [--owner codex] [--note "一句话进展"]
  （--by 缺省时取环境变量 PM_ACTOR，两者都没有则报错；日志不再默认署名 someone）

只依赖 Python 3 标准库；若装有 PyYAML 则用它解析 front matter，否则用内置的简易解析器。
"""
from __future__ import annotations

import argparse
import datetime as dt
import html
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TASKS_DIR = ROOT / "pm" / "tasks"
PM_DIR = ROOT / "pm"
TEMPLATE = PM_DIR / "_template.html"
REPO_BLOB = "https://github.com/idadawn/qian/blob/main/"

STATUSES = ["todo", "doing", "review", "done", "blocked"]
STAGES = ["S1", "S2", "S3", "S4"]
PRIORITIES = ["P0", "P1", "P2"]
TYPES = ["feature", "fix", "decision", "doc", "infra", "test"]
AREAS = {
    "KUN": "Kun 缺口修复", "RT": "运行时 / CLI / 市场", "TEN": "租户", "IDP": "身份与连接器",
    "AZ": "鉴权", "DATA": "数据", "FILE": "文件", "CFG": "配置", "GW": "网关", "EVT": "事件",
    "AUD": "审计与可观测", "NTF": "消息通知", "SCH": "调度与实时", "ENT": "权益", "YUE": "外壳与 yue",
    "MGW": "模型网关", "DEP": "部署物料", "AGT": "Agent 就绪", "QA": "测试 / TCK / 门禁",
    "DEC": "待拍板决策", "PM": "流程与协作", "DOCS": "文档站与组件文档",
    # S2+ 预留
    "META": "元数据", "FORM": "表单", "LIST": "列表", "WF": "流程", "PRINT": "打印", "MKT": "市场",
    "BILL": "计费", "PORTAL": "门户", "AGENT": "Agent 平台",
}
REQUIRED = ["id", "title", "status", "stage", "area", "wave", "priority", "type", "owner",
            "estimate", "depends", "refs", "updated"]
BANNED = re.compile(r"jnpf|引迈|xing", re.I)


# ---------------------------------------------------------------- front matter
def _mini_yaml(text: str) -> dict:
    """足够解析本项目 front matter 的简易 YAML：标量、行内列表、块列表、# 注释。"""
    data: dict = {}
    key = None
    for raw in text.splitlines():
        line = raw.split(" #")[0].rstrip() if not raw.strip().startswith("#") else ""
        if not line.strip():
            continue
        m = re.match(r"^(\w[\w-]*):\s*(.*)$", line)
        if m:
            key, val = m.group(1), m.group(2).strip()
            if val == "":
                data[key] = []  # 块列表或空
            elif val.startswith("[") and val.endswith("]"):
                inner = val[1:-1].strip()
                data[key] = [_scalar(v) for v in re.split(r",\s*", inner)] if inner else []
            else:
                data[key] = _scalar(val)
        elif re.match(r"^\s+-\s+", line) and key is not None:
            if not isinstance(data.get(key), list):
                data[key] = []
            data[key].append(_scalar(re.sub(r"^\s+-\s+", "", line)))
    return data


def _scalar(v: str):
    v = v.strip()
    if len(v) >= 2 and v[0] == v[-1] and v[0] in "\"'":
        return v[1:-1]
    if re.fullmatch(r"-?\d+", v):
        return int(v)
    return v


def parse_task(path: Path) -> tuple[dict, str]:
    text = path.read_text(encoding="utf-8")
    m = re.match(r"^---\n(.*?)\n---\n?(.*)$", text, re.S)
    if not m:
        raise ValueError(f"{path.name}: 缺少 front matter")
    fm_text, body = m.group(1), m.group(2)
    try:
        import yaml  # type: ignore

        fm = yaml.safe_load(fm_text) or {}
    except ImportError:
        fm = _mini_yaml(fm_text)
    for k in ("depends", "refs"):
        if fm.get(k) is None:
            fm[k] = []
    if fm.get("owner") is None:
        fm["owner"] = ""
    if isinstance(fm.get("updated"), (dt.date, dt.datetime)):
        fm["updated"] = fm["updated"].isoformat()[:10]
    return fm, body


def load_tasks() -> list[dict]:
    tasks = []
    for p in sorted(TASKS_DIR.glob("*.md")):
        fm, body = parse_task(p)
        fm["_file"] = p.name
        fm["_body"] = body
        tasks.append(fm)
    return tasks


# ---------------------------------------------------------------- validate
def validate(tasks: list[dict]) -> list[str]:
    errors: list[str] = []
    ids = {}
    for t in tasks:
        f = t["_file"]
        for k in REQUIRED:
            if k not in t:
                errors.append(f"{f}: 缺字段 {k}")
        tid = str(t.get("id", ""))
        if f != f"{tid}.md":
            errors.append(f"{f}: 文件名应为 {tid}.md")
        if tid in ids:
            errors.append(f"{f}: id 重复（{ids[tid]}）")
        ids[tid] = f
        if not re.fullmatch(r"S[1-4]-[A-Z]+-\d{3}", tid):
            errors.append(f"{f}: id 格式应为 S<阶段>-<AREA>-<三位序号>")
        if t.get("status") not in STATUSES:
            errors.append(f"{f}: status 非法 {t.get('status')}")
        if t.get("stage") not in STAGES:
            errors.append(f"{f}: stage 非法 {t.get('stage')}")
        if t.get("area") not in AREAS:
            errors.append(f"{f}: area 非法 {t.get('area')}")
        if t.get("priority") not in PRIORITIES:
            errors.append(f"{f}: priority 非法 {t.get('priority')}")
        if t.get("type") not in TYPES:
            errors.append(f"{f}: type 非法 {t.get('type')}")
        if not isinstance(t.get("wave"), int) or not 0 <= t["wave"] <= 4:
            errors.append(f"{f}: wave 应为 0–4 的整数")
        if not re.fullmatch(r"\d+(\.\d+)?d", str(t.get("estimate", ""))):
            errors.append(f"{f}: estimate 应形如 3d / 0.5d")
        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", str(t.get("updated", ""))):
            errors.append(f"{f}: updated 应为 YYYY-MM-DD")
        if tid and tid.split("-")[0] != t.get("stage"):
            errors.append(f"{f}: id 的阶段前缀与 stage 不一致")
        if tid and tid.split("-")[1] != t.get("area"):
            errors.append(f"{f}: id 的 AREA 与 area 不一致")
        if not isinstance(t.get("depends"), list) or not isinstance(t.get("refs"), list):
            errors.append(f"{f}: depends / refs 应为列表")
        for r in t.get("refs", []):
            rel = str(r).split("#")[0]
            if rel and not (ROOT / rel).exists():
                errors.append(f"{f}: refs 指向不存在的文件 {rel}")
        if BANNED.search(t.get("title", "") + t["_body"]):
            errors.append(f"{f}: 含禁用关键字")
        for sec in ("## 背景", "## 验收", "## 进度日志"):
            if sec not in t["_body"]:
                errors.append(f"{f}: 正文缺少「{sec}」")
        if "someone" in re.findall(r"^- \d{4}-\d{2}-\d{2} ([^：\s]+)：", t["_body"], flags=re.M):
            errors.append(f"{f}: 进度日志署名不能是 someone，写实际代理名（pm.py 用 --by 或 PM_ACTOR）")
    # depends 存在性与环
    for t in tasks:
        for d in t.get("depends", []):
            if d not in ids:
                errors.append(f"{t['_file']}: depends 指向不存在的任务 {d}")
    graph = {t["id"]: [d for d in t.get("depends", []) if d in ids] for t in tasks if "id" in t}
    state: dict[str, int] = {}

    def dfs(n: str, stack: list[str]):
        state[n] = 1
        for m in graph.get(n, []):
            if state.get(m) == 1:
                errors.append(f"依赖环：{' -> '.join(stack + [n, m])}")
            elif state.get(m) is None:
                dfs(m, stack + [n])
        state[n] = 2

    for n in graph:
        if state.get(n) is None:
            dfs(n, [])
    return errors


# ---------------------------------------------------------------- build
def _md_to_html(md: str) -> str:
    out: list[str] = []
    in_list = False
    for line in md.splitlines():
        s = line.rstrip()
        if not s:
            if in_list:
                out.append("</ul>")
                in_list = False
            continue
        if s.startswith("## "):
            if in_list:
                out.append("</ul>")
                in_list = False
            out.append(f"<h4>{_inline(s[3:])}</h4>")
            continue
        m = re.match(r"^\s*-\s+(\[( |x|X)\]\s+)?(.*)$", s)
        if m:
            if not in_list:
                out.append("<ul>")
                in_list = True
            if m.group(1):
                checked = m.group(2).lower() == "x"
                out.append(
                    f"<li class='chk{' done' if checked else ''}'><span class='box'>{'✓' if checked else ''}</span>{_inline(m.group(3))}</li>")
            else:
                out.append(f"<li>{_inline(m.group(3))}</li>")
            continue
        if in_list:
            out.append("</ul>")
            in_list = False
        out.append(f"<p>{_inline(s)}</p>")
    if in_list:
        out.append("</ul>")
    return "\n".join(out)


def _inline(s: str) -> str:
    s = html.escape(s)
    s = re.sub(r"`([^`]+)`", r"<code>\1</code>", s)
    s = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", s)
    return s


def _est_days(e: str) -> float:
    m = re.fullmatch(r"(\d+(?:\.\d+)?)d", str(e))
    return float(m.group(1)) if m else 0.0


def _order_ranks(tasks: list[dict]) -> dict[str, int]:
    """可做顺序：0 = 没有未完成前置（现在就能开始）；n = 等它的未完成前置里最深的一层 + 1。done 任务为 -1。"""
    by = {t["id"]: t for t in tasks}
    memo: dict[str, int] = {}

    def rank(tid: str, stack: tuple = ()) -> int:
        if tid in memo:
            return memo[tid]
        t = by.get(tid)
        if t is None or t["status"] == "done":
            memo[tid] = -1
            return -1
        if tid in stack:  # 环由 validate 报错，这里只防死循环
            return 0
        unmet = [d for d in t.get("depends", []) if d in by and by[d]["status"] != "done"]
        memo[tid] = 0 if not unmet else 1 + max(rank(d, stack + (tid,)) for d in unmet)
        return memo[tid]

    return {t["id"]: rank(t["id"]) for t in tasks}


def export(tasks: list[dict]) -> list[dict]:
    done_ids = {t["id"] for t in tasks if t["status"] == "done"}
    ranks = _order_ranks(tasks)
    rows = []
    for t in tasks:
        acc_total = len(re.findall(r"^\s*-\s+\[( |x|X)\]", t["_body"], re.M))
        acc_done = len(re.findall(r"^\s*-\s+\[(x|X)\]", t["_body"], re.M))
        rows.append({
            "id": t["id"], "title": t["title"], "status": t["status"], "stage": t["stage"],
            "area": t["area"], "areaName": AREAS.get(t["area"], t["area"]), "wave": t["wave"],
            "priority": t["priority"], "type": t["type"], "owner": t["owner"] or "",
            "estimate": t["estimate"], "days": _est_days(t["estimate"]), "depends": t["depends"],
            "blockedBy": [d for d in t["depends"] if d not in done_ids],
            "order": ranks[t["id"]],
            "readyNow": t["status"] == "todo" and not [d for d in t["depends"] if d not in done_ids],
            "refs": [{"path": str(r), "url": REPO_BLOB + str(r).split("#")[0]} for r in t["refs"]],
            "updated": str(t["updated"]), "acceptance": {"done": acc_done, "total": acc_total},
            "bodyHtml": _md_to_html(t["_body"]), "file": f"pm/tasks/{t['_file']}",
        })
    return rows


def status_md(rows: list[dict]) -> str:
    def bar(done: int, total: int) -> str:
        pct = 0 if total == 0 else round(done * 100 / total)
        filled = pct // 10
        return f"`{'█' * filled}{'░' * (10 - filled)}` {pct}%（{done}/{total}）"

    lines = ["# 进度概览（由 `python3 scripts/pm.py build` 生成，勿手改）", ""]
    total = len(rows)
    done = sum(r["status"] == "done" for r in rows)
    lines.append(f"- 总体：{bar(done, total)}")
    by_status = {s: sum(r['status'] == s for r in rows) for s in STATUSES}
    lines.append("- 状态：" + " · ".join(f"{s} {n}" for s, n in by_status.items()))
    lines.append(f"- 最近更新：{max((r['updated'] for r in rows), default='—')}")
    lines.append("")
    for title, key in (("按阶段", "stage"), ("按波次", "wave"), ("按领域", "area"), ("按负责人", "owner")):
        lines.append(f"## {title}")
        lines.append("")
        lines.append("| 分组 | 进度 | 进行中 | 阻塞 |")
        lines.append("|---|---|---|---|")
        groups: dict = {}
        for r in rows:
            k = r[key] if r[key] != "" else "（未认领）"
            groups.setdefault(k, []).append(r)
        for k in sorted(groups, key=lambda x: str(x)):
            g = groups[k]
            label = f"{k} {AREAS.get(k, '')}".strip() if key == "area" else (f"第 {k} 波" if key == "wave" and k != 0 else ("决策" if key == "wave" else str(k)))
            lines.append(
                f"| {label} | {bar(sum(r['status'] == 'done' for r in g), len(g))} | {sum(r['status'] == 'doing' for r in g)} | {sum(1 for r in g if r['status'] == 'blocked' or (r['blockedBy'] and r['status'] != 'done'))} |")
        lines.append("")
    lines.append("## 接下来可做（无未完成前置的待办，按优先级 / 波次）")
    lines.append("")
    ready = sorted([r for r in rows if r["readyNow"]], key=lambda r: (r["priority"], r["wave"], r["id"]))
    if ready:
        lines.append("| # | 任务 | 优先级 | 波次 | 估算 | 领域 |")
        lines.append("|---|---|---|---|---|---|")
        for i, r in enumerate(ready, 1):
            lines.append(f"| {i} | [{r['id']}]({r['file'].replace('pm/', '')}) {r['title']} | {r['priority']} | {'决策' if r['wave'] == 0 else r['wave']} | {r['estimate']} | {r['area']} |")
    else:
        lines.append("（无）")
    lines.append("")
    lines.append("## 待办的可做层级（第 1 层 = 现在可做，第 n 层 = 等前面 n-1 层完成）")
    lines.append("")
    levels: dict = {}
    for r in rows:
        if r["status"] in ("todo", "blocked"):
            levels.setdefault(r["order"], []).append(r)
    lines.append("| 层级 | 任务数 | 合计估算 |")
    lines.append("|---|---|---|")
    for k in sorted(levels):
        g = levels[k]
        lines.append(f"| 第 {k + 1} 层 | {len(g)} | {sum(r['days'] for r in g):g}d |")
    lines.append("")
    lines.append("## 进行中")
    lines.append("")
    doing = [r for r in rows if r["status"] in ("doing", "review")]
    if doing:
        lines.append("| 任务 | 状态 | 负责人 | 更新 |")
        lines.append("|---|---|---|---|")
        for r in doing:
            lines.append(f"| [{r['id']}]({r['file'].replace('pm/', '')}) {r['title']} | {r['status']} | {r['owner'] or '—'} | {r['updated']} |")
    else:
        lines.append("（无）")
    lines.append("")
    return "\n".join(lines)


def render(rows: list[dict]) -> str:
    tpl = TEMPLATE.read_text(encoding="utf-8")
    payload = json.dumps(rows, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    return tpl.replace("/*__TASKS_JSON__*/[]", payload)


def build(write: bool = True) -> dict[str, str]:
    tasks = load_tasks()
    errs = validate(tasks)
    if errs:
        print("\n".join(errs))
        sys.exit(1)
    rows = export(tasks)
    outputs = {
        "pm/tasks.json": json.dumps(rows, ensure_ascii=False, indent=1) + "\n",
        "pm/index.html": render(rows),
        "pm/STATUS.md": status_md(rows),
    }
    if write:
        for rel, content in outputs.items():
            (ROOT / rel).write_text(content, encoding="utf-8")
        print(f"已生成 {', '.join(outputs)}（{len(rows)} 个任务）")
    return outputs


# ---------------------------------------------------------------- mutate
def next_id(stage: str, area: str, tasks: list[dict]) -> str:
    nums = [int(t["id"].split("-")[2]) for t in tasks if t["id"].startswith(f"{stage}-{area}-")]
    return f"{stage}-{area}-{(max(nums) + 1 if nums else 1):03d}"


def require_actor(a: argparse.Namespace) -> str:
    """进度日志必须署实际代理名；缺省不再写成 someone（S1-DEC-019 R71）。"""
    actor = (a.by or "").strip()
    if not actor or actor == "someone" or "：" in actor or any(ch.isspace() for ch in actor):
        sys.exit("请用 --by <代理名> 或环境变量 PM_ACTOR 指明日志署名（如 cody、claude、planner），不能缺省或写 someone。")
    return actor


def cmd_add(a: argparse.Namespace) -> None:
    a.by = require_actor(a)
    tasks = load_tasks()
    tid = next_id(a.stage, a.area, tasks)
    today = dt.date.today().isoformat()
    deps = [d.strip() for d in (a.depends or "").split(",") if d.strip()]
    refs = a.ref or []
    fm = [
        "---", f"id: {tid}", f"title: {a.title}", "status: todo", f"stage: {a.stage}", f"area: {a.area}",
        f"wave: {a.wave}", f"priority: {a.priority}", f"type: {a.type}", 'owner: ""', f"estimate: {a.estimate}",
        "depends: [" + ", ".join(deps) + "]",
    ]
    fm.append("refs:" if refs else "refs: []")
    fm += [f"  - {r}" for r in refs]
    fm += [f"updated: {today}", "---", "", "## 背景", "", a.background or "（待补充）", "", "## 验收", "",
           "- [ ] （待补充）", "", "## 进度日志", "", f"- {today} {a.by}：创建任务", ""]
    path = TASKS_DIR / f"{tid}.md"
    path.write_text("\n".join(fm), encoding="utf-8")
    print(f"已创建 {path.relative_to(ROOT)}")


def cmd_set(a: argparse.Namespace) -> None:
    a.by = require_actor(a)
    path = TASKS_DIR / f"{a.id}.md"
    if not path.exists():
        sys.exit(f"任务不存在：{a.id}")
    text = path.read_text(encoding="utf-8")
    today = dt.date.today().isoformat()
    if a.status:
        if a.status not in STATUSES:
            sys.exit(f"status 非法：{a.status}")
        text = re.sub(r"^status: .*$", f"status: {a.status}", text, count=1, flags=re.M)
    if a.owner is not None:
        text = re.sub(r"^owner: .*$", f'owner: "{a.owner}"', text, count=1, flags=re.M)
    text = re.sub(r"^updated: .*$", f"updated: {today}", text, count=1, flags=re.M)
    note = a.note or (f"状态 → {a.status}" if a.status else "更新")
    text = text.rstrip("\n") + f"\n- {today} {a.by}：{note}\n"
    path.write_text(text, encoding="utf-8")
    print(f"已更新 {a.id}")


def cmd_list(a: argparse.Namespace) -> None:
    rows = export(load_tasks())
    for r in rows:
        if a.status and r["status"] != a.status:
            continue
        if a.area and r["area"] != a.area:
            continue
        if a.owner is not None and r["owner"] != a.owner:
            continue
        if a.stage and r["stage"] != a.stage:
            continue
        flag = " ⛔" if r["blockedBy"] and r["status"] not in ("done",) else ""
        print(f"{r['id']:<12} {r['status']:<8} {r['priority']} w{r['wave']} {r['owner'] or '-':<8} {r['title']}{flag}")


def cmd_stats(_: argparse.Namespace) -> None:
    print(status_md(export(load_tasks())))


def cmd_check_fresh(_: argparse.Namespace) -> None:
    outputs = build(write=False)
    stale = [rel for rel, content in outputs.items()
             if not (ROOT / rel).exists() or (ROOT / rel).read_text(encoding="utf-8") != content]
    if stale:
        sys.exit("生成物过期，请运行 python3 scripts/pm.py build 并提交：" + ", ".join(stale))
    print("生成物与任务文件一致")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("validate")
    sub.add_parser("build")
    sub.add_parser("check-fresh")
    sub.add_parser("stats")
    p = sub.add_parser("list")
    p.add_argument("--status"); p.add_argument("--area"); p.add_argument("--owner"); p.add_argument("--stage")
    p = sub.add_parser("add")
    p.add_argument("--stage", required=True, choices=STAGES); p.add_argument("--area", required=True, choices=sorted(AREAS))
    p.add_argument("--title", required=True); p.add_argument("--priority", default="P1", choices=PRIORITIES)
    p.add_argument("--wave", type=int, default=1); p.add_argument("--type", default="feature", choices=TYPES)
    p.add_argument("--estimate", default="1d"); p.add_argument("--depends"); p.add_argument("--ref", action="append")
    p.add_argument("--background"); p.add_argument("--by", default=os.environ.get("PM_ACTOR"))
    p = sub.add_parser("set")
    p.add_argument("id"); p.add_argument("--status", choices=STATUSES); p.add_argument("--owner"); p.add_argument("--note")
    p.add_argument("--by", default=os.environ.get("PM_ACTOR"))
    a = ap.parse_args()
    TASKS_DIR.mkdir(parents=True, exist_ok=True)
    if a.cmd == "validate":
        errs = validate(load_tasks())
        if errs:
            print("\n".join(errs)); sys.exit(1)
        print(f"校验通过（{len(list(TASKS_DIR.glob('*.md')))} 个任务）")
    elif a.cmd == "build":
        build()
    elif a.cmd == "check-fresh":
        cmd_check_fresh(a)
    elif a.cmd == "stats":
        cmd_stats(a)
    elif a.cmd == "list":
        cmd_list(a)
    elif a.cmd == "add":
        cmd_add(a)
    elif a.cmd == "set":
        cmd_set(a)


if __name__ == "__main__":
    main()
