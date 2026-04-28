(function () {
    let updateInterval;
    let clockInterval;

    async function fetchLeaderboardData() {
        try {
            const response = await fetch("/api/leaderboard-data?_=" + new Date().getTime());
            if (!response.ok) throw new Error("leaderboard fetch failed");
            const data = await response.json();
            if (data && data.users && Array.isArray(data.users)) {
                updateLeaderboardTable(data.users);
                window.__lastUpdateAt = Date.now();
            }
        } catch {
            const lastUpdate = document.getElementById("last-update");
            if (lastUpdate) lastUpdate.textContent = "(שגיאה בעדכון - מחכה...)";
        }
    }

    function updateLeaderboardTable(data) {
        const table = document.getElementById("leaderboard-table");
        if (!table) return;
        const tbody = table.querySelector("tbody");
        if (!tbody) return;
        tbody.innerHTML = "";

        data.forEach((user, index) => {
            const row = tbody.insertRow();
            row.className = `leaderboard-data-row ${index % 2 === 0 ? "leaderboard-row-even" : "leaderboard-row-odd"}`;

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

            const correctCell = row.insertCell();
            correctCell.className = "leaderboard-cell";
            correctCell.textContent = user.correctAnswers;
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

    window.addEventListener("load", () => {
        fetchLeaderboardData();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });
})();
