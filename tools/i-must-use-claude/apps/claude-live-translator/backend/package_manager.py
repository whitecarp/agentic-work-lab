"""Explicit, bounded Argos catalog/download/import operations; JSON-lines CLI.

--list is local only. Only --refresh and --install may use the network.
Installed packages live in the user's data directory, never in an EXE cache.
"""
import argparse
import contextlib
import datetime
import hashlib
import json
import os
import re
import shutil
import signal
import stat
import sys
import threading
import time
import uuid
import zipfile
from pathlib import Path, PurePosixPath

# Windows embeddable Python uses an isolated ._pth and omits the script directory.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from package_store import (data_directory, packages_directory, metadata_info, read_json,
                           installed_packages, model_files, is_link)

CATALOG_URL = "https://raw.githubusercontent.com/argosopentech/argospm-index/main/index.json"
BUNDLED_CATALOG = Path(__file__).with_name("argos_catalog.json")
MAX_CATALOG = 2 * 1024 * 1024
MAX_DOWNLOAD = 1024 * 1024 * 1024
MAX_UNPACKED = 2 * 1024 * 1024 * 1024
MAX_ENTRY = 1024 * 1024 * 1024
MAX_FILES = 4096
READ_SIZE = 256 * 1024
STAGE_NAME = re.compile(r"[0-9]+-[0-9a-f]{32}\Z")
PACKAGE_ID = re.compile(r"translate-[a-z0-9-]+_[a-z0-9-]+-[0-9_]+\Z")
SHA256_HEX = re.compile(r"[0-9a-f]{64}\Z")


class PackageError(Exception):
    pass


def check_cancel(cancel):
    if cancel is not None and cancel.is_set():
        raise PackageError("cancelled")


def allowed_url(url, catalog=False):
    from urllib.parse import urlsplit, unquote
    try:
        parsed = urlsplit(url)
        if parsed.scheme != "https" or parsed.username or parsed.password or parsed.port not in (None, 443) or parsed.query or parsed.fragment:
            return False
        if catalog:
            return url == CATALOG_URL
        decoded = unquote(parsed.path)
        if "\\" in decoded or "\0" in decoded or any(part == ".." for part in decoded.split("/")):
            return False
        prefixes = {"argos-net.com": "/v1/", "data.argosopentech.com": "/argospm/v1/"}
        prefix = prefixes.get(parsed.hostname)
        return bool(prefix and parsed.path.startswith(prefix) and re.fullmatch(r"translate-[a-z0-9_-]+\.argosmodel", parsed.path[len(prefix):]))
    except (TypeError, ValueError):
        return False


