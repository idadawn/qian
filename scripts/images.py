#!/usr/bin/env python3
"""profile 镜像的统一构建入口与供应链门禁（S1-DEP-003 / R106）。

  python3 scripts/images.py build --kind jit|aot --project <csproj> --assembly <程序集名> --tag <registry/名称:标签>
  python3 scripts/images.py selfcheck --tools <目录> [--out TestResults/images] [--fail-on high]

build：按 deploy/images/<kind>.Dockerfile 构建并推送，输出按 digest 的引用与构建证据（源码 SHA、KunVersion、
profile 与组件目录的 git 树修订）。推到哪个 registry 由 --tag 决定；向外部 registry 发布归 S1-DEP-008 / S1-DEP-005。

selfcheck：CI 自检（.github/workflows/images.yml）。在本机起临时 registry，构建 JIT 与 AOT 两个自检镜像，依次跑：
  1. Syft 按 digest 生成 SBOM，Grype 扫描；达到 --fail-on 的漏洞即失败，豁免写在
     deploy/images/vulnerability-allowlist.json，必须带理由与 YYYY-MM-DD 期限，过期失效；
     反例：已知含严重漏洞的组件清单经同一 Grype 与阈值判定，必须报出违规；
  2. Cosign 签名并附 SBOM 证明，验签约束发布者身份与 issuer；反例：缺签名、签名身份不符、issuer 不符、
     缺 SBOM 证明，四种都必须验不过，且报错原因与预期一致；
  3. dump-config 漂移门禁：源码树与镜像内对五个 profile 分别合成，逐项比较解析后的结构与来源层；
     反例：镜像内改动 patch 后必须报出含篡改内容的漂移；
  4. 运行期证据：镜像配置的用户不是 root；以只读根文件系统启动后自报有效 UID / GID 非 0、根挂载只读、
     写探针被拒、/tmp 可写；对照运行（不加只读）写探针必须可写，证明写失败来自只读根而不是权限。
签名用本次运行临时签发的 CA 与证书；cosign 的外网访问被不可达代理阻断，不写公共透明日志。结果写到
<out>/evidence.json，逐条命令及输出写到 <out>/commands.log。自检镜像带 io.qian.image-role=selfcheck，
只存在于本次运行的临时 registry，不是任何 profile 的产品镜像。
"""
from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
# 临时 registry 用 distribution 项目在 GHCR 发布的官方镜像，避开 Docker Hub 的拉取限流与镜像站差异。
REGISTRY_IMAGE = "ghcr.io/distribution/distribution:3.0.0@sha256:4ba3adf47f5c866e9a29288c758c5328ef03396cb8f5f6454463655fa8bc83e2"
PROFILES = ("platform", "identity", "gateway", "standalone", "minimal")
SEVERITIES = ("negligible", "low", "medium", "high", "critical")
# 自检签名模拟 CI 发布者：GitHub Actions 工作流身份与 OIDC issuer，由本次运行的临时 CA 写进证书。
SIGNER_IDENTITY = "https://github.com/idadawn/qian/.github/workflows/images.yml@refs/heads/main"
SIGNER_ISSUER = "https://token.actions.githubusercontent.com"
SELFCHECK_IMAGES = {
    "jit": ("tools/Qian.Image.SelfCheck/Qian.Image.SelfCheck.csproj", "Qian.Image.SelfCheck", "qian-selfcheck-jit"),
    "aot": ("tools/Qian.Image.SelfCheck.Aot/Qian.Image.SelfCheck.Aot.csproj", "Qian.Image.SelfCheck.Aot", "qian-selfcheck-aot"),
}
TAMPER_PROFILE = "standalone"
TAMPER_PATCH = "- id: config\n  note: 镜像内篡改（漂移门禁反例）\n"
# 扫描阈值反例：已知含严重漏洞（CVE-2021-26701，critical）的 NuGet 组件，任何阈值下都必须报出违规。
SCAN_PROBE = ("System.Text.Encodings.Web", "4.5.0")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
ALLOWLIST = ROOT / "deploy" / "images" / "vulnerability-allowlist.json"
# 每个反例预期的失败原因（cosign v3.1 报错原文与篡改内容）。门禁失败但原因对不上（网络、配置错误）不算拦住。
NEGATIVE_REASONS = {
    "签名身份不符": "expected SAN value",
    "issuer 不符": "expected issuer value",
    "缺签名": "no signatures found",
    "缺 SBOM 证明": "none of the attestations matched the predicate type",
    "镜像内改动 patch": "镜像内篡改（漂移门禁反例）",
    "漏洞达到阈值": SCAN_PROBE[0],
}
MISSING = "<缺失>"
# cosign 只该访问本机临时 registry：外网请求经不可达代理失败（回环地址不走代理），不会触达公共 Sigstore 服务。
OFFLINE = {}
for _name, _value in (("https_proxy", "http://127.0.0.1:9"), ("http_proxy", "http://127.0.0.1:9"), ("no_proxy", "127.0.0.1,localhost")):
    OFFLINE[_name] = OFFLINE[_name.upper()] = _value


