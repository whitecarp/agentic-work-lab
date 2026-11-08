"""Google Translate web transport. No keys, accounts, local fallback or text logs."""
from __future__ import annotations
import collections
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from glossary import validated_entries, replacement_spans

ENDPOINT = "https://translate.googleapis.com/translate_a/single"
MAX_TEXT, MAX_CHUNK = 32000, 2600
# Keep request language parameters bounded. The UI sends one of these exact target codes;
# automatic source detection happens at Google, rather than guessing from Latin/CJK letters.
TARGET_LANGUAGES = frozenset((
    "zh-CN", "zh-TW", "en", "ja", "ko", "fr", "de", "es", "it", "pt", "ru",
    "ar", "hi", "th", "vi", "id", "ms",
))

# Text that must reach the reader unchanged: links and inline code. A link stops at whitespace,
# quotes, CJK text and full-width punctuation, so a link inside Chinese prose cannot swallow the
# rest of the sentence (Chinese has no spaces to end it).
PROTECTED = re.compile(
    r"(?:https?://|www\.)[^\s<>\"'`　-〿㐀-鿿豈-﫿＀-￯]+"
    r"|`[^`\n]+`", re.I)
# While a paragraph is translated, each protected span is replaced by a code-like token so the
# sentence stays in one piece. Google keeps such tokens far more reliably than brackets or symbols;
# the pattern also accepts lower case and a space before the number. Any mismatch falls back to
# translating the pieces between protected spans separately, which never loses protected text.
TOKEN_PREFIX = "ZXQ"
TOKEN_PATTERN = re.compile(r"(?<![A-Za-z0-9])ZXQ ?(\d{1,3})(?![0-9])", re.I)
MAX_TOKENS = 400


class TranslationError(Exception):
    pass


def language_pair(direction: str) -> tuple[str, str]:
    """Return validated Google sl/tl values; retain old compose-window directions."""
    if direction == "en_zh":
        return "en", "zh-CN"
    if direction == "zh_en":
        return "zh-CN", "en"
    if isinstance(direction, str) and direction.startswith("auto:"):
        target = direction[5:]
        if target in TARGET_LANGUAGES:
            return "auto", target
    raise TranslationError("invalid_direction")


class GoogleOnlyRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        parsed = urllib.parse.urlsplit(newurl)
        host = (parsed.hostname or "").lower()
        if parsed.scheme == "https" and host == "translate.googleapis.com":
            return super().redirect_request(req, fp, code, msg, headers, newurl)
        if (host == "google.com" or host.endswith(".google.com")) and parsed.path.startswith("/sorry"):
            # Google's "unusual traffic" check (a CAPTCHA page). Common on shared VPN/proxy exits.
            raise TranslationError("google_blocked")
        raise TranslationError("unexpected_redirect")


def call_google(text: str, direction: str) -> str:
    source, target = language_pair(direction)
    query = urllib.parse.urlencode({"client": "gtx", "sl": source, "tl": target, "dt": "t"})
    request = urllib.request.Request(ENDPOINT + "?" + query,
        data=urllib.parse.urlencode({"q": text}).encode("utf-8"),
        headers={"Content-Type": "application/x-www-form-urlencoded;charset=UTF-8", "User-Agent": "Mozilla/5.0"})
    try:
        with urllib.request.build_opener(GoogleOnlyRedirect()).open(request, timeout=15) as response:
            if response.status != 200:
                raise TranslationError("service_unavailable")
            raw = response.read(1000000)
        data = json.loads(raw)
        if not isinstance(data, list) or not data or not isinstance(data[0], list):
            raise TranslationError("invalid_response")
        pieces = [item[0] for item in data[0] if isinstance(item, list) and item and isinstance(item[0], str)]
        if not pieces:
            raise TranslationError("invalid_response")
        # Remove artifacts only from Google's returned translation. Source text and
        # protected code/URLs are left untouched and restored by the caller later.
        return "".join(pieces).replace("\u200b", "").replace("\ufeff", "")
    except urllib.error.HTTPError as exc:
        if exc.code == 429:
            raise TranslationError("rate_limited") from None
        if exc.code == 407:
            raise TranslationError("proxy_authentication") from None
        if exc.code in (401, 403):
            raise TranslationError("access_denied") from None
        raise TranslationError("service_unavailable") from None
    except urllib.error.URLError as exc:
        # Proxy CONNECT failures can be wrapped in URLError rather than raised as HTTPError.
        # Only classify an explicit 407; generic refusals can also mean direct-network failure.
        if re.search(r"\b407\b", str(exc.reason)):
            raise TranslationError("proxy_authentication") from None
        raise TranslationError("network_error") from None
    except (TimeoutError, OSError):
        raise TranslationError("network_error") from None
    except (ValueError, TypeError, IndexError):
        raise TranslationError("invalid_response") from None


