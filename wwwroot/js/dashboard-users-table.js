(function () {
    const dashboard = window.Dashboard;
    if (!dashboard) return;

    function isInactiveDays(iso, days) {
        if (!iso) return false;
        const then = new Date(iso);
        if (Number.isNaN(then.getTime())) return false;
        return Date.now() - then.getTime() >= days * 86400000;
    }

    const USER_FILTER_PREDICATES = {
        all: () => true,
        online: (user) => user.isOnline,
        today: (user) => (user.dailyCorrect || 0) > 0,
        cheaters: (user) => user.isCheater,
        banned: (user) => user.isBanned,
        inactive7: (user) =>
            !user.isBanned && !user.isCheater && (user.totalAnswered || 0) > 0 && isInactiveDays(user.lastSeenIso, 7)
    };

    function passesUserFilter(user) {
        const search = dashboard.state.userSearch;
        if (search && !user.username.toLowerCase().includes(search.toLowerCase())) return false;
        const predicate = USER_FILTER_PREDICATES[dashboard.state.userFilter] ?? USER_FILTER_PREDICATES.all;
        return predicate(user);
    }

    function buildUserFlagsHtml(user) {
        const flags = [];
        if (user.isCheater) flags.push('<span class="dashboard-badge dashboard-badge-warn">cheater</span>');
        if (user.isBanned) flags.push('<span class="dashboard-badge dashboard-badge-danger">חסום</span>');
        return flags.join(" ") || "—";
    }

    function buildUserOnlineStatusHtml(user) {
        return user.isOnline
            ? '<span class="dashboard-status-online"><span class="dashboard-online-dot"></span>מחובר</span>'
            : '<span class="dashboard-status-offline">לא מחובר</span>';
    }

    function formatBestExamScore(user) {
        return user.bestExamScore > 0 ? `${user.bestExamCorrect} (${user.bestExamScore})` : "—";
    }

    function buildUserRowCells(user) {
        return [
            `<button type="button" class="dashboard-user-link" data-username="${window.escapeHtml(user.username)}">${window.escapeHtml(user.username)}</button>`,
            buildUserOnlineStatusHtml(user),
            dashboard.formatRelativeTime(user.lastSeenIso),
            user.level,
            user.xp,
            user.dailyCorrect,
            user.weeklyCorrect,
            user.totalAnswered,
            user.correctAnswers,
            `${user.successRate}%`,
            formatBestExamScore(user),
            buildUserFlagsHtml(user)
        ];
    }

    function insertUserRow(table, user) {
        const row = table.insertRow();
        const values = buildUserRowCells(user);
        values.forEach((html, i) => {
            const cell = row.insertCell();
            if (i === 0) cell.className = "dashboard-sticky-col";
            cell.innerHTML = html;
        });
        row.querySelector(".dashboard-user-link").addEventListener("click", () => dashboard.openUserDetail(user.username));
    }

    dashboard.renderAllUsersTable = function renderAllUsersTable() {
        const table = document.getElementById("all-users-table");
        if (!table) return;
        const headerRow = table.querySelector("tr");
        table.innerHTML = "";
        if (headerRow) table.appendChild(headerRow);

        const filtered = dashboard.state.allUsersCache.filter(passesUserFilter);
        if (!filtered.length) {
            const row = table.insertRow();
            const cell = row.insertCell();
            cell.colSpan = 12;
            cell.className = "dashboard-empty-cell";
            cell.textContent = "אין משתמשים להצגה";
            return;
        }
        filtered.forEach((user) => insertUserRow(table, user));
    };

    dashboard.bindUserUi = function bindUserUi() {
        document.querySelectorAll(".dashboard-filter-btn[data-filter]").forEach((btn) => {
            btn.addEventListener("click", () => {
                document.querySelectorAll(".dashboard-filter-btn[data-filter]").forEach((b) => b.classList.remove("is-active"));
                btn.classList.add("is-active");
                dashboard.state.userFilter = btn.dataset.filter || "all";
                dashboard.renderAllUsersTable();
            });
        });

        const search = document.getElementById("user-search");
        if (search) {
            search.addEventListener("input", () => {
                dashboard.state.userSearch = search.value.trim();
                dashboard.renderAllUsersTable();
            });
        }

        document.querySelectorAll("[data-close-modal]").forEach((el) => {
            el.addEventListener("click", dashboard.closeUserModal);
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") dashboard.closeUserModal();
        });
    };
})();