class StepFailed(Exception):
    """外部命令失败；消息里带命令与输出尾部。"""


# ---------- 纯逻辑：门禁判定（tests/gates/test_images.py 覆盖） ----------

def normalize_dump(dump):
    """只去掉诊断里每次装配随机生成的追踪号；组件配置等其余字段（结构、来源层、诊断内容）原样参与比较。"""
    def strip(node):
        if isinstance(node, dict):
            return {key: strip(value) for key, value in node.items() if key not in ("trace_id", "traceId")}
        if isinstance(node, list):
            return [strip(value) for value in node]
        return node
    if isinstance(dump, dict) and isinstance(dump.get("diagnostics"), list):
        return {**dump, "diagnostics": strip(dump["diagnostics"])}
    return dump


def diff_json(source, image, path=""):
    """逐项比较两份解析后的 JSON，返回 (JSON Pointer, 源码树的值, 镜像内的值)；一侧没有的项记为 <缺失>。"""
    if isinstance(source, dict) and isinstance(image, dict):
        found = []
        for key in sorted(set(source) | set(image)):
            child = path + "/" + key.replace("~", "~0").replace("/", "~1")
            if key not in source:
                found.append((child, MISSING, image[key]))
            elif key not in image:
                found.append((child, source[key], MISSING))
            else:
                found.extend(diff_json(source[key], image[key], child))
        return found
    if isinstance(source, list) and isinstance(image, list):
        found = []
        for index in range(max(len(source), len(image))):
            child = f"{path}/{index}"
            if index >= len(source):
                found.append((child, MISSING, image[index]))
            elif index >= len(image):
                found.append((child, source[index], MISSING))
            else:
                found.extend(diff_json(source[index], image[index], child))
        return found
    same = source == image and type(source) is type(image)
    return [] if same else [(path or "/", source, image)]


def severity_counts(report):
    counts = {}
    for match in report.get("matches", []):
        severity = str(match.get("vulnerability", {}).get("severity", "unknown")).lower()
        counts[severity] = counts.get(severity, 0) + 1
    return dict(sorted(counts.items()))


def scan_violations(report, threshold, allowlist, today):
    """达到阈值且没有有效豁免的漏洞。豁免必须写理由与期限（YYYY-MM-DD），过了期限或期限格式不对即失效。"""
    limit = SEVERITIES.index(threshold)
    exempt = {item["id"] for item in allowlist if item.get("id") and str(item.get("reason", "")).strip()
              and DATE.match(str(item.get("until", ""))) and str(item["until"]) >= today}
    found = []
    for match in report.get("matches", []):
        vulnerability, artifact = match.get("vulnerability", {}), match.get("artifact", {})
        severity = str(vulnerability.get("severity", "unknown")).lower()
        if severity in SEVERITIES and SEVERITIES.index(severity) >= limit and vulnerability.get("id") not in exempt:
            found.append({"id": vulnerability.get("id"), "severity": severity, "package": artifact.get("name"),
                          "version": artifact.get("version"), "fixedIn": vulnerability.get("fix", {}).get("versions", [])})
    return sorted(found, key=lambda item: (-SEVERITIES.index(item["severity"]), str(item["id"]), str(item["package"])))


