"""A durable handoff to the host agent's real in-app browser.

No cookie export, private browser port, screen relay, or simulated login.
The host agent performs UI actions using its supported browser tool. A CLI
cannot independently inspect the host's browser and never claims that it did.
"""
from __future__ import annotations

import re
from urllib.parse import urlsplit, parse_qs

from .core import (Ledger, VpubError, atomic_json, file_sha, home, now,
                   publication_lock, read_json, stop_check, platform_check)

URLS = {
    "bilibili": "https://member.bilibili.com/",
    "youtube": "https://studio.youtube.com/",
    "douyin": "https://creator.douyin.com/creator-micro/home",
    "x": "https://x.com/",
    "instagram": "https://www.instagram.com/",
}


def login(platform):
    platform_check(platform)
    return {"state": "NATIVE_BROWSER_REQUIRED", "platform": platform,
            "method": "native", "logged_in": None, "login_url": URLS[platform],
            "hint": "Open this official URL directly in the host's built-in browser. Reuse the existing tab/session; let the user complete login. Inspect the actual creator dashboard. CLI cannot read that session."}


def queue(platform, plan, options):
    platform_check(platform)
    key = Ledger.key(platform, options["account"], plan["video_sha256"])
    path = home() / "browser-jobs" / (key + ".json")
    atomic_json(path, {"key": key, "platform": platform, "created_at": now(),
                       "plan": plan, "options": options})
    return {"state": "WAITING_FOR_BROWSER", "method": "native", "key": key,
            "request_file": str(path), "login_url": URLS[platform],
            "hint": "No upload has started. Host agent: read the browser request, verify the intended account, run browser begin, then upload through the same built-in browser. Never repeat an uncertain attempt."}


def request(key):
    ledger = Ledger()
    try:
        old = ledger.get(key)
        if not old or old["data"].get("options", {}).get("method") != "native":
            raise VpubError("not_found", "Native browser request not found.")
        platform_check(old["platform"])
        stop_check(old["platform"])
        job = read_json(home() / "browser-jobs" / (key + ".json"))
        if job["plan"]["confirmation"] != old["data"]["plan_hash"]:
            raise VpubError("request_changed", "Browser request differs from its recorded plan.")
        approved = old["data"]["approved_plan"]
        if job["options"] != old["data"]["options"] or job["plan"]["platforms"] != approved["platforms"]:
            raise VpubError("request_changed", "Browser request metadata differs from the approved plan.")
        for field in ("video_sha256", "video_bytes", "cover_sha256"):
            if job["plan"][field] != approved[field]:
                raise VpubError("request_changed", "Browser media record differs from the approved plan.")
        for field, sha_field in (("video", "video_sha256"), ("cover", "cover_sha256")):
            if job["plan"].get(field) and file_sha(job["plan"][field]) != job["plan"][sha_field]:
                raise VpubError("source_changed", "Browser job media changed; do not upload.")
        return {**job, "state": old["state"], "can_begin": old["state"] == "WAITING_FOR_BROWSER"}
    finally:
        ledger.close()


def begin(key, identity):
    # Persist BEFORE the host selects the video. Even uploading a draft is a write.
    with publication_lock():
        job = request(key)
        if not job["can_begin"]:
            raise VpubError("already_started", "This browser attempt is already started or finished. Inspect it; never start another upload.")
        if not identity or not identity.strip():
            raise VpubError("identity_required", "Read the actual account identity in the creator dashboard first.")
        if job["platform"] == "youtube" and not re.fullmatch(r"UC[\w-]{22}", identity):
            raise VpubError("invalid_identity", "Use the channel ID observed in the actual YouTube Studio URL.")
        if job["platform"] == "x" and not re.fullmatch(r"[A-Za-z0-9_]{1,15}", identity):
            raise VpubError("invalid_identity", "Use the signed-in X handle without @.")
        expected = job["options"].get("expected_channel_id") or job["options"].get("expected_identity") or job["options"].get("expected_uid")
        matches = expected.lower() == identity.lower() if expected and job["platform"] == "x" else expected == identity
        if expected and not matches:
            raise VpubError("wrong_account", "Visible channel differs from the manifest.")
        ledger = Ledger()
        try:
            old = ledger.get(key)
            old["data"]["browser_identity"] = identity
            old["data"]["browser_started_at"] = now()
            ledger.save(key, "BROWSER_IN_PROGRESS", old["data"])
        finally:
            ledger.close()
        return {**job, "state": "BROWSER_IN_PROGRESS", "identity": identity,
                "hint": "Use only this job's snapshot and approved metadata. Complete at most one final submission. Persist any video ID even if verification fails."}


