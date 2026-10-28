"""Local-only validation, approval hashes and crash-safe publication ledger."""
from __future__ import annotations

import contextlib
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
import tempfile
from datetime import datetime, timezone

PLATFORMS = ("bilibili", "youtube", "douyin", "tiktok", "instagram")
SAU_COMMIT = "3847fff01f5434a8a32829833163cd6723cabc59"


class VpubError(Exception):
    def __init__(self, code, message, hint=""):
        super().__init__(message)
        self.code, self.message, self.hint = code, message, hint


def now():
    return datetime.now(timezone.utc).isoformat()


def home():
    override = os.environ.get("VPUB_HOME")
    base = Path(os.environ.get("LOCALAPPDATA", Path.home() / ".local/share"))
    p = Path(override) if override else base / "WhiteCarpVideoPublisher"
    p.mkdir(parents=True, exist_ok=True, mode=0o700)
    return p.resolve()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def atomic_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    fd, name = tempfile.mkstemp(prefix=".vpub-", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as f:
            json.dump(value, f, ensure_ascii=False, indent=2)
            f.write("\n")
            f.flush()
            os.fsync(f.fileno())
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                     separators=(",", ":")).encode()).hexdigest()


def file_sha(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def account_name(value):
    if not isinstance(value, str) or not re.fullmatch(r"[a-zA-Z0-9_-]{1,40}", value):
        raise VpubError("invalid_account", "Account alias must be 1–40 letters, digits, _ or -.")
    return value


def config():
    p = home() / "config.json"
    return read_json(p) if p.exists() else {}


def stop_check():
    candidates = [home() / "STOP.json"]
    external = config().get("external_stop_file")
    if external:
        candidates.append(Path(external))
    for path in candidates:
        # Presence stops publication; malformed stop files also stop it.
        if path.exists():
            raise VpubError("stopped", f"Publication stop file exists: {path}",
                            "Inspect the stop reason. Only the operator may clear it.")


def load_plan(path, selected=None):
    path = Path(path).resolve()
    m = read_json(path)
    if m.get("schema_version") != 1:
        raise VpubError("invalid_manifest", "schema_version must be 1.")
    allowed = {"schema_version", "video", "cover", "title", "description", "tags", "platforms"}
    if set(m) - allowed:
        raise VpubError("invalid_manifest", "Unknown top-level fields: " + ", ".join(sorted(set(m) - allowed)))
    vp = (path.parent / m.get("video", "")).resolve()
    if not vp.is_file() or vp.suffix.lower() not in {".mp4", ".mov", ".webm", ".mkv"} or vp.stat().st_size == 0:
        raise VpubError("invalid_video", "Select a complete, nonempty video file.")
    cover = (path.parent / m["cover"]).resolve() if m.get("cover") else None
    if cover and (not cover.is_file() or cover.suffix.lower() not in {".jpg", ".jpeg", ".png"}):
        raise VpubError("invalid_cover", "Cover must be an existing JPG or PNG.")
    entries = m.get("platforms")
    if not isinstance(entries, dict) or not entries or set(entries) - set(PLATFORMS):
        raise VpubError("invalid_platform", "Choose bilibili, youtube, douyin, tiktok and/or instagram.")
    chosen = selected or list(entries)
    if not chosen or set(chosen) - set(entries) or len(chosen) != len(set(chosen)):
        raise VpubError("invalid_platform", "Selected platforms must exist in the manifest and be unique.")
    plans = {}
    for platform in chosen:
        v = entries[platform]
        if not isinstance(v, dict):
            raise VpubError("invalid_manifest", "Each platform needs an object.")
        permitted = {"account", "title", "description", "tags", "visibility", "ai_generated", "schedule"}
        permitted |= {"tid", "expected_uid", "method"} if platform == "bilibili" else set()
        permitted |= {"category_id", "made_for_kids", "expected_channel_id", "method"} if platform == "youtube" else set()
        permitted |= {"method"} if platform == "douyin" else set()
        permitted |= {"method", "expected_identity"} if platform in {"tiktok", "instagram"} else set()
        if set(v) - permitted:
            raise VpubError("invalid_manifest", f"Unsupported {platform} options: {sorted(set(v) - permitted)}")
        if v.get("account", "default") != "default":
            raise VpubError("unsupported_account", "v0.1 supports one account per platform: default.")
        if platform == "youtube" and v.get("method", "native") not in {"native", "api"}:
            raise VpubError("invalid_method", "YouTube method must be native or api. The screen-relay prototype was retired.")
        if platform == "douyin" and v.get("method", "browser") not in {"native", "browser"}:
            raise VpubError("invalid_method", "Douyin method must be native or browser.")
        if platform == "bilibili" and v.get("method", "cli") not in {"native", "cli"}:
            raise VpubError("invalid_method", "Bilibili method must be native or cli.")
        if platform in {"tiktok", "instagram"} and v.get("method", "native") != "native":
            raise VpubError("invalid_method", "TikTok/Instagram currently require the host's native browser; official API integration is not implemented.")
        if platform == "douyin" and cover:
            raise VpubError("unsupported_cover", "Douyin v0.1 uses the platform's automatic cover. Remove the shared cover or publish Bilibili/YouTube separately with it.")
        title = v.get("title", m.get("title", ""))
        desc = v.get("description", m.get("description", ""))
        tags = v.get("tags", m.get("tags", []))
        limit = {"bilibili": 80, "youtube": 100, "douyin": 30, "tiktok": 100, "instagram": 100}[platform]
        if not isinstance(title, str) or not title.strip() or len(title) > limit or any(c in title for c in "\r\n\x00"):
            raise VpubError("invalid_title", f"{platform} title must contain 1–{limit} characters, one line.")
        if not isinstance(desc, str) or "\x00" in desc or len(desc) > (2000 if platform != "youtube" else 5000):
            raise VpubError("invalid_description", f"Invalid {platform} description.")
        if platform == "youtube" and len(desc.encode("utf-8")) > 5000:
            raise VpubError("invalid_description", "YouTube description exceeds 5000 UTF-8 bytes.")
        if not isinstance(tags, list) or not all(isinstance(t, str) and t.strip() and not any(c in t for c in ",\r\n\x00") for t in tags):
            raise VpubError("invalid_tags", "Tags must be a list of nonempty strings without commas/newlines.")
        if platform == "bilibili" and not 1 <= len(tags) <= 10:
            raise VpubError("invalid_tags", "Bilibili requires 1–10 tags.")
        if platform == "youtube" and sum(len(t) + 1 for t in tags) > 500:
            raise VpubError("invalid_tags", "YouTube tags exceed the 500-character budget.")
        visibility = v.get("visibility")
        supported = {"bilibili": {"public", "private"}, "youtube": {"public", "private", "unlisted"}, "douyin": {"public"},
                     "tiktok": {"public", "friends", "private"}, "instagram": {"account"}}
        if visibility not in supported[platform]:
            raise VpubError("invalid_visibility", f"Explicit {platform} visibility required: {sorted(supported[platform])}")
        if type(v.get("ai_generated")) is not bool:
            raise VpubError("invalid_declaration", "Explicit ai_generated true/false required per platform.")
        # v0.1 implements Douyin's AI declaration, not arbitrary declaration variants.
        if platform in {"bilibili", "douyin"} and not v["ai_generated"]:
            raise VpubError("unsupported_declaration", "v0.1 Bilibili/Douyin route supports AI-declared videos only.")
        if platform == "youtube" and type(v.get("made_for_kids")) is not bool:
            raise VpubError("invalid_audience", "YouTube made_for_kids must be explicitly true/false.")
        schedule = v.get("schedule")
        if schedule:
            if platform in {"douyin", "tiktok", "instagram"} or v.get("method") == "native":
                raise VpubError("unsupported_schedule", "This route supports immediate publication only.")
            if platform == "youtube" and v.get("method", "native") != "api":
                raise VpubError("unsupported_schedule", "YouTube browser v0.1 supports immediate publication; scheduled API mode is optional.")
            try:
                when = datetime.fromisoformat(schedule.replace("Z", "+00:00"))
                delta = (when - datetime.now(timezone.utc)).total_seconds()
            except (ValueError, TypeError):
                raise VpubError("invalid_schedule", "Use a future ISO timestamp with an explicit timezone.")
            if delta < 60 or (platform == "bilibili" and not 7200 <= delta <= 15 * 86400):
                raise VpubError("invalid_schedule", "Schedule is too near/past or outside Bilibili's 2h–15d range.")
            if visibility != "private" and platform == "youtube":
                raise VpubError("invalid_schedule", "YouTube schedule requires visibility=private.")
        plans[platform] = {**v, "account": account_name(v.get("account", "default")),
                           "title": title, "description": desc, "tags": tags, "visibility": visibility}
        if platform == "youtube":
            plans[platform]["method"] = v.get("method", "native")
        if platform == "douyin":
            plans[platform]["method"] = v.get("method", "browser")
        if platform in {"tiktok", "instagram"}:
            plans[platform]["method"] = "native"
        if platform == "bilibili":
            plans[platform]["method"] = v.get("method", "cli")
    payload = {"schema_version": 1, "video": str(vp), "video_sha256": file_sha(vp),
               "video_bytes": vp.stat().st_size, "cover": str(cover) if cover else None,
               "cover_sha256": file_sha(cover) if cover else None, "platforms": plans}
    return {**payload, "confirmation": digest(payload)}


class Ledger:
    def __init__(self, path=None):
        self.path = Path(path or home() / "publications.sqlite3")
        self.db = sqlite3.connect(self.path, timeout=5)
        self.db.row_factory = sqlite3.Row
        self.db.execute("PRAGMA journal_mode=WAL")
        self.db.execute("""CREATE TABLE IF NOT EXISTS publications (
            key TEXT PRIMARY KEY, platform TEXT, account TEXT, media_sha TEXT,
            state TEXT, data TEXT, updated_at TEXT)""")
        self.db.commit()

    @staticmethod
    def key(platform, account, sha):
        return digest([platform, account, sha])

    def get(self, key):
        r = self.db.execute("SELECT * FROM publications WHERE key=?", (key,)).fetchone()
        return {**dict(r), "data": json.loads(r["data"])} if r else None

    def rows(self):
        return [self.get(r[0]) for r in self.db.execute("SELECT key FROM publications ORDER BY updated_at DESC")]

    def claim(self, platform, account, sha, data):
        key = self.key(platform, account, sha)
        try:
            self.db.execute("INSERT INTO publications VALUES (?,?,?,?,?,?,?)",
                            (key, platform, account, sha, "IN_PROGRESS", json.dumps(data), now()))
            self.db.commit()
            return key
        except sqlite3.IntegrityError:
            self.db.rollback()
            return None

    def save(self, key, state, data):
        self.db.execute("UPDATE publications SET state=?,data=?,updated_at=? WHERE key=?",
                        (state, json.dumps(data, ensure_ascii=False), now(), key))
        self.db.commit()

    def close(self):
        self.db.close()


@contextlib.contextmanager
def publication_lock():
    path = home() / "publisher.lock"
    try:
        f = path.open("x", encoding="utf-8")
    except FileExistsError:
        raise VpubError("busy", "Another publication is running or a previous process was interrupted.",
                        "Check the ledger and running processes; never blindly clear this lock.")
    with f:
        f.write(json.dumps({"pid": os.getpid(), "created_at": now()}))
    try:
        yield
    finally:
        path.unlink(missing_ok=True)