def negative_verdict(passed, output, reason):
    """反例的判定：门禁必须不放行，且输出里有预期原因；不放行但原因对不上也不算拦住。"""
    if passed:
        return "未拦住"
    return "拦住" if reason in output else "失败原因不符"


def is_root_user(user):
    """镜像配置里的 User：空、root、0（不论组）都按 root 处理。"""
    return str(user or "").split(":", 1)[0] in ("", "root", "0")


def runtime_failures(report, kind):
    """只读根运行时的自报必须全部成立；JIT 镜像应可动态生成代码，NativeAOT 镜像不应可以。"""
    failures = []
    for key in ("uid", "gid"):
        value = report.get(key)
        if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
            failures.append(f"有效 {key.upper()} 应为非 0 用户，实际 {value!r}")
    if report.get("rootMountReadOnly") is not True:
        failures.append(f"根挂载应为只读，实际 {report.get('rootMountReadOnly')!r}")
    probe = report.get("writeProbe") or {}
    if probe.get("writable") is not False:
        failures.append(f"写探针 {probe.get('path')} 在只读根下不应可写")
    temp = report.get("temp") or {}
    if temp.get("writable") is not True:
        failures.append(f"声明的临时目录 {temp.get('path')} 应可写：{temp.get('error')}")
    expected = kind == "jit"
    if report.get("dynamicCode") is not expected:
        failures.append(f"{kind} 镜像的 dynamicCode 应为 {expected}，实际 {report.get('dynamicCode')!r}")
    return failures


def control_failures(report):
    """对照运行不加只读：写探针必须可写、根挂载不是只读，否则只读运行里的写失败不能归因于只读根。"""
    probe = report.get("writeProbe") or {}
    if probe.get("writable") is True and report.get("rootMountReadOnly") is False:
        return []
    return [f"对照运行的写探针应可写、根挂载应可写，实际 writable={probe.get('writable')!r} rootMountReadOnly={report.get('rootMountReadOnly')!r}"]


# ---------- 外部命令 ----------

def run(command, *, env=None, check=True, log=None, cwd=ROOT):
    merged = {**os.environ, **(env or {})}
    result = subprocess.run([str(part) for part in command], cwd=cwd, env=merged, text=True, capture_output=True)
    if log is not None:
        with open(log, "a", encoding="utf-8") as handle:
            handle.write("$ " + " ".join(str(part) for part in command) + f"\n[exit {result.returncode}]\n")
            handle.write(result.stdout[-20000:] + result.stderr[-20000:] + "\n")
    if check and result.returncode != 0:
        tail = (result.stdout + result.stderr).strip()[-2000:]
        raise StepFailed(f"{' '.join(str(part) for part in command[:4])} … 退出码 {result.returncode}：{tail}")
    return result


def ci_context(environ=os.environ):
    """CI 运行信息。PR 运行里 HEAD 是 GitHub 合成的合并提交，合入后查不到，另记 PR 头与 base 的 SHA 以便追溯。"""
    context = {name: environ[key] for name, key in (("event", "GITHUB_EVENT_NAME"), ("runId", "GITHUB_RUN_ID"))
               if environ.get(key)}
    event_path = environ.get("GITHUB_EVENT_PATH")
    if event_path and Path(event_path).is_file():
        pull = json.loads(Path(event_path).read_text(encoding="utf-8")).get("pull_request") or {}
        for side in ("head", "base"):
            sha = (pull.get(side) or {}).get("sha")
            if sha:
                context[f"{side}Sha"] = sha
    return context