def open_official(url, catalog=False):
    import urllib.request
    if not allowed_url(url, catalog):
        raise PackageError("untrusted_source")
    class Redirects(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            if not allowed_url(newurl, catalog):
                raise PackageError("untrusted_redirect")
            return super().redirect_request(req, fp, code, msg, headers, newurl)
    response = urllib.request.build_opener(Redirects()).open(
        urllib.request.Request(url, headers={"User-Agent": "DafeiyuTranslator-PackageManager/1"}), timeout=10)
    if not allowed_url(response.geturl(), catalog):
        response.close()
        raise PackageError("untrusted_redirect")
    return response


def normalize_catalog(document):
    rows = document.get("packages") if isinstance(document, dict) else document
    if not isinstance(rows, list) or not rows or len(rows) > 5000:
        raise PackageError("invalid_catalog")
    output = {}
    for row in rows:
        try:
            info = metadata_info(row)
            links = row.get("links", [])
            link = next((url for url in links if isinstance(url, str) and allowed_url(url)), None)
            if not link:
                continue
            info["download_url"] = link
            # The official catalog does not identify its tokenizer or provide checksums.
            # Installation validates SentencePiece/CTranslate2 compatibility before commit.
            info["compatibility"] = "checked_on_install"
            output[info["id"]] = info
        except (ValueError, TypeError, AttributeError):
            continue
    if not output:
        raise PackageError("invalid_catalog")
    return sorted(output.values(), key=lambda item: (item["from_name"], item["to_name"], item["package_version"]))


def pinned_sha256(package_id):
    """Known-good SHA-256 of an official package version, or None when it is not pinned yet.

    Pins live only in the bundled catalog (an optional "sha256" on a package row), so a
    refreshed online catalog can add packages but can never replace or remove a pin."""
    try:
        document = read_json(BUNDLED_CATALOG, MAX_CATALOG)
    except (ValueError, OSError):
        return None
    rows = document.get("packages") if isinstance(document, dict) else None
    for row in rows if isinstance(rows, list) else []:
        if not isinstance(row, dict) or "sha256" not in row:
            continue
        try:
            row_id = metadata_info(row)["id"]
        except (ValueError, TypeError, AttributeError):
            continue
        if row_id != package_id:
            continue
        value = row["sha256"]
        if not isinstance(value, str) or not SHA256_HEX.match(value):
            raise PackageError("invalid_catalog")
        return value
    return None


def catalog():
    cached = data_directory() / "argos_catalog.json"
    if cached.is_file():
        try:
            return normalize_catalog(read_json(cached, MAX_CATALOG)), "refreshed"
        except (ValueError, OSError, PackageError):
            pass
    try:
        return normalize_catalog(read_json(BUNDLED_CATALOG, MAX_CATALOG)), "bundled"
    except (ValueError, OSError):
        raise PackageError("invalid_catalog") from None


def list_packages():
    # Listing never creates directories or uses the network. If a prior manager was
    # forcibly killed, recover its interrupted commit and remove only its stale staging.
    cleanup_stale_stages(packages_directory() / ".staging")
    available, source = catalog()
    return {"installed": installed_packages(), "catalog": available, "catalog_source": source}


def _process_identity(pid):
    if os.name == "nt":
        import ctypes
        from ctypes import wintypes
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
        kernel.OpenProcess.restype = wintypes.HANDLE
        kernel.CloseHandle.argtypes = (wintypes.HANDLE,)
        kernel.GetProcessTimes.argtypes = (wintypes.HANDLE,) + (ctypes.POINTER(wintypes.FILETIME),) * 4
        handle = kernel.OpenProcess(0x1000, False, pid)
        if not handle:
            return "unknown" if ctypes.get_last_error() == 5 else None
        try:
            times = [wintypes.FILETIME() for _ in range(4)]
            if not kernel.GetProcessTimes(handle, *(ctypes.byref(item) for item in times)):
                return "unknown"
            return str((times[0].dwHighDateTime << 32) | times[0].dwLowDateTime)
        finally:
            kernel.CloseHandle(handle)
    try:
        os.kill(pid, 0)
        return "alive"
    except ProcessLookupError:
        return None
    except PermissionError:
        return "unknown"


def _safe_remove_stage(path, root):
    # Only our narrow staging names may be recursively removed, never installed packs.
    if not STAGE_NAME.fullmatch(path.name) or is_link(path) or path.parent.resolve() != root.resolve():
        return
    if not recover_commit(path):
        return
    shutil.rmtree(path, ignore_errors=True)


def recover_commit(stage):
    """A hard kill between Windows directory renames must not lose the previous pack."""
    marker = stage / "commit.json"
    if not marker.is_file():
        return True
    try:
        transaction = read_json(marker)
        identifier, backup_name = transaction["id"], transaction["backup"]
        if not PACKAGE_ID.fullmatch(identifier) or not re.fullmatch(re.escape(identifier) + r"-[0-9a-f]{32}", backup_name):
            return False
        root = packages_directory()
        target, backup = root / identifier, root / ".backups" / backup_name
        if is_link(root / ".backups") or is_link(backup) or is_link(target):
            return False
        if not target.exists() and backup.is_dir():
            backup.rename(target)
        return True
    except (ValueError, KeyError, TypeError, OSError):
        return False


def cleanup_stale_stages(root):
    if not root.is_dir() or is_link(root):
        return
    for path in root.iterdir():
        if not path.is_dir() or is_link(path) or not STAGE_NAME.fullmatch(path.name):
            continue
        try:
            owner = read_json(path / "owner.json")
            pid = owner["pid"]
            if not isinstance(pid, int) or pid <= 0:
                continue
            actual = _process_identity(pid)
            if actual is None or (actual != "unknown" and actual != owner.get("identity")):
                _safe_remove_stage(path, root)
        except (OSError, ValueError, KeyError):
            # A process may be killed between mkdir and writing its owner record.
            if time.time() - path.stat().st_mtime > 86400:
                _safe_remove_stage(path, root)


@contextlib.contextmanager
def staging():
    root = packages_directory() / ".staging"
    if is_link(packages_directory()) or is_link(root):
        raise PackageError("unsafe_data_path")
    root.mkdir(parents=True, exist_ok=True)
    cleanup_stale_stages(root)
    stage = root / (str(os.getpid()) + "-" + uuid.uuid4().hex)
    stage.mkdir()
    (stage / "owner.json").write_text(json.dumps({"pid": os.getpid(), "identity": _process_identity(os.getpid())}), encoding="utf-8")
    try:
        yield stage
    finally:
        _safe_remove_stage(stage, root)


def refresh_catalog(cancel=None, progress=None):
    check_cancel(cancel)
    try:
        with open_official(CATALOG_URL, True) as response:
            raw = response.read(MAX_CATALOG + 1)
        check_cancel(cancel)
        if len(raw) > MAX_CATALOG:
            raise PackageError("catalog_too_large")
        document = json.loads(raw.decode("utf-8"))
        rows = normalize_catalog(document)
        wrapped = {"schema_version": 1, "source": CATALOG_URL,
                   "retrieved_on": datetime.datetime.now(datetime.timezone.utc).isoformat(), "packages": document}
        with staging() as stage:
            target = data_directory() / "argos_catalog.json"
            temporary = stage / "catalog.json"
            temporary.write_text(json.dumps(wrapped, ensure_ascii=False), encoding="utf-8")
            check_cancel(cancel)
            os.replace(temporary, target)
        return {"catalog": rows, "catalog_source": "refreshed", "installed": installed_packages()}
    except PackageError:
        raise
    except (ValueError, UnicodeError):
        raise PackageError("invalid_catalog") from None
    except Exception:
        raise PackageError("network_error") from None


def download(url, path, cancel=None, progress=None):
    try:
        with open_official(url) as response, path.open("xb") as output:
            total = response.headers.get("Content-Length")
            total = int(total) if total and total.isdigit() else None
            if total is not None and total > MAX_DOWNLOAD:
                raise PackageError("download_too_large")
            count, last = 0, 0.0
            digest = hashlib.sha256()
            while True:
                check_cancel(cancel)
                chunk = response.read(READ_SIZE)
                if not chunk:
                    break
                count += len(chunk)
                if count > MAX_DOWNLOAD:
                    raise PackageError("download_too_large")
                output.write(chunk)
                digest.update(chunk)
                if progress and (time.monotonic() - last >= 0.25 or count == total):
                    progress("download", count, total)
                    last = time.monotonic()
            if not count or (total is not None and count != total):
                raise PackageError("incomplete_download")
            check_cancel(cancel)
            return digest.hexdigest()
    except PackageError:
        raise
    except Exception:
        check_cancel(cancel)
        raise PackageError("network_error") from None


def _archive_entries(archive):
    infos = archive.infolist()
    if not infos or len(infos) > MAX_FILES:
        raise PackageError("archive_limit")
    output, seen, total = [], set(), 0
    for item in infos:
        name = item.orig_filename
        path = PurePosixPath(name)
        if ("\\" in name or "\0" in name or path.is_absolute() or len(name) > 240 or
                any(part in (".", "..") or ":" in part or part.endswith((".", " ")) for part in path.parts)):
            raise PackageError("unsafe_archive")
        if any(re.fullmatch(r"(?:con|prn|aux|nul|com[0-9]|lpt[0-9])(?:\..*)?", part, re.I) for part in path.parts):
            raise PackageError("unsafe_archive")
        mode = item.external_attr >> 16
        if stat.S_ISLNK(mode) or (stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR)) or item.flag_bits & 1:
            raise PackageError("unsafe_archive")
        key = str(path).casefold()
        if key in seen:
            raise PackageError("unsafe_archive")
        seen.add(key)
        total += item.file_size
        if (item.file_size > MAX_ENTRY or total > MAX_UNPACKED or
                (item.file_size > 1024 * 1024 and item.file_size > max(1, item.compress_size) * 1000)):
            raise PackageError("archive_limit")
        output.append((item, path))
    metadata = [(item, path) for item, path in output if path.name == "metadata.json" and len(path.parts) in (1, 2)]
    if len(metadata) != 1:
        raise PackageError("invalid_metadata")
    meta_item, meta_path = metadata[0]
    prefix = meta_path.parts[:-1]
    if any(path.parts[:len(prefix)] != prefix for _, path in output):
        raise PackageError("unsafe_archive")
    if meta_item.file_size > 65536:
        raise PackageError("invalid_metadata")
    return output, prefix, meta_item


