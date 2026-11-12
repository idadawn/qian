#!/usr/bin/env python3
"""staging 的端到端核验（S1-DEP-011）。

  python3 scripts/staging-e2e.py --shell https://qian-staging.deepsingularity.cn --issuer https://qian-staging-id.deepsingularity.cn \
      --operator <平台运营用户名> --operator-password-file <文件> [--operator-new-password-file <文件>] [--out <证据.json>]

经公网主机名、照外壳的真实路径（身份发行方的账号接口 → 网关 /auth/login 授权码 → 网关路由到平台）走一遍任务验收里能从外部验证的部分：

1. 匿名：发行方发现文档的 issuer 与 --issuer 一致；外壳首页与 /app-config.json 可取；两个主机上 /__dev/ 下的路由都不是 200。
2. 平台运营登录：首登要求改密时改成新密码。新密码取自 --operator-new-password-file；文件不存在就生成一个写进去（权限 0600），
   不打印。经网关登录后 bootstrap 不代管，平台控制台挂载租户运营与开通申请两个平台组件。
3. 运营开户：新建两个管理员账号，开甲乙两个租户并激活（经网关的运营簇）。
4. 甲方管理员首登改密、选甲方、经网关登录：工作台上下文是甲方，不代管，本租户摘要可读。
5. 乙方管理员读甲方摘要 403 AUTHZ_DENIED，读运营接口的租户列表 403 / 404。
6. D4（R150 ④）：发验证码 503 SIGNUP_UNAVAILABLE；之后运营再开一户并激活，照常。

只用标准库（Python 3.9+）。不经系统代理。密码只从文件读或在本次运行中生成，不打印、不写进证据；
测试数据（两个管理员账号、三个租户）留在 staging，代码与用户名带本次运行的后缀。证据 JSON 写到 --out 或标准输出，任何一步不符即退出码 1。
"""
from __future__ import annotations

import argparse
import http.cookiejar
import json
import os
from pathlib import Path
import secrets
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone

CLIENT_ID = "qian-gateway"
USER_AGENT = "qian-staging-e2e/1"


class Failed(Exception):
    pass


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    # 重定向由调用方逐跳核对（授权码流程的每一跳都要看 Location），这里不自动跟随。
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Browser:
    """一个浏览器会话：自己的 Cookie 罐（按主机与 Secure 属性发送），不自动跟随重定向。"""

    def __init__(self, name: str):
        self.name = name
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), _NoRedirect(), urllib.request.HTTPCookieProcessor(self.jar))

    def send(self, method: str, url: str, body=None, headers=None):
        data = None if body is None else json.dumps(body).encode("utf-8")
        merged = {"Accept": "application/json", "User-Agent": USER_AGENT}
        if body is not None:
            merged["Content-Type"] = "application/json"
        merged.update(headers or {})
        request = urllib.request.Request(url, data=data, method=method, headers=merged)
        try:
            with self.opener.open(request, timeout=30) as response:
                return response.status, response.headers, response.read()
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read()

    def json(self, method: str, url: str, body=None, headers=None, expected=200):
        status, _, payload = self.send(method, url, body, headers)
        if status != expected:
            raise Failed(f"{self.name}: {method} {path_of(url)} 期望 {expected}，实际 {status}：{payload[:500].decode('utf-8', 'replace')}")
        return json.loads(payload) if payload else None


def path_of(url: str) -> str:
    parsed = urllib.parse.urlsplit(url)
    return parsed.path + ("?" + parsed.query if parsed.query else "")


def uuid7() -> str:
    """UUIDv7：48 位毫秒时间戳 + 版本与变体位 + 随机数（Python 3.9 没有 uuid.uuid7）。"""
    value = (int(time.time() * 1000) << 80) | secrets.randbits(80)
    value = (value & ~(0xF << 76)) | (0x7 << 76)
    value = (value & ~(0x3 << 62)) | (0x2 << 62)
    return str(uuid.UUID(int=value))


def new_password() -> str:
    return "E2e-" + secrets.token_urlsafe(18) + "-7q"