def build_evidence():
    """源码 SHA、KunVersion 与 profile / 组件目录的 git 树修订；工作区改过这两个目录时如实标出；CI 里另记运行信息。"""
    def git(*args):
        return run(["git", *args]).stdout.strip()
    props = (ROOT / "Directory.Packages.props").read_text(encoding="utf-8")
    match = re.search(r"<KunVersion>([^<]+)</KunVersion>", props)
    if match is None:
        raise StepFailed("Directory.Packages.props 里没有 KunVersion。")
    evidence = {"sourceSha": git("rev-parse", "HEAD"), "kunVersion": match.group(1),
                "profileRevision": git("rev-parse", "HEAD:profiles"), "pluginsRevision": git("rev-parse", "HEAD:plugins"),
                "profileTreeDirty": bool(git("status", "--porcelain", "--", "profiles", "plugins"))}
    ci = ci_context()
    if ci:
        evidence["ci"] = ci
    return evidence


def push_digest(tag, log):
    run(["docker", "push", tag], log=log)
    digests = json.loads(run(["docker", "image", "inspect", "--format", "{{json .RepoDigests}}", tag]).stdout)
    repository = tag.rsplit(":", 1)[0]
    for item in digests or []:
        if item.startswith(repository + "@"):
            digest = item.split("@", 1)[1]
            return f"{repository}@{digest}", digest
    raise StepFailed(f"推送后取不到 {tag} 的 digest。")


def docker_build(kind, project, assembly, tag, evidence, role, log=None):
    command = ["docker", "build", "-f", ROOT / "deploy" / "images" / f"{kind}.Dockerfile", "-t", tag,
               "--build-arg", f"PROJECT={project}", "--build-arg", f"ASSEMBLY={assembly}",
               "--build-arg", f"SOURCE_SHA={evidence['sourceSha']}", "--build-arg", f"KUN_VERSION={evidence['kunVersion']}",
               "--build-arg", f"PROFILE_REVISION={evidence['profileRevision']}",
               "--build-arg", f"PLUGINS_REVISION={evidence['pluginsRevision']}", "--build-arg", f"IMAGE_ROLE={role}", "."]
    run(command, env={"DOCKER_BUILDKIT": "1"}, log=log)


def build_image(kind, project, assembly, tag, evidence, role, log=None):
    """统一构建入口：按模板构建、推送，返回按 digest 的引用。"""
    docker_build(kind, project, assembly, tag, evidence, role, log)
    return push_digest(tag, log)


@contextlib.contextmanager
def temporary_registry(log):
    container = run(["docker", "run", "-d", "--rm", "-p", "127.0.0.1::5000", REGISTRY_IMAGE], log=log).stdout.strip()
    try:
        port = run(["docker", "port", container, "5000/tcp"]).stdout.split()[0].rsplit(":", 1)[1]
        address = f"127.0.0.1:{port}"
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        for _ in range(60):
            try:
                with opener.open(f"http://{address}/v2/", timeout=2) as response:
                    if response.status == 200:
                        break
            except (urllib.error.URLError, OSError):
                time.sleep(0.5)
        else:
            raise StepFailed("临时 registry 30 秒内没有就绪。")
        yield address
    finally:
        run(["docker", "rm", "-f", container], check=False, log=log)