def _keep_file(relative):
    if relative in ("metadata.json", "sentencepiece.model") or (relative.startswith("model/") and PurePosixPath(relative).suffix.lower() in (".bin", ".json", ".txt")):
        return True
    parts = PurePosixPath(relative).parts
    return PurePosixPath(relative).suffix.lower() in ("", ".txt", ".md", ".rst") and any(part.lower().startswith(("readme", "license", "copying", "notice")) for part in parts)


def validate_engine(folder):
    try:
        import logging
        import ctranslate2
        import sentencepiece
        ctranslate2.set_log_level(logging.ERROR)
        sentencepiece.SentencePieceProcessor(model_file=str(folder / "sentencepiece.model"))
        engine = ctranslate2.Translator(str(folder / "model"), device="cpu", compute_type="int8", inter_threads=1, intra_threads=1)
        del engine
    except (ImportError, OSError):
        raise PackageError("offline_runtime_unavailable") from None
    except Exception:
        raise PackageError("unsupported_package") from None


def install_archive(path, stage, expected=None, digest=None, cancel=None, progress=None, validator=validate_engine, reinstall=False):
    check_cancel(cancel)
    if not path.is_file() or path.stat().st_size > MAX_DOWNLOAD:
        raise PackageError("archive_limit")
    folder = stage / "package"
    folder.mkdir()
    try:
        with zipfile.ZipFile(path) as archive:
            entries, prefix, meta_item = _archive_entries(archive)
            try:
                metadata = json.loads(archive.read(meta_item).decode("utf-8-sig"))
                info = metadata_info(metadata)
            except (ValueError, UnicodeError):
                raise PackageError("invalid_metadata") from None
            if metadata.get("dependencies") or metadata.get("from_codes") or metadata.get("to_codes"):
                raise PackageError("unsupported_package")
            if expected and info["id"] != expected["id"]:
                raise PackageError("package_mismatch")
            names = {str(PurePosixPath(*member.parts[len(prefix):])) for item, member in entries if not item.is_dir()}
            if "sentencepiece.model" not in names or "model/model.bin" not in names or "model/config.json" not in names:
                raise PackageError("unsupported_package")
            if not ("model/shared_vocabulary.json" in names or {"model/source_vocabulary.json", "model/target_vocabulary.json"} <= names):
                raise PackageError("unsupported_package")
            if progress:
                progress("validate", 0, None)
            for item, member in entries:
                check_cancel(cancel)
                relative = str(PurePosixPath(*member.parts[len(prefix):]))
                if item.is_dir() or not _keep_file(relative):
                    continue
                destination = folder.joinpath(*PurePosixPath(relative).parts)
                if not destination.resolve().is_relative_to(folder.resolve()):
                    raise PackageError("unsafe_archive")
                destination.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(item) as source, destination.open("xb") as output:
                    written = 0
                    while True:
                        check_cancel(cancel)
                        chunk = source.read(READ_SIZE)
                        if not chunk:
                            break
                        written += len(chunk)
                        if written > item.file_size:
                            raise PackageError("archive_limit")
                        output.write(chunk)
            if any(not part.is_file() or part.stat().st_size <= 0 for part in model_files(folder)):
                raise PackageError("unsupported_package")
        check_cancel(cancel)
        validator(folder)
        check_cancel(cancel)
        if digest is None:
            digest_state = hashlib.sha256()
            with path.open("rb") as stream:
                while True:
                    check_cancel(cancel)
                    chunk = stream.read(READ_SIZE)
                    if not chunk:
                        break
                    digest_state.update(chunk)
            digest = digest_state.hexdigest()
        receipt = dict(info, complete=True, schema_version=1, archive_sha256=digest,
                       origin="official" if expected else "local_import",
                       installed_at=datetime.datetime.now(datetime.timezone.utc).isoformat())
        (folder / ".install.json").write_text(json.dumps(receipt, ensure_ascii=False), encoding="utf-8")
        check_cancel(cancel)
        destination = packages_directory() / info["id"]
        if progress:
            progress("install", 0, None)
        if destination.exists():
            if not reinstall and any(package["id"] == info["id"] for package in installed_packages()):
                return dict(info, already_installed=True)
            if not reinstall:
                raise PackageError("package_conflict")
            if is_link(destination):
                raise PackageError("unsafe_data_path")
            backups = packages_directory() / ".backups"
            if is_link(backups):
                raise PackageError("unsafe_data_path")
            backups.mkdir(exist_ok=True)
            backup = backups / (info["id"] + "-" + uuid.uuid4().hex)
            (stage / "commit.json").write_text(json.dumps({"id": info["id"], "backup": backup.name}), encoding="utf-8")
            try:
                destination.rename(backup)
            except OSError:
                raise PackageError("package_busy") from None
            try:
                folder.rename(destination)
            except OSError:
                recover_commit(stage)
                raise PackageError("package_busy") from None
            return dict(info, reinstalled=True)
        try:
            folder.rename(destination)
        except FileExistsError:
            if not any(package["id"] == info["id"] for package in installed_packages()):
                raise PackageError("package_conflict") from None
        return info
    except PackageError:
        raise
    except (zipfile.BadZipFile, RuntimeError, EOFError, OSError, ValueError):
        raise PackageError("invalid_archive") from None


