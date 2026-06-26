#!/usr/bin/env python3
"""End-to-end exam mode flow test against local server."""
import json
import os
import re
import sys
import urllib.parse
import urllib.request
from http.cookiejar import CookieJar

BASE = os.environ.get("E2E_BASE", "http://localhost:5001")
USERNAME = os.environ.get("E2E_USER", "e2etestuser99")
PASSWORD = os.environ.get("E2E_PASS", "testpass99")
TOTAL_QUESTIONS = 17


def sb_get(path):
    url = os.environ["SUPABASE_URL"]
    key = os.environ["SERVICE_ROLE_SECRET"]
    req = urllib.request.Request(
        f"{url}{path}",
        headers={"apikey": key, "Authorization": f"Bearer {key}"},
    )
    with urllib.request.urlopen(req) as r:
        return json.load(r)


class Client:
    def __init__(self):
        self.jar = CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))

    def request(self, method, path, data=None, headers=None):
        url = BASE + path
        hdrs = headers or {}
        body = None
        if data is not None:
            if isinstance(data, dict):
                body = urllib.parse.urlencode(data).encode()
                hdrs.setdefault("Content-Type", "application/x-www-form-urlencoded")
            else:
                body = data
        req = urllib.request.Request(url, data=body, headers=hdrs, method=method)
        try:
            with self.opener.open(req) as resp:
                return resp.status, resp.geturl(), resp.read().decode("utf-8", errors="replace")
        except urllib.error.HTTPError as e:
            return e.code, e.geturl(), e.read().decode("utf-8", errors="replace")

    def get(self, path):
        return self.request("GET", path)

    def post(self, path, data):
        return self.request("POST", path, data=data)


def extract_token(html):
    m = re.search(r'name="token"\s+value="([^"]+)"', html)
    return m.group(1) if m else None


def extract_antiforgery(html):
    m = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html)
    if not m:
        m = re.search(r'value="([^"]+)"\s+name="__RequestVerificationToken"', html)
    return m.group(1) if m else None


def extract_question_index(html):
    m = re.search(r'name="questionIndex"\s+value="(\d+)"', html)
    return int(m.group(1)) if m else None


def login_or_register(c):
    status, _, html = c.get("/Login")
    token = extract_antiforgery(html)
    if not token:
        fail("No antiforgery token on Login page")

    for action in ("register", "login"):
        status, url, html = c.post(
            "/Login",
            {
                "__RequestVerificationToken": token,
                "action": action,
                "Username": USERNAME,
                "Password": PASSWORD,
            },
        )
        if status in (200, 302) and ("/Index" in url or "Logged in as" in html):
            ok(f"{action} succeeded for {USERNAME}")
            return
        # refresh token for retry
        status, _, html = c.get("/Login")
        token = extract_antiforgery(html)

    fail(f"Could not login/register as {USERNAME}")


def expire_active_sessions():
    rows = sb_get(
        f"/rest/v1/test_sessions?Username=eq.{urllib.parse.quote(USERNAME)}&Status=eq.active&select=Token"
    )
    for row in rows:
        token = row["Token"]
        patch = json.dumps({"Status": "expired"}).encode()
        url = f"{os.environ['SUPABASE_URL']}/rest/v1/test_sessions?Token=eq.{urllib.parse.quote(token)}"
        req = urllib.request.Request(
            url,
            data=patch,
            method="PATCH",
            headers={
                "apikey": os.environ["SERVICE_ROLE_SECRET"],
                "Authorization": f"Bearer {os.environ['SERVICE_ROLE_SECRET']}",
                "Content-Type": "application/json",
                "Prefer": "return=minimal",
            },
        )
        urllib.request.urlopen(req)