def make_pki(directory, log):
    """临时 CA 与签名证书：SAN 为发布者身份，Fulcio 的 OIDC issuer 扩展（1.3.6.1.4.1.57264.1.8）写 issuer。"""
    (directory / "ca.cnf").write_text(
        "[req]\ndistinguished_name = dn\nprompt = no\nx509_extensions = v3_ca\n[dn]\nCN = qian-image-selfcheck-ca\n"
        "[v3_ca]\nbasicConstraints = critical, CA:TRUE\nkeyUsage = critical, keyCertSign, cRLSign\nsubjectKeyIdentifier = hash\n",
        encoding="utf-8")
    (directory / "leaf.cnf").write_text(
        "[req]\ndistinguished_name = dn\nprompt = no\n[dn]\nO = qian-image-selfcheck\n[v3_leaf]\n"
        "basicConstraints = critical, CA:FALSE\nkeyUsage = critical, digitalSignature\nextendedKeyUsage = codeSigning\n"
        f"subjectAltName = URI:{SIGNER_IDENTITY}\nauthorityKeyIdentifier = keyid\n"
        f"1.3.6.1.4.1.57264.1.8 = ASN1:UTF8String:{SIGNER_ISSUER}\n", encoding="utf-8")
    for name in ("ca", "leaf"):
        run(["openssl", "ecparam", "-name", "prime256v1", "-genkey", "-noout", "-out", directory / f"{name}.ec"], log=log)
        run(["openssl", "pkcs8", "-topk8", "-nocrypt", "-in", directory / f"{name}.ec", "-out", directory / f"{name}.key"], log=log)
    run(["openssl", "req", "-x509", "-new", "-sha256", "-key", directory / "ca.key", "-days", "1", "-config", directory / "ca.cnf",
         "-out", directory / "ca.pem"], log=log)
    run(["openssl", "req", "-new", "-key", directory / "leaf.key", "-config", directory / "leaf.cnf", "-out", directory / "leaf.csr"], log=log)
    run(["openssl", "x509", "-req", "-sha256", "-in", directory / "leaf.csr", "-CA", directory / "ca.pem", "-CAkey", directory / "ca.key",
         "-CAcreateserial", "-days", "1", "-extfile", directory / "leaf.cnf", "-extensions", "v3_leaf",
         "-out", directory / "leaf.pem"], log=log)


class Cosign:
    """离线签名与验签：密钥加证书签名，不用签名配置里的公共服务；验签只信任本次运行的 CA。"""

    def __init__(self, binary, directory, log):
        self.binary, self.directory, self.log = binary, directory, log
        self.env = {**OFFLINE, "COSIGN_PASSWORD": ""}
        make_pki(directory, log)
        self.call(["import-key-pair", "--key", directory / "leaf.key", "--output-key-prefix", directory / "signer"])
        self.call(["trusted-root", "create", "--fulcio", f"url=https://ca.selfcheck.invalid,certificate-chain={directory / 'ca.pem'}",
                   "--no-default-fulcio", "--no-default-rekor", "--no-default-ctfe", "--no-default-tsa",
                   "--out", directory / "trusted_root.json"])
        # 不含任何服务的签名配置：不向 Fulcio 申请证书，不上传透明日志，也不取时间戳。
        self.call(["signing-config", "create", "--out", directory / "signing_config.json"])

    def call(self, args, check=True):
        return run([self.binary, *args], env=self.env, check=check, log=self.log)

    def _signing(self):
        # 只带叶证书，不传 --certificate-chain：v0.3 bundle 里写入证书链后，cosign 3 默认拒验
        # （需 --allow-certificate-chain）；叶证书由 trusted root 里的 CA 补链，与 Fulcio 签发的形态一致。
        return ["--yes", "--key", self.directory / "signer.key", "--certificate", self.directory / "leaf.pem",
                "--signing-config", self.directory / "signing_config.json",
                "--trusted-root", self.directory / "trusted_root.json", "--allow-http-registry"]

    def _verifying(self, identity, issuer):
        return ["--trusted-root", self.directory / "trusted_root.json", "--certificate-identity", identity,
                "--certificate-oidc-issuer", issuer, "--insecure-ignore-tlog=true", "--insecure-ignore-sct=true",
                "--allow-http-registry"]

    def sign(self, ref):
        self.call(["sign", *self._signing(), ref])

    def attest(self, ref, sbom):
        self.call(["attest", *self._signing(), "--type", "spdxjson", "--predicate", sbom, ref])

    @staticmethod
    def _outcome(result):
        return result.returncode == 0, (result.stdout + result.stderr).strip()

    def verify(self, ref, identity=SIGNER_IDENTITY, issuer=SIGNER_ISSUER):
        """返回 (是否通过, 输出)。"""
        return self._outcome(self.call(["verify", *self._verifying(identity, issuer), ref], check=False))

    def verify_attestation(self, ref, identity=SIGNER_IDENTITY, issuer=SIGNER_ISSUER):
        return self._outcome(self.call(["verify-attestation", "--type", "spdxjson", *self._verifying(identity, issuer), ref],
                                       check=False))


