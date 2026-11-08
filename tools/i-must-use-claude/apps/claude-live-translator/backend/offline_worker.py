"""Offline translation using language pairs explicitly installed by the user.

This worker has no Google, HTTP, download, or automatic fallback path. Its protocol
matches google_worker.py, while installed models load only for a needed request.
"""
from __future__ import annotations

import collections
import json
import logging
import re
import sys
import time
from pathlib import Path
# The embeddable runtime's isolated ._pth does not add the script directory.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from package_store import installed_for_pair, installed_packages, valid_code
from glossary import validated_entries, replacement_spans

ROOT = Path(__file__).resolve().parents[1]
MAX_TEXT = 32000
MAX_CHUNK = 1200
MAX_INPUT_TOKENS = 120
MAX_DECODE_TOKENS = 512
MAX_CACHE_CHARS = 600000
PROTECTED = re.compile(
    r"(?:https?://|www\.)[^\s<>\"'`\u3000-\u303f\u3400-\u9fff\uf900-\ufaff\uff00-\uffef]+"
    r"|`[^`\r\n]+`", re.I)
HAN = re.compile(r"[\u3400-\u9fff\uf900-\ufaff\U00020000-\U0002fa1f]")
CHINESE_SPAN = re.compile(r"[\u3400-\u9fff\uf900-\ufaff\U00020000-\U0002fa1f\u3000-\u303f\uff00-\uffef]+")
ENGLISH = re.compile(r"[A-Za-z]")
LINE_END = re.compile(r"([\r\n\v\f\x1c-\x1e\x85\u2028\u2029]+)$")


class TranslationError(Exception):
    """Only a bounded error code is exposed to the parent application."""


def normalize_direction(direction):
    if direction in ("en_zh", "auto:zh-CN"):
        return "en_zh"
    if direction in ("zh_en", "auto:en"):
        return "zh_en"
    if isinstance(direction, str) and direction.startswith("pair:"):
        parts = direction.split(":")
        if len(parts) == 3 and valid_code(parts[1]) and valid_code(parts[2]) and parts[1] != parts[2]:
            if parts[1:] == ["en", "zh"]:
                return "en_zh"
            if parts[1:] == ["zh", "en"]:
                return "zh_en"
            return direction
    raise TranslationError("offline_unsupported_target")


def direction_pair(direction):
    if direction == "en_zh":
        return "en", "zh"
    if direction == "zh_en":
        return "zh", "en"
    _, source, target = direction.split(":")
    return source, target


def needs_translation(text, direction):
    source, _ = direction_pair(direction)
    if source in ("zh", "zt"):
        return bool(HAN.search(text))
    if source == "en":
        return bool(ENGLISH.search(text))
    return any(character.isalpha() for character in text)


def protected_spans(text):
    for match in PROTECTED.finditer(text):
        start, end = match.span()
        value = match.group(0)
        if not value.startswith("`"):
            while value and (value[-1] in ".,;:!?" or
                             (value[-1] == ")" and value.count(")") > value.count("("))):
                value, end = value[:-1], end - 1
            if value.lower() in ("http://", "https://", "www."):
                continue
        yield start, end


def bounded_chunks(text):
    """Do not drop any source characters, even if a sentence exceeds model limits."""
    while len(text) > MAX_CHUNK:
        boundaries = list(re.finditer(r"[。！？；]+|[.!?;]\s+|\s+", text[:MAX_CHUNK]))
        end = boundaries[-1].end() if boundaries and boundaries[-1].end() > MAX_CHUNK // 2 else MAX_CHUNK
        yield text[:end]
        text = text[end:]
    if text:
        yield text


class ModelTransport:
    def __init__(self, model_dirs=None):
        # An explicit map is a test-only injection; production has no runtime/cache fallback.
        self.model_dirs = None if model_dirs is None else dict(model_dirs)
        self.engines = {}
        self.prefixes = {}

    def _load(self, direction):
        if direction in self.engines:
            return self.engines[direction]
        if self.model_dirs is not None:
            folder = self.model_dirs[direction]
            prefix = ""
        else:
            package = installed_for_pair(*direction_pair(direction))
            if package is None:
                raise TranslationError("offline_model_missing")
            folder = Path(package["path"])
            prefix = package.get("target_prefix", "")
        required = (folder / "sentencepiece.model", folder / "model" / "model.bin",
                    folder / "model" / "config.json")
        if not all(path.is_file() for path in required):
            raise TranslationError("offline_model_missing")
        try:
            import ctranslate2
            import sentencepiece
        except (ImportError, OSError):
            raise TranslationError("offline_runtime_unavailable") from None
        try:
            ctranslate2.set_log_level(logging.ERROR)
            translator = ctranslate2.Translator(str(folder / "model"), device="cpu",
                compute_type="int8", inter_threads=1, intra_threads=2)
            tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(folder / "sentencepiece.model"))
        except Exception:
            raise TranslationError("offline_model_load_failed") from None
        self.engines[direction] = translator, tokenizer
        self.prefixes[direction] = prefix
        return self.engines[direction]

    def __call__(self, text, direction):
        translator, tokenizer = self._load(direction)
        try:
            tokens = tokenizer.encode(text, out_type=str)
            output = []
            for start in range(0, len(tokens), MAX_INPUT_TOKENS):
                options = {}
                prefix = self.prefixes.get(direction, "")
                if prefix:
                    options["target_prefix"] = [[prefix]]
                result = translator.translate_batch([tokens[start:start + MAX_INPUT_TOKENS]],
                    beam_size=1, max_input_length=0, max_decoding_length=MAX_DECODE_TOKENS, **options)[0]
                hypothesis = result.hypotheses[0]
                if len(hypothesis) >= MAX_DECODE_TOKENS:
                    raise TranslationError("offline_output_limit")
                value = tokenizer.decode(hypothesis).replace("▁", " ").strip()
                if prefix and value.startswith(prefix):
                    value = value[len(prefix):].lstrip()
                if direction_pair(direction)[1] in ("zh", "zt"):
                    value = re.sub(r"\s+([，。！？；：、])", r"\1", value)
                if not value:
                    raise TranslationError("translation_failed")
                output.append(value)
            return ("" if direction_pair(direction)[1] in ("zh", "zt", "ja") else " ").join(output)
        except TranslationError:
            raise
        except Exception:
            raise TranslationError("translation_failed") from None


