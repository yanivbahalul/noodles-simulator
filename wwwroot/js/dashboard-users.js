(function () {
    const D = window.Dashboard;
    if (!D) return;

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
        const search = D.state.userSearch;
        if (search && !user.username.toLowerCase().includes(search.toLowerCase())) return false;
        const predicate = USER_FILTER_PREDICATES[D.state.userFilter] ?? USER_FILTER_PREDICATES.all;
        return predicate(user);
    }

    function canSupportUser(username) {
        return username && username.toLowerCase() !== "admin";
    }

    function canDeleteUser(username) {
        return username && username.toLowerCase() !== "admin";
    }

    function buildUserRowCells(user) {
        const flags = [];
        if (user.isCheater) flags.push('<span class="dashboard-badge dashboard-badge-warn">cheater</span>');
        if (user.isBanned) flags.push('<span class="dashboard-badge dashboard-badge-danger">חסום</span>');
        const status = user.isOnline
            ? '<span class="dashboard-status-online"><span class="dashboard-online-dot"></span>מחובר</span>'
            : '<span class="dashboard-status-offline">לא מחובר</span>';
        const bestExam = user.bestExamScore > 0
            ? `${user.bestExamCorrect} (${user.bestExamScore})`
            : "—";
        return [
            `<button type="button" class="dashboard-user-link" data-username="${window.escapeHtml(user.username)}">${window.escapeHtml(user.username)}</button>`,
            status,
            D.formatRelativeTime(user.lastSeenIso),
            user.level,
            user.xp,
            user.dailyCorrect,
            user.weeklyCorrect,
            user.totalAnswered,
            user.correctAnswers,
            `${user.successRate}%`,
            bestExam,
            flags.join(" ") || "—"
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
        row.querySelector(".dashboard-user-link").addEventListener("click", () => D.openUserDetail(user.username));
    }

    D.renderAllUsersTable = function renderAllUsersTable() {
        const table = document.getElementById("all-users-table");
        if (!table) return;
        const headerRow = table.querySelector("tr");
        table.innerHTML = "";
        if (headerRow) table.appendChild(headerRow);

        const filtered = D.state.allUsersCache.filter(passesUserFilter);
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

    D.openUserDetail = async function openUserDetail(username) {
        const modal = document.getElementById("user-detail-modal");
        const body = document.getElementById("user-detail-body");
        const actions = document.getElementById("user-detail-actions");
        const title = document.getElementById("user-detail-title");
        if (!modal || !body) return;

        title.textContent = `פרטי משתמש: ${username}`;
        body.innerHTML = '<p class="dashboard-empty-hint">טוען...</p>';
        actions.innerHTML = "";
        modal.hidden = false;
        modal.setAttribute("aria-hidden", "false");
        document.body.classList.add("dashboard-modal-open");

        try {
            const res = await fetch(`/api/dashboard-user?username=${encodeURIComponent(username)}`);
            if (!res.ok) throw new Error("failed");
            const detail = await res.json();
            D.renderUserDetail(detail, body, actions);
            body.scrollTop = 0;
        } catch {
            body.innerHTML = '<p class="dashboard-empty-hint">שגיאה בטעינת פרטי משתמש</p>';
        }
    };

    function renderQuestionsTable(questions) {
        if (!questions?.length) return '<p class="dashboard-empty-hint">אין נתוני שאלות</p>';
        const rows = questions.map((q) => {
            const label = D.formatQuestionLabel(q.questionId);
            const full = window.escapeHtml(q.questionId);
            const viewUrl = `/QuestionView?id=${encodeURIComponent(q.questionId)}&from=dashboard`;
            return `<tr>
            <td>
                <span class="dashboard-question-cell">
                    <span class="dashboard-question-label" title="${full}">${window.escapeHtml(label)}</span>
                    <a href="${viewUrl}" target="_blank" rel="noopener" class="difficulty-question-link" title="הצג שאלה">🔍</a>
                </span>
            </td>
            <td class="dashboard-num-cell">${D.formatAttemptScore(q.correct)}</td>
            <td class="dashboard-num-cell">${D.formatAttemptTotal(q.attempts)}</td>
            <td title="האם הניסיון האחרון על השאלה היה נכון">${D.resultBadge(q.lastWasCorrect)}</td>
            <td class="dashboard-time-cell">${D.formatRelativeTime(q.lastAnsweredIso)}</td>
        </tr>`;
        }).join("");
        return `<div class="dashboard-table-shell dashboard-modal-table-shell">
            <table class="dashboard-table dashboard-table-compact dashboard-modal-table">
                <tr>
                    <th>שאלה</th>
                    <th>נכונות</th>
                    <th>ניסיונות</th>
                    <th title="האם הניסיון האחרון היה נכון">ניסיון אחרון</th>
                    <th>מתי</th>
                </tr>
                ${rows}
            </table>
        </div>`;
    }

    function renderAchievementChips(achievements) {
        if (!achievements?.length) return '<p class="dashboard-empty-hint">אין הישגים</p>';
        return achievements.map((a) => `<span class="dashboard-achievement-chip">${window.escapeHtml(a.emoji)} ${window.escapeHtml(a.title)}</span>`).join("");
    }

    function renderExamsTable(exams) {
        if (!exams?.length) return '<p class="dashboard-empty-hint">אין מבחנים</p>';
        const rows = exams.map((e) => {
            const expireBtn = e.status === "active"
                ? `<button type="button" class="dashboard-action-btn dashboard-action-btn-sm" data-expire-exam="${window.escapeHtml(e.token)}">סיים</button>`
                : "—";
            return `<tr>
            <td>${window.escapeHtml(D.formatExamStatus(e.status))}</td>
            <td>${e.score}/${e.maxScore}</td>
            <td class="dashboard-time-cell">${D.formatClock(e.startedIso)}</td>
            <td class="dashboard-time-cell">${e.completedIso ? D.formatClock(e.completedIso) : "—"}</td>
            <td>${expireBtn}</td>
        </tr>`;
        }).join("");
        return `<div class="dashboard-table-shell dashboard-modal-table-shell">
            <table class="dashboard-table dashboard-table-compact dashboard-modal-table">
                <tr><th>סטטוס</th><th>ציון</th><th>התחיל</th><th>הסתיים</th><th>פעולה</th></tr>
                ${rows}
            </table>
        </div>`;
    }

    D.renderUserDetail = function renderUserDetail(detail, body, actions) {
        const user = detail.user;
        const statusBadge = user.isOnline
            ? '<span class="dashboard-status-online"><span class="dashboard-online-dot"></span>מחובר</span>'
            : '<span class="dashboard-status-offline">לא מחובר</span>';

        body.innerHTML = `
            <div class="dashboard-user-stats">
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">סטטוס</span>
                    <span class="dashboard-user-stat-value">${statusBadge}</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">נראה לאחרונה</span>
                    <span class="dashboard-user-stat-value">${D.formatRelativeTime(user.lastSeenIso)}</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">רמה / XP</span>
                    <span class="dashboard-user-stat-value dashboard-stat-ltr">רמה ${user.level} · ${user.xp} XP</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">היום / השבוע</span>
                    <span class="dashboard-user-stat-value">${user.dailyCorrect} / ${user.weeklyCorrect}</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">סה״כ הצלחה</span>
                    <span class="dashboard-user-stat-value">${user.correctAnswers}/${user.totalAnswered} (${user.successRate}%)</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">מבחן מיטבי</span>
                    <span class="dashboard-user-stat-value">${user.bestExamScore > 0 ? `${user.bestExamCorrect} נק׳ (${user.bestExamScore})` : "—"}</span>
                </div>
            </div>
            <section class="dashboard-modal-section">
                <h4 class="dashboard-detail-subtitle">שאלות אחרונות</h4>
                ${renderQuestionsTable(detail.recentQuestions)}
            </section>
            <section class="dashboard-modal-section">
                <h4 class="dashboard-detail-subtitle">הישגים <span class="dashboard-detail-count">${(detail.achievements || []).length}</span></h4>
                <div class="dashboard-achievement-chips">${renderAchievementChips(detail.achievements)}</div>
            </section>
            <section class="dashboard-modal-section">
                <h4 class="dashboard-detail-subtitle">מבחנים</h4>
                ${renderExamsTable(detail.exams)}
            </section>
        `;

        body.querySelectorAll("[data-expire-exam]").forEach((btn) => {
            btn.addEventListener("click", () => D.expireExam(btn.dataset.expireExam));
        });

        actions.innerHTML = `
            ${canSupportUser(user.username) ? `
            <button type="button" class="dashboard-action-btn" data-action="reset-progress" data-username="${window.escapeHtml(user.username)}">
                אפס התקדמות
            </button>` : ""}
            <button type="button" class="dashboard-action-btn" data-action="toggle-cheater" data-username="${window.escapeHtml(user.username)}" data-value="${!user.isCheater}">
                ${user.isCheater ? "הסר סימון cheater" : "סמן כ-cheater"}
            </button>
            <button type="button" class="dashboard-action-btn dashboard-action-btn-danger" data-action="toggle-ban" data-username="${window.escapeHtml(user.username)}" data-value="${!user.isBanned}">
                ${user.isBanned ? "בטל חסימה" : "חסום משתמש"}
            </button>
            ${canDeleteUser(user.username) ? `
            <button type="button" class="dashboard-action-btn dashboard-action-btn-danger" data-action="delete-user" data-username="${window.escapeHtml(user.username)}">
                מחק משתמש לצמיתות
            </button>` : ""}
        `;

        actions.querySelectorAll(".dashboard-action-btn").forEach((btn) => {
            btn.addEventListener("click", () => D.runUserAction(btn.dataset.action, btn.dataset.username, btn.dataset.value === "true"));
        });
    };

    async function resetUserProgress(username) {
        if (!canSupportUser(username)) return;
        const confirmed = await D.confirmDashboardAction(
            `לאפס את כל ההתקדמות של "${username}"?\n\nהמשתמש יאבד XP, הישגים, וסטטיסטיקות — פעולה בלתי הפיכה.`
        );
        if (!confirmed) return;
        try {
            const res = await fetch("/api/dashboard-user-reset", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ username })
            });
            if (!res.ok) throw new Error("failed");
            await D.openUserDetail(username);
            await D.fetchDashboardData(true);
        } catch {
            await D.showDashboardError("איפוס ההתקדמות נכשל");
        }
    }

    async function deleteUser(username) {
        if (!canDeleteUser(username)) return;
        const confirmed = await D.confirmDashboardAction(
            `האם למחוק את המשתמש "${username}" לצמיתות?\n\nפעולה זו בלתי הפיכה ותמחק את כל הנתונים שלו מהמערכת.`
        );
        if (!confirmed) return;

        try {
            const res = await fetch("/api/dashboard-user-delete", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ username })
            });
            if (!res.ok) {
                const msg = await res.text();
                throw new Error(msg || "failed");
            }
            D.closeUserModal();
            await D.fetchDashboardData(true);
        } catch (err) {
            const suffix = err?.message ? `: ${err.message}` : "";
            await D.showDashboardError(`מחיקת המשתמש נכשלה${suffix}`);
        }
    }

    async function toggleUserProperty(username, action, value) {
        const payload = { username };
        if (action === "toggle-cheater") payload.isCheater = value;
        if (action === "toggle-ban") payload.isBanned = value;

        try {
            const res = await fetch("/api/dashboard-user-action", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            if (!res.ok) throw new Error("failed");
            await D.openUserDetail(username);
            await D.fetchDashboardData(true);
        } catch {
            await D.showDashboardError("הפעולה נכשלה");
        }
    }

    D.runUserAction = async function runUserAction(action, username, value) {
        if (action === "reset-progress") return resetUserProgress(username);
        if (action === "delete-user") return deleteUser(username);
        return toggleUserProperty(username, action, value);
    };

    D.closeUserModal = function closeUserModal() {
        const modal = document.getElementById("user-detail-modal");
        if (!modal) return;
        modal.hidden = true;
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("dashboard-modal-open");
    };

    D.bindUserUi = function bindUserUi() {
        document.querySelectorAll(".dashboard-filter-btn[data-filter]").forEach((btn) => {
            btn.addEventListener("click", () => {
                document.querySelectorAll(".dashboard-filter-btn[data-filter]").forEach((b) => b.classList.remove("is-active"));
                btn.classList.add("is-active");
                D.state.userFilter = btn.dataset.filter || "all";
                D.renderAllUsersTable();
            });
        });

        const search = document.getElementById("user-search");
        if (search) {
            search.addEventListener("input", () => {
                D.state.userSearch = search.value.trim();
                D.renderAllUsersTable();
            });
        }

        document.querySelectorAll("[data-close-modal]").forEach((el) => {
            el.addEventListener("click", D.closeUserModal);
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") D.closeUserModal();
        });
    };
})();
