"""镜像供应链门禁 scripts/images.py 的自测（S1-DEP-003 / R106）。

运行：python3 -m unittest discover -s tests/gates -v
只测门禁的判定逻辑（漂移比对、漏洞阈值与豁免、root 判定、运行期证据），不需要 Docker；
构建、签名与验签的端到端由 .github/workflows/images.yml 的 selfcheck 真跑。
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))

import images  # noqa: E402


def dump(entries, layers=None, diagnostics=None):
    return {"profile": "standalone", "layers": layers or [{"index": 0, "kind": "bundle", "path": "plugins/b/config/qian.patch.yml"}],
            "entries": entries, "mountOrder": [entry["id"] for entry in entries], "diagnostics": diagnostics or []}


def match(identifier, severity, package="pkg"):
    return {"vulnerability": {"id": identifier, "severity": severity, "fix": {"versions": ["9.9"]}},
            "artifact": {"name": package, "version": "1.0"}}


class DriftTests(unittest.TestCase):
    def test_identical_dumps_have_no_drift(self):
        source = dump([{"id": "config", "config": {}, "layer": 0}])
        self.assertEqual(images.diff_json(source, json.loads(json.dumps(source))), [])

    def test_tampered_patch_is_reported_item_by_item(self):
        source = dump([{"id": "config", "config": {}, "layer": 0}])
        image = dump([{"id": "config", "config": {}, "layer": 1, "note": "篡改"}])
        self.assertEqual(images.diff_json(source, image),
                         [("/entries/0/layer", 0, 1), ("/entries/0/note", images.MISSING, "篡改")])

    def test_missing_entry_and_layer_order_are_drift(self):
        source = dump([{"id": "a"}, {"id": "b"}])
        image = dump([{"id": "a"}])
        pointers = [pointer for pointer, _, _ in images.diff_json(source, image)]
        self.assertIn("/entries/1", pointers)
        self.assertIn("/mountOrder/1", pointers)

    def test_type_change_counts_even_when_python_compares_equal(self):
        self.assertEqual(images.diff_json({"present": True}, {"present": 1}), [("/present", True, 1)])

    def test_pointer_escapes_slash_and_tilde(self):
        self.assertEqual(images.diff_json({"a/b~c": 1}, {"a/b~c": 2}), [("/a~1b~0c", 1, 2)])

    def test_trace_ids_are_ignored_but_diagnostic_content_is_compared(self):
        diagnostic = {"file": "plugins/b/config/qian.patch.yml", "severity": "warning", "error": {"code": "INPUT_INVALID", "trace_id": "a"}}
        other = json.loads(json.dumps(diagnostic)); other["error"]["trace_id"] = "b"
        self.assertEqual(images.diff_json(images.normalize_dump(dump([], diagnostics=[diagnostic])),
                                          images.normalize_dump(dump([], diagnostics=[other]))), [])
        other["severity"] = "error"
        self.assertEqual(len(images.diff_json(images.normalize_dump(dump([], diagnostics=[diagnostic])),
                                              images.normalize_dump(dump([], diagnostics=[other])))), 1)

    def test_trace_id_in_component_config_is_still_compared(self):
        source = dump([{"id": "telemetry", "config": {"trace_id": "x-trace"}, "layer": 0}])
        image = dump([{"id": "telemetry", "config": {"trace_id": "x-other"}, "layer": 0}])
        self.assertEqual(images.diff_json(images.normalize_dump(source), images.normalize_dump(image)),
                         [("/entries/0/config/trace_id", "x-trace", "x-other")])


class ScanTests(unittest.TestCase):
    REPORT = {"matches": [match("CVE-1", "Critical"), match("CVE-2", "High", "lib"), match("CVE-3", "Medium"), match("CVE-4", "Unknown")]}

    def test_threshold_counts_severity_and_above(self):
        self.assertEqual([item["id"] for item in images.scan_violations(self.REPORT, "high", [], "2026-10-16")], ["CVE-1", "CVE-2"])
        self.assertEqual([item["id"] for item in images.scan_violations(self.REPORT, "critical", [], "2026-10-16")], ["CVE-1"])
        self.assertEqual(images.severity_counts(self.REPORT), {"critical": 1, "high": 1, "medium": 1, "unknown": 1})

    def test_allowlist_needs_reason_and_unexpired_date(self):
        allowlist = [{"id": "CVE-1", "reason": "上游未修复，运行镜像不加载该组件", "until": "2026-12-31"},
                     {"id": "CVE-2", "reason": "", "until": "2026-12-31"}]
        self.assertEqual([item["id"] for item in images.scan_violations(self.REPORT, "high", allowlist, "2026-10-16")], ["CVE-2"])
        self.assertEqual([item["id"] for item in images.scan_violations(self.REPORT, "high", allowlist, "2027-01-01")], ["CVE-1", "CVE-2"])

    def test_allowlist_until_must_be_a_full_date(self):
        for until in ("2027", "2027-1-1", "长期", ""):
            allowlist = [{"id": "CVE-1", "reason": "上游未修复", "until": until}]
            self.assertIn("CVE-1", [item["id"] for item in images.scan_violations(self.REPORT, "high", allowlist, "2026-10-16")], until)

    def test_probe_sbom_names_the_probe_component(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "probe.cdx.json"
            images.write_probe_sbom(path)
            component = json.loads(path.read_text(encoding="utf-8"))["components"][0]
        name, version = images.SCAN_PROBE
        self.assertEqual(component["purl"], f"pkg:nuget/{name}@{version}")

    def test_violation_carries_package_and_fix(self):
        violation = images.scan_violations(self.REPORT, "high", [], "2026-10-16")[1]
        self.assertEqual(violation, {"id": "CVE-2", "severity": "high", "package": "lib", "version": "1.0", "fixedIn": ["9.9"]})


class NegativeTests(unittest.TestCase):
    def test_negative_is_caught_only_with_the_expected_reason(self):
        reason = images.NEGATIVE_REASONS["签名身份不符"]
        self.assertEqual(images.negative_verdict(False, 'Error: expected SAN value "x", got "y"', reason), "拦住")
        self.assertEqual(images.negative_verdict(True, "Verified OK", reason), "未拦住")
        self.assertEqual(images.negative_verdict(False, "dial tcp 127.0.0.1:5000: connect: connection refused", reason), "失败原因不符")

    def test_every_negative_has_a_reason(self):
        self.assertEqual(set(images.NEGATIVE_REASONS),
                         {"签名身份不符", "issuer 不符", "缺签名", "缺 SBOM 证明", "镜像内改动 patch", "漏洞达到阈值"})
        self.assertIn(images.NEGATIVE_REASONS["镜像内改动 patch"], images.TAMPER_PATCH)
        self.assertEqual(images.NEGATIVE_REASONS["漏洞达到阈值"], images.SCAN_PROBE[0])


class CiContextTests(unittest.TestCase):
    def test_pull_request_run_records_head_and_base(self):
        with tempfile.TemporaryDirectory() as directory:
            event = Path(directory) / "event.json"
            event.write_text(json.dumps({"pull_request": {"head": {"sha": "a" * 40}, "base": {"sha": "b" * 40}}}), encoding="utf-8")
            context = images.ci_context({"GITHUB_EVENT_NAME": "pull_request", "GITHUB_RUN_ID": "7", "GITHUB_EVENT_PATH": str(event)})
        self.assertEqual(context, {"event": "pull_request", "runId": "7", "headSha": "a" * 40, "baseSha": "b" * 40})

    def test_outside_ci_there_is_no_context(self):
        self.assertEqual(images.ci_context({}), {})


class RuntimeTests(unittest.TestCase):
    READ_ONLY = {"uid": 1654, "gid": 1654, "rootMountReadOnly": True, "writeProbe": {"path": "/app/.write-probe", "writable": False},
                 "temp": {"path": "/tmp/", "writable": True}, "dynamicCode": True}

    def test_root_user_forms(self):
        for user in ("", None, "root", "0", "0:0", "root:app"):
            self.assertTrue(images.is_root_user(user), user)
        for user in ("1654", "app", "1654:1654"):
            self.assertFalse(images.is_root_user(user), user)

    def test_read_only_report_passes_for_jit(self):
        self.assertEqual(images.runtime_failures(self.READ_ONLY, "jit"), [])

    def test_aot_must_not_support_dynamic_code(self):
        self.assertEqual(len(images.runtime_failures(self.READ_ONLY, "aot")), 1)
        self.assertEqual(images.runtime_failures({**self.READ_ONLY, "dynamicCode": False}, "aot"), [])

    def test_each_runtime_property_is_enforced(self):
        broken = [{"uid": 0}, {"gid": 0}, {"uid": True}, {"rootMountReadOnly": None}, {"writeProbe": {"writable": True}},
                  {"temp": {"writable": False}}]
        for change in broken:
            self.assertEqual(len(images.runtime_failures({**self.READ_ONLY, **change}, "jit")), 1, change)

    def test_control_run_must_show_writable_probe(self):
        self.assertEqual(images.control_failures({"rootMountReadOnly": False, "writeProbe": {"writable": True}}), [])
        self.assertEqual(len(images.control_failures({"rootMountReadOnly": False, "writeProbe": {"writable": False}})), 1)
        self.assertEqual(len(images.control_failures({"rootMountReadOnly": True, "writeProbe": {"writable": True}})), 1)


class LockTests(unittest.TestCase):
    def test_lock_pins_every_platform_with_sha256(self):
        lock = json.loads((ROOT / "scripts" / "image-tools.lock.json").read_text(encoding="utf-8"))
        self.assertEqual(set(lock), {"syft", "grype", "cosign"})
        for name, spec in lock.items():
            self.assertEqual(set(spec["assets"]), {"linux_amd64", "linux_arm64", "darwin_amd64", "darwin_arm64"}, name)
            for asset in spec["assets"].values():
                self.assertRegex(asset["sha256"], r"^[0-9a-f]{64}$")
                if name != "cosign":
                    self.assertIn(spec["version"], asset["name"])

    def test_dockerfiles_pin_base_images_by_digest(self):
        for kind in ("jit", "aot"):
            text = (ROOT / "deploy" / "images" / f"{kind}.Dockerfile").read_text(encoding="utf-8")
            images_lines = [line for line in text.splitlines() if line.startswith("ARG ") and "_IMAGE=" in line]
            self.assertEqual(len(images_lines), 2, kind)
            for line in images_lines:
                self.assertRegex(line, r"@sha256:[0-9a-f]{64}$", line)
            self.assertIn("USER $APP_UID", text)
        self.assertRegex(images.REGISTRY_IMAGE, r"@sha256:[0-9a-f]{64}$")


if __name__ == "__main__":
    unittest.main()