def read_secret(path: Path) -> str:
    value = path.read_text(encoding="utf-8").strip()
    if not value:
        raise Failed(f"{path} 是空的。")
    return value


def write_secret(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
        handle.write(value + "\n")


class Run:
    def __init__(self, args):
        self.shell = args.shell.rstrip("/")
        self.issuer = args.issuer.rstrip("/")
        self.steps = []
        self.suffix = secrets.token_hex(3)

    def step(self, name: str, **detail):
        self.steps.append({"step": name, "at": datetime.now(timezone.utc).isoformat(timespec="seconds"), **detail})
        print(f"[通过] {name}", file=sys.stderr)

    # 发行方账号接口的密码登录：先取防伪令牌；首登要求改密时换成 replacement()；要求选择时选 tenant 的成员身份。返回第一步的状态。
    def identity_sign_in(self, browser: Browser, identifier: str, password: str, tenant: str | None, replacement) -> str:
        token = browser.json("GET", self.issuer + "/api/identity/csrf")
        headers = {token["header"]: token["token"]}
        result = browser.json("POST", self.issuer + "/api/identity/login",
                              {"client_id": CLIENT_ID, "identifier": identifier, "password": password, "tenant_id": tenant}, headers)
        first = result["status"]
        for _ in range(3):
            status = result["status"]
            if status == "authenticated":
                return first
            if status == "password_change_required":
                result = browser.json("POST", self.issuer + "/api/identity/login/password", {"ticket": result["ticket"], "new_password": replacement()}, headers)
            elif status == "selection_required":
                choice = next((item for item in result["choices"] if item.get("tenant_id") == tenant), None)
                if choice is None:
                    raise Failed(f"{browser.name}: 选择列表里没有租户 {tenant}。")
                result = browser.json("POST", self.issuer + "/api/identity/login/select", {"ticket": result["ticket"], "membership_id": choice["membership_id"]}, headers)
            else:
                raise Failed(f"{browser.name}: 登录状态不认识：{status}")
        if result["status"] != "authenticated":
            raise Failed(f"{browser.name}: 三步之内没有完成登录（{result['status']}）。")
        return first

    # 网关 BFF 登录：/auth/login → 发行方授权端点（已有账号 Cookie，直接签发授权码）→ 网关回调 → 回到外壳根路径。
    def gateway_sign_in(self, browser: Browser) -> None:
        status, headers, _ = browser.send("GET", self.shell + "/auth/login?returnTo=%2F")
        authorize = headers.get("Location") if status == 302 else None
        if not authorize or urllib.parse.urlsplit(authorize).netloc != urllib.parse.urlsplit(self.issuer).netloc:
            raise Failed(f"{browser.name}: /auth/login 没有重定向到发行方（{status} {authorize}）。")
        status, headers, payload = browser.send("GET", authorize)
        callback = headers.get("Location") if status == 302 else None
        if not callback or urllib.parse.urlsplit(callback).netloc != urllib.parse.urlsplit(self.shell).netloc:
            raise Failed(f"{browser.name}: 授权端点没有带授权码回到网关（{status} {payload[:300]!r}）。")
        status, headers, payload = browser.send("GET", callback)
        if status != 302 or headers.get("Location") != "/":
            raise Failed(f"{browser.name}: 网关回调没有完成登录（{status} {headers.get('Location')} {payload[:300]!r}）。")

    def csrf(self, browser: Browser) -> dict:
        token = browser.json("GET", self.shell + "/auth/csrf")
        return {token["headerName"]: token["token"]}

    def new_admin(self, browser: Browser, csrf: dict, display_name: str, username: str, password: str) -> str:
        created = browser.json("POST", self.shell + "/api/tenancy/admin-accounts",
                               {"displayName": display_name, "identifierKind": "username", "identifier": username, "initialPassword": password}, csrf, expected=201)
        return created["subjectId"]

    def open_tenant(self, browser: Browser, csrf: dict, code: str, name: str, administrator: str) -> str:
        created = browser.json("POST", self.shell + "/api/tenancy/tenants",
                               {"code": code, "name": name, "administrator": {"membershipId": uuid7(), "subjectId": administrator}}, csrf, expected=201)
        tenant = created["id"]
        active = browser.json("POST", self.shell + f"/api/tenancy/tenants/{tenant}:activate", {}, csrf)
        if active.get("status") != "active":
            raise Failed(f"租户 {code} 激活后的状态是 {active.get('status')}。")
        return tenant


def main() -> int:
    parser = argparse.ArgumentParser(description="staging 的端到端核验（S1-DEP-011）")
    parser.add_argument("--shell", required=True, help="外壳（网关）的地址，如 https://qian-staging.deepsingularity.cn")
    parser.add_argument("--issuer", required=True, help="身份发行方地址，如 https://qian-staging-id.deepsingularity.cn")
    parser.add_argument("--operator", required=True, help="平台运营用户名")
    parser.add_argument("--operator-password-file", required=True, type=Path, help="平台运营当前密码的文件")
    parser.add_argument("--operator-new-password-file", type=Path, help="首登改密时的新密码文件：存在就用，不存在就生成并写入（0600）")
    parser.add_argument("--out", type=Path, help="证据 JSON 的输出文件，缺省写标准输出")
    args = parser.parse_args()
    run = Run(args)
    try:
        # 1 匿名
        anonymous = Browser("匿名")
        discovery = anonymous.json("GET", run.issuer + "/.well-known/openid-configuration")
        if discovery.get("issuer", "").rstrip("/") != run.issuer:
            raise Failed(f"发现文档的 issuer 是 {discovery.get('issuer')}。")
        status, _, page = anonymous.send("GET", run.shell + "/", headers={"Accept": "text/html"})
        if status != 200 or b"<html" not in page.lower():
            raise Failed(f"外壳首页 {status}。")
        anonymous.json("GET", run.shell + "/app-config.json")
        dev = {}
        for base in (run.issuer, run.shell):
            for path in ("/__dev/signup/outbox?phone=13900000000", "/__dev/identity/bump"):
                dev[base + path] = anonymous.send("GET", base + path)[0]
        if any(code == 200 for code in dev.values()):
            raise Failed(f"开发路由可达：{dev}")
        run.step("匿名：发现文档、外壳首页与运行配置可取，/__dev/ 不可达", issuer=discovery["issuer"], dev=dev)

        # 2 平台运营
        operator = Browser("平台运营")
        current = read_secret(args.operator_password_file)

        def operator_replacement() -> str:
            if args.operator_new_password_file is None:
                raise Failed("平台运营要求首登改密，但没有给 --operator-new-password-file。")
            if args.operator_new_password_file.exists():
                return read_secret(args.operator_new_password_file)
            value = new_password()
            write_secret(args.operator_new_password_file, value)
            return value

        first = run.identity_sign_in(operator, args.operator, current, None, operator_replacement)
        run.gateway_sign_in(operator)
        console = operator.json("GET", run.shell + "/api/shell/bootstrap")
        mounted = sorted(console.get("mounted") or [])
        routes = sorted(item.get("route") for item in console.get("navigation") or [])
        if console.get("managed") is not False or not {"global|qian.tenancy", "global|qian.tenancy.signup"} <= set(mounted):
            raise Failed(f"平台控制台不对：managed={console.get('managed')} mounted={mounted}")
        run.step("平台运营经外壳登录进平台控制台", firstStatus=first, mounted=mounted, navigation=routes)

        # 3 运营开户：甲乙两户
        csrf = run.csrf(operator)
        initial = new_password()
        admin_a = run.new_admin(operator, csrf, "甲方管理员", f"e2e{run.suffix}a", initial)
        admin_b = run.new_admin(operator, csrf, "乙方管理员", f"e2e{run.suffix}b", initial)
        tenant_a = run.open_tenant(operator, csrf, f"e2e{run.suffix}a", f"E2E 甲方 {run.suffix}", admin_a)
        tenant_b = run.open_tenant(operator, csrf, f"e2e{run.suffix}b", f"E2E 乙方 {run.suffix}", admin_b)
        listed = [item["id"] for item in operator.json("GET", run.shell + "/api/tenancy/tenants?keyword=e2e" + run.suffix)["items"]]
        if tenant_a not in listed or tenant_b not in listed:
            raise Failed(f"运营列表里找不到新开的租户：{listed}")
        run.step("运营新建管理员账号、开户并激活两个租户", tenants={"a": tenant_a, "b": tenant_b}, admins={"a": admin_a, "b": admin_b})

        # 4 甲方管理员
        admin = Browser("甲方管理员")
        first = run.identity_sign_in(admin, f"e2e{run.suffix}a", initial, tenant_a, new_password)
        run.gateway_sign_in(admin)
        workbench = admin.json("GET", run.shell + "/api/shell/bootstrap")
        context = ((workbench.get("context") or {}).get("tenant") or {}).get("id")
        if workbench.get("managed") is not False or context != tenant_a:
            raise Failed(f"甲方工作台不对：managed={workbench.get('managed')} tenant={context}")
        summary = admin.json("GET", run.shell + f"/api/tenancy/tenants/{tenant_a}/summary")
        if (summary.get("code"), summary.get("administratorName")) != (f"e2e{run.suffix}a", "甲方管理员"):
            raise Failed(f"甲方摘要不对：{summary}")
        run.step("甲方管理员首登改密、选租户后进入工作台，本租户摘要可读", firstStatus=first, tenant=context)

        # 5 乙方管理员看不到甲方
        other = Browser("乙方管理员")
        run.identity_sign_in(other, f"e2e{run.suffix}b", initial, tenant_b, new_password)
        run.gateway_sign_in(other)
        own = ((other.json("GET", run.shell + "/api/shell/bootstrap").get("context") or {}).get("tenant") or {}).get("id")
        status, _, payload = other.send("GET", run.shell + f"/api/tenancy/tenants/{tenant_a}/summary")
        code = json.loads(payload).get("code") if payload else None
        listing = other.send("GET", run.shell + "/api/tenancy/tenants")[0]
        if own != tenant_b or status != 403 or code != "AUTHZ_DENIED" or listing not in (403, 404):
            raise Failed(f"租户隔离不对：工作台 {own}，读甲方摘要 {status} {code}，读运营列表 {listing}")
        run.step("乙方管理员看不到甲方的数据", summary=[status, code], operationsList=listing)

        # 6 D4：发码 503，运营开户不受影响
        applicant = Browser("申请人")
        origin = {"Origin": run.issuer}
        status, _, payload = applicant.send("POST", run.issuer + "/api/signup/verifications", {"phone": "13912345678"}, origin)
        code = json.loads(payload).get("code") if payload else None
        if (status, code) != (503, "SIGNUP_UNAVAILABLE"):
            raise Failed(f"发验证码期望 503 SIGNUP_UNAVAILABLE，实际 {status} {code}")
        admin_c = run.new_admin(operator, run.csrf(operator), "丙方管理员", f"e2e{run.suffix}c", initial)
        tenant_c = run.open_tenant(operator, run.csrf(operator), f"e2e{run.suffix}c", f"E2E 丙方 {run.suffix}", admin_c)
        run.step("D4：发验证码 503 SIGNUP_UNAVAILABLE，之后运营开户照常", signup=[status, code], tenant=tenant_c)
    except Failed as error:
        print(f"[失败] {error}", file=sys.stderr)
        emit(args, run, ok=False, failure=str(error))
        return 1
    emit(args, run, ok=True)
    return 0


def emit(args, run: Run, ok: bool, failure: str | None = None) -> None:
    document = {"format": "qian.staging-e2e/v1", "shell": run.shell, "issuer": run.issuer, "suffix": run.suffix, "ok": ok, "steps": run.steps}
    if failure:
        document["failure"] = failure
    text = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    if args.out:
        args.out.write_text(text, encoding="utf-8")
    else:
        sys.stdout.write(text)


if __name__ == "__main__":
    sys.exit(main())
