"""Opt-in local cover lead-in. Never changes or publishes the source video."""
from fractions import Fraction
from pathlib import Path
import json
import math
import os
import shutil
import subprocess
import tempfile

from .core import VpubError, atomic_json, digest, file_sha, read_json


def _binary(value):
    found = shutil.which(str(value))
    if not found:
        raise VpubError("dependency_missing", "Install FFmpeg/ffprobe or supply their executable paths.")
    return found


def _probe(binary, source):
    try:
        result = subprocess.run([binary, "-v", "error", "-show_streams", "-show_format", "-of", "json", str(source)],
                                capture_output=True, check=True, timeout=60)
        return json.loads(result.stdout)
    except (subprocess.SubprocessError, ValueError) as exc:
        raise VpubError("invalid_media", "Cannot inspect this local media file.") from exc


def prepend(video, cover, output, seconds=1.0, ffmpeg="ffmpeg", ffprobe="ffprobe"):
    video, cover, output = (Path(p).resolve() for p in (video, cover, output))
    if not math.isfinite(seconds) or not 0.5 <= seconds <= 5:
        raise VpubError("invalid_duration", "Choose a cover lead-in between 0.5 and 5 seconds.")
    if output in {video, cover} or output.suffix.lower() != ".mp4":
        raise VpubError("invalid_output", "Choose a new MP4 path, never an input path.")
    if not video.is_file() or cover.suffix.lower() not in {".jpg", ".jpeg", ".png"} or not cover.is_file():
        raise VpubError("invalid_media", "Choose a complete video and production JPG/PNG.")
    receipt_path = output.with_suffix(output.suffix + ".receipt.json")
    recipe = {"version": 1, "video_sha256": file_sha(video), "cover_sha256": file_sha(cover),
              "seconds": seconds, "mode": "prepend_contain_silent", "encoder": "libx264-crf18-aac192k"}
    recipe_hash = digest(recipe)
    if output.exists() or receipt_path.exists():
        if output.is_file() and receipt_path.is_file():
            saved = read_json(receipt_path)
            if saved.get("recipe_sha256") == recipe_hash and saved.get("output_sha256") == file_sha(output):
                return {**saved, "state": "REUSED"}
        raise VpubError("already_exists", "Output exists or differs; choose a new path.")
    encoder, probe = _binary(ffmpeg), _binary(ffprobe)
    info = _probe(probe, video)
    streams = info.get("streams", [])
    picture = next((s for s in streams if s.get("codec_type") == "video"), None)
    if not picture:
        raise VpubError("invalid_media", "Input has no video stream.")
    if any(s.get("rotation", 0) for s in picture.get("side_data_list", [])) or picture.get("tags", {}).get("rotate", "0") != "0":
        raise VpubError("unsupported_rotation", "Normalize rotation in a separate production export first.")
    width, height = picture["width"], picture["height"]
    if width % 2 or height % 2 or width * height > 16777216:
        raise VpubError("unsupported_dimensions", "Use even video dimensions up to 16 megapixels.")
    try:
        fps = Fraction(picture.get("avg_frame_rate") or picture["r_frame_rate"])
        duration = float(info["format"]["duration"])
        if not 1 <= fps <= 120 or not math.isfinite(duration) or duration <= 0:
            raise ValueError()
    except (ValueError, KeyError, ZeroDivisionError) as exc:
        raise VpubError("invalid_media", "Input frame rate/duration is not supported.") from exc
    frames = max(1, round(seconds * fps))
    actual = float(frames / fps)
    audio = any(s.get("codec_type") == "audio" for s in streams)
    graph = (f"[0:v]scale={width}:{height}:force_original_aspect_ratio=decrease,"
             f"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,"
             f"fps={fps},format=yuv420p,trim=end_frame={frames},setpts=PTS-STARTPTS[c];"
             f"[1:v:0]fps={fps},setsar=1,setpts=PTS-STARTPTS[v];"
             "[c][v]concat=n=2:v=1:a=0[outv]")
    if audio:
        graph += f";[1:a:0]aresample=48000,asetpts=PTS-STARTPTS,adelay={round(actual*48000)}S:all=1[outa]"
    output.parent.mkdir(parents=True, exist_ok=True)
    lock = output.with_suffix(output.suffix + ".lock")
    try:
        lock_fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError as exc:
        raise VpubError("render_busy", "Another render owns this output; inspect it before retrying.") from exc
    os.close(lock_fd)
    try:
        if output.exists() or receipt_path.exists():
            raise VpubError("already_exists", "Output was created by another operation.")
        with tempfile.TemporaryDirectory(prefix=".crosscast-video-", dir=output.parent) as staging:
            target = Path(staging) / "video.mp4"
            command = [encoder, "-hide_banner", "-loglevel", "error", "-nostdin", "-n", "-loop", "1", "-framerate", str(fps),
                       "-i", str(cover), "-i", str(video), "-filter_complex", graph, "-map", "[outv]"]
            if audio:
                command += ["-map", "[outa]", "-c:a", "aac", "-b:a", "192k"]
            command += ["-c:v", "libx264", "-preset", "fast", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(target)]
            try:
                subprocess.run(command, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, timeout=7200)
            except subprocess.SubprocessError as exc:
                raise VpubError("render_failed", "FFmpeg did not complete; no final output was published.") from exc
            rendered = _probe(probe, target)
            out_duration = float(rendered["format"]["duration"])
            if abs(out_duration - duration - actual) > max(0.15, 3 / float(fps)):
                raise VpubError("duration_mismatch", "Rendered duration does not preserve the full source plus cover.")
            if recipe["video_sha256"] != file_sha(video) or recipe["cover_sha256"] != file_sha(cover):
                raise VpubError("source_changed", "An input changed during render; prepare again after production finishes.")
            receipt = {"state": "PREPARED", "recipe_sha256": recipe_hash, "recipe": recipe,
                       "output": str(output), "output_sha256": file_sha(target), "output_bytes": target.stat().st_size,
                       "source_duration": duration, "output_duration": out_duration, "cover_seconds": actual,
                       "width": width, "height": height, "audio_preserved": audio, "platform_writes": 0,
                       "note": "A new re-encoded variant, not a publication. Original inputs remain unchanged. Shift chapters by cover_seconds if needed; inspect the rendered transition and phone cover selection."}
            # Exclusive creation prevents clobbering a competing non-CrossCast writer.
            with output.open("xb") as destination, target.open("rb") as source:
                shutil.copyfileobj(source, destination)
            atomic_json(receipt_path, receipt)
            return receipt
    finally:
        lock.unlink(missing_ok=True)
