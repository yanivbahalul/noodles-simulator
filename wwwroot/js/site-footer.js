(function () {
    function setText(id, value) {
        const el = document.getElementById(id);
        if (el) el.textContent = value;
    }

    function setStatsPanelOpen(panel, toggle, willOpen) {
        panel.classList.toggle("is-open", willOpen);
        panel.setAttribute("aria-hidden", willOpen ? "false" : "true");
        if (!toggle) return;
        toggle.classList.toggle("footer-stats-toggle-open", willOpen);
        toggle.classList.toggle("footer-stats-toggle-closed", !willOpen);
        toggle.setAttribute("aria-expanded", willOpen ? "true" : "false");
    }

    async function fetchDrawerStats() {
        try {
            const res = await fetch(`/api/stats-data?_=${Date.now()}`);
            if (!res.ok) return;
            const data = await res.json();
            if (data?.correct === undefined) return;
            setText("stat-correct-panel", data.correct);
            setText("stat-total-panel", data.total);
            setText("stat-success-panel", `${data.successRate}%`);
            if (data.streak !== undefined) setText("stat-streak", data.streak);
            if (data.level !== undefined) setText("stat-level-value", data.level);
            if (data.xp !== undefined) setText("stat-xp-value", data.xp);
        } catch {
            // keep existing values
        }
    }

    function toggleFooterStats() {
        const panel = document.getElementById("stats-panel");
        const toggle = document.getElementById("footer-stats-toggle");
        if (!panel) {
            window.location.assign("/Stats");
            return;
        }
        const willOpen = !panel.classList.contains("is-open");
        setStatsPanelOpen(panel, toggle, willOpen);
        if (willOpen) fetchDrawerStats();
    }

    window.fetchDrawerStats = fetchDrawerStats;
    window.toggleFooterStats = toggleFooterStats;

    let presenceInterval = null;

    async function presenceHeartbeat() {
        try {
            await fetch(`/api/online-count?heartbeat=1&_=${Date.now()}`, { credentials: "same-origin" });
        } catch {
            // keep session presence best-effort
        }
    }

    function startPresence() {
        stopPresence();
        presenceHeartbeat();
        presenceInterval = setInterval(presenceHeartbeat, 60000);
    }

    function stopPresence() {
        if (presenceInterval) {
            clearInterval(presenceInterval);
            presenceInterval = null;
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        const btn = document.getElementById("footer-stats-toggle");
        if (btn) btn.addEventListener("click", toggleFooterStats);
        startPresence();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopPresence();
        else startPresence();
    });
})();
