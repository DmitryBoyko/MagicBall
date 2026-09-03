from proxy.meaning_bank import RECORDS, synthesize
from proxy.schemas import DeterministicProfile, DynamicSnapshot, OracleIn
from proxy.fallback import fingerprint, synthesize_result


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


def test_fingerprint_includes_inquiry_pulse() -> None:
    calm = OracleIn(
        dynamic_snapshot=DynamicSnapshot(inquiry_pulse_aura="Ровный / Созерцательный")
    )
    tense = OracleIn(
        dynamic_snapshot=DynamicSnapshot(inquiry_pulse_aura="Тревожный / Частый зов")
    )
    assert fingerprint(calm) != fingerprint(tense)
    dumped = tense.model_dump()["dynamic_snapshot"]
    assert dumped["inquiry_pulse_aura"].startswith("Тревожный")
