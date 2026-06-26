#!/usr/bin/env python3
"""Load test for Noodles Simulator — reports latency, DB latency and status codes."""
import concurrent.futures
import os
import re
import statistics
import sys
import time
import urllib.parse
import urllib.request
from http.cookiejar import CookieJar

BASE = os.environ.get("LOAD_BASE", "http://localhost:5001")
USERNAME = os.environ.get("LOAD_USER", "e2etestuser99")
PASSWORD = os.environ.get("LOAD_PASS", "testpass99")


class Client:
    def __init__(self):
        self.jar = CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))

    def fetch(self, path, method="GET", data=None, timeout=30):
        url = BASE + path
        body = None
        headers = {}
        if data is not None:
            body = urllib.parse.urlencode(data).encode()
            headers["Content-Type"] = "application/x-www-form-urlencoded"
        req = urllib.request.Request(url, data=body, headers=headers, method=method)
        t0 = time.perf_counter()
        try:
            with self.opener.open(req, timeout=timeout) as resp:
                resp.read()
                elapsed = time.perf_counter() - t0
                return resp.status, elapsed, None
        except urllib.error.HTTPError as e:
            elapsed = time.perf_counter() - t0
            try:
                e.read()
            except Exception:
                pass
            return e.code, elapsed, str(e.reason)
        except Exception as e:
            elapsed = time.perf_counter() - t0
            return 0, elapsed, str(e)


def login(client):
    status, _, err = client.fetch("/Login")
    if status != 200:
        return False, f"Login page HTTP {status} {err}"
    # Get token from cookie jar session — need HTML
    req = urllib.request.Request(BASE + "/Login")
    with client.opener.open(req, timeout=10) as resp:
        html = resp.read().decode("utf-8", errors="replace")
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    if not m:
        return False, "CSRF token not found"
    token = m.group(1)
    status, elapsed, err = client.fetch(
        "/Login",
        method="POST",
        data={
            "__RequestVerificationToken": token,
            "Username": USERNAME,
            "Password": PASSWORD,
            "action": "login",
        },
        timeout=15,
    )
    if status not in (200, 302):
        return False, f"Login POST HTTP {status} {err} ({elapsed:.2f}s)"
    return True, f"logged in as {USERNAME} ({elapsed:.2f}s)"


def percentile(values, p):
    if not values:
        return 0.0
    s = sorted(values)
    k = (len(s) - 1) * p / 100
    f = int(k)
    c = min(f + 1, len(s) - 1)
    if f == c:
        return s[f]
    return s[f] + (s[c] - s[f]) * (k - f)


def run_endpoint(name, path, n=50, workers=5, timeout=30, client_factory=None):
    print(f"\n{'='*60}")
    print(f"  {name}")
    print(f"  {path}  |  n={n}  workers={workers}")
    print(f"{'='*60}")

    statuses = {}
    times = []
    errors = []

    def one(_):
        c = client_factory() if client_factory else Client()
        return c.fetch(path, timeout=timeout)

    t0 = time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as ex:
        results = list(ex.map(one, range(n)))
    wall = time.perf_counter() - t0

    for status, elapsed, err in results:
        statuses[status] = statuses.get(status, 0) + 1
        if status == 200 or status == 302:
            times.append(elapsed)
        if err:
            errors.append(err)

    ok = statuses.get(200, 0) + statuses.get(302, 0)
    print(f"  Wall time:     {wall:.2f}s")
    print(f"  Throughput:    {n/wall:.1f} req/s")
    print(f"  Status codes:  {dict(sorted(statuses.items()))}")
    if times:
        print(f"  Latency (ok):  min={min(times)*1000:.0f}ms  avg={statistics.mean(times)*1000:.0f}ms  "
              f"p50={percentile(times,50)*1000:.0f}ms  p95={percentile(times,95)*1000:.0f}ms  "
              f"p99={percentile(times,99)*1000:.0f}ms  max={max(times)*1000:.0f}ms")
    if errors:
        print(f"  Errors:        {errors[:3]}")
    return {"name": name, "ok": ok, "total": n, "p95_ms": percentile(times, 95) * 1000 if times else None}


def main():
    print("Noodles Simulator Load Test (Python)")
    print(f"Base: {BASE}  |  {time.strftime('%Y-%m-%d %H:%M:%S')}")
    print("Rate limit: 120 req/min/IP — keeping each scenario ≤50 requests\n")

    results = []
    results.append(run_endpoint("Health", "/health", n=50, workers=10, timeout=5))
    time.sleep(1)
    results.append(run_endpoint("Login page", "/Login", n=50, workers=10, timeout=5))
    time.sleep(1)
    results.append(run_endpoint("Static CSS", "/css/site.css", n=50, workers=10, timeout=5))
    time.sleep(1)
    results.append(run_endpoint("Leaderboard API", "/api/leaderboard-data?tab=total", n=30, workers=5, timeout=15))
    time.sleep(1)
    results.append(run_endpoint("Online count API", "/api/online-count", n=30, workers=5, timeout=15))
    time.sleep(1)
    results.append(run_endpoint("Leaderboard page", "/Leaderboard", n=30, workers=5, timeout=15))

    # Authenticated Index (heaviest page)
    auth_client = Client()
    ok, msg = login(auth_client)
    print(f"\n{'='*60}")
    print(f"  Auth setup: {msg}")
    print(f"{'='*60}")
    if ok:
        def auth_factory():
            return auth_client
        results.append(run_endpoint(
            "Index (authenticated, quiz page)",
            "/Index",
            n=10,
            workers=2,
            timeout=60,
            client_factory=auth_factory,
        ))
    else:
        print("  Skipping Index test — login failed")

    print(f"\n{'='*60}")
    print("  SUMMARY")
    print(f"{'='*60}")
    print(f"{'Endpoint':<35} {'OK/Total':>10} {'p95':>10}")
    print("-" * 58)
    for r in results:
        p95 = f"{r['p95_ms']:.0f}ms" if r["p95_ms"] is not None else "n/a"
        print(f"{r['name']:<35} {r['ok']}/{r['total']:>8} {p95:>10}")

    failed = [r for r in results if r["ok"] < r["total"]]
    if failed:
        print("\n⚠ Some requests failed (likely 429 rate limit or timeouts)")
        return 1
    print("\n✓ All requests succeeded")
    return 0


if __name__ == "__main__":
    sys.exit(main())