def bounded_chunks(text: str):
    while len(text) > MAX_CHUNK:
        # Prefer sentence ends (Chinese ones need no following space), then line breaks and spaces.
        boundaries = list(re.finditer(r"[。！？；]+|[.!?;]\s+|\n|\s+", text[:MAX_CHUNK]))
        end = boundaries[-1].end() if boundaries and boundaries[-1].end() > MAX_CHUNK // 2 else MAX_CHUNK
        yield text[:end]
        text = text[end:]
    if text:
        yield text


def needs_translation(text: str, direction: str) -> bool:
    source, _ = language_pair(direction)
    if not text.strip():
        return False
    if source == "auto":
        # Unicode letters cover Japanese kana, Korean, Cyrillic, Arabic, Thai, etc.
        # Do not apply the legacy bilingual filter to an automatically detected source.
        return any(character.isalpha() for character in text)
    if direction == "en_zh":
        return re.search(r"[a-zA-Z]", text) is not None
    return re.search(r"[㐀-鿿]", text) is not None


def protected_spans(block: str):
    spans = []
    for match in PROTECTED.finditer(block):
        start, end = match.span()
        value = match.group(0)
        if not value.startswith("`"):
            # Sentence punctuation after a link belongs to the sentence. Keep ")" only when the
            # link itself opened one, as in Wikipedia-style addresses.
            while value and (value[-1] in ".,;:!?" or (value[-1] == ")" and value.count(")") > value.count("("))):
                value, end = value[:-1], end - 1
            if value.lower() in ("http://", "https://", "www."):
                continue
        spans.append((start, end))
    return spans


def restore_tokens(text: str, values):
    """Put protected spans back. None unless every token comes back exactly once."""
    found = sorted(int(match.group(1)) for match in TOKEN_PATTERN.finditer(text))
    if found != list(range(len(values))):
        return None
    return TOKEN_PATTERN.sub(lambda match: values[int(match.group(1))], text)