def write_probe_sbom(path):
    """扫描阈值反例用的 CycloneDX 组件清单，只含 SCAN_PROBE 一个组件。"""
    name, version = SCAN_PROBE
    component = {"type": "library", "name": name, "version": version, "purl": f"pkg:nuget/{name}@{version}"}
    path.write_text(json.dumps({"bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1, "components": [component]}),
                    encoding="utf-8")


def dump_source(tool, profile, log):
    result = run(["dotnet", tool, "dump-config", "--root", ROOT, "--profile", profile], log=log)
    return json.loads(result.stdout)


def dump_image(ref, profile, log):
    result = run(["docker", "run", "--rm", "--read-only", "--tmpfs", "/tmp:rw,size=16m", ref,
                  "dump-config", "--root", "/app/qian", "--profile", profile], log=log)
    return json.loads(result.stdout)


def self_report(ref, read_only, log):
    flags = ["--read-only", "--tmpfs", "/tmp:rw,size=16m"] if read_only else []
    return json.loads(run(["docker", "run", "--rm", *flags, ref, "self-report"], log=log).stdout)


def build_tampered(base_ref, tag, log):
    """在自检镜像上改动一份 patch（不改源码树），作为漂移与签名反例。"""
    with tempfile.TemporaryDirectory() as context:
        Path(context, "qian.patch.yml").write_text(TAMPER_PATCH, encoding="utf-8")
        Path(context, "Dockerfile").write_text(
            f"FROM {base_ref}\nCOPY qian.patch.yml /app/qian/profiles/{TAMPER_PROFILE}/qian.patch.yml\n", encoding="utf-8")
        run(["docker", "build", "-t", tag, context], env={"DOCKER_BUILDKIT": "1"}, log=log)
    return push_digest(tag, log)


# ---------- 子命令 ----------

def cmd_build(args):
    evidence = build_evidence()
    ref, digest = build_image(args.kind, args.project, args.assembly, args.tag, evidence, args.role)
    print(json.dumps({"ref": ref, "digest": digest, "build": evidence}, ensure_ascii=False, indent=2))
    return 0


def cmd_selfcheck(args):
    tools = args.tools.resolve()
    for name in ("syft", "grype", "cosign"):
        if not (tools / name).is_file():
            raise SystemExit(f"{tools / name} 不存在：先运行 python3 scripts/prepare_image_tools.py --destination {args.tools}")
    out = args.out.resolve()
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    log = out / "commands.log"
    lock = json.loads((ROOT / "scripts" / "image-tools.lock.json").read_text(encoding="utf-8"))
    allowlist = json.loads(ALLOWLIST.read_text(encoding="utf-8"))
    today = dt.date.today().isoformat()
    evidence = {"generatedBy": "scripts/images.py selfcheck", "build": build_evidence(),
                "tools": {name: spec["version"] for name, spec in lock.items()}, "failOn": args.fail_on,
                "signer": {"identity": SIGNER_IDENTITY, "issuer": SIGNER_ISSUER, "ca": "本次运行临时签发"},
                "images": {}, "negativeChecks": [], "drift": {}, "failures": []}
    failures = evidence["failures"]

    def gate(name, ok, detail=""):
        if not ok:
            failures.append(f"{name}：{detail}" if detail else name)
        return ok

    def negative(name, outcome):
        """反例必须被拦住：门禁不放行，且原因是 NEGATIVE_REASONS 里预期的那一个。"""
        verdict = negative_verdict(*outcome, NEGATIVE_REASONS[name])
        evidence["negativeChecks"].append({"check": name, "expected": "拦住", "reason": NEGATIVE_REASONS[name], "observed": verdict})
        gate(f"反例 {name}", verdict == "拦住", verdict + "：" + outcome[1][-300:])

    try:
        run(["dotnet", "build", ROOT / SELFCHECK_IMAGES["jit"][0], "-c", "Release", "-nologo"], log=log)
        tool = ROOT / "tools" / "Qian.Image.SelfCheck" / "bin" / "Release" / "net10.0" / "Qian.Image.SelfCheck.dll"
        with temporary_registry(log) as registry, tempfile.TemporaryDirectory() as keys:
            cosign = Cosign(tools / "cosign", Path(keys), log)
            sha = evidence["build"]["sourceSha"][:12]
            for kind, (project, assembly, name) in SELFCHECK_IMAGES.items():
                print(f"== {kind}：构建并推送", flush=True)
                ref, digest = build_image(kind, project, assembly, f"{registry}/{name}:{sha}", evidence["build"], "selfcheck", log)
                image = evidence["images"][kind] = {"ref": ref.split("/", 1)[1], "digest": digest}
                print(f"== {kind}：SBOM、扫描、签名与证明", flush=True)
                sbom = out / f"sbom-{kind}.spdx.json"
                run([tools / "syft", "scan", f"registry:{ref}", "-o", f"spdx-json={sbom}", "-q"],
                    env={"SYFT_REGISTRY_INSECURE_USE_HTTP": "true", "SYFT_CHECK_FOR_APP_UPDATE": "false"}, log=log)
                report_path = out / f"scan-{kind}.json"
                run([tools / "grype", f"sbom:{sbom}", "-o", "json", "--file", report_path, "-q"],
                    env={"GRYPE_CHECK_FOR_APP_UPDATE": "false"}, log=log)
                report = json.loads(report_path.read_text(encoding="utf-8"))
                violations = scan_violations(report, args.fail_on, allowlist, today)
                image["sbom"] = {"file": sbom.name, "packages": len(json.loads(sbom.read_text(encoding="utf-8")).get("packages", []))}
                image["scan"] = {"file": report_path.name, "counts": severity_counts(report), "violations": violations}
                gate(f"{kind} 镜像漏洞扫描", not violations, f"{len(violations)} 项达到 {args.fail_on}：" +
                     ", ".join(f"{item['id']}({item['package']})" for item in violations[:10]))
                cosign.sign(ref)
                cosign.attest(ref, sbom)
                passed, output = cosign.verify(ref)
                image["signature"] = gate(f"{kind} 镜像验签（身份与 issuer 约束）", passed, output[-300:])
                passed, output = cosign.verify_attestation(ref)
                image["attestation"] = gate(f"{kind} 镜像 SBOM 证明验签", passed, output[-300:])
                print(f"== {kind}：运行期证据", flush=True)
                config = json.loads(run(["docker", "image", "inspect", "--format", "{{json .Config}}", ref]).stdout)
                labels = config.get("Labels") or {}
                gate(f"{kind} 镜像配置的用户不是 root", not is_root_user(config.get("User")), f"User={config.get('User')!r}")
                gate(f"{kind} 镜像标签记录源码 SHA", labels.get("org.opencontainers.image.revision") == evidence["build"]["sourceSha"])
                read_only = self_report(ref, True, log)
                control = self_report(ref, False, log)
                problems = runtime_failures(read_only, kind) + control_failures(control)
                gate(f"{kind} 镜像运行期证据", not problems, "；".join(problems))
                image["runtime"] = {"user": config.get("User"), "labels": labels, "readOnly": read_only, "control": control}

            print("== 反例：已知漏洞组件的扫描", flush=True)
            probe, probe_report = out / "scan-probe.cdx.json", out / "scan-probe.json"
            write_probe_sbom(probe)
            run([tools / "grype", f"sbom:{probe}", "-o", "json", "--file", probe_report, "-q"],
                env={"GRYPE_CHECK_FOR_APP_UPDATE": "false"}, log=log)
            probe_violations = scan_violations(json.loads(probe_report.read_text(encoding="utf-8")), args.fail_on, allowlist, today)
            evidence["scanProbe"] = {"component": "@".join(SCAN_PROBE), "violations": probe_violations}
            negative("漏洞达到阈值", (not probe_violations, json.dumps(probe_violations, ensure_ascii=False)))

            jit = evidence["images"]["jit"]
            jit_ref = f"{registry}/{jit['ref']}"
            negative("签名身份不符", cosign.verify(jit_ref, identity="https://github.com/someone/else/.github/workflows/x.yml@refs/heads/main"))
            negative("issuer 不符", cosign.verify(jit_ref, issuer="https://accounts.example.invalid"))

            print("== dump-config 漂移门禁", flush=True)
            for profile in PROFILES:
                differences = diff_json(normalize_dump(dump_source(tool, profile, log)), normalize_dump(dump_image(jit_ref, profile, log)))
                evidence["drift"][profile] = [{"pointer": p, "source": s, "image": i} for p, s, i in differences]
                gate(f"profile {profile} 源码树与镜像内的合成结果一致", not differences, f"{len(differences)} 处不同")

            print("== 反例：篡改 patch 的镜像", flush=True)
            tampered_ref, tampered_digest = build_tampered(jit_ref, f"{registry}/qian-selfcheck-tampered:{sha}", log)
            evidence["tampered"] = {"digest": tampered_digest, "profile": TAMPER_PROFILE, "patch": TAMPER_PATCH}
            negative("缺签名", cosign.verify(tampered_ref))
            tampered_diff = diff_json(normalize_dump(dump_source(tool, TAMPER_PROFILE, log)),
                                      normalize_dump(dump_image(tampered_ref, TAMPER_PROFILE, log)))
            evidence["tampered"]["drift"] = [{"pointer": p, "source": s, "image": i} for p, s, i in tampered_diff]
            negative("镜像内改动 patch", (not tampered_diff, json.dumps(evidence["tampered"]["drift"], ensure_ascii=False)))
            cosign.sign(tampered_ref)
            negative("缺 SBOM 证明", cosign.verify_attestation(tampered_ref))
    except StepFailed as error:
        failures.append(f"步骤中断：{error}")
    gate("反例全部执行", {item["check"] for item in evidence["negativeChecks"]} == set(NEGATIVE_REASONS))

    evidence["ok"] = not failures
    (out / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for kind, image in evidence["images"].items():
        print(f"{kind}: {image.get('digest')} 漏洞 {image.get('scan', {}).get('counts')} "
              f"签名={image.get('signature')} 证明={image.get('attestation')}")
    print(f"反例 {len(evidence['negativeChecks'])} 项；漂移 {sum(1 for v in evidence['drift'].values() if not v)}/{len(PROFILES)} 个 profile 一致")
    for failure in failures:
        print(f"失败：{failure}", file=sys.stderr)
    print(f"证据：{out / 'evidence.json'}")
    return 0 if not failures else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    build = sub.add_parser("build", help="按模板构建并推送一个 profile 镜像")
    build.add_argument("--kind", choices=("jit", "aot"), required=True)
    build.add_argument("--project", required=True, help="仓库内的 csproj 相对路径")
    build.add_argument("--assembly", required=True, help="程序集名（apphost 或原生可执行文件名）")
    build.add_argument("--tag", required=True, help="registry/名称:标签")
    build.add_argument("--role", choices=("product", "selfcheck"), default="product")
    check = sub.add_parser("selfcheck", help="CI 自检：构建自检镜像并跑全部供应链门禁与反例")
    check.add_argument("--tools", type=Path, default=Path(os.environ.get("QIAN_IMAGE_TOOLS", "")), help="syft / grype / cosign 所在目录")
    check.add_argument("--out", type=Path, default=ROOT / "TestResults" / "images")
    check.add_argument("--fail-on", choices=SEVERITIES, default="high")
    args = parser.parse_args()
    try:
        return cmd_build(args) if args.command == "build" else cmd_selfcheck(args)
    except StepFailed as error:
        print(f"失败：{error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
