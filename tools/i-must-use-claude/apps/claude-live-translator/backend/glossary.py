"""Bounded, literal user glossary. No file access, network, or text logging.

Terms ignore case unless an entry sets "matchCase": true. Names that are also
ordinary words (Skills, Sonnet, React...) use matchCase so everyday text still
translates normally.
"""
import re

MAX_ENTRIES = 200


def normalize_target(code):
    return {"zh-CN": "zh", "zh-TW": "zt"}.get(code, code)


def case_key(source, match_case):
    # Mirrors GlossaryStore.Validate: ignore-case terms collide when they differ only in case.
    return (source, True) if match_case else (source.lower(), False)


def validated_entries(raw):
    if raw is None:
        return []
    if not isinstance(raw, list) or len(raw) > MAX_ENTRIES:
        raise ValueError("invalid_glossary")
    result, seen = [], set()
    for entry in raw:
        if not isinstance(entry, dict):
            raise ValueError("invalid_glossary")
        source, replacement, target = (entry.get(k) for k in ("source", "replacement", "target"))
        match_case = entry.get("matchCase", False)
        if (not isinstance(source, str) or not isinstance(replacement, str) or
                not isinstance(target, str) or not isinstance(match_case, bool) or
                not source.strip() or not replacement.strip() or
                len(source) > 64 or len(replacement) > 128 or
                any(ord(c) < 32 for c in source + replacement) or
                not re.fullmatch(r"\*|[a-z]{2,3}(?:-[A-Z]{2})?", target)):
            raise ValueError("invalid_glossary")
        source, replacement, target = source.strip(), replacement.strip(), normalize_target(target)
        key = case_key(source, match_case) + (target,)
        if key in seen:
            raise ValueError("invalid_glossary")
        seen.add(key)
        result.append((source, replacement, target, match_case))
    return result


def replacement_spans(text, entries, target, protected):
    """Protected code/URL wins. Longest literal wins; at equal length an exact-case
    term beats an ignore-case one; target-specific wins over '*'."""
    target = normalize_target(target)
    protected = list(protected)
    candidates = {}
    for wanted in ("*", target):
        for entry in entries:
            source, replacement, language = entry[:3]
            match_case = entry[3] if len(entry) > 3 else False
            if language == wanted:
                candidates[case_key(source, match_case)] = (source, replacement, match_case)
    spans = [(a, b, text[a:b]) for a, b in protected]
    if not candidates:
        return spans
    ordered = sorted(candidates.values(), key=lambda c: (-len(c[0]), not c[2], c[0]))
    patterns = []
    for index, (source, _, match_case) in enumerate(ordered):
        # Latin identifiers must not match inside another identifier. Han phrases may be adjacent.
        left = r"(?<![A-Za-z0-9_])" if re.match(r"[A-Za-z0-9_]", source) else ""
        right = r"(?![A-Za-z0-9_])" if re.search(r"[A-Za-z0-9_]$", source) else ""
        body = re.escape(source) if match_case else "(?i:" + re.escape(source) + ")"
        # A named group per term tells us exactly which entry matched, whatever its case.
        patterns.append("(?P<t%d>%s%s%s)" % (index, left, body, right))
    pattern = re.compile("|".join(patterns))
    cursor = 0
    for start, end in protected + [(len(text), len(text))]:
        for match in pattern.finditer(text, cursor, start):
            spans.append((match.start(), match.end(), ordered[int(match.lastgroup[1:])][1]))
        cursor = end
    return sorted(spans)
