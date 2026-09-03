from __future__ import annotations

import time
import uuid
from typing import Any

import httpx

from proxy.settings import Settings

_OAUTH = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth"
_CHAT_V1 = "https://api.giga.chat/v1/chat/completions"
_CHAT_V2 = "https://api.giga.chat/v2/chat/completions"

ORACLE_SYSTEM = (
    "Ты — беспристрастный, древний и мудрый дух Хрустального Шара. "
    "Твоя единственная задача — составить одно глубокое, психологическое и метафорическое "
    "предсказание-совет для пользователя на основе переданных энергетических и контекстных потоков.\n\n"
    "СТРОГИЕ ПРАВИЛА ГЕНЕРАЦИИ:\n"
    "1. ЯЗЫК: Пиши строго на русском языке.\n"
    "2. ДЛИНА: Итоговый текст должен быть строго от 240 до 290 символов с учетом пробелов. "
    "Превышение порога в 300 символов или генерация короткого ответа (менее 200 символов) "
    "является критической ошибкой.\n"
    "3. ФОРМАТ: Текст должен состоять строго из одной емкой, законченной мысли, разбитой "
    "максимум на 2-3 коротких предложения. Без приветствий, без вступлений, без подписей, "
    "без markdown, без списков и разделителей. Сразу выдавай текст гадания.\n"
    "4. ТАБУ: Запрещено давать ответы в стиле да/нет, называть календарные даты, "
    "обещать богатство или смерть. Тон серьёзный, кинематографичный, нуарный.\n"
    "5. ЛОГИКА СВЯЗЕЙ: Незаметно вплети погоду, заряд смартфона, палитру фото и слово-якорь "
    "в единую метафору.\n"
    "6. ФИНАЛ: После основного текста отдельной строкой напиши маркер [[ИТОГ]] и сразу за ним "
    "ключевую фразу обычными словами. Саму фразу не бери в квадратные скобки, кавычки, "
    "звёздочки и любую другую разметку. В тексте гадания и в фразе — только буквы, пробелы "
    "и обычные знаки препинания."
)


class AIUnavailableError(Exception):
    def __init__(self, reason: str) -> None:
        self.reason = reason
        super().__init__(reason)


class GigaChatClient:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._token: str | None = None
        self._token_expires = 0.0
        self.last_model: str | None = None

    def configured(self) -> bool:
        return bool(self._settings.gigachat_credentials.strip())

    async def generate(self, user_json: str) -> str:
        if not self.configured():
            raise AIUnavailableError("GIGACHAT_CREDENTIALS пуст")
        timeout = self._settings.ai_timeout_seconds
        verify = self._settings.gigachat_ssl_verify
        models = list(self._settings.gigachat_rotation)
        errors: list[str] = []
        messages = [
            {"role": "system", "content": ORACLE_SYSTEM},
            {"role": "user", "content": user_json},
        ]
        try:
            async with httpx.AsyncClient(timeout=timeout, verify=verify) as client:
                token = await self._access_token(client)
                headers = {
                    "Authorization": f"Bearer {token}",
                    "Accept": "application/json",
                    "Content-Type": "application/json",
                }
                for model in models:
                    try:
                        payload = await _chat(client, headers, model, messages)
                    except AIUnavailableError as exc:
                        errors.append(str(exc))
                        continue
                    text = _extract_content(payload)
                    if not text:
                        errors.append(f"{model}: empty chat")
                        continue
                    self.last_model = model
                    return text
        except httpx.HTTPError as exc:
            raise AIUnavailableError(f"network: {exc}") from exc
        raise AIUnavailableError("; ".join(errors) or "all rotated models failed")

    async def _access_token(self, client: httpx.AsyncClient) -> str:
        if self._token and time.time() < self._token_expires - 30:
            return self._token
        try:
            response = await client.post(
                _OAUTH,
                headers={
                    "Authorization": f"Basic {self._settings.gigachat_credentials.strip()}",
                    "RqUID": str(uuid.uuid4()),
                    "Content-Type": "application/x-www-form-urlencoded",
                    "Accept": "application/json",
                },
                data={"scope": self._settings.gigachat_scope},
            )
            response.raise_for_status()
            payload: dict[str, Any] = response.json()
        except httpx.HTTPError as exc:
            raise AIUnavailableError(f"oauth failed: {exc}") from exc
        token = payload.get("access_token")
        if not token:
            raise AIUnavailableError("oauth returned no access_token")
        self._token = str(token)
        expires = payload.get("expires_at")
        if expires:
            value = float(expires)
            self._token_expires = value / 1000 if value > 1e12 else value
        else:
            self._token_expires = time.time() + 25 * 60
        return self._token


async def _chat(
    client: httpx.AsyncClient,
    headers: dict[str, str],
    model: str,
    messages: list[dict[str, str]],
) -> dict[str, Any]:
    body = {
        "model": model,
        "messages": messages,
        "stream": False,
        "repetition_penalty": 1,
        "max_tokens": 400,
    }
    response = await client.post(_CHAT_V1, headers=headers, json=body)
    if response.status_code == 404:
        response = await client.post(_CHAT_V2, headers=headers, json=body)
    if response.status_code in {400, 401, 402, 403, 404, 429, 500, 502, 503, 504}:
        raise AIUnavailableError(f"{model} http {response.status_code}: {response.text[:200]}")
    response.raise_for_status()
    payload = response.json()
    if not isinstance(payload, dict):
        raise AIUnavailableError(f"{model}: unexpected response")
    return payload


def _extract_content(payload: dict[str, Any]) -> str:
    try:
        return str(payload["choices"][0]["message"]["content"]).strip()
    except (KeyError, IndexError, TypeError):
        return ""