def install(package_id, cancel=None, progress=None, reinstall=False):
    available, _ = catalog()
    choice = next((item for item in available if item["id"] == package_id), None)
    if choice is None:
        raise PackageError("unknown_package")
    current = next((item for item in installed_packages() if item["id"] == package_id), None)
    if current and not reinstall:
        return dict(current, already_installed=True)
    expected_digest = pinned_sha256(package_id)
    with staging() as stage:
        archive = stage / "download.argosmodel"
        digest = download(choice["download_url"], archive, cancel, progress)
        if expected_digest is not None and digest != expected_digest:
            raise PackageError("checksum_mismatch")
        return install_archive(archive, stage, choice, digest, cancel, progress, reinstall=reinstall)


def import_package(path, cancel=None, progress=None, validator=validate_engine):
    with staging() as stage:
        return install_archive(Path(path), stage, cancel=cancel, progress=progress, validator=validator)


def main(argv=None):
    for stream in (sys.stdin, sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="strict")
    parser = argparse.ArgumentParser()
    commands = parser.add_mutually_exclusive_group(required=True)
    commands.add_argument("--list", action="store_true")
    commands.add_argument("--refresh", action="store_true")
    commands.add_argument("--install")
    commands.add_argument("--reinstall")
    commands.add_argument("--import", dest="import_path")
    args = parser.parse_args(argv)
    operation = "list" if args.list else "refresh" if args.refresh else "install" if args.install else "reinstall" if args.reinstall else "import"
    cancel = threading.Event()
    def watch_cancel():
        try:
            for line in sys.stdin:
                if line.strip().lower() == "cancel" or line.strip() == '{"cancel":true}':
                    cancel.set()
                    return
            # A closed parent pipe means the UI has exited; do not keep downloading.
            cancel.set()
        except (OSError, ValueError):
            cancel.set()
    if operation != "list":
        threading.Thread(target=watch_cancel, daemon=True).start()
        for signum in (signal.SIGINT, signal.SIGTERM):
            signal.signal(signum, lambda *_: cancel.set())
    def emit(value):
        print(json.dumps(value, ensure_ascii=False), flush=True)
    def progress(phase, count, total):
        emit({"event": "progress", "operation": operation, "phase": phase, "bytes": count, "total_bytes": total})
    try:
        if args.list:
            result = list_packages()
        elif args.refresh:
            result = refresh_catalog(cancel, progress)
        elif args.install:
            result = {"package": install(args.install, cancel, progress)}
        elif args.reinstall:
            result = {"package": install(args.reinstall, cancel, progress, reinstall=True)}
        else:
            result = {"package": import_package(args.import_path, cancel, progress)}
        emit(dict(result, event="result", operation=operation, ok=True))
        return 0
    except PackageError as error:
        emit({"event": "result", "operation": operation, "ok": False, "error": str(error)})
    except Exception:
        emit({"event": "result", "operation": operation, "ok": False, "error": "package_operation_failed"})
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
