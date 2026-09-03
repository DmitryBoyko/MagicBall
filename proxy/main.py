from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from proxy.queue import InterpretQueue
from proxy.schemas import OracleIn, OracleOut
from proxy.service import OracleService
from proxy.settings import get_settings


def create_app(settings=None) -> FastAPI:
    settings = settings or get_settings()
    queue = InterpretQueue(settings.ai_queue_timeout, settings.ai_queue_max_depth)
    service = OracleService(settings, queue)

    @asynccontextmanager
    async def lifespan(_app: FastAPI):
        logging.basicConfig(level=logging.INFO)
        queue.start()
        yield
        await queue.stop()

    app = FastAPI(title="MagicalBall oracle proxy", lifespan=lifespan)
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_methods=["*"],
        allow_headers=["*"],
    )

    @app.get("/health")
    async def health() -> dict[str, str]:
        return {"status": "ok", "service": "magicalball"}

    @app.post("/api/v1/oracle", response_model=OracleOut)
    async def oracle(body: OracleIn) -> OracleOut:
        return await service.interpret(body)

    return app


app = create_app()
