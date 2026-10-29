"""Default Instagram preparation: landscape gets a cover lead; portrait passes through."""
from copy import deepcopy
from pathlib import Path
from .core import VpubError, atomic_json, digest, file_sha, load_plan, read_json
from .cover_video import _binary, _probe, prepend


def prepare(manifest, output, ffmpeg="ffmpeg", ffprobe="ffprobe"):
    manifest, output = Path(manifest).resolve(), Path(output).resolve()
    if manifest == output:
        raise VpubError("invalid_output", "Choose a separate prepared manifest.")
    source = read_json(manifest)
    plan = load_plan(manifest, ["instagram"])
    video = Path(plan["video"])
    streams = _probe(_binary(ffprobe), video).get("streams", [])
    picture = next((s for s in streams if s.get("codec_type") == "video"), None)
    if not picture:
        raise VpubError("invalid_media", "Input has no video stream.")
    rotation = next((s.get("rotation", 0) for s in picture.get("side_data_list", []) if "rotation" in s), 0)
    rotation = float(picture.get("tags", {}).get("rotate", rotation))
    width, height = picture["width"], picture["height"]
    if abs(rotation) % 180 == 90:
        width, height = height, width
    cover = plan.get("cover") or plan.get("covers", {}).get("instagram", {}).get("landscape", {}).get("path")
    value = deepcopy(source)
    value["platforms"] = {"instagram": deepcopy(source["platforms"]["instagram"])}
    value.pop("covers", None)
    if cover:
        value["cover"] = str(Path(cover).resolve())
    else:
        value.pop("cover", None)
    previous_receipt = video.with_suffix(video.suffix + ".receipt.json")
    prior = read_json(previous_receipt) if previous_receipt.is_file() else {}
    already_prepared = prior.get("output_sha256") == plan["video_sha256"] and prior.get("recipe", {}).get("mode") == "prepend_contain_silent"
    if already_prepared and cover and prior["recipe"].get("cover_sha256") != file_sha(cover):
        raise VpubError("cover_changed", "Prepare the original source with the new cover; do not stack cover lead-ins.")
    if width > height and not already_prepared:
        if not cover:
            raise VpubError("cover_required", "Landscape Instagram videos need the dedicated production cover.")
        if source.get("chapters") or source["platforms"]["instagram"].get("chapters"):
            raise VpubError("retime_required", "This manifest has chapters; retime them explicitly for the added cover second.")
        recipe = digest({"video": plan["video_sha256"], "cover": file_sha(cover), "rule": "landscape-cover-lead-v1"})
        rendered = output.parent / "instagram-media" / (recipe + ".mp4")
        proof = prepend(video, cover, rendered, 1.0, ffmpeg, ffprobe)
        value["video"] = str(rendered)
        mode = "LANDSCAPE_COVER_PREPENDED"
    else:
        value["video"] = str(video)
        mode = "ALREADY_PREPARED" if already_prepared else "ORIGINAL_PORTRAIT_OR_SQUARE"
    if output.exists():
        if digest(read_json(output)) != digest(value):
            raise VpubError("already_exists", "Prepared manifest differs; choose a new output path.")
    else:
        atomic_json(output, value)
    return {"state": "PREPARED", "mode": mode, "manifest": str(output), "video": value["video"],
            "video_sha256": file_sha(value["video"]), "platforms": ["instagram"], "platform_writes": 0,
            "next": "Plan/publish this Instagram manifest. Landscape preparation is automatic; portrait/square footage is unchanged. Verify cover selection on the phone separately."}