class OfflineTranslator:
    def __init__(self, transport=None):
        self.transport = ModelTransport() if transport is None else transport
        self.cache = collections.OrderedDict()
        self.cache_chars = 0

    def prose(self, text, direction, deadline):
        if not needs_translation(text, direction):
            return text
        output = []
        for chunk in bounded_chunks(text):
            before, body, after = re.match(r"^(\s*)(.*?)(\s*)$", chunk, re.S).groups()
            if not body or not needs_translation(body, direction):
                output.append(chunk)
                continue
            key = direction, body
            if key in self.cache:
                self.cache.move_to_end(key)
                value = self.cache[key]
            else:
                if time.monotonic() >= deadline:
                    raise TranslationError("offline_deadline")
                value = self.transport(body, direction)
                if time.monotonic() >= deadline:
                    raise TranslationError("offline_deadline")
                if not isinstance(value, str) or not value.strip():
                    raise TranslationError("translation_failed")
                # Line structure belongs to the source, not to model-generated formatting.
                value = re.sub(r"[\r\n\v\f\x1c-\x1e\x85\u2028\u2029]+", " ", value).strip()
                self.cache[key] = value
                self.cache_chars += len(body) + len(value)
                while len(self.cache) > 200 or self.cache_chars > MAX_CACHE_CHARS:
                    old, old_value = self.cache.popitem(last=False)
                    self.cache_chars -= len(old[1]) + len(old_value)
            output.append(before + value + after)
        return "".join(output)

    def mixed_prose(self, text, direction, deadline):
        if direction != "en_zh" or not HAN.search(text):
            return self.prose(text, direction, deadline)
        # The English-source Argos model corrupts existing Chinese in mixed input.
        # Keep those spans exact while still translating every surrounding English span.
        output, cursor = [], 0
        for match in CHINESE_SPAN.finditer(text):
            output.append(self.prose(text[cursor:match.start()], direction, deadline))
            output.append(match.group(0))
            cursor = match.end()
        output.append(self.prose(text[cursor:], direction, deadline))
        return "".join(output)

    def line(self, text, direction, deadline, glossary=()):
        # Protected contents never enter the model. Fragment translation is deliberate:
        # these local models are not reliable enough with placeholder tokens.
        output, cursor = [], 0
        for start, end, replacement in replacement_spans(text, glossary, direction_pair(direction)[1], protected_spans(text)):
            output.append(self.mixed_prose(text[cursor:start], direction, deadline))
            output.append(replacement)
            cursor = end
        output.append(self.mixed_prose(text[cursor:], direction, deadline))
        return "".join(output)

    def translate(self, text, direction, glossary=None):
        direction = normalize_direction(direction)
        try:
            entries = validated_entries(glossary)
        except ValueError:
            raise TranslationError("invalid_glossary") from None
        if not isinstance(text, str):
            raise TranslationError("invalid_request")
        if len(text) > MAX_TEXT:
            raise TranslationError("text_too_long")
        deadline = time.monotonic() + 120
        output, fence = [], None
        for line in text.splitlines(keepends=True):
            opening = re.match(r" {0,3}(`{3,}|~{3,})", line)
            if fence:
                output.append(line)
                closing = re.fullmatch(r" {0,3}([`~]+)[ \t]*(?:\r?\n|\r)?", line)
                if closing and set(closing.group(1)) == {fence[0]} and len(closing.group(1)) >= len(fence):
                    fence = None
            elif opening:
                fence = opening.group(1)
                output.append(line)
            elif line.startswith(("    ", "\t")) or not line.strip():
                output.append(line)
            else:
                ending = LINE_END.search(line)
                body, suffix = (line[:ending.start()], ending.group(0)) if ending else (line, "")
                output.append(self.line(body, direction, deadline, entries) + suffix)
        return "".join(output)


def main():
    for stream in (sys.stdin, sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="strict")
    engine = OfflineTranslator()
    # Ready means local protocol readiness; it does not claim models have loaded.
    installed = installed_packages()
    print(json.dumps({"ready": True, "offline": True, "provider": "argos-cpu",
                      "installed_pairs": [item["pair"] for item in installed],
                      "modelsLoaded": False}), flush=True)
    for raw in sys.stdin:
        request_id = None
        try:
            try:
                # Accept the optional BOM emitted by redirected .NET Framework stdin.
                request = json.loads(raw.lstrip("\ufeff"))
            except (ValueError, TypeError):
                raise TranslationError("invalid_request") from None
            if not isinstance(request, dict):
                raise TranslationError("invalid_request")
            request_id = request.get("id")
            if request_id is None or not isinstance(request.get("text"), str):
                raise TranslationError("invalid_request")
            result = engine.translate(request["text"], request.get("direction", "en_zh"), request.get("glossary"))
            response = {"id": request_id, "translation": result, "provider": "argos-cpu", "offline": True}
        except TranslationError as error:
            response = {"id": request_id, "error": str(error), "offline": True}
        except Exception:
            response = {"id": request_id, "error": "translation_failed", "offline": True}
        print(json.dumps(response, ensure_ascii=False), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
