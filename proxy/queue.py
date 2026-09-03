from __future__ import annotations

import asyncio
import logging
import time
from collections.abc import Awaitable, Callable
from dataclasses import dataclass
from typing import TypeVar

log = logging.getLogger("oracle.queue")

T = TypeVar("T")


@dataclass(slots=True)
class QueueResult:
    value: T
    waited_seconds: float
    from_db_forced: bool
    overflow: bool = False


class InterpretQueue:
    """FIFO. Wait > timeout or depth overflow → skip AI."""

    def __init__(self, timeout_seconds: float, max_depth: int = 2) -> None:
        self._timeout = timeout_seconds
        self._max_depth = max_depth
        self._queue: asyncio.Queue[tuple[float, Callable[[bool], Awaitable[T]], asyncio.Future]] = (
            asyncio.Queue()
        )
        self._task: asyncio.Task[None] | None = None

    def start(self) -> None:
        if self._task is None:
            self._task = asyncio.create_task(self._worker(), name="oracle-queue")

    async def stop(self) -> None:
        if self._task is not None:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
            self._task = None

    async def submit(self, job: Callable[[bool], Awaitable[T]]) -> QueueResult[T]:
        self.start()
        if self._queue.qsize() >= self._max_depth:
            value = await job(True)
            return QueueResult(value=value, waited_seconds=0.0, from_db_forced=True, overflow=True)

        loop = asyncio.get_running_loop()
        future: asyncio.Future[QueueResult[T]] = loop.create_future()
        await self._queue.put((time.monotonic(), job, future))
        return await future

    async def _worker(self) -> None:
        while True:
            enqueued_at, job, future = await self._queue.get()
            waited = time.monotonic() - enqueued_at
            from_db = waited > self._timeout
            try:
                value = await job(from_db)
                if not future.cancelled():
                    future.set_result(
                        QueueResult(value=value, waited_seconds=waited, from_db_forced=from_db)
                    )
            except Exception as exc:
                log.exception("oracle job failed")
                if not future.cancelled():
                    future.set_exception(exc)
            finally:
                self._queue.task_done()
