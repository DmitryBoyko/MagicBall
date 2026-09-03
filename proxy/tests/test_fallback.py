from proxy.meaning_bank import RECORDS, synthesize
from proxy.schemas import DeterministicProfile, OracleIn
from proxy.fallback import synthesize_result


def test_meaning_bank_has_156() -> None:
    assert len(RECORDS) == 156


def test_synthesize_has_marker() -> None:
    raw = synthesize("Дмитрий")
    assert "[[ИТОГ]]" in raw


def test_synthesize_result_flags() -> None:
    body = OracleIn(deterministic_profile=DeterministicProfile(user_name="Анна"))
    result = synthesize_result(body, "silent")
    assert result.osiris_present is False
    assert result.fallback_used is True
    assert result.source == "synthesized"
    assert result.summary
