from __future__ import annotations

from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

PROXY_DIR = Path(__file__).resolve().parent
ROOT_DIR = PROXY_DIR.parent


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=(ROOT_DIR / ".env", PROXY_DIR / ".env"),
        env_file_encoding="utf-8",
        extra="ignore",
        populate_by_name=True,
    )

    host: str = Field(default="0.0.0.0", alias="PROXY_HOST")
    port: int = Field(default=17879, alias="API_PORT")
    ai_queue_timeout: float = Field(default=5.0, alias="AI_QUEUE_TIMEOUT")
    ai_queue_max_depth: int = Field(default=2, alias="AI_QUEUE_MAX_DEPTH")
    ai_timeout_seconds: float = Field(default=20.0, alias="AI_TIMEOUT_SECONDS")
    semantic_threshold: float = Field(default=0.82, alias="SEMANTIC_THRESHOLD")
    cache_path: Path = Field(
        default=PROXY_DIR / "data" / "semantic_cache.json",
        alias="CACHE_PATH",
    )

    gigachat_credentials: str = Field(default="", alias="GIGACHAT_CREDENTIALS")
    gigachat_scope: str = Field(default="GIGACHAT_API_PERS", alias="GIGACHAT_SCOPE")
    gigachat_model: str = Field(default="GigaChat-2", alias="GIGACHAT_MODEL")
    gigachat_models: str = Field(
        default="GigaChat-2,GigaChat-2-Pro,GigaChat-2-Max,GigaChat-3-Ultra",
        alias="GIGACHAT_MODELS",
    )
    gigachat_verify_ssl: bool = Field(default=True, alias="GIGACHAT_VERIFY_SSL")
    gigachat_ca_bundle: str = Field(
        default=str(PROXY_DIR / "ce" / "russian_trusted_root_ca_pem.crt"),
        alias="GIGACHAT_CA_BUNDLE",
    )

    @property
    def gigachat_rotation(self) -> list[str]:
        models = [part.strip() for part in self.gigachat_models.split(",") if part.strip()]
        return models or [self.gigachat_model]

    @property
    def gigachat_ssl_verify(self) -> bool | str:
        raw = self.gigachat_ca_bundle.strip()
        if raw:
            path = Path(raw)
            if not path.is_absolute():
                path = PROXY_DIR / path
            if path.is_file():
                return str(path)
        return self.gigachat_verify_ssl


def get_settings() -> Settings:
    return Settings()
