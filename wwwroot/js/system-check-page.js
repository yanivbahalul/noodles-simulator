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
    let streamFinished = false;

    function renderPlan() {
        tbody.innerHTML = "";
        rows.clear();
        completedChecks = 0;
        counts = { passed: 0, failed: 0, warnings: 0 };
        progress.style.width = "0%";
        summary.hidden = true;
        updateSummary();

        for (const item of plan) {
            const id = item.id || item.Id;
            const name = item.name || item.Name;
            const category = item.category || item.Category;
            const tr = document.createElement("tr");
            tr.id = `check-row-${id}`;
            tr.className = "system-check-row-pending";
            tr.innerHTML = `
                <td class="system-check-category">${window.escapeHtml(category)}</td>
                <td>${window.escapeHtml(name)}</td>
                <td><span class="system-check-badge system-check-badge-pending">${statusLabel.pending}</span></td>
                <td class="system-check-detail">—</td>
                <td class="system-check-time">—</td>
            `;
            tbody.appendChild(tr);
            rows.set(id, tr);
        }
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

    const STATUS_LOOKUP = new Map([
        ["ok", "ok"], ["Ok", "ok"], [2, "ok"],
        ["warn", "warn"], ["Warn", "warn"], [4, "warn"],
        ["fail", "fail"], ["Fail", "fail"], [3, "fail"],
        ["running", "running"], ["Running", "running"], [1, "running"]
    ]);

    function normalizeStatus(raw) {
        if (STATUS_LOOKUP.has(raw)) return STATUS_LOOKUP.get(raw);
        if (typeof raw === "string") return raw.toLowerCase();
        return "fail";
    }

    function getPhase(evt) {
        return evt.phase || evt.Phase || "";
    }

    function finishStream() {
        streamFinished = true;
        runBtn.disabled = false;
        runBtn.classList.remove("is-running");
        if (eventSource) {
            eventSource.close();
            eventSource = null;
        }
    }

    function handlePhaseError(evt) {
        setBanner("fail", `❌ שגיאה: ${evt.detail || evt.Detail || "לא ידוע"}`);
        finishStream();
    }

    function handlePhaseStart(evt) {
        const planItems = evt.plan || evt.Plan;
        totalChecks = planItems?.length || plan.length;
        setBanner("running", "⏳ מריץ בדיקות...");
        updateClock(`התחיל ב-${formatTime(new Date())}`);
    }

    function handlePhaseRunning(evt) {
        applyRowState(evt.id || evt.Id, "running", "בודק…");
    }

    function recordCheckResult(status) {
        completedChecks++;
        if (status === "ok") counts.passed++;
        else if (status === "warn") counts.warnings++;
        else counts.failed++;
        updateProgress();
        updateSummary();
        summary.hidden = false;
    }

    function handlePhaseCheck(evt) {
        const status = normalizeStatus(evt.status ?? evt.Status);
        applyRowState(evt.id || evt.Id, status, evt.detail || evt.Detail || "", evt.elapsedMs ?? evt.ElapsedMs);
        recordCheckResult(status);
    }

    function completeBannerText(failed, warnings) {
        if (failed > 0) return `❌ הסתיים — ${failed} נכשלו, ${warnings} אזהרות`;
        if (warnings > 0) return `⚠️ הסתיים — הכל עבר עם ${warnings} אזהרות`;
        return "✅ כל הבדיקות עברו בהצלחה";
    }

    function handlePhaseComplete(evt) {
        const failed = evt.failed ?? evt.Failed ?? counts.failed;
        const warnings = evt.warnings ?? evt.Warnings ?? counts.warnings;
        const bannerKind = failed > 0 ? "fail" : warnings > 0 ? "warn" : "ok";
        setBanner(bannerKind, completeBannerText(failed, warnings));
        updateClock(`הסתיים ב-${formatTime(new Date())}`);
        finishStream();
    }

    const PHASE_HANDLERS = {
        error: handlePhaseError,
        start: handlePhaseStart,
        running: handlePhaseRunning,
        check: handlePhaseCheck,
        complete: handlePhaseComplete
    };

    function handleEvent(evt) {
        const handler = PHASE_HANDLERS[getPhase(evt)];
        if (handler) handler(evt);
    }

    function handleStreamError() {
        if (streamFinished) return;
        if (completedChecks > 0 && eventSource?.readyState === EventSource.CLOSED) return;
        setBanner("fail", "❌ החיבור לבדיקות נותק — נסה שוב");
        finishStream();
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
        streamFinished = false;
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

        eventSource.onerror = handleStreamError;
    }

    runBtn.addEventListener("click", startRun);
    renderPlan();
    startRun();
})();
