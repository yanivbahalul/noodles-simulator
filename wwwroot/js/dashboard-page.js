(function () {
    let updateInterval;
    let currentFilter = null;

    async function fetchDashboardData() {
        try {
            const response = await fetch("/api/dashboard-data?_=" + new Date().getTime());
            if (!response.ok) throw new Error("dashboard fetch failed");
            const data = await response.json();

            document.getElementById("all-users-count").textContent = data.allUsersCount;
            document.getElementById("online-users-count").textContent = data.onlineUsersCount;
            document.getElementById("cheaters-count").textContent = data.cheatersCount;
            document.getElementById("banned-users-count").textContent = data.bannedUsersCount;
            document.getElementById("average-success-rate").textContent = data.averageSuccessRate + "%";

            updateTable("online-users-table", data.onlineUsersList, ["rank", "username", "totalAnswered", "correctAnswers", "successRate"]);
            updateTable("top-users-table", data.topUsersList, ["rank", "username", "totalAnswered", "correctAnswers", "successRate"]);
        } catch {
            const lastUpdate = document.getElementById("last-update");
            if (lastUpdate) lastUpdate.textContent = "(שגיאה בעדכון - מחכה...)";
        }
    }

    function updateTable(tableId, data, columns) {
        const table = document.getElementById(tableId);
        if (!table) return;
        const headerRow = table.querySelector("tr");
        table.innerHTML = "";
        if (headerRow) table.appendChild(headerRow);

        data.forEach((user, index) => {
            const row = table.insertRow();
            columns.forEach((column) => {
                const cell = row.insertCell();
                if (column === "rank") cell.textContent = index + 1;
                else if (column === "successRate") cell.textContent = user[column] + "%";
                else cell.textContent = user[column];
            });
        });
    }

    function startAutoUpdate() {
        updateInterval = setInterval(fetchDashboardData, 3000);
        if (!window.__dashClockInterval) {
            window.__dashClockInterval = setInterval(() => {
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
    }

    function filterByDifficulty(difficulty) {
        const rows = document.querySelectorAll(".difficulty-row");
        const titleEl = document.getElementById("difficulty-title");
        const countEl = document.getElementById("difficulty-count");
        if (!titleEl || !countEl) return;

        if (currentFilter === difficulty) {
            currentFilter = null;
            rows.forEach((row) => row.classList.remove("difficulty-row-hidden"));
            titleEl.textContent = "רשימת שאלות (מעודכן אוטומטית)";
            countEl.textContent = `מציג את כל ${rows.length} השאלות`;
            updateDifficultyCardStates(null);
            return;
        }

        currentFilter = difficulty;
        let visibleCount = 0;
        rows.forEach((row) => {
            if (row.getAttribute("data-difficulty") === difficulty) {
                row.classList.remove("difficulty-row-hidden");
                visibleCount++;
            } else {
                row.classList.add("difficulty-row-hidden");
            }
        });
        const diffText = difficulty === "easy" ? "קלות" : difficulty === "medium" ? "בינוניות" : "קשות";
        titleEl.textContent = `שאלות ${diffText} (${visibleCount} שאלות)`;
        countEl.textContent = `מציג ${visibleCount} שאלות ${diffText}`;
        updateDifficultyCardStates(difficulty);
    }

    function updateDifficultyCardStates(activeDifficulty) {
        const cards = document.querySelectorAll(".difficulty-card[data-difficulty]");
        cards.forEach((card) => {
            const isActive = activeDifficulty !== null && card.dataset.difficulty === activeDifficulty;
            const isDimmed = activeDifficulty !== null && !isActive;
            card.classList.toggle("is-active", isActive);
            card.classList.toggle("is-dimmed", isDimmed);
            card.setAttribute("aria-pressed", isActive ? "true" : "false");
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        const cards = document.querySelectorAll(".difficulty-card[data-difficulty]");
        cards.forEach((card) => {
            card.addEventListener("click", () => filterByDifficulty(card.dataset.difficulty));
        });
    });

    window.addEventListener("load", () => {
        fetchDashboardData();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });
})();
