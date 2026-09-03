from __future__ import annotations

import re

_MARKER_RE = re.compile(
    r"(?:\*{1,2}|_{1,2})?\[\[\s*итог\s*\]\](?:\*{1,2}|_{1,2})?",
    re.IGNORECASE,
)
_LEAD_PUNCT_RE = re.compile(r"^[\s:;—–\-]+")


def strip_markup(text: str) -> str:
    if not text:
        return ""
    cleaned = text.replace("\r\n", "\n").replace("\r", "\n")
    cleaned = re.sub(r"```(?:\w+)?\n?([\s\S]*?)```", r"\1", cleaned)
    cleaned = cleaned.replace("```", "")
    cleaned = re.sub(r"`+", "", cleaned)
    cleaned = re.sub(r"(?m)^\s{0,3}#{1,6}\s*", "", cleaned)
    cleaned = re.sub(r"\*{1,3}([^*]+)\*{1,3}", r"\1", cleaned)
    cleaned = re.sub(r"_{1,3}([^_]+)_{1,3}", r"\1", cleaned)
    cleaned = re.sub(r"(?m)^\s*(?:[-+•]|\d+[.)])\s+", "", cleaned)
    cleaned = re.sub(r" {2,}", " ", cleaned)
    return cleaned.strip()


def extract_summary(text: str) -> tuple[str, str]:
    if not text:
        return "", ""
    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    matches = list(_MARKER_RE.finditer(normalized))
    if not matches:
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
    return strip_markup(body), summary
