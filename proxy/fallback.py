from __future__ import annotations

import json
import logging
import math
from pathlib import Path

from proxy.meaning_bank import synthesize
from proxy.schemas import OracleIn, OracleOut
from proxy.summary import extract_summary

DIM = 64


def fingerprint(body: OracleIn) -> str:
    profile = body.deterministic_profile
    snap = body.dynamic_snapshot
    return "|".join(
        [
            profile.user_name,
            profile.zodiac_sign,
            str(profile.destiny_number),
            snap.entropy_word_anchor,
            snap.photo_mystic_tag,
            snap.time_of_day,
            snap.ball_mood_modifier,
            snap.ball_tint_modifier,
        ]
    )


def embed(text: str) -> list[float]:
    vector = [0.0] * DIM
    norm = text.strip().lower()
    if len(norm) < 3:
        return vector
    for i in range(len(norm) - 2):
        gram = (ord(norm[i]) * 73856093) ^ (ord(norm[i + 1]) * 19349663) ^ (ord(norm[i + 2]) * 83492791)
        vector[abs(gram) % DIM] += 1.0
    length = math.sqrt(sum(item * item for item in vector))
    if length <= 0:
        return vector
    return [item / length for item in vector]


def cosine(left: list[float], right: list[float]) -> float:
    if not left or not right or len(left) != len(right):
        return 0.0
    return sum(a * b for a, b in zip(left, right))


class SemanticCache:
    def __init__(self, path: Path, threshold: float) -> None:
        self._path = path
        self._threshold = threshold
        self._entries: list[dict] = []
        self.load()

    def load(self) -> None:
        if not self._path.is_file():
            self._entries = []
            return
        try:
            self._entries = json.loads(self._path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            self._entries = []

    def save(self) -> None:
        try:
            self._path.parent.mkdir(parents=True, exist_ok=True)
            self._path.write_text(json.dumps(self._entries, ensure_ascii=False), encoding="utf-8")
        except OSError:
            logging.getLogger(__name__).warning("semantic cache not writable: %s", self._path)

    def find(self, question: str) -> OracleOut | None:
        if not question or not self._entries:
            return None
        query = embed(question)
        best: dict | None = None
        score = 0.0
        for entry in self._entries:
            similarity = cosine(query, entry.get("embedding") or [])
            if similarity > score:
                score = similarity
                best = entry
        if best is None or score < self._threshold:
            return None
        body, summary = extract_summary(str(best.get("interpretation") or ""))
        return OracleOut(
            interpretation=body,
            summary=summary,
            osiris_present=False,
            source="semantic",
            fallback_used=True,
            fallback_reason=f"semantic hit {score:.3f} ~ {best.get('question')}",
            similarity=score,
        )

    def remember(self, question: str, raw: str) -> None:
        if not question or not raw:
            return
        vector = embed(question)
        for entry in self._entries:
            if entry.get("question") == question:
                entry["interpretation"] = raw
                entry["embedding"] = vector
                self.save()
                return
        self._entries.append({"question": question, "interpretation": raw, "embedding": vector})
        self.save()


def synthesize_result(body: OracleIn, reason: str) -> OracleOut:
    raw = synthesize(body.deterministic_profile.user_name)
    interpretation, summary = extract_summary(raw)
    return OracleOut(
        interpretation=interpretation,
        summary=summary,
        osiris_present=False,
        source="synthesized",
        fallback_used=True,
        fallback_reason=reason,
    )
