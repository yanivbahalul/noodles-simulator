(function () {
    const D = window.Dashboard = window.Dashboard || {};

    D.state = {
        updateInterval: null,
        currentFilter: null,
        userFilter: "all",
        userSearch: "",
        allUsersCache: [],
        liveActivityCache: [],
        recentActivityCache: [],
        liveActivityFilter: "all",
        recentActivityFilter: "all",
        reportFilter: "open",
        questionReportsCache: [],
        activeTab: "overview"
    };

    function setText(id, value) {
        const el = document.getElementById(id);
        if (el) el.textContent = value ?? "—";
    }

    D.setText = setText;

    D.showDashboardError = async function showDashboardError(message) {
        await window.notifyAppError?.(message);
    };

    D.confirmDashboardAction = async function confirmDashboardAction(message) {
        return await window.confirmAppAction?.(message) ?? false;
    };

    function updateOpenReportsBadge(count) {
        const badge = document.getElementById("open-reports-badge");
        const countEl = document.getElementById("open-reports-count");
        if (countEl) countEl.textContent = count ?? 0;
        if (!badge) return;
        if (count > 0) {
            badge.textContent = count;
            badge.hidden = false;
            badge.classList.remove("dashboard-tab-badge-hidden");
        } else {
            badge.hidden = true;
            badge.classList.add("dashboard-tab-badge-hidden");
        }
    }

    function switchTab(tabId) {
        D.state.activeTab = tabId || "overview";
        document.querySelectorAll(".dashboard-tab").forEach((btn) => {
            const isActive = btn.dataset.dashboardTab === D.state.activeTab;
            btn.classList.toggle("is-active", isActive);
            btn.setAttribute("aria-selected", isActive ? "true" : "false");
        });
        document.querySelectorAll(".dashboard-tab-panel").forEach((panel) => {
            const isActive = panel.dataset.dashboardPanel === D.state.activeTab;
            panel.classList.toggle("is-active", isActive);
            panel.hidden = !isActive;
        });
        try { localStorage.setItem("dashboardTab", D.state.activeTab); } catch { /* ignore */ }
    }

    function renderHealthWidget(health) {
        const widget = document.getElementById("system-health-widget");
        if (!widget) return;
        if (!health?.checks?.length) {
            widget.innerHTML = '<p class="dashboard-empty-hint">אין נתוני סטטוס</p>';
            return;
        }
        const statusClass = health.allOk ? "dashboard-health-ok" : "dashboard-health-warn";
        const statusText = health.allOk ? "✅ הכל תקין" : "⚠️ יש בעיות";
        widget.innerHTML = `
            <div class="dashboard-health-header ${statusClass}">
                <span>${statusText}</span>
                <span class="dashboard-health-time">${D.formatRelativeTime(health.checkedAtIso)}</span>
            </div>
            <ul class="dashboard-health-list">
                ${health.checks.map((c) => `
                    <li class="dashboard-health-item ${c.ok ? "is-ok" : "is-bad"}">
                        <span class="dashboard-health-icon">${c.ok ? "✓" : "✗"}</span>
                        <span class="dashboard-health-name">${window.escapeHtml(c.name)}</span>
                        <span class="dashboard-health-detail">${window.escapeHtml(c.detail)}</span>
                    </li>`).join("")}
            </ul>`;
    }

    function applyDashboardCounts(data) {
        document.getElementById("all-users-count").textContent = data.allUsersCount;
        document.getElementById("online-users-count").textContent = data.onlineUsersCount;
        document.getElementById("cheaters-count").textContent = data.cheatersCount;
        document.getElementById("banned-users-count").textContent = data.bannedUsersCount;
        document.getElementById("average-success-rate").textContent = `${data.averageSuccessRate}%`;
        setText("new-users-today", data.newUsersToday);
        setText("new-users-week", data.newUsersWeek);
        setText("inactive-7-count", data.inactive7Days);
        setText("inactive-30-count", data.inactive30Days);
        updateOpenReportsBadge(data.openQuestionReports ?? 0);
    }

    function applyDashboardPeriodStats(data) {
        const activeToday = document.getElementById("active-today-count");
        const answersToday = document.getElementById("answers-today-count");
        const dailySuccessRate = document.getElementById("daily-success-rate");
        const activeWeek = document.getElementById("active-week-count");
        const answersWeek = document.getElementById("answers-week-count");
        const weeklySuccessRate = document.getElementById("weekly-success-rate");
        if (activeToday) activeToday.textContent = data.activeToday ?? "—";
        if (answersToday) answersToday.textContent = data.answersToday ?? "—";
        if (dailySuccessRate) dailySuccessRate.textContent = data.dailySuccessRate != null ? `${data.dailySuccessRate}%` : "—";
        if (activeWeek) activeWeek.textContent = data.activeThisWeek ?? "—";
        if (answersWeek) answersWeek.textContent = data.answersThisWeek ?? "—";
        if (weeklySuccessRate) weeklySuccessRate.textContent = data.weeklySuccessRate != null ? `${data.weeklySuccessRate}%` : "—";
    }

    function applyDashboardLists(data) {
        renderHealthWidget(data.health);
        D.renderProblematicQuestions?.(data.problematicQuestions);
        D.renderQuestionReports?.(data.questionReports);
        D.state.allUsersCache = data.allUsersList || [];
        D.renderAllUsersTable?.();
        D.renderActiveExams?.(data.activeExams || []);
        D.state.liveActivityCache = data.liveActivity || [];
        D.state.recentActivityCache = data.recentActivity || [];
        renderActivityFeed("live-activity-feed", D.state.liveActivityCache, true, D.state.liveActivityFilter);
        renderActivityFeed("recent-activity-feed", D.state.recentActivityCache, false, D.state.recentActivityFilter);
    }

    D.fetchDashboardData = async function fetchDashboardData(fresh = false) {
        try {
            const freshParam = fresh ? "&fresh=1" : "";
            const response = await fetch(`/api/dashboard-data?_=${Date.now()}${freshParam}`);
            if (!response.ok) throw new Error("dashboard fetch failed");
            const data = await response.json();
            applyDashboardCounts(data);
            applyDashboardPeriodStats(data);
            applyDashboardLists(data);
        } catch {
            const lastUpdate = document.getElementById("last-update");
            if (lastUpdate) lastUpdate.textContent = "(שגיאה בעדכון - מחכה...)";
        }
    };

    function passesActivityFilter(item, filter) {
        if (!filter || filter === "all") return true;
        return (item.category || "other") === filter;
    }

    function buildActivityItemHtml(item) {
        const kindClass = `dashboard-activity-kind dashboard-activity-kind-${window.escapeHtml(item.kind || "other")}`;
        const kindText = window.escapeHtml(item.kindLabel || item.kind || "");
        return `<div class="dashboard-activity-item">
            <span class="dashboard-activity-time" title="${window.escapeHtml(item.timestampIso)}">${D.formatRelativeTime(item.timestampIso)}</span>
            <span class="${kindClass}">${kindText}</span>
            <button type="button" class="dashboard-user-link" data-username="${window.escapeHtml(item.username)}">${window.escapeHtml(item.username)}</button>
            <span class="dashboard-activity-message">${window.escapeHtml(item.message)}</span>
        </div>`;
    }

    function renderActivityFeed(containerId, items, isLive, filter) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const filtered = (items || []).filter((item) => passesActivityFilter(item, filter));
        if (!filtered.length) {
            const emptyMsg = !items?.length
                ? (isLive ? "אין עדיין אירועים — יופיעו אחרי פעילות משתמשים" : "אין פעילות אחרונה")
                : "אין אירועים בסינון הנוכחי";
            container.innerHTML = `<p class="dashboard-empty-hint">${emptyMsg}</p>`;
            return;
        }

        container.innerHTML = filtered.map(buildActivityItemHtml).join("");
        container.querySelectorAll(".dashboard-user-link").forEach((btn) => {
            btn.addEventListener("click", () => D.openUserDetail?.(btn.dataset.username));
        });
    }

    function bindActivityFilters(containerId, cacheKey) {
        const container = document.getElementById(containerId);
        if (!container) return;

        container.querySelectorAll("[data-activity-filter]").forEach((btn) => {
            btn.addEventListener("click", () => {
                container.querySelectorAll("[data-activity-filter]").forEach((b) => b.classList.remove("is-active"));
                btn.classList.add("is-active");
                const filter = btn.dataset.activityFilter || "all";
                if (cacheKey === "live") {
                    D.state.liveActivityFilter = filter;
                    renderActivityFeed("live-activity-feed", D.state.liveActivityCache, true, D.state.liveActivityFilter);
                } else {
                    D.state.recentActivityFilter = filter;
                    renderActivityFeed("recent-activity-feed", D.state.recentActivityCache, false, D.state.recentActivityFilter);
                }
            });
        });
    }

    function startAutoUpdate() {
        D.state.updateInterval = setInterval(D.fetchDashboardData, 5000);
        if (!window.__dashClockInterval) {
            window.__dashClockInterval = setInterval(() => {
                const el = document.getElementById("last-update");
                if (!el) return;
                el.textContent = `(עודכן ב-${new Date().toLocaleTimeString("he-IL")})`;
            }, 1000);
        }
    }

    function stopAutoUpdate() {
        if (D.state.updateInterval) {
            clearInterval(D.state.updateInterval);
            D.state.updateInterval = null;
        }
    }

    function showAllDifficultyRows(rows, titleEl, countEl) {
        rows.forEach((row) => row.classList.remove("difficulty-row-hidden"));
        titleEl.textContent = "רשימת שאלות (מעודכן אוטומטית)";
        countEl.textContent = `מציג את כל ${rows.length} השאלות`;
        updateDifficultyCardStates(null);
    }

    function filterRowsByDifficulty(rows, difficulty) {
        let visibleCount = 0;
        rows.forEach((row) => {
            const matches = row.getAttribute("data-difficulty") === difficulty;
            row.classList.toggle("difficulty-row-hidden", !matches);
            if (matches) visibleCount++;
        });
        return visibleCount;
    }

    function filterByDifficulty(difficulty) {
        const rows = document.querySelectorAll(".difficulty-row");
        const titleEl = document.getElementById("difficulty-title");
        const countEl = document.getElementById("difficulty-count");
        if (!titleEl || !countEl) return;

        if (D.state.currentFilter === difficulty) {
            D.state.currentFilter = null;
            showAllDifficultyRows(rows, titleEl, countEl);
            return;
        }

        D.state.currentFilter = difficulty;
        const visibleCount = filterRowsByDifficulty(rows, difficulty);
        const diffText = D.DIFFICULTY_LABELS[difficulty] || difficulty;
        titleEl.textContent = `שאלות ${diffText} (${visibleCount} שאלות)`;
        countEl.textContent = `מציג ${visibleCount} שאלות ${diffText}`;
        updateDifficultyCardStates(difficulty);
    }

    function updateDifficultyCardStates(activeDifficulty) {
        document.querySelectorAll(".difficulty-card[data-difficulty]").forEach((card) => {
            const isActive = activeDifficulty !== null && card.dataset.difficulty === activeDifficulty;
            const isDimmed = activeDifficulty !== null && !isActive;
            card.classList.toggle("is-active", isActive);
            card.classList.toggle("is-dimmed", isDimmed);
            card.setAttribute("aria-pressed", isActive ? "true" : "false");
        });
    }

    function bindRecalculateDifficultiesForm() {
        const recalcForm = document.getElementById("recalculate-difficulties-form");
        if (!recalcForm) return;
        recalcForm.addEventListener("submit", async (e) => {
            e.preventDefault();
            const btn = document.getElementById("recalculate-difficulties-btn");
            if (btn) btn.disabled = true;
            try {
                const token = recalcForm.querySelector('input[name="__RequestVerificationToken"]')?.value;
                const res = await fetch("/Dashboard?handler=RecalculateDifficulties", {
                    method: "POST",
                    headers: {
                        RequestVerificationToken: token || "",
                        "Content-Type": "application/x-www-form-urlencoded"
                    },
                    body: new URLSearchParams({ __RequestVerificationToken: token || "" })
                });
                if (res.ok) location.reload();
            } finally {
                if (btn) btn.disabled = false;
            }
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        try {
            const savedTab = localStorage.getItem("dashboardTab");
            if (savedTab) switchTab(savedTab);
        } catch { /* ignore */ }

        document.querySelectorAll(".dashboard-tab").forEach((btn) => {
            btn.addEventListener("click", () => switchTab(btn.dataset.dashboardTab));
        });

        D.bindReportUi?.();
        D.bindUserUi?.();

        document.querySelectorAll(".difficulty-card[data-difficulty]").forEach((card) => {
            card.addEventListener("click", () => filterByDifficulty(card.dataset.difficulty));
        });

        bindActivityFilters("live-activity-filters", "live");
        bindActivityFilters("recent-activity-filters", "recent");
        bindRecalculateDifficultiesForm();
    });

    window.addEventListener("load", () => {
        D.fetchDashboardData();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });
})();
