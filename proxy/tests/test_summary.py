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


def test_strip_markup_drops_leaked_itog_word() -> None:
    cleaned = strip_markup("Держи курс.\nИТОГ\nШаг за шагом")
    assert "ИТОГ" not in cleaned.upper()
    assert "Шаг за шагом" in cleaned


def test_extract_summary_plain_itog_line() -> None:
    body, summary = extract_summary("Держи курс мягко.\nИТОГ\nШаг за шагом")
    assert "ИТОГ" not in body.upper()
    assert summary == "Шаг за шагом"


def test_extract_summary_itog_same_line_no_colon() -> None:
    body, summary = extract_summary("Держи курс мягко.\nИТОГ Шаг за шагом")
    assert "ИТОГ" not in body.upper()
    assert "ИТОГ" not in summary.upper()
    assert summary == "Шаг за шагом"


def test_strip_markup_drops_itog_token_everywhere() -> None:
    cleaned = strip_markup("Слушай.\nИТОГ: Верни себе право\nИ ещё ИТОГ в конце")
    assert "ИТОГ" not in cleaned.upper()
    assert "Верни себе право" in cleaned


def test_strip_keeps_itoge_word() -> None:
    cleaned = strip_markup("В итоге ты уже готов.")
    assert "итоге" in cleaned.lower()

