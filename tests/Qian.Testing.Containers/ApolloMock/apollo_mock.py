#!/usr/bin/env python3
"""Apollo 配置服务的协议替身，供 Qian 集成测试使用（S1-QA-001）。只依赖 Python 标准库。

覆盖 Apollo「其它语言客户端接入」公开 HTTP 协议中客户端会用到的部分：

  GET /services/config                                    Meta Server：返回本服务地址
  GET /configs/{appId}/{cluster}/{namespace}              拉配置；releaseKey 未变返回 304，未发布返回 404
  GET /configfiles/json/{appId}/{cluster}/{namespace}     配置的 JSON 视图
  GET /notifications/v2?appId=&cluster=&notifications=    长轮询：有更新立即返回，否则挂起至多 hold 秒后 304

访问密钥：给某个 appId 设置密钥后，配置与通知接口要求请求头
  Timestamp: <毫秒时间戳>
  Authorization: Apollo <appId>:<Base64(HMAC-SHA1(secret, timestamp + "\\n" + pathWithQuery))>
时间差超过 60 秒或签名不符返回 401，口径与 Apollo 服务端一致（path 取解码后的路径，query 取原文）。

测试控制面（不属于 Apollo 协议，统一以 /__mock 开头）：
  GET    /__mock/health
  PUT    /__mock/configs/{appId}/{cluster}/{namespace}   请求体 {"key": "value", ...}，发布一个新版本
  DELETE /__mock/configs/{appId}/{cluster}/{namespace}   撤下该 namespace，之后拉取返回 404
  PUT    /__mock/access-keys/{appId}                     请求体 {"secret": "..."}，开启签名校验
  DELETE /__mock/access-keys/{appId}
  PUT    /__mock/settings                                请求体 {"holdSeconds": 60}
  POST   /__mock/reset                                   清空全部状态

不覆盖：灰度发布与 label 规则、Portal / Admin Service 接口、多实例 Meta Server、增量同步、properties 文本视图。
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import hmac
import json
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Dict, List, Optional, Tuple
from urllib.parse import parse_qs, unquote, urlsplit

SIGNATURE_WINDOW_MS = 60_000
Key = Tuple[str, str, str]  # (appId, cluster, namespace)


def normalize_namespace(name: str) -> str:
    """Apollo 把 `application.properties` 与 `application` 视为同一个 namespace。"""
    return name[: -len(".properties")] if name.endswith(".properties") else name


class State:
    def __init__(self, hold_seconds: float) -> None:
        self.cond = threading.Condition()
        self.default_hold = hold_seconds
        self.hold_seconds = hold_seconds
        self.releases: Dict[Key, dict] = {}
        self.notification_ids: Dict[Key, int] = {}  # 撤下后仍保留，客户端才能收到变更通知
        self.access_keys: Dict[str, str] = {}
        self.notification_seq = 0
        self.release_seq = 0

    def reset(self) -> None:
        with self.cond:
            self.releases.clear()
            self.notification_ids.clear()
            self.access_keys.clear()
            self.hold_seconds = self.default_hold
            self.cond.notify_all()

    def publish(self, key: Key, configurations: Dict[str, str]) -> dict:
        with self.cond:
            self.release_seq += 1
            self.notification_seq += 1
            release_key = f"{time.strftime('%Y%m%d%H%M%S')}-{self.release_seq:08x}"
            self.releases[key] = {"configurations": dict(configurations), "releaseKey": release_key}
            self.notification_ids[key] = self.notification_seq
            self.cond.notify_all()
            return {"releaseKey": release_key, "notificationId": self.notification_seq}

    def delete(self, key: Key) -> Optional[dict]:
        with self.cond:
            if key not in self.releases:
                return None
            del self.releases[key]
            self.notification_seq += 1
            self.notification_ids[key] = self.notification_seq
            self.cond.notify_all()
            return {"notificationId": self.notification_seq}

    def changed(self, app_id: str, cluster: str, client: List[Tuple[str, int]]) -> List[dict]:
        out = []
        for sent_name, client_id in client:
            ns = normalize_namespace(sent_name)
            current = self.notification_ids.get((app_id, cluster, ns))
            if current is not None and current > client_id:
                out.append({
                    "namespaceName": sent_name,
                    "notificationId": current,
                    "messages": {"details": {f"{app_id}+{cluster}+{ns}": current}},
                })
        return out

    def poll(self, app_id: str, cluster: str, client: List[Tuple[str, int]]) -> Optional[List[dict]]:
        deadline = time.monotonic() + self.hold_seconds
        with self.cond:
            while True:
                result = self.changed(app_id, cluster, client)
                if result:
                    return result
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    return None
                self.cond.wait(remaining)


STATE = State(60)


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server_version = "qian-apollo-mock/1"

    # ---------- 输出 ----------
    def send_json(self, status: int, body: object) -> None:
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json;charset=UTF-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def send_empty(self, status: int) -> None:
        self.send_response(status)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def log_message(self, format: str, *args: object) -> None:  # noqa: A002 - 覆盖基类签名
        sys.stderr.write("apollo-mock %s\n" % (format % args))

    # ---------- 解析 ----------
    def target(self) -> Tuple[List[str], Dict[str, List[str]], str, str]:
        parts = urlsplit(self.path)
        segments = [unquote(s) for s in parts.path.split("/") if s]
        return segments, parse_qs(parts.query, keep_blank_values=True), unquote(parts.path), parts.query

    def read_body(self) -> bytes:
        # .NET 的 JsonContent 等客户端会用分块传输发请求体，不带 Content-Length。
        if "chunked" in (self.headers.get("Transfer-Encoding") or "").lower():
            chunks = []
            while True:
                size = int(self.rfile.readline().split(b";", 1)[0].strip() or b"0", 16)
                if size == 0:
                    while self.rfile.readline() not in (b"\r\n", b"\n", b""):
                        pass  # 丢弃 trailer
                    return b"".join(chunks)
                chunks.append(self.rfile.read(size))
                self.rfile.readline()  # 块尾的 CRLF
        length = int(self.headers.get("Content-Length") or 0)
        return self.rfile.read(length) if length else b""

    def read_json(self) -> object:
        raw = self.read_body()
        return json.loads(raw.decode("utf-8")) if raw else None

    def authorized(self, app_id: str, path: str, query: str) -> bool:
        secret = STATE.access_keys.get(app_id)
        if secret is None:
            return True
        timestamp = self.headers.get("Timestamp") or ""
        authorization = self.headers.get("Authorization") or ""
        try:
            skew = int(time.time() * 1000) - int(timestamp)
        except ValueError:
            return False
        if abs(skew) > SIGNATURE_WINDOW_MS or ":" not in authorization:
            return False
        path_with_query = f"{path}?{query}" if query else path
        digest = hmac.new(secret.encode("utf-8"), f"{timestamp}\n{path_with_query}".encode("utf-8"), hashlib.sha1).digest()
        expected = base64.b64encode(digest).decode("ascii")
        return hmac.compare_digest(authorization.rsplit(":", 1)[1], expected)

    # ---------- 路由 ----------
    def do_GET(self) -> None:  # noqa: N802 - http.server 约定
        seg, query, path, raw_query = self.target()
        if seg == ["__mock", "health"]:
            return self.send_json(200, {"status": "UP"})
        if seg[:1] == ["services"]:
            host = self.headers.get("Host") or "localhost"
            if seg == ["services", "config"]:
                return self.send_json(200, [{"appName": "APOLLO-CONFIGSERVICE", "instanceId": "qian-apollo-mock", "homepageUrl": f"http://{host}/"}])
            return self.send_json(200, [])
        if len(seg) == 4 and seg[0] == "configs":
            app_id, cluster, ns = seg[1], seg[2], normalize_namespace(seg[3])
            if not self.authorized(app_id, path, raw_query):
                return self.send_empty(401)
            release = STATE.releases.get((app_id, cluster, ns))
            if release is None:
                return self.send_empty(404)
            if query.get("releaseKey", [""])[0] == release["releaseKey"]:
                return self.send_empty(304)
            return self.send_json(200, {
                "appId": app_id,
                "cluster": cluster,
                "namespaceName": seg[3],
                "configurations": release["configurations"],
                "releaseKey": release["releaseKey"],
            })
        if len(seg) == 5 and seg[:2] == ["configfiles", "json"]:
            app_id, cluster, ns = seg[2], seg[3], normalize_namespace(seg[4])
            if not self.authorized(app_id, path, raw_query):
                return self.send_empty(401)
            release = STATE.releases.get((app_id, cluster, ns))
            return self.send_json(200, release["configurations"]) if release else self.send_empty(404)
        if seg == ["notifications", "v2"]:
            app_id = query.get("appId", [""])[0]
            cluster = query.get("cluster", ["default"])[0]
            if not app_id:
                return self.send_json(400, {"message": "appId is required"})
            if not self.authorized(app_id, path, raw_query):
                return self.send_empty(401)
            try:
                items = json.loads(query.get("notifications", ["[]"])[0])
                client = [(str(i["namespaceName"]), int(i.get("notificationId", -1))) for i in items]
            except (ValueError, KeyError, TypeError):
                return self.send_json(400, {"message": "invalid notifications"})
            result = STATE.poll(app_id, cluster, client)
            return self.send_json(200, result) if result else self.send_empty(304)
        return self.send_empty(404)

    def do_PUT(self) -> None:  # noqa: N802
        seg, _, _, _ = self.target()
        try:
            body = self.read_json()
        except ValueError:
            return self.send_json(400, {"message": "body must be JSON"})
        if len(seg) == 5 and seg[:2] == ["__mock", "configs"]:
            if not isinstance(body, dict) or not all(isinstance(k, str) and isinstance(v, str) for k, v in body.items()):
                return self.send_json(400, {"message": "configurations must be a string-to-string object"})
            return self.send_json(200, STATE.publish((seg[2], seg[3], normalize_namespace(seg[4])), body))
        if len(seg) == 3 and seg[:2] == ["__mock", "access-keys"]:
            if not isinstance(body, dict) or not isinstance(body.get("secret"), str) or not body["secret"]:
                return self.send_json(400, {"message": "secret is required"})
            STATE.access_keys[seg[2]] = body["secret"]
            return self.send_empty(204)
        if seg == ["__mock", "settings"]:
            hold = body.get("holdSeconds") if isinstance(body, dict) else None
            if not isinstance(hold, (int, float)) or hold < 0:
                return self.send_json(400, {"message": "holdSeconds must be a non-negative number"})
            STATE.hold_seconds = float(hold)
            return self.send_empty(204)
        return self.send_empty(404)

    def do_DELETE(self) -> None:  # noqa: N802
        self.read_body()  # 保持长连接可复用：即使不用也要读掉请求体
        seg, _, _, _ = self.target()
        if len(seg) == 5 and seg[:2] == ["__mock", "configs"]:
            result = STATE.delete((seg[2], seg[3], normalize_namespace(seg[4])))
            return self.send_json(200, result) if result else self.send_empty(404)
        if len(seg) == 3 and seg[:2] == ["__mock", "access-keys"]:
            STATE.access_keys.pop(seg[2], None)
            return self.send_empty(204)
        return self.send_empty(404)

    def do_POST(self) -> None:  # noqa: N802
        self.read_body()
        seg, _, _, _ = self.target()
        if seg == ["__mock", "reset"]:
            STATE.reset()
            return self.send_empty(204)
        return self.send_empty(404)


def main() -> None:
    parser = argparse.ArgumentParser(description="Apollo 配置服务协议替身")
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--hold-seconds", type=float, default=60, help="长轮询挂起秒数，Apollo 服务端为 60")
    args = parser.parse_args()
    STATE.default_hold = STATE.hold_seconds = args.hold_seconds
    server = ThreadingHTTPServer(("0.0.0.0", args.port), Handler)
    server.daemon_threads = True
    sys.stderr.write(f"apollo-mock listening on :{args.port}, hold {args.hold_seconds}s\n")
    server.serve_forever()


if __name__ == "__main__":
    main()
