"""Network-free paths and metadata for explicitly installed Argos language packs."""
import json
import os
import re
from pathlib import Path

CODE = re.compile(r"[a-z][a-z0-9]*(?:-[a-z0-9]+)*\Z")
VERSION = re.compile(r"[0-9]{1,3}(?:\.[0-9]{1,3}){0,3}\Z")
MAX_METADATA = 65536


def data_directory():
    override = os.environ.get("DAFEIYU_DATA_DIR")
    if override:
        return Path(override).expanduser().resolve()
    local = os.environ.get("LOCALAPPDATA")
    return (Path(local) if local else Path.home() / "AppData" / "Local") / "DafeiyuTranslator"


def packages_directory():
    return data_directory() / "packages"


def valid_code(value):
    return isinstance(value, str) and 2 <= len(value) <= 16 and bool(CODE.fullmatch(value))


def metadata_info(metadata):
    if not isinstance(metadata, dict) or metadata.get("type", "translate") != "translate":
        raise ValueError("invalid_metadata")
    source, target, version = (metadata.get(key) for key in ("from_code", "to_code", "package_version"))
    if not valid_code(source) or not valid_code(target) or source == target or not isinstance(version, str) or not VERSION.fullmatch(version):
        raise ValueError("invalid_metadata")
    names = [metadata.get("from_name", source), metadata.get("to_name", target)]
    if any(not isinstance(name, str) or not name or len(name) > 100 or any(ord(c) < 32 for c in name) for name in names):
        raise ValueError("invalid_metadata")
    prefix = metadata.get("target_prefix", "")
    if not isinstance(prefix, str) or len(prefix) > 128 or any(ord(c) < 32 for c in prefix):
        raise ValueError("invalid_metadata")
    return {"id": "translate-{}_{}-{}".format(source, target, version.replace(".", "_")),
            "from_code": source, "from_name": names[0], "to_code": target, "to_name": names[1],
            "package_version": version, "pair": "pair:{}:{}".format(source, target)}


def read_json(path, limit=MAX_METADATA):
    with Path(path).open("rb") as stream:
        raw = stream.read(limit + 1)
    if len(raw) > limit:
        raise ValueError("invalid_metadata")
    return json.loads(raw.decode("utf-8-sig"))


def is_link(path):
    return path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction())


def model_files(folder):
    required = [folder / "metadata.json", folder / "sentencepiece.model",
                folder / "model" / "model.bin", folder / "model" / "config.json"]
    shared = folder / "model" / "shared_vocabulary.json"
    required.extend([shared] if shared.is_file() else [folder / "model" / "source_vocabulary.json",
                                                     folder / "model" / "target_vocabulary.json"])
    return required


def installed_packages(include_path=False):
    root = packages_directory()
    if not root.is_dir() or is_link(root):
        return []
    result = []
    for folder in root.iterdir():
        if folder.name.startswith(".") or not folder.is_dir() or is_link(folder):
            continue
        try:
            metadata = read_json(folder / "metadata.json")
            info = metadata_info(metadata)
            receipt = read_json(folder / ".install.json")
            if folder.name != info["id"] or receipt.get("id") != info["id"] or receipt.get("complete") is not True:
                continue
            files = model_files(folder)
            if is_link(folder / "model") or any(is_link(path) or not path.is_file() or path.stat().st_size <= 0 for path in files):
                continue
            if include_path:
                info = dict(info, path=str(folder), target_prefix=metadata.get("target_prefix", ""))
            result.append(info)
        except (OSError, ValueError, TypeError):
            continue
    return sorted(result, key=lambda item: (item["from_code"], item["to_code"], item["package_version"]))


def installed_for_pair(source, target):
    matches = [item for item in installed_packages(True) if item["from_code"] == source and item["to_code"] == target]
    return max(matches, key=lambda item: tuple(map(int, item["package_version"].split("."))), default=None)
