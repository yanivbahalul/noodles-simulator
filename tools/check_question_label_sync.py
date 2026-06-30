#!/usr/bin/env python3
"""ponytail: contract check for QuestionLabel.cs and html-utils formatQuestionLabel — run: python3 tools/check_question_label_sync.py"""
import re
import sys

MONTHS = {
    "jan": "01", "feb": "02", "mar": "03", "apr": "04", "may": "05", "jun": "06",
    "jul": "07", "aug": "08", "sep": "09", "oct": "10", "nov": "11", "dec": "12",
}

SCREENSHOT = re.compile(
    r"^Screenshot at (\w{3}) (\d{1,2}) (\d{2})-(\d{2})-(\d{2})$",
    re.IGNORECASE,
)


def format_question_label(question_id: str) -> str:
    if not question_id or not question_id.strip():
        return "—"
    name = question_id.replace("\\", "/").split("/")[-1]
    name = re.sub(r"\.(png|jpg|jpeg|webp)$", "", name, flags=re.IGNORECASE)
    match = SCREENSHOT.match(name)
    if match:
        mon = MONTHS.get(match.group(1).lower(), match.group(1))
        day = match.group(2).zfill(2)
        return f"{day}/{mon} {match.group(3)}:{match.group(4)}"
    if len(name) > 28:
        return name[:25] + "…"
    return name


CASES = [
    ("", "—"),
    ("foo/bar.png", "bar"),
    ("Screenshot at Jan 5 12-30-45.png", "05/01 12:30"),
    ("a" * 30 + ".png", "a" * 25 + "…"),
]


def main() -> int:
    failed = 0
    for raw, expected in CASES:
        got = format_question_label(raw)
        if got != expected:
            print(f"FAIL {raw!r}: got {got!r}, want {expected!r}")
            failed += 1
    if failed:
        print(f"{failed} case(s) failed — sync QuestionLabel.cs and html-utils.js to this script")
        return 1
    print("[ponytail] question label contract ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
