"""Offline contract tests. These do not establish live platform compatibility."""
import copy
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from video_publisher import adapters, cli, native_browser
from video_publisher.core import (Ledger, VpubError, atomic_json, home, load_plan,
                                  publication_lock, read_json, stop_check)


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.env = patch.dict(os.environ, {"VPUB_HOME": str(self.root / "runtime")})
        self.env.start()
        self.video = self.root / "complete.mp4"
        self.video.write_bytes(b"synthetic test fixture, not a playable upload")
        self.manifest = self.root / "publish.json"
        self.value = cli.template(self.video)
        self.save()

    def tearDown(self):
        self.env.stop()
        self.temp.cleanup()

    def save(self):
        atomic_json(self.manifest, self.value)

    def plan(self, platforms=None):
        return load_plan(self.manifest, platforms)

    def code(self, expected, func, *args, **kwargs):
        with self.assertRaises(VpubError) as e:
            func(*args, **kwargs)
        self.assertEqual(expected, e.exception.code)

    def native_job(self, platform="youtube"):
        plan = self.plan([platform])
        result = cli.publish_plan(plan, plan["confirmation"])["results"][platform]
        self.assertEqual("WAITING_FOR_BROWSER", result["state"])
        return result["key"]

    def evidence(self, **updates):
        opts = self.plan(["youtube"])["platforms"]["youtube"]
        e = {k: opts[k] for k in ("title", "description", "tags", "visibility", "ai_generated", "made_for_kids")}
        e.update(id="abcdefghijk", identity="UC" + "a" * 22,
                 page_url="https://studio.youtube.com/video/abcdefghijk/edit",
                 observation="Synthetic test only: expected fields visible in a test fixture.", outcome="verified")
        e.update(updates)
        path = self.root / "evidence.json"
        atomic_json(path, e)
        return path

    def test_changed_text_invalidates_confirmation(self):
        old = self.plan()
        self.value["description"] = "changed"
        self.save()
        new = self.plan()
        self.assertNotEqual(old["confirmation"], new["confirmation"])
        with patch.object(adapters, "publish") as upload:
            self.code("confirmation_required", cli.publish_plan, new, old["confirmation"])
            upload.assert_not_called()

    def test_changed_video_invalidates_confirmation(self):
        old = self.plan()
        self.video.write_bytes(b"new bytes")
        self.assertNotEqual(old["confirmation"], self.plan()["confirmation"])

    def test_subset_isolation(self):
        self.value["platforms"]["douyin"]["unsupported"] = True
        self.save()
        self.assertEqual(["youtube"], list(self.plan(["youtube"])["platforms"]))
        self.code("invalid_manifest", self.plan)

    def test_visibility_and_ai_declaration_required(self):
        del self.value["platforms"]["youtube"]["visibility"]
        self.save()
        self.code("invalid_visibility", self.plan, ["youtube"])
        self.value["platforms"]["youtube"]["visibility"] = "private"
        self.value["platforms"]["youtube"]["ai_generated"] = "true"
        self.save()
        self.code("invalid_declaration", self.plan, ["youtube"])

    def test_stop_file_even_malformed_blocks_upload(self):
        (home() / "STOP.json").write_text("broken json")
        self.code("stopped", stop_check)
        plan = self.plan()
        with patch.object(adapters, "publish") as upload:
            self.code("stopped", cli.publish_plan, plan, plan["confirmation"])
            upload.assert_not_called()

    def test_external_stop_file(self):
        external = self.root / "platform-stop.json"
        external.write_text("pause")
        atomic_json(home() / "config.json", {"external_stop_file": str(external)})
        self.code("stopped", stop_check)

    def test_concurrent_lock_refused(self):
        with publication_lock():
            self.code("busy", lambda: publication_lock().__enter__())
        self.assertFalse((home() / "publisher.lock").exists())

    def test_atomic_claim_same_video(self):
        a, b = Ledger(), Ledger()
        try:
            self.assertTrue(a.claim("youtube", "default", "sha", {}))
            self.assertIsNone(b.claim("youtube", "default", "sha", {}))
        finally:
            a.close()
            b.close()

    def test_unknown_attempt_never_retries(self):
        self.value["platforms"]["bilibili"]["method"] = "cli"
        self.save()
        plan = self.plan(["bilibili"])
        with patch.object(adapters, "preflight", return_value={"identity": "test"}), patch.object(adapters, "publish", side_effect=TimeoutError) as upload:
            first = cli.publish_plan(plan, plan["confirmation"])
            second = cli.publish_plan(plan, plan["confirmation"])
            self.assertEqual("UNKNOWN", first["results"]["bilibili"]["state"])
            self.assertEqual("SKIPPED_EXISTING", second["results"]["bilibili"]["state"])
            self.assertEqual(1, upload.call_count)

    def test_id_survives_verification_error(self):
        plan = self.plan(["bilibili"])
        def fail(platform, plan, opts, checkpoint):
            checkpoint({"state": "SUBMITTED", "id": "BV1234567890"})
            raise TimeoutError
        with patch.object(adapters, "preflight", return_value={}), patch.object(adapters, "publish", side_effect=fail):
            r = cli.publish_plan(plan, plan["confirmation"])["results"]["bilibili"]
            self.assertEqual("BV1234567890", r["id"])
            self.assertEqual("SUBMITTED_UNVERIFIED", r["state"])

    def test_worker_receipt_survives_worker_failure(self):
        plan = self.plan(["youtube"])
        atomic_json(home() / "browser-receipts" / (plan["confirmation"] + "-youtube.json"),
                    {"state": "SUBMITTED", "id": "abcdefghijk", "method": "browser"})
        with patch.object(adapters, "publish", side_effect=TimeoutError):
            r = cli.publish_plan(plan, plan["confirmation"])["results"]["youtube"]
        self.assertEqual("abcdefghijk", r["id"])
        self.assertEqual("SUBMITTED_UNVERIFIED", r["state"])

    def test_partial_failure_preserves_other_results(self):
        plan = self.plan()
        def publish(platform, plan, opts, checkpoint):
            if platform == "youtube":
                raise TimeoutError
            return {"state": "SUBMISSION_VERIFIED", "id": platform}
        with patch.object(adapters, "preflight", return_value={}), patch.object(adapters, "publish", side_effect=publish):
            r = cli.publish_plan(plan, plan["confirmation"])
        self.assertFalse(r["all_submissions_verified"])
        self.assertEqual("SUBMISSION_VERIFIED", r["results"]["bilibili"]["state"])
        self.assertEqual("SUBMISSION_VERIFIED", r["results"]["douyin"]["state"])

    def test_risk_stops_remaining_platforms(self):
        plan = self.plan()
        with patch.object(adapters, "preflight", side_effect=VpubError("captcha_required", "test")), patch.object(adapters, "publish") as upload:
            r = cli.publish_plan(plan, plan["confirmation"])
            upload.assert_not_called()
        self.assertTrue((home() / "STOP.json").exists())
        self.assertEqual("stopped", r["results"]["youtube"]["code"])

    def test_native_login_does_not_launch_or_claim_logged_in(self):
        with patch("subprocess.Popen") as launch:
            for platform in cli.PLATFORMS:
                r = adapters.auth_login(platform, "default")
                self.assertIsNone(r["logged_in"])
                self.assertEqual("NATIVE_BROWSER_REQUIRED", r["state"])
            launch.assert_not_called()

    def test_native_job_preserves_approved_snapshot(self):
        key = self.native_job()
        self.video.write_bytes(b"later render")
        job = native_browser.request(key)
        self.assertEqual(b"synthetic test fixture, not a playable upload", Path(job["plan"]["video"]).read_bytes())

    def test_native_begin_is_single_use(self):
        key = self.native_job()
        native_browser.begin(key, "UC" + "a" * 22)
        self.code("already_started", native_browser.begin, key, "UC" + "a" * 22)

    def test_native_wrong_channel_blocked_before_upload(self):
        self.value["platforms"]["youtube"]["expected_channel_id"] = "UC" + "b" * 22
        self.save()
        key = self.native_job()
        self.code("wrong_account", native_browser.begin, key, "UC" + "a" * 22)

    def test_native_modified_job_metadata_rejected(self):
        key = self.native_job()
        path = home() / "browser-jobs" / (key + ".json")
        job = read_json(path)
        job["options"]["visibility"] = "public"
        atomic_json(path, job)
        self.code("request_changed", native_browser.begin, key, "UC" + "a" * 22)

    def test_native_evidence_requires_begin(self):
        key = self.native_job()
        self.code("not_started", native_browser.record, key, self.evidence())

    def test_native_evidence_checks_account_url_and_metadata(self):
        key = self.native_job()
        native_browser.begin(key, "UC" + "a" * 22)
        self.code("wrong_account", native_browser.record, key, self.evidence(identity="other"))
        self.code("invalid_evidence", native_browser.record, key, self.evidence(page_url="https://example.com/abcdefghijk"))
        self.code("metadata_mismatch", native_browser.record, key, self.evidence(visibility="public"))
        self.code("invalid_evidence", native_browser.record, key, self.evidence(cookie="secret"))

    def test_native_agent_evidence_distinct_from_api_verification(self):
        key = self.native_job()
        native_browser.begin(key, "UC" + "a" * 22)
        r = native_browser.record(key, self.evidence())
        self.assertEqual("AGENT_REPORTED_VERIFIED", r["state"])
        self.assertEqual("host_agent_ui_observation", r["verification_source"])
        result = adapters.verify("youtube", "default", r)
        self.assertEqual("BROWSER_VERIFICATION_REQUIRED", result["state"])

    def test_receipt_cannot_replace_prior_video_id(self):
        key = self.native_job()
        native_browser.begin(key, "UC" + "a" * 22)
        native_browser.record(key, self.evidence())
        self.code("receipt_conflict", native_browser.record, key,
                  self.evidence(id="12345678901", page_url="https://studio.youtube.com/video/12345678901/edit"))

    def test_evidence_can_be_saved_after_stop(self):
        key = self.native_job()
        native_browser.begin(key, "UC" + "a" * 22)
        atomic_json(home() / "STOP.json", {"reason": "test"})
        self.assertEqual("AGENT_REPORTED_VERIFIED", native_browser.record(key, self.evidence())["state"])

    def test_all_five_manifest_and_instagram_audience(self):
        out = self.root / "all.json"
        cli.execute(cli.parser().parse_args(["init", "--video", str(self.video), "--out", str(out), "--platforms", ",".join(cli.PLATFORMS)]))
        p = load_plan(out)
        self.assertEqual(5, len(p["platforms"]))
        self.assertEqual("account", p["platforms"]["instagram"]["visibility"])
        self.assertTrue(all(v["method"] == "native" for v in p["platforms"].values()))

    def test_cli_plan_never_calls_platform(self):
        with patch.object(adapters, "publish") as publish, patch.object(adapters, "auth_status") as status:
            r = cli.execute(cli.parser().parse_args(["publish", str(self.manifest), "--dry-run"]))
            self.assertEqual(0, r["platform_writes"])
            publish.assert_not_called()
            status.assert_not_called()


if __name__ == "__main__":
    unittest.main()
