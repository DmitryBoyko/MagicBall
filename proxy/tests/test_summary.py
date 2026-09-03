from proxy.summary import extract_summary


def test_extract_summary_strips_marker() -> None:
    body, summary = extract_summary("Держи курс мягко.\n[[ИТОГ]] Шаг за шагом")
    assert "ИТОГ" not in body
    assert summary == "Шаг за шагом"


def test_extract_summary_missing_marker() -> None:
    body, summary = extract_summary("Только текст без маркера.")
    assert body.startswith("Только текст")
    assert summary == ""