class GoogleTranslator:
    def __init__(self, transport=call_google, minimum_interval=0.4):
        self.transport, self.minimum_interval = transport, minimum_interval
        self.last_call = 0.0
        self.cache, self.cache_chars = collections.OrderedDict(), 0
        self.fallbacks = 0  # paragraphs whose placeholders Google did not return intact

    def prose(self, text: str, direction: str, deadline: float) -> str:
        if not needs_translation(text, direction):
            return text
        pieces = []
        for chunk in bounded_chunks(text):
            before, body, after = re.match(r"^(\s*)(.*?)(\s*)$", chunk, re.S).groups()
            if not body:
                pieces.append(chunk)
                continue
            key = (direction, body)
            if key in self.cache:
                self.cache.move_to_end(key)
                value = self.cache[key]
            else:
                if time.monotonic() >= deadline:
                    raise TranslationError("deadline")
                delay = self.minimum_interval - (time.monotonic() - self.last_call)
                if delay > 0:
                    time.sleep(delay)
                if time.monotonic() >= deadline:
                    raise TranslationError("deadline")
                self.last_call = time.monotonic()
                value = self.transport(body, direction)
                if time.monotonic() >= deadline:
                    raise TranslationError("deadline")
                if not isinstance(value, str) or not value.strip():
                    raise TranslationError("invalid_response")
                self.cache[key] = value
                self.cache_chars += len(body) + len(value)
                while len(self.cache) > 200 or self.cache_chars > 600000:
                    old, old_value = self.cache.popitem(last=False)
                    self.cache_chars -= len(old[1]) + len(old_value)
            pieces.append(before + value + after)
        return "".join(pieces)

    def fragments(self, block: str, spans, direction: str, deadline: float) -> str:
        output, cursor = [], 0
        for start, end, replacement in spans:
            output.append(self.prose(block[cursor:start], direction, deadline))
            output.append(replacement)
            cursor = end
        output.append(self.prose(block[cursor:], direction, deadline))
        return "".join(output)

    def paragraph(self, block: str, direction: str, deadline: float, glossary=()) -> str:
        spans = replacement_spans(block, glossary, language_pair(direction)[1], protected_spans(block))
        if not spans:
            return self.prose(block, direction, deadline)
        if len(spans) <= MAX_TOKENS and TOKEN_PATTERN.search(block) is None:
            masked, values, cursor = [], [], 0
            for index, (start, end, replacement) in enumerate(spans):
                masked.append(block[cursor:start])
                masked.append(TOKEN_PREFIX + str(index))
                values.append(replacement)
                cursor = end
            masked.append(block[cursor:])
            masked_text = "".join(masked)
            if not needs_translation(TOKEN_PATTERN.sub(" ", masked_text), direction):
                return self.fragments(block, spans, direction, deadline)
            # Transport errors propagate: a failed request is never retried as fragments.
            restored = restore_tokens(self.prose(masked_text, direction, deadline), values)
            if restored is not None:
                return restored
            self.fallbacks += 1
        return self.fragments(block, spans, direction, deadline)

    def translate(self, text: str, direction: str, glossary=None) -> str:
        language_pair(direction)
        try:
            entries = validated_entries(glossary)
        except ValueError:
            raise TranslationError("invalid_glossary") from None
        if len(text) > MAX_TEXT:
            raise TranslationError("text_too_long")
        deadline = time.monotonic() + 120
        output, pending = [], []
        fence = None
        def flush():
            if not pending:
                return
            block = "".join(pending)
            pending.clear()
            output.append(self.paragraph(block, direction, deadline, entries))
        for line in text.splitlines(keepends=True):
            fm = re.match(r"(`{3,}|~{3,})", line.lstrip())
            if fence:
                flush()
                output.append(line)
                closing = re.fullmatch(r" {0,3}([`~]+)[ \t]*(?:\r?\n)?", line)
                if (closing and set(closing.group(1)) == {fence[0]}
                        and len(closing.group(1)) >= len(fence)):
                    fence = None
            elif fm:
                flush()
                fence = fm.group(1)
                output.append(line)
            elif line.startswith(("    ", "\t")) or not line.strip():
                flush()
                output.append(line)
            else:
                pending.append(line)
        flush()
        return "".join(output)


def main():
    for stream in (sys.stdin, sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="strict")
    engine = GoogleTranslator()
    # This handshake only confirms that the local worker started. Google connectivity is
    # checked separately on demand, so do not report the service as online here.
    print(json.dumps({"ready": True}), flush=True)
    for raw in sys.stdin:
        request_id = None
        try:
            # .NET Framework's redirected stdin writer can emit a UTF-8 BOM on
            # UTF-8 Windows consoles, before the application's raw JSON bytes.
            request = json.loads(raw.lstrip("\ufeff"))
            if not isinstance(request, dict):
                raise TranslationError("invalid_request")
            request_id = request.get("id")
            text = request.get("text")
            if not isinstance(text, str) or request_id is None:
                raise TranslationError("invalid_request")
            result = engine.translate(text, request.get("direction", "en_zh"), request.get("glossary"))
            response = {"id": request_id, "translation": result, "provider": "google-web"}
        except TranslationError as exc:
            response = {"id": request_id, "error": str(exc)}
        except Exception:
            response = {"id": request_id, "error": "translation_failed"}
        print(json.dumps(response, ensure_ascii=False), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