def record(key, evidence_path):
    evidence = read_json(evidence_path)
    allowed = {"id", "page_url", "identity", "title", "description", "tags", "visibility", "made_for_kids",
               "ai_generated", "observation", "outcome", "post_text"}
    if not isinstance(evidence, dict) or set(evidence) - allowed:
        raise VpubError("invalid_evidence", "Use only documented browser-evidence fields; never include cookies or raw page dumps.")
    outcome = evidence.get("outcome")
    if outcome not in {"verified", "uncertain", "rejected"}:
        raise VpubError("invalid_evidence", "outcome must be verified, uncertain or rejected.")
    if not isinstance(evidence.get("observation"), str) or not 1 <= len(evidence["observation"]) <= 1000:
        raise VpubError("invalid_evidence", "Supply a short factual UI observation, at most 1000 characters.")
    # Recording an outcome remains possible after STOP: preserving receipts is safe.
    with publication_lock():
        ledger = Ledger()
        try:
            old = ledger.get(key)
            if not old or old["state"] not in {"BROWSER_IN_PROGRESS", "SUBMITTED_UNVERIFIED", "AGENT_REPORTED_VERIFIED"}:
                raise VpubError("not_started", "Begin the native browser request before recording its result.")
            opts = old["data"]["options"]
            if evidence.get("identity") != old["data"].get("browser_identity"):
                raise VpubError("wrong_account", "Observed account differs from the one that began the upload.")
            video_id, page_url = evidence.get("id"), evidence.get("page_url", "")
            if video_id:
                u = urlsplit(page_url)
                if u.scheme != "https" or u.username or u.password or u.fragment:
                    raise VpubError("invalid_evidence", "Use an official HTTPS work page without credentials or fragments.")
                if old["platform"] == "youtube":
                    valid = isinstance(video_id, str) and re.fullmatch(r"[\w-]{11}", video_id)
                    valid = valid and ((u.netloc == "studio.youtube.com" and u.path == f"/video/{video_id}/edit" and not u.query)
                                       or (u.netloc == "www.youtube.com" and u.path == "/watch" and parse_qs(u.query) == {"v": [video_id]}))
                elif old["platform"] == "douyin":
                    valid = isinstance(video_id, str) and video_id.isdigit() and u.netloc == "www.douyin.com" and u.path == f"/video/{video_id}" and not u.query
                elif old["platform"] == "bilibili":
                    valid = isinstance(video_id, str) and re.fullmatch(r"BV[\w]{10}", video_id) and u.netloc == "www.bilibili.com" and u.path.rstrip("/") == f"/video/{video_id}" and not u.query
                elif old["platform"] == "tiktok":
                    # Legacy receipts remain recordable; new requests are disabled.
                    valid = isinstance(video_id, str) and video_id.isdigit() and u.netloc == "www.tiktok.com" and re.fullmatch(r"/@[\w.-]+/video/" + re.escape(video_id), u.path) and not u.query
                elif old["platform"] == "x":
                    handle = old["data"]["browser_identity"]
                    valid = (isinstance(video_id, str) and re.fullmatch(r"[0-9]+", video_id)
                             and u.netloc in {"x.com", "www.x.com", "twitter.com", "www.twitter.com"}
                             and u.path.lower() == f"/{handle}/status/{video_id}".lower() and not u.query)
                else:
                    valid = isinstance(video_id, str) and re.fullmatch(r"[\w-]+", video_id) and u.netloc == "www.instagram.com" and u.path.rstrip("/") in {f"/reel/{video_id}", f"/p/{video_id}"} and not u.query
                if not valid:
                    raise VpubError("invalid_evidence", "The official work URL and video ID must agree.")
            elif outcome == "verified":
                raise VpubError("invalid_evidence", "Verified evidence requires the platform video ID.")
            if outcome == "verified":
                fields = ["title", "description", "tags", "visibility", "ai_generated"]
                if old["platform"] == "youtube":
                    fields.append("made_for_kids")
                if old["platform"] == "x":
                    fields.append("post_text")
                if any(type(evidence.get(f)) is not type(opts[f]) or evidence[f] != opts[f] for f in fields):
                    raise VpubError("metadata_mismatch", "Read-back must match every approved metadata/declaration field.")
            previous = old["data"].get("receipt", {}).get("id")
            if previous and previous != video_id:
                raise VpubError("receipt_conflict", "An existing video ID cannot be replaced or cleared.")
            state = {"verified": "AGENT_REPORTED_VERIFIED", "uncertain": "SUBMITTED_UNVERIFIED", "rejected": "PLATFORM_REJECTED"}[outcome]
            receipt = {**evidence, "state": state, "method": "native", "url": page_url,
                       "recorded_at": now(), "verification_source": "host_agent_ui_observation",
                       "note": "Host agent supplied UI evidence; CLI did not independently query the platform. Processing/public playback is separate."}
            old["data"]["receipt"] = receipt
            ledger.save(key, state, old["data"])
            return {"key": key, **receipt}
        finally:
            ledger.close()
