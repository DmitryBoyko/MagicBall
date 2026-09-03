from proxy.summary import extract_summary, strip_markup


def test_extract_summary_strips_marker() -> None:
    body, summary = extract_summary("Держи курс мягко.\n[[ИТОГ]] Шаг за шагом")
    assert "ИТОГ" not in body
    assert summary == "Шаг за шагом"


def test_extract_summary_missing_marker() -> None:
    body, summary = extract_summary("Только текст без маркера.")
    assert body.startswith("Только текст")
    assert summary == ""


def test_extract_summary_unwraps_gold_brackets() -> None:
    body, summary = extract_summary(
        "Тень прошлого больше не кормит тебя.\n[[ИТОГ]] [[Освободись от прошлого]]"
    )
    assert "[[" not in body
    assert "]]" not in body
    assert "[" not in summary
    assert "]" not in summary
    assert summary == "Освободись от прошлого"


def test_extract_summary_trailing_brackets_without_marker() -> None:
    body, summary = extract_summary("Шар видит развилку. [[Освободись от прошлого]]")
    assert "[[" not in body + summary
    assert summary == "Освободись от прошлого"
    assert "развилку" in body


def test_strip_markup_drops_markdown_and_brackets() -> None:
    cleaned = strip_markup("**Совет:** [[Иди дальше]] и *не* оглядывайся")
    assert "[" not in cleaned
    assert "*" not in cleaned
    assert "Иди дальше" in cleaned
    assert "не" in cleaned
    assert "оглядывайся" in cleaned