def run_exam(c, difficulty):
    ok(f"--- Testing difficulty: {difficulty} ---")
    status, url, html = c.get(f"/Test?start=1&difficulty={difficulty}")
    if status >= 400:
        fail(f"Start test HTTP {status}")

    if "Failed to create test session" in html:
        fail("Session creation failed")
    if "Test session service is not available" in html:
        fail("TestSessionService unavailable")

    session_token = extract_token(html)
    if not session_token:
        # maybe redirect URL has token
        m = re.search(r"token=([^&\"']+)", url)
        session_token = m.group(1) if m else None
    if not session_token:
        fail(f"No session token after start (url={url})")

    if "מצב מבחן" not in html:
        fail("Test page title not found")

    # verify questions in DB for this session
    sessions = sb_get(
        f"/rest/v1/test_sessions?Token=eq.{urllib.parse.quote(session_token)}&select=QuestionsJson,Score,MaxScore,Status"
    )
    if not sessions:
        fail("Session not found in DB")
    session = sessions[0]
    questions = json.loads(session["QuestionsJson"])
    q_files = [q["Question"] for q in questions]
    ok(f"Session has {len(q_files)} questions (expected up to {TOTAL_QUESTIONS})")

    if len(q_files) == 0:
        fail("Zero questions in session — difficulty filter produced empty set")

    # build DB difficulty map
    diff_map = {
        row["QuestionFile"]: row["Difficulty"]
        for row in sb_get("/rest/v1/question_difficulties?select=QuestionFile,Difficulty")
    }

    wrong_diff = [f for f in q_files if diff_map.get(f) != difficulty]
    if wrong_diff:
        fail(f"{len(wrong_diff)} questions not tagged '{difficulty}' in DB: {wrong_diff[:3]}")

    ok(f"All {len(q_files)} questions match DB difficulty '{difficulty}'")

    # answer all questions
    for i in range(len(q_files)):
        if session_token not in html:
            session_token = extract_token(html) or session_token
        idx = extract_question_index(html)
        af = extract_antiforgery(html)
        if af is None:
            fail(f"No antiforgery on question {i + 1}")

        status, url, html = c.post(
            "/Test",
            {
                "__RequestVerificationToken": af,
                "token": session_token,
                "questionIndex": str(idx if idx is not None else i),
                "answer": "correct",
            },
        )
        if status >= 400 and "/TestResults" not in url:
            fail(f"Answer POST failed on Q{i + 1}: HTTP {status}")

    if "/TestResults" not in url and "סיכום מבחן" not in html:
        fail("Did not reach results page")

    # parse score from results HTML
    m = re.search(r"(\d+)\s*/\s*(\d+)", html)
    score_txt = m.group(0) if m else "?"
    ok(f"Results page reached, score pattern: {score_txt}")

    sessions = sb_get(
        f"/rest/v1/test_sessions?Token=eq.{urllib.parse.quote(session_token)}&select=Score,MaxScore,Status,CurrentIndex"
    )
    s = sessions[0]
    expected_score = len(q_files) * 6
    if s["Status"] != "completed":
        fail(f"Session status={s['Status']}, expected completed")
    if s["Score"] != expected_score:
        fail(f"Score {s['Score']} != expected {expected_score} (all correct)")
    if s["MaxScore"] != expected_score:
        fail(f"MaxScore {s['MaxScore']} != {expected_score}")

    ok(f"DB session: completed, score {s['Score']}/{s['MaxScore']}")
    return len(q_files)


def fail(msg):
    print(f"FAIL: {msg}")
    sys.exit(1)


def ok(msg):
    print(f"OK: {msg}")


def main():
    if not os.environ.get("SUPABASE_URL"):
        fail("Run with: set -a && source .env && set +a && python3 scripts/e2e-exam-flow.py")

    # health
    status, _, body = Client().get("/health")
    if status != 200 or '"ok":true' not in body.replace(" ", ""):
        fail(f"Health check failed: {status}")

    c = Client()
    login_or_register(c)
    expire_active_sessions()

    counts = {}
    for diff in ("easy", "medium", "hard"):
        expire_active_sessions()
        counts[diff] = run_exam(c, diff)

    print("\n=== SUMMARY ===")
    for diff, n in counts.items():
        print(f"  {diff}: {n} questions, full score, DB difficulty verified")
    print("ALL EXAM FLOWS PASSED")


if __name__ == "__main__":
    main()
