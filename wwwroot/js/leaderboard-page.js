(function () {
    let updateInterval = null;
    let clockInterval = null;
    let activeTab = "total";

    const scoreHeaders = {
        total: "תשובות נכונות",
        rate: "אחוז הצלחה",
        weekly: "נכונות השבוע",
        exam: "ציון מבחן",
        daily: "נכון באתגר"
    };

    async function fetchLeaderboardData() {
        try {
            const response = await fetch(`/api/leaderboard-data?tab=${encodeURIComponent(activeTab)}&_=${Date.now()}`);
            if (!response.ok) throw new Error("leaderboard fetch failed");
            const data = await response.json();
            if (Array.isArray(data?.users)) {
                updateLeaderboardTable(data.users, data.tab || activeTab);
                window.__lastUpdateAt = Date.now();
            }
        } catch {
            const lastUpdate = document.getElementById("last-update");
            if (lastUpdate) lastUpdate.textContent = "(שגיאה בעדכון - מחכה...)";
        }
    }

    function updateLeaderboardTable(data, tab) {
        const table = document.getElementById("leaderboard-table");
        if (!table) return;
        const tbody = table.querySelector("tbody");
        if (!tbody) return;
        tbody.innerHTML = "";

        const header = document.getElementById("score-header");
        if (header) header.textContent = scoreHeaders[tab] || scoreHeaders.total;

        data.forEach((user) => {
            const row = tbody.insertRow();
            row.className = `leaderboard-data-row ${(user.rank - 1) % 2 === 0 ? "leaderboard-row-even" : "leaderboard-row-odd"}`;

            const rankCell = row.insertCell();
            rankCell.className = "leaderboard-cell";
            rankCell.textContent = user.rank;

            const usernameCell = row.insertCell();
            usernameCell.className = "leaderboard-cell";
            if (user.isOnline) {
                const strongElement = document.createElement("strong");
                strongElement.className = "leaderboard-online-user";
                strongElement.textContent = user.username;
                usernameCell.appendChild(strongElement);
            } else {
                usernameCell.textContent = user.username;
            }

            const scoreCell = row.insertCell();
            scoreCell.className = "leaderboard-cell";
            scoreCell.textContent = user.scoreDisplay ?? user.correctAnswers;
        });
    }

    function bindTabs() {
        document.querySelectorAll(".leaderboard-tab").forEach((btn) => {
            btn.addEventListener("click", () => {
                activeTab = btn.dataset.tab || "total";
                document.querySelectorAll(".leaderboard-tab").forEach((b) => b.classList.toggle("active", b === btn));
                fetchLeaderboardData();
            });
        });
    }

    function startAutoUpdate() {
        updateInterval = setInterval(fetchLeaderboardData, 3000);
        if (!clockInterval) {
            clockInterval = setInterval(() => {
                const el = document.getElementById("last-update");
                if (!el) return;
                const now = new Date();
                el.textContent = `(עודכן ב-${now.toLocaleTimeString("he-IL")})`;
            }, 1000);
        }
    }

    function stopAutoUpdate() {
        if (updateInterval) {
            clearInterval(updateInterval);
            updateInterval = null;
        }
        if (clockInterval) {
            clearInterval(clockInterval);
            clockInterval = null;
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        const data = document.getElementById("leaderboard-page-data");
        if (data?.dataset.activeTab) activeTab = data.dataset.activeTab;
        bindTabs();
    });

    window.addEventListener("load", () => {
        fetchLeaderboardData();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });
})();
