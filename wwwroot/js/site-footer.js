(function () {
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
            window.setText("stat-correct-panel", data.correct);
            window.setText("stat-total-panel", data.total);
            window.setText("stat-success-panel", `${data.successRate}%`);
            if (data.streak !== undefined) window.setText("stat-streak", data.streak);
            if (data.level !== undefined) window.setText("stat-level-value", data.level);
            if (data.xp !== undefined) window.setText("stat-xp-value", data.xp);
            if (typeof window.applyLevelProgressLive === "function") {
                window.applyLevelProgressLive(data);
            }
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

    window.addEventListener("pagehide", () => {
        try {
            navigator.sendBeacon("/api/offline");
        } catch {
            // best-effort
        }
    });
})();
