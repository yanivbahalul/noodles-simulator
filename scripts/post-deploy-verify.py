#!/usr/bin/env python3
"""Post-deploy verification: public APIs, auth flow, Supabase schema, data consistency."""
from __future__ import annotations

import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from http.cookiejar import CookieJar
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = os.environ.get("VERIFY_BASE", "https://noodles-simulator.up.railway.app")
USERNAME = os.environ.get("VERIFY_USER", "e2etestuser99")
PASSWORD = os.environ.get("VERIFY_PASS", "testpass99")

PASS = 0
FAIL = 0
WARN = 0


def load_dotenv():
    env_path = ROOT / ".env"
    if not env_path.exists():
        return
    for line in env_path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        k, v = line.split("=", 1)
        k, v = k.strip(), v.strip().strip('"')
        if k and k not in os.environ:
            os.environ[k] = v


def ok(name: str, detail: str = ""):
    global PASS
    PASS += 1
    print(f"  ✅ {name}" + (f" — {detail}" if detail else ""))


def bad(name: str, detail: str = ""):
    global FAIL
    FAIL += 1
    print(f"  ❌ {name}" + (f" — {detail}" if detail else ""))


def warn(name: str, detail: str = ""):
    global WARN
    WARN += 1
    print(f"  ⚠️  {name}" + (f" — {detail}" if detail else ""))


class Client:
    def __init__(self):
        self.jar = CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))

    def request(self, path, method="GET", data=None, json_body=None, headers=None, timeout=30, follow_redirects=True):
        url = BASE + path
        headers = dict(headers or {})
        body = None
        if json_body is not None:
            body = json.dumps(json_body).encode()
            headers["Content-Type"] = "application/json"
        elif data is not None:
            body = urllib.parse.urlencode(data).encode()
            headers["Content-Type"] = "application/x-www-form-urlencoded"
        req = urllib.request.Request(url, data=body, headers=headers, method=method)
        t0 = time.perf_counter()
        try:
            resp = self.opener.open(req, timeout=timeout) if follow_redirects else self._open_no_redirect(req, timeout)
            if not follow_redirects:
                status, raw, final_url = resp
                elapsed = (time.perf_counter() - t0) * 1000
                return status, raw, elapsed, None, final_url
            raw = resp.read()
            elapsed = (time.perf_counter() - t0) * 1000
            return resp.status, raw, elapsed, None, resp.geturl()
        except urllib.error.HTTPError as e:
            raw = e.read()
            elapsed = (time.perf_counter() - t0) * 1000
            return e.code, raw, elapsed, str(e.reason), getattr(e, "url", url)

    def _open_no_redirect(self, req, timeout):
        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self, req, fp, code, msg, headers, newurl):
                return None

        opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.jar), NoRedirect()
        )
        try:
            resp = opener.open(req, timeout=timeout)
            return resp.status, resp.read(), resp.geturl()
        except urllib.error.HTTPError as e:
            if e.code in (301, 302, 303, 307, 308):
                loc = e.headers.get("Location", "")
                if loc.startswith("/"):
                    loc = urllib.parse.urljoin(BASE, loc)
                return e.code, e.read(), loc
            raise

    def antiforgery_from_html(self, html: str) -> str:
        m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
        return m.group(1) if m else ""


