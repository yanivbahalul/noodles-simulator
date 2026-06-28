(function () {
    const dashboard = window.Dashboard;
    if (!dashboard) return;

    function canSupportUser(username) {
        return username && username.toLowerCase() !== "admin";
    }

    function canDeleteUser(username) {
        return username && username.toLowerCase() !== "admin";
    }

    function buildUserOnlineStatusHtml(user) {
        return user.isOnline
            ? '<span class="dashboard-status-online"><span class="dashboard-online-dot"></span>מחובר</span>'
            : '<span class="dashboard-status-offline">לא מחובר</span>';
    }

    function renderQuestionsTable(questions) {
        if (!questions?.length) return '<p class="dashboard-empty-hint">אין נתוני שאלות</p>';
        const rows = questions.map((q) => {
            const label = dashboard.formatQuestionLabel(q.questionId);
            const full = window.escapeHtml(q.questionId);
            const viewUrl = `/QuestionView?id=${encodeURIComponent(q.questionId)}&from=dashboard`;
            return `<tr>
            <td>
                <span class="dashboard-question-cell">
                    <span class="dashboard-question-label" title="${full}">${window.escapeHtml(label)}</span>
                    <a href="${viewUrl}" target="_blank" rel="noopener" class="difficulty-question-link" title="הצג שאלה">🔍</a>
                </span>
            </td>
            <td class="dashboard-num-cell">${dashboard.formatAttemptScore(q.correct)}</td>
            <td class="dashboard-num-cell">${dashboard.formatAttemptTotal(q.attempts)}</td>
            <td title="האם הניסיון האחרון על השאלה היה נכון">${dashboard.resultBadge(q.lastWasCorrect)}</td>
            <td class="dashboard-time-cell">${dashboard.formatRelativeTime(q.lastAnsweredIso)}</td>
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
            <td>${window.escapeHtml(dashboard.formatExamStatus(e.status))}</td>
            <td>${e.score}/${e.maxScore}</td>
            <td class="dashboard-time-cell">${dashboard.formatClock(e.startedIso)}</td>
            <td class="dashboard-time-cell">${e.completedIso ? dashboard.formatClock(e.completedIso) : "—"}</td>
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

    function buildUserStatsHtml(user) {
        const statusBadge = buildUserOnlineStatusHtml(user);
        const bestExam = user.bestExamScore > 0 ? `${user.bestExamCorrect} נק׳ (${user.bestExamScore})` : "—";
        return `
            <div class="dashboard-user-stats">
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">סטטוס</span>
                    <span class="dashboard-user-stat-value">${statusBadge}</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">נראה לאחרונה</span>
                    <span class="dashboard-user-stat-value">${dashboard.formatRelativeTime(user.lastSeenIso)}</span>
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
                    <span class="dashboard-user-stat-value">${bestExam}</span>
                </div>
            </div>`;
    }

    function buildUserActionButtons(user) {
        const safeName = window.escapeHtml(user.username);
        const resetBtn = canSupportUser(user.username)
            ? `<button type="button" class="dashboard-action-btn" data-action="reset-progress" data-username="${safeName}">אפס התקדמות</button>`
            : "";
        const deleteBtn = canDeleteUser(user.username)
            ? `<button type="button" class="dashboard-action-btn dashboard-action-btn-danger" data-action="delete-user" data-username="${safeName}">מחק משתמש לצמיתות</button>`
            : "";
        return `
            ${resetBtn}
            <button type="button" class="dashboard-action-btn" data-action="toggle-cheater" data-username="${safeName}" data-value="${!user.isCheater}">
                ${user.isCheater ? "הסר סימון cheater" : "סמן כ-cheater"}
            </button>
            <button type="button" class="dashboard-action-btn dashboard-action-btn-danger" data-action="toggle-ban" data-username="${safeName}" data-value="${!user.isBanned}">
                ${user.isBanned ? "בטל חסימה" : "חסום משתמש"}
            </button>
            ${deleteBtn}`;
    }

    dashboard.openUserDetail = async function openUserDetail(username) {
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
            dashboard.renderUserDetail(detail, body, actions);
            body.scrollTop = 0;
        } catch {
            body.innerHTML = '<p class="dashboard-empty-hint">שגיאה בטעינת פרטי משתמש</p>';
        }
    };

    dashboard.renderUserDetail = function renderUserDetail(detail, body, actions) {
        const user = detail.user;

        body.innerHTML = `
            ${buildUserStatsHtml(user)}
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
            btn.addEventListener("click", () => dashboard.expireExam(btn.dataset.expireExam));
        });

        actions.innerHTML = buildUserActionButtons(user);

        actions.querySelectorAll(".dashboard-action-btn").forEach((btn) => {
            btn.addEventListener("click", () => dashboard.runUserAction(btn.dataset.action, btn.dataset.username, btn.dataset.value === "true"));
        });
    };

    async function resetUserProgress(username) {
        if (!canSupportUser(username)) return;
        const confirmed = await dashboard.confirmDashboardAction(
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
            await dashboard.openUserDetail(username);
            await dashboard.fetchDashboardData(true);
        } catch {
            await dashboard.showDashboardError("איפוס ההתקדמות נכשל");
        }
    }

    async function requestUserDelete(username) {
        const res = await fetch("/api/dashboard-user-delete", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username })
        });
        if (!res.ok) {
            const msg = await res.text();
            throw new Error(msg || "failed");
        }
    }

    async function deleteUser(username) {
        if (!canDeleteUser(username)) return;
        const confirmed = await dashboard.confirmDashboardAction(
            `האם למחוק את המשתמש "${username}" לצמיתות?\n\nפעולה זו בלתי הפיכה ותמחק את כל הנתונים שלו מהמערכת.`
        );
        if (!confirmed) return;

        try {
            await requestUserDelete(username);
            dashboard.closeUserModal();
            await dashboard.fetchDashboardData(true);
        } catch (err) {
            const suffix = err?.message ? `: ${err.message}` : "";
            await dashboard.showDashboardError(`מחיקת המשתמש נכשלה${suffix}`);
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
            await dashboard.openUserDetail(username);
            await dashboard.fetchDashboardData(true);
        } catch {
            await dashboard.showDashboardError("הפעולה נכשלה");
        }
    }

    dashboard.runUserAction = function runUserAction(action, username, value) {
        if (action === "reset-progress") return resetUserProgress(username);
        if (action === "delete-user") return deleteUser(username);
        return toggleUserProperty(username, action, value);
    };

    dashboard.closeUserModal = function closeUserModal() {
        const modal = document.getElementById("user-detail-modal");
        if (!modal) return;
        modal.hidden = true;
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("dashboard-modal-open");
    };
})();
