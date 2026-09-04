from __future__ import annotations

from proxy.fallback import SemanticCache, fingerprint, synthesize_result
from proxy.gigachat import AIUnavailableError, GigaChatClient
from proxy.queue import InterpretQueue
from proxy.schemas import OracleIn, OracleOut
from proxy.settings import Settings
from proxy.summary import extract_summary


class OracleService:
    def __init__(self, settings: Settings, queue: InterpretQueue) -> None:
        self._settings = settings
        self._queue = queue
        self._client = GigaChatClient(settings)
        self._cache = SemanticCache(settings.cache_path, settings.semantic_threshold)

    async def interpret(self, body: OracleIn) -> OracleOut:
        queued = await self._queue.submit(lambda skip: self._job(body, skip))
        result = queued.value
        if queued.overflow or queued.from_db_forced:
            result.fallback_used = True
            result.osiris_present = False
            result.fallback_reason = (
                "очередь переполнена"
                if queued.overflow
                else f"ожидание {queued.waited_seconds:.2f}с > AI_QUEUE_TIMEOUT"
            )
        return result

    async def _job(self, body: OracleIn, skip_ai: bool) -> OracleOut:
        _sanitize_visual_noise(body)
        key = fingerprint(body)
        if skip_ai:
            return self._fallback(body, key, "очередь превысила AI_QUEUE_TIMEOUT или переполнена")
        try:
            raw = await self._client.generate(body.model_dump_json())
            interpretation, summary = extract_summary(raw)
            self._cache.remember(key, raw)
            return OracleOut(
                interpretation=interpretation,
                summary=summary,
                osiris_present=True,
                source="gigachat",
                ai_model=self._client.last_model,
                fallback_used=False,
            )
        except AIUnavailableError as exc:
            return self._fallback(body, key, str(exc))

    def _fallback(self, body: OracleIn, key: str, reason: str) -> OracleOut:
        cached = self._cache.find(key)
        if cached is not None:
            cached.fallback_reason = f"{reason}; {cached.fallback_reason}"
            return cached
        return synthesize_result(body, reason)


def _sanitize_visual_noise(body: OracleIn) -> None:
    """Убираем сырьё палитры/имён красок — иначе модель лепит «малиновые тени»."""
    snap = body.dynamic_snapshot
    snap.photo_color_palette = ""
    snap.ball_tint_name = ""
    mod = (snap.ball_tint_modifier or "").strip()
    if "—" in mod:
        snap.ball_tint_modifier = mod.split("—")[-1].strip()
    elif " - " in mod:
        snap.ball_tint_modifier = mod.split(" - ")[-1].strip()