def supabase_get(path: str, prefer_count: bool = False) -> tuple[int, str, str | None]:
    url = os.environ.get("SUPABASE_URL", "").rstrip("/") + "/rest/v1/" + path.lstrip("/")
    key = os.environ.get("SERVICE_ROLE_SECRET") or os.environ.get("SUPABASE_SERVICE_ROLE_KEY", "")
    if not url or not key:
        return 0, "", "missing Supabase env"
    headers = {"apikey": key, "Authorization": f"Bearer {key}"}
    if prefer_count:
        headers["Prefer"] = "count=exact"
    req = urllib.request.Request(url, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            body = resp.read().decode()
            cr = resp.headers.get("Content-Range", "")
            return resp.status, body, cr if prefer_count else None
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(), e.reason


def login(client: Client, username: str | None = None, password: str | None = None, *, count_failure: bool = True) -> bool:
    username = username or USERNAME
    password = password or PASSWORD
    status, raw, ms, err, _ = client.request("/Login")
    if status != 200:
        bad("Login page", f"HTTP {status} {err}")
        return False
    html = raw.decode("utf-8", errors="replace")
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    if not m:
        bad("Login CSRF token")
        return False
    status, raw, ms, err, _ = client.request(
        "/Login",
        method="POST",
        data={
            "__RequestVerificationToken": m.group(1),
            "Username": username,
            "Password": password,
            "action": "login",
        },
        timeout=20,
    )
    has_session = any(c.name.startswith(".Noodles.Session") for c in client.jar)
    if status in (302,) or has_session:
        ok("Login", f"{username} ({ms:.0f}ms)")
        return True
    if status == 200 and b"RedirectToPage" not in raw and "שגויים" not in raw.decode("utf-8", errors="replace"):
        # Some hosts return 200 on redirect target
        if has_session:
            ok("Login", f"{username} ({ms:.0f}ms)")
            return True
    bad("Login POST", f"HTTP {status} — check credentials for {username}") if count_failure else warn(
        "Login POST", f"HTTP {status} — check credentials for {username}"
    )
    return False


def register_and_login(client: Client) -> tuple[bool, str]:
    ts = int(time.time())
    user = f"verify{ts}"
    pw = f"verify{ts}pw"
    status, raw, _, _, _ = client.request("/Login")
    html = raw.decode("utf-8", errors="replace")
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    if not m:
        return False, user
    status, _, _, _, _ = client.request(
        "/Login",
        method="POST",
        data={
            "__RequestVerificationToken": m.group(1),
            "Username": user,
            "Password": pw,
            "action": "register",
        },
        timeout=20,
    )
    if not login(client, user, pw):
        return False, user
    return True, user


def check_public_apis():
    print("\n── Public APIs ──")
    endpoints = [
        ("/health", 200),
        ("/api/online-count", 200),
        ("/api/leaderboard-data?tab=total", 200),
        ("/api/leaderboard-data?tab=level", 200),
        ("/api/leaderboard-data?tab=weekly", 200),
        ("/api/leaderboard-data?tab=achievement", 200),
        ("/api/question-difficulty", 200),
        ("/Login", 200),
        ("/Leaderboard", 200),
    ]
    for path, expect in endpoints:
        c = Client()
        status, raw, ms, err, _ = c.request(path, timeout=20)
        if status == expect:
            ok(path, f"{ms:.0f}ms")
        else:
            bad(path, f"HTTP {status} (expected {expect}) {err}")

    c = Client()
    status, raw, ms, _, _ = c.request("/health")
    if status == 200:
        h = json.loads(raw)
        for k in ("supabaseUrl", "supabaseAnon", "supabaseService", "supabaseBucket"):
            if h.get(k) == "ok":
                ok(f"health.{k}")
            else:
                bad(f"health.{k}", str(h.get(k)))


def check_supabase_schema():
    print("\n── Supabase schema & counts ──")
    tables = ["user_stats", "user_question_stats", "user_progress", "user_achievements", "test_sessions"]
    for t in tables:
        code, body, cr = supabase_get(f"{t}?select=*&limit=1", prefer_count=True)
        if code in (200, 206):
            count = cr.split("/")[-1] if cr and "/" in cr else "?"
            ok(f"table {t}", f"rows={count}")
        else:
            bad(f"table {t}", f"HTTP {code} {body[:120]}")

    code, body, _ = supabase_get(
        'test_sessions?select=Token,QuestionCount,QuestionsStoragePath&QuestionsStoragePath=neq.&limit=1'
    )
    if code in (200, 206):
        rows = json.loads(body)
        if rows:
            ok("test_sessions storage", f"{len(rows)} session(s) use Storage")
        else:
            warn("test_sessions storage", "no sessions with Storage path yet (start new exam to verify)")

    code, body, _ = supabase_get(
        'user_progress?select=Username&ProgressData->QuestionStats=not.is.null&limit=1'
    )
    if code == 200:
        legacy = json.loads(body)
        if legacy:
            warn("legacy ProgressData JSON", f"{len(legacy)}+ users still have QuestionStats in JSON (will slim on save)")
        else:
            ok("ProgressData slim", "no QuestionStats left in JSON")


def check_user_stats_consistency():
    print("\n── Data consistency ──")
    code, users_body, users_cr = supabase_get("users?select=Username&limit=1", prefer_count=True)
    code2, stats_body, stats_cr = supabase_get("user_stats?select=Username&limit=1", prefer_count=True)
    if code in (200, 206) and code2 in (200, 206):
        u = int(users_cr.split("/")[-1]) if users_cr and "/" in users_cr else 0
        s = int(stats_cr.split("/")[-1]) if stats_cr and "/" in stats_cr else 0
        if s >= u * 0.9:
            ok("user_stats coverage", f"{s}/{u} users")
        elif s > 0:
            warn("user_stats coverage", f"{s}/{u} users")
        else:
            bad("user_stats coverage", "empty")

    code, body, _ = supabase_get(
        f'user_stats?Username=eq.{urllib.parse.quote(USERNAME)}&select=Username,Xp,Level'
    )
    if code in (200, 206):
        rows = json.loads(body)
        if rows and rows[0].get("Xp", 0) >= 0:
            ok("user_stats row for test user", f"Xp={rows[0].get('Xp')}")
        else:
            bad("user_stats row for test user", "missing")


def check_authenticated_flow(client: Client, test_user: str):
    print("\n── Authenticated flow ──")
    status, raw, ms, err, _ = client.request("/Index", timeout=45)
    if status != 200:
        bad("Index page", f"HTTP {status} {err}")
        return
    ok("Index page", f"{ms:.0f}ms")

    status, raw, ms, err, _ = client.request("/api/stats-data", timeout=15)
    if status == 200:
        stats_before = json.loads(raw)
        ok("stats-data", f"xp={stats_before.get('xp')} level={stats_before.get('level')} ({ms:.0f}ms)")
    else:
        warn("stats-data", f"HTTP {status}")
        stats_before = {}

    status, raw, ms, err, _ = client.request("/Index?handler=NextQuestion", timeout=45)
    if status != 200:
        bad("NextQuestion", f"HTTP {status} {err}")
        return
    q = json.loads(raw)
    if q.get("error"):
        bad("NextQuestion", q.get("error"))
        return
    question_image = q.get("questionImage") or q.get("questionImageOriginalName")
    answers_raw = q.get("answers") or q.get("answerImageUrls") or []
    if isinstance(answers_raw, list) and answers_raw:
        answer_key = answers_raw[0].get("key") if isinstance(answers_raw[0], dict) else answers_raw[0]
    elif isinstance(answers_raw, dict) and answers_raw:
        answer_key = next(iter(answers_raw.keys()))
    else:
        bad("NextQuestion payload", "missing question or answers")
        return
    ok("NextQuestion", f"question={str(question_image)[:40]}… ({ms:.0f}ms)")

    # Anti-forgery token required for POST handlers (same as index-page.js)
    status_ix, raw_ix, _, _, _ = client.request("/Index", timeout=45)
    antiforgery = ""
    if status_ix == 200:
        m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', raw_ix.decode("utf-8", errors="replace"))
        if m:
            antiforgery = m.group(1)

    headers_extra = {}
    if antiforgery:
        headers_extra["RequestVerificationToken"] = antiforgery

    status, raw, ms, err, _ = client.request(
        "/Index?handler=SubmitAnswer",
        method="POST",
        json_body={"questionImage": question_image, "answer": answer_key},
        headers=headers_extra,
        timeout=45,
    )
    if status != 200:
        bad("SubmitAnswer", f"HTTP {status} {err}")
        return
    ans = json.loads(raw)
    if ans.get("error"):
        bad("SubmitAnswer", ans.get("error"))
        return
    ok(
        "SubmitAnswer",
        f"correct={ans.get('isCorrect')} xp={ans.get('xp')} ({ms:.0f}ms)",
    )

    status, raw, ms, _, _ = client.request("/api/stats-data", timeout=15)
    if status == 200:
        stats_after = json.loads(raw)
        ok("stats-data after answer", f"xp={stats_after.get('xp')} total={stats_after.get('total')} ({ms:.0f}ms)")

    # user_stats row for test user after answer
    code, body, _ = supabase_get(
        f'user_stats?Username=eq.{urllib.parse.quote(test_user)}&select=Username,Xp,DailyCorrect'
    )
    if code in (200, 206):
        rows = json.loads(body)
        if rows:
            ok("user_stats after answer", f"Xp={rows[0].get('Xp')} DailyCorrect={rows[0].get('DailyCorrect')}")
        else:
            warn("user_stats after answer", "no row yet")

    # user_question_stats is written async — poll latest row for this user
    qrow = None
    for _ in range(4):
        code, body, _ = supabase_get(
            f"user_question_stats?username=eq.{urllib.parse.quote(test_user)}"
            f"&select=question_id,attempts,correct&order=last_answered_utc.desc&limit=1"
        )
        if code in (200, 206):
            rows = json.loads(body)
            if rows and rows[0].get("attempts", 0) > 0:
                qrow = rows[0]
                break
        time.sleep(0.75)
    if qrow:
        ok("user_question_stats write", f"attempts={qrow['attempts']} q={str(qrow.get('question_id',''))[:35]}…")
    else:
        warn("user_question_stats write", "no row found after answer (async write may have failed)")


def check_exam_storage_flow(client: Client, test_user: str):
    print("\n── Exam session (Storage) ──")
    status, raw, ms, err, final_url = client.request("/Test?start=1", timeout=60)
    html = raw.decode("utf-8", errors="replace")
    token = ""
    m = re.search(r"[?&]token=([A-Za-z0-9_-]+)", final_url or "")
    if m:
        token = m.group(1)
    if not token:
        m = re.search(r'name="token"\s+value="([^"]+)"', html)
        if m:
            token = m.group(1)
    if status not in (200, 302) or not token:
        bad("Start exam", f"HTTP {status} token={token or 'missing'} {err}")
        return

    ok("Start exam", f"token={token[:12]}… ({ms:.0f}ms)")

    safe_user = urllib.parse.quote(test_user)
    safe_token = urllib.parse.quote(token)
    code, body, _ = supabase_get(
        f"test_sessions?Token=eq.{safe_token}&select=Token,Username,QuestionCount,QuestionsJson,QuestionsStoragePath,Status"
    )
    if code not in (200, 206):
        bad("exam DB row", f"HTTP {code}")
        return
    rows = json.loads(body)
    if not rows:
        bad("exam DB row", "not found")
        return
    row = rows[0]
    if row.get("Username") != test_user:
        warn("exam owner", f"expected {test_user}, got {row.get('Username')}")
    qc = row.get("QuestionCount") or 0
    storage_path = row.get("QuestionsStoragePath") or ""
    qjson = row.get("QuestionsJson") or ""
    if storage_path and (not qjson or qjson == "[]"):
        ok("exam Storage offload", f"QuestionCount={qc} path={storage_path[:40]}…")
    elif storage_path:
        ok("exam Storage path set", f"QuestionCount={qc} (QuestionsJson also present)")
    else:
        warn("exam Storage offload", "QuestionsStoragePath empty — storage may be disabled")

    if "test-main-question-image" in html or "answers-grid" in html:
        ok("exam UI loaded", "question page rendered")
    else:
        warn("exam UI", "could not confirm question UI in HTML")


def check_dashboard_requires_admin():
    print("\n── Admin-only (expect redirect) ──")
    c = Client()
    status, raw, _, _, _ = c.request("/Dashboard", timeout=15)
    body = raw.decode("utf-8", errors="replace")[:800]
    if status in (302, 401, 403):
        ok("Dashboard protected", f"HTTP {status}")
    elif status == 200 and ("התחברות" in body or "Login" in body or 'name="Username"' in body):
        ok("Dashboard protected", "redirects to login content")
    elif status == 200:
        warn("Dashboard", "returned 200 — verify admin-only manually")
    else:
        bad("Dashboard", f"HTTP {status}")


def main():
    load_dotenv()
    print("=" * 60)
    print("Noodles Simulator — Post-Deploy Verification")
    print(f"Base: {BASE}")
    print(f"Time: {time.strftime('%Y-%m-%d %H:%M:%S UTC', time.gmtime())}")
    print("=" * 60)

    check_public_apis()
    check_supabase_schema()
    check_user_stats_consistency()
    check_dashboard_requires_admin()

    client = Client()
    test_user = USERNAME
    authed = login(client, count_failure=False)
    if not authed:
        warn(f"Login as {USERNAME}", "failed — trying fresh register")
        client = Client()
        authed, test_user = register_and_login(client)
        if authed:
            ok("Register+Login", test_user)
        else:
            warn("Authenticated flow", "skipped — could not authenticate")

    if authed:
        status, raw, _, _, _ = client.request("/Index", timeout=30)
        html = raw.decode("utf-8", errors="replace")
        if status == 200 and 'id="logout' in html.lower() or "התנתק" in html or test_user.lower() in html.lower():
            ok("Session active after login", test_user)
        else:
            warn("Session", "could not confirm logged-in UI")
        check_authenticated_flow(client, test_user)
        check_exam_storage_flow(client, test_user)

    print("\n" + "=" * 60)
    print(f"SUMMARY: {PASS} passed, {WARN} warnings, {FAIL} failed")
    print("=" * 60)
    return 1 if FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
