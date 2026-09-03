from fastapi.testclient import TestClient

from proxy.main import create_app
from proxy.settings import Settings


def test_health() -> None:
    with TestClient(create_app(Settings(gigachat_credentials=""))) as client:
        response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"


def test_oracle_fallback_without_credentials() -> None:
    with TestClient(create_app(Settings(gigachat_credentials=""))) as client:
        response = client.post(
            "/api/v1/oracle",
            json={
                "deterministic_profile": {"user_name": "Дмитрий", "zodiac_sign": "Лев"},
                "dynamic_snapshot": {"entropy_word_anchor": "Забытый Ключ", "time_of_day": "Ночь"},
            },
        )
    assert response.status_code == 200
    payload = response.json()
    assert payload["osiris_present"] is False
    assert payload["fallback_used"] is True
    assert payload["interpretation"]
    assert payload["summary"]
