(function () {
    const main = document.querySelector(".system-check-page");
    if (!main) return;

    const plan = JSON.parse(main.dataset.plan || "[]");
    const tbody = document.getElementById("system-check-tbody");
    const banner = document.getElementById("system-check-banner");
    const progress = document.getElementById("system-check-progress");
    const summary = document.getElementById("system-check-summary");
    const runBtn = document.getElementById("system-check-run-btn");
    const clock = document.getElementById("system-check-clock");
    const summaryPassed = document.getElementById("summary-passed");
    const summaryWarnings = document.getElementById("summary-warnings");
    const summaryFailed = document.getElementById("summary-failed");

    const statusLabel = {
        pending: "ממתין",
        running: "בתהליך…",
        ok: "תקין",
        fail: "נכשל",
        warn: "אזהרה"
    };

    const rows = new Map();
    let eventSource = null;
    let totalChecks = plan.length;
    let completedChecks = 0;
    let counts = { passed: 0, failed: 0, warnings: 0 };

    function renderPlan() {
        tbody.innerHTML = "";
        rows.clear();
        completedChecks = 0;
        counts = { passed: 0, failed: 0, warnings: 0 };
        progress.style.width = "0%";
        summary.hidden = true;
        updateSummary();

        for (const item of plan) {
            const tr = document.createElement("tr");
            tr.id = `check-row-${item.id}`;
            tr.className = "system-check-row-pending";
            tr.innerHTML = `
                <td class="system-check-category">${escapeHtml(item.category)}</td>
                <td>${escapeHtml(item.name)}</td>
                <td><span class="system-check-badge system-check-badge-pending">${statusLabel.pending}</span></td>
                <td class="system-check-detail">—</td>
                <td class="system-check-time">—</td>
            `;
            tbody.appendChild(tr);
            rows.set(item.id, tr);
        }
    }

    function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function setBanner(kind, text) {
        banner.className = `dashboard-alert system-check-banner system-check-banner-${kind}`;
        banner.textContent = text;
    }

    function updateClock(text) {
        clock.textContent = text;
    }

    function updateSummary() {
        summaryPassed.textContent = String(counts.passed);
        summaryWarnings.textContent = String(counts.warnings);
        summaryFailed.textContent = String(counts.failed);
    }

    function updateProgress() {
        const pct = totalChecks > 0 ? Math.round((completedChecks / totalChecks) * 100) : 0;
        progress.style.width = `${pct}%`;
    }

    function applyRowState(id, status, detail, elapsedMs) {
        const tr = rows.get(id);
        if (!tr) return;

        tr.className = `system-check-row-${status}`;
        const badge = tr.querySelector(".system-check-badge");
        badge.className = `system-check-badge system-check-badge-${status}`;
        badge.textContent = statusLabel[status] || status;

        const detailCell = tr.querySelector(".system-check-detail");
        const timeCell = tr.querySelector(".system-check-time");
        if (detail) detailCell.textContent = detail;
        if (typeof elapsedMs === "number") timeCell.textContent = `${elapsedMs} ms`;
    }

    function normalizeStatus(raw) {
        if (raw === "Ok" || raw === 2) return "ok";
        if (raw === "Warn" || raw === 4) return "warn";
        if (raw === "Fail" || raw === 3) return "fail";
        if (raw === "Running" || raw === 1) return "running";
        if (typeof raw === "string") return raw.toLowerCase();
        return "fail";
    }

    function handleEvent(evt) {
        if (evt.phase === "start") {
            totalChecks = (evt.plan && evt.plan.length) || plan.length;
            setBanner("running", "⏳ מריץ בדיקות...");
            updateClock(`התחיל ב-${formatTime(new Date())}`);
            return;
        }

        if (evt.phase === "running") {
            applyRowState(evt.id, "running", "בודק…");
            return;
        }

        if (evt.phase === "check") {
            const status = normalizeStatus(evt.status);

            applyRowState(evt.id, status, evt.detail || "", evt.elapsedMs);
            completedChecks++;
            if (status === "ok") counts.passed++;
            else if (status === "warn") counts.warnings++;
            else counts.failed++;
            updateProgress();
            updateSummary();
            summary.hidden = false;
            return;
        }

        if (evt.phase === "complete") {
            const failed = evt.failed ?? counts.failed;
            const warnings = evt.warnings ?? counts.warnings;
            if (failed > 0) {
                setBanner("fail", `❌ הסתיים — ${failed} נכשלו, ${warnings} אזהרות`);
            } else if (warnings > 0) {
                setBanner("warn", `⚠️ הסתיים — הכל עבר עם ${warnings} אזהרות`);
            } else {
                setBanner("ok", "✅ כל הבדיקות עברו בהצלחה");
            }
            updateClock(`הסתיים ב-${formatTime(new Date())}`);
            runBtn.disabled = false;
            runBtn.classList.remove("is-running");
            if (eventSource) {
                eventSource.close();
                eventSource = null;
            }
        }
    }

    function formatTime(d) {
        return d.toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit", second: "2-digit" });
    }

    function startRun() {
        if (eventSource) {
            eventSource.close();
            eventSource = null;
        }

        renderPlan();
        runBtn.disabled = true;
        runBtn.classList.add("is-running");
        setBanner("running", "⏳ מתחבר לבדיקות חיות...");
        updateClock("מריץ...");

        eventSource = new EventSource("/SystemCheck?handler=Stream");

        eventSource.onmessage = (message) => {
            try {
                const evt = JSON.parse(message.data);
                handleEvent(evt);
            } catch (err) {
                console.error("system-check parse error", err);
            }
        };

        eventSource.onerror = () => {
            setBanner("fail", "❌ החיבור לבדיקות נותק — נסה שוב");
            runBtn.disabled = false;
            runBtn.classList.remove("is-running");
            if (eventSource) {
                eventSource.close();
                eventSource = null;
            }
        };
    }

    runBtn.addEventListener("click", startRun);
    renderPlan();
    startRun();
})();
