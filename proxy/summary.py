from __future__ import annotations

import re

# [[ИТОГ]], markdown-обёртки, строка только «ИТОГ»
_MARKER_RE = re.compile(
    r"(?im)(?:\*{1,2}|_{1,2}|\[)?\s*\[\[\s*итог\s*\]\]\s*(?:\*{1,2}|_{1,2}|\])?"
    r"|^\s*(?:\*{1,2}|_{1,2})?\s*итог\s*(?:\*{1,2}|_{1,2})?\s*$",
)
# «ИТОГ фраза» / «ИТОГ: фраза» в начале строки
_PLAIN_ITOG_HEADER_RE = re.compile(
    r"(?mi)^(\s*)(?:\*{1,2}|_{1,2}|\[)?\s*итог\s*(?:\*{1,2}|_{1,2}|\])?\s*[:.—–\-]?\s+",
)
# Отдельное слово «итог» (не «в итоге» / «итогов»)
_ITOG_TOKEN_RE = re.compile(r"(?i)(?<![\wа-яёА-ЯЁ])итог(?![\wа-яёА-ЯЁ])")
_LEAD_PUNCT_RE = re.compile(r"^[\s:;—–\-]+")
_TRAIL_BRACKETS_RE = re.compile(r"\[\[\s*([^\[\]]+?)\s*\]\]\s*$")
_DOUBLE_BRACKETS_RE = re.compile(r"\[\[\s*(.*?)\s*\]\]", re.DOTALL)
_SINGLE_BRACKETS_RE = re.compile(r"\[([^\[\]\n]{1,80})\]")
_VISIBLE_RE = re.compile(
    r"[^\w\s.,!?:;…\-—–()«»\"'“”‘’‚„]",
    re.UNICODE,
)


def _normalize_plain_itog_headers(text: str) -> str:
    cleaned = _MARKER_RE.sub("[[ИТОГ]]", text)
    cleaned = _PLAIN_ITOG_HEADER_RE.sub(r"\1[[ИТОГ]] ", cleaned)
    return cleaned


def _strip_itog_label(text: str) -> str:
    cleaned = _MARKER_RE.sub("", text)
    cleaned = _PLAIN_ITOG_HEADER_RE.sub(r"\1", cleaned)
    cleaned = _ITOG_TOKEN_RE.sub(" ", cleaned)
    cleaned = re.sub(r" {2,}", " ", cleaned)
    return cleaned


def strip_markup(text: str) -> str:
    if not text:
        return ""
    cleaned = text.replace("\r\n", "\n").replace("\r", "\n")
    cleaned = _normalize_plain_itog_headers(cleaned)
    cleaned = _MARKER_RE.sub("", cleaned)
    cleaned = re.sub(r"```(?:\w+)?\n?([\s\S]*?)```", r"\1", cleaned)
    cleaned = cleaned.replace("```", "")
    cleaned = re.sub(r"`+", "", cleaned)
    cleaned = re.sub(r"(?m)^\s{0,3}#{1,6}\s*", "", cleaned)
    cleaned = re.sub(r"(?m)^\s{0,3}>\s?", "", cleaned)
    cleaned = re.sub(r"!\[([^\]]*)\]\([^)]+\)", r"\1", cleaned)
    cleaned = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", cleaned)
    cleaned = _unwrap_brackets(cleaned)
    cleaned = re.sub(r"\*{1,3}([^*]+)\*{1,3}", r"\1", cleaned)
    cleaned = re.sub(r"_{1,3}([^_]+)_{1,3}", r"\1", cleaned)
    cleaned = re.sub(r"[*_`#~|\\]+", "", cleaned)
    cleaned = re.sub(r"(?m)^\s*(?:[-+•]|\d+[.)])\s+", "", cleaned)
    cleaned = _VISIBLE_RE.sub("", cleaned)
    cleaned = cleaned.replace("_", "")
    cleaned = _strip_itog_label(cleaned)
    cleaned = re.sub(r"[ \t]+\n", "\n", cleaned)
    cleaned = re.sub(r"\n{3,}", "\n\n", cleaned)
    cleaned = re.sub(r" {2,}", " ", cleaned)
    return cleaned.strip()


def _is_itog_only(text: str) -> bool:
    t = text.strip().strip("«»\"':.—–-")
    return t.casefold() == "итог"


def extract_summary(text: str) -> tuple[str, str]:
    if not text:
        return "", ""
    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    normalized = _normalize_plain_itog_headers(normalized)
    matches = list(_MARKER_RE.finditer(normalized))
    if not matches:
        stripped = normalized.strip()
        trail = _TRAIL_BRACKETS_RE.search(stripped)
        if trail:
            inner = trail.group(1).strip()
            if 0 < len(inner) <= 80 and not _is_itog_only(inner):
                return strip_markup(stripped[: trail.start()]), strip_markup(inner)
        return strip_markup(normalized), ""

    match = matches[-1]
    before = normalized[: match.start()]
    after = _LEAD_PUNCT_RE.sub("", normalized[match.end() :])
    summary = ""
    leftover = after
    lines = after.split("\n")
    for index, line in enumerate(lines):
        piece = line.strip().strip("«»\"'")
        if piece:
            summary = _MARKER_RE.sub("", piece).strip().strip("«»\"':")
            leftover = "\n".join(lines[index + 1 :])
            break
        leftover = "\n".join(lines[index + 1 :])
    leftover = leftover.strip()
    body = before.strip()
    if leftover:
        body = f"{body}\n{leftover}".strip() if body else leftover
    body = _MARKER_RE.sub("", body)
    body = re.sub(r"[ \t]+\n", "\n", body)
    body = re.sub(r"\n{3,}", "\n\n", body).strip()
    summary = strip_markup(summary)
    if len(summary) > 300:
        summary = summary[:300].strip()
    if _is_itog_only(summary):
        summary = ""
    return strip_markup(body), summary


def _unwrap_brackets(text: str) -> str:
    prev = None
    cleaned = text
    while prev != cleaned:
        prev = cleaned
        cleaned = _DOUBLE_BRACKETS_RE.sub(r"\1", cleaned)
    return _SINGLE_BRACKETS_RE.sub(r"\1", cleaned)
