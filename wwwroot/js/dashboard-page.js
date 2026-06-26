(function () {
    let updateInterval = null;
    let currentFilter = null;
    let userFilter = "all";
    let userSearch = "";
    let allUsersCache = [];

    const DIFFICULTY_LABELS = {
        easy: "קלות",
        medium: "בינוניות",
        hard: "קשות"
    };

    function formatRelativeTime(iso) {
        if (!iso) return "—";
        const then = new Date(iso);
        if (Number.isNaN(then.getTime())) return "—";
        const diffSec = Math.floor((Date.now() - then.getTime()) / 1000);
        if (diffSec < 60) return "לפני פחות מדקה";
        if (diffSec < 3600) return `לפני ${Math.floor(diffSec / 60)} דק׳`;
        if (diffSec < 86400) return `לפני ${Math.floor(diffSec / 3600)} שע׳`;
        return then.toLocaleString("he-IL", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
    }

    function formatClock(iso) {
        if (!iso) return "—";
        const d = new Date(iso);
        if (Number.isNaN(d.getTime())) return "—";
        return d.toLocaleString("he-IL", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
    }

    function escapeHtml(text) {
        return String(text ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function formatQuestionLabel(questionId) {
        if (!questionId) return "—";
        let name = String(questionId).split("/").pop().replace(/\.(png|jpg|jpeg|webp)$/i, "");
        const screenshotMatch = name.match(/^Screenshot at (\w{3}) (\d{1,2}) (\d{2})-(\d{2})-(\d{2})$/i);
        if (screenshotMatch) {
            const months = { Jan: "01", Feb: "02", Mar: "03", Apr: "04", May: "05", Jun: "06", Jul: "07", Aug: "08", Sep: "09", Oct: "10", Nov: "11", Dec: "12" };
            const mon = months[screenshotMatch[1]] || screenshotMatch[1];
            return `${screenshotMatch[2].padStart(2, "0")}/${mon} ${screenshotMatch[3]}:${screenshotMatch[4]}`;
        }
        if (name.length > 28) return `${name.slice(0, 25)}…`;
        return name;
    }

    function formatExamStatus(status) {
        const map = { active: "פעיל", completed: "הושלם", expired: "פג תוקף" };
        return map[status] || status || "—";
    }

    function formatAttemptScore(correct, attempts) {
        return String(correct);
    }

    function formatAttemptTotal(attempts) {
        return String(attempts);
    }

    function resultBadge(wasCorrect) {
        return wasCorrect
            ? '<span class="dashboard-result-badge dashboard-result-badge-ok">נכון</span>'
            : '<span class="dashboard-result-badge dashboard-result-badge-bad">שגוי</span>';
    }

    async function fetchDashboardData() {
        try {
            const response = await fetch(`/api/dashboard-data?_=${Date.now()}`);
            if (!response.ok) throw new Error("dashboard fetch failed");
            const data = await response.json();

            document.getElementById("all-users-count").textContent = data.allUsersCount;
            document.getElementById("online-users-count").textContent = data.onlineUsersCount;
            document.getElementById("cheaters-count").textContent = data.cheatersCount;
            document.getElementById("banned-users-count").textContent = data.bannedUsersCount;
            document.getElementById("average-success-rate").textContent = `${data.averageSuccessRate}%`;

            const activeToday = document.getElementById("active-today-count");
            const answersToday = document.getElementById("answers-today-count");
            if (activeToday) activeToday.textContent = data.activeToday ?? "—";
            if (answersToday) answersToday.textContent = data.answersToday ?? "—";

            allUsersCache = data.allUsersList || [];
            renderAllUsersTable();
            updateTable("online-users-table", data.onlineUsersList, ["rank", "username", "totalAnswered", "correctAnswers", "successRate"], { usernameClickable: true });
            updateTable("top-users-table", data.topUsersList, ["rank", "username", "totalAnswered", "correctAnswers", "successRate"], { usernameClickable: true });
            renderActiveExams(data.activeExams || []);
            renderActivityFeed("live-activity-feed", data.liveActivity || [], true);
            renderActivityFeed("recent-activity-feed", data.recentActivity || [], false);
        } catch {
            const lastUpdate = document.getElementById("last-update");
            if (lastUpdate) lastUpdate.textContent = "(שגיאה בעדכון - מחכה...)";
        }
    }

    function renderActivityFeed(containerId, items, isLive) {
        const container = document.getElementById(containerId);
        if (!container) return;

        if (!items.length) {
            container.innerHTML = `<p class="dashboard-empty-hint">${isLive ? "אין עדיין אירועים חיים — יופיעו אחרי תשובות ומבחנים" : "אין פעילות אחרונה"}</p>`;
            return;
        }

        container.innerHTML = items.map((item) => {
            const kindClass = `dashboard-activity-kind dashboard-activity-kind-${escapeHtml(item.kind || "other")}`;
            return `<div class="dashboard-activity-item">
                <span class="dashboard-activity-time" title="${escapeHtml(item.timestampIso)}">${formatRelativeTime(item.timestampIso)}</span>
                <span class="${kindClass}">${escapeHtml(item.kind || "")}</span>
                <button type="button" class="dashboard-user-link" data-username="${escapeHtml(item.username)}">${escapeHtml(item.username)}</button>
                <span class="dashboard-activity-message">${escapeHtml(item.message)}</span>
            </div>`;
        }).join("");

        container.querySelectorAll(".dashboard-user-link").forEach((btn) => {
            btn.addEventListener("click", () => openUserDetail(btn.dataset.username));
        });
    }

    function renderActiveExams(exams) {
        const table = document.getElementById("active-exams-table");
        if (!table) return;
        const headerRow = table.querySelector("tr");
        table.innerHTML = "";
        if (headerRow) table.appendChild(headerRow);

        if (!exams.length) {
            const row = table.insertRow();
            const cell = row.insertCell();
            cell.colSpan = 6;
            cell.className = "dashboard-empty-cell";
            cell.textContent = "אין מבחנים פעילים כרגע";
            return;
        }

        exams.forEach((exam) => {
            const row = table.insertRow();
            const qDisplay = exam.totalQuestions > 0
                ? `${exam.currentIndex + 1}/${exam.totalQuestions}`
                : `${exam.currentIndex + 1}`;
            const cells = [
                exam.username,
                formatClock(exam.startedIso),
                formatRelativeTime(exam.updatedIso),
                qDisplay,
                `${exam.score}/${exam.maxScore}`,
                `${exam.remainingMinutes} דק׳`
            ];
            cells.forEach((text, i) => {
                const cell = row.insertCell();
                if (i === 0) {
                    cell.innerHTML = `<button type="button" class="dashboard-user-link" data-username="${escapeHtml(exam.username)}">${escapeHtml(exam.username)}</button>`;
                    cell.querySelector(".dashboard-user-link").addEventListener("click", () => openUserDetail(exam.username));
                } else {
                    cell.textContent = text;
                }
            });
        });
    }

    function passesUserFilter(user) {
        if (userSearch && !user.username.toLowerCase().includes(userSearch.toLowerCase())) return false;
        switch (userFilter) {
            case "online": return user.isOnline;
            case "today": return (user.dailyCorrect || 0) > 0;
            case "cheaters": return user.isCheater;
            case "banned": return user.isBanned;
            default: return true;
        }
    }

    function renderAllUsersTable() {
        const table = document.getElementById("all-users-table");
        if (!table) return;
        const headerRow = table.querySelector("tr");
        table.innerHTML = "";
        if (headerRow) table.appendChild(headerRow);

        const filtered = allUsersCache.filter(passesUserFilter);
        if (!filtered.length) {
            const row = table.insertRow();
            const cell = row.insertCell();
            cell.colSpan = 12;
            cell.className = "dashboard-empty-cell";
            cell.textContent = "אין משתמשים להצגה";
            return;
        }

        filtered.forEach((user) => {
            const row = table.insertRow();
            const flags = [];
            if (user.isCheater) flags.push('<span class="dashboard-badge dashboard-badge-warn">cheater</span>');
            if (user.isBanned) flags.push('<span class="dashboard-badge dashboard-badge-danger">חסום</span>');
            const status = user.isOnline
                ? '<span class="dashboard-status-online"><span class="dashboard-online-dot"></span>מחובר</span>'
                : '<span class="dashboard-status-offline">לא מחובר</span>';
            const bestExam = user.bestExamScore > 0
                ? `${user.bestExamCorrect} (${user.bestExamScore})`
                : "—";

            const values = [
                `<button type="button" class="dashboard-user-link" data-username="${escapeHtml(user.username)}">${escapeHtml(user.username)}</button>`,
                status,
                formatRelativeTime(user.lastSeenIso),
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

            values.forEach((html, i) => {
                const cell = row.insertCell();
                if (i === 0) cell.className = "dashboard-sticky-col";
                cell.innerHTML = html;
            });

            row.querySelector(".dashboard-user-link").addEventListener("click", () => openUserDetail(user.username));
        });
    }

    function updateTable(tableId, data, columns, options = {}) {
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
                else if (column === "successRate") cell.textContent = `${user[column]}%`;
                else if (column === "username" && options.usernameClickable) {
                    cell.innerHTML = `<button type="button" class="dashboard-user-link" data-username="${escapeHtml(user[column])}">${escapeHtml(user[column])}</button>`;
                    cell.querySelector(".dashboard-user-link").addEventListener("click", () => openUserDetail(user[column]));
                } else cell.textContent = user[column];
            });
        });
    }

    async function openUserDetail(username) {
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
            renderUserDetail(detail, body, actions);
            body.scrollTop = 0;
        } catch {
            body.innerHTML = '<p class="dashboard-empty-hint">שגיאה בטעינת פרטי משתמש</p>';
        }
    }

    function renderUserDetail(detail, body, actions) {
        const u = detail.user;
        const statusBadge = u.isOnline
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
                    <span class="dashboard-user-stat-value">${formatRelativeTime(u.lastSeenIso)}</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">רמה / XP</span>
                    <span class="dashboard-user-stat-value dashboard-stat-ltr">רמה ${u.level} · ${u.xp} XP</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">היום / השבוע</span>
                    <span class="dashboard-user-stat-value">${u.dailyCorrect} / ${u.weeklyCorrect}</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">סה״כ הצלחה</span>
                    <span class="dashboard-user-stat-value">${u.correctAnswers}/${u.totalAnswered} (${u.successRate}%)</span>
                </div>
                <div class="dashboard-user-stat">
                    <span class="dashboard-user-stat-label">מבחן מיטבי</span>
                    <span class="dashboard-user-stat-value">${u.bestExamScore > 0 ? `${u.bestExamCorrect} נק׳ (${u.bestExamScore})` : "—"}</span>
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

        actions.innerHTML = `
            <button type="button" class="dashboard-action-btn" data-action="toggle-cheater" data-username="${escapeHtml(u.username)}" data-value="${!u.isCheater}">
                ${u.isCheater ? "הסר סימון cheater" : "סמן כ-cheater"}
            </button>
            <button type="button" class="dashboard-action-btn dashboard-action-btn-danger" data-action="toggle-ban" data-username="${escapeHtml(u.username)}" data-value="${!u.isBanned}">
                ${u.isBanned ? "בטל חסימה" : "חסום משתמש"}
            </button>
        `;

        actions.querySelectorAll(".dashboard-action-btn").forEach((btn) => {
            btn.addEventListener("click", () => runUserAction(btn.dataset.action, btn.dataset.username, btn.dataset.value === "true"));
        });
    }

    function renderQuestionsTable(questions) {
        if (!questions?.length) return '<p class="dashboard-empty-hint">אין נתוני שאלות</p>';
        const rows = questions.map((q) => {
            const label = formatQuestionLabel(q.questionId);
            const full = escapeHtml(q.questionId);
            const viewUrl = `/QuestionView?id=${encodeURIComponent(q.questionId)}`;
            return `<tr>
            <td>
                <span class="dashboard-question-cell">
                    <span class="dashboard-question-label" title="${full}">${escapeHtml(label)}</span>
                    <a href="${viewUrl}" target="_blank" rel="noopener" class="difficulty-question-link" title="הצג שאלה">🔍</a>
                </span>
            </td>
            <td class="dashboard-num-cell">${formatAttemptScore(q.correct, q.attempts)}</td>
            <td class="dashboard-num-cell">${formatAttemptTotal(q.attempts)}</td>
            <td title="האם הניסיון האחרון על השאלה היה נכון">${resultBadge(q.lastWasCorrect)}</td>
            <td class="dashboard-time-cell">${formatRelativeTime(q.lastAnsweredIso)}</td>
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
        return achievements.map((a) => `<span class="dashboard-achievement-chip">${escapeHtml(a.emoji)} ${escapeHtml(a.title)}</span>`).join("");
    }

    function renderExamsTable(exams) {
        if (!exams?.length) return '<p class="dashboard-empty-hint">אין מבחנים</p>';
        const rows = exams.map((e) => `<tr>
            <td>${escapeHtml(formatExamStatus(e.status))}</td>
            <td>${e.score}/${e.maxScore}</td>
            <td class="dashboard-time-cell">${formatClock(e.startedIso)}</td>
            <td class="dashboard-time-cell">${e.completedIso ? formatClock(e.completedIso) : "—"}</td>
        </tr>`).join("");
        return `<div class="dashboard-table-shell dashboard-modal-table-shell">
            <table class="dashboard-table dashboard-table-compact dashboard-modal-table">
                <tr><th>סטטוס</th><th>ציון</th><th>התחיל</th><th>הסתיים</th></tr>
                ${rows}
            </table>
        </div>`;
    }

    async function runUserAction(action, username, value) {
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
            await openUserDetail(username);
            fetchDashboardData();
        } catch {
            alert("הפעולה נכשלה");
        }
    }

    function closeUserModal() {
        const modal = document.getElementById("user-detail-modal");
        if (!modal) return;
        modal.hidden = true;
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("dashboard-modal-open");
    }

    function startAutoUpdate() {
        updateInterval = setInterval(fetchDashboardData, 5000);
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

        if (currentFilter === difficulty) {
            currentFilter = null;
            showAllDifficultyRows(rows, titleEl, countEl);
            return;
        }

        currentFilter = difficulty;
        const visibleCount = filterRowsByDifficulty(rows, difficulty);
        const diffText = DIFFICULTY_LABELS[difficulty] || difficulty;
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

        document.querySelectorAll(".dashboard-filter-btn").forEach((btn) => {
            btn.addEventListener("click", () => {
                document.querySelectorAll(".dashboard-filter-btn").forEach((b) => b.classList.remove("is-active"));
                btn.classList.add("is-active");
                userFilter = btn.dataset.filter || "all";
                renderAllUsersTable();
            });
        });

        const search = document.getElementById("user-search");
        if (search) {
            search.addEventListener("input", () => {
                userSearch = search.value.trim();
                renderAllUsersTable();
            });
        }

        document.querySelectorAll("[data-close-modal]").forEach((el) => {
            el.addEventListener("click", closeUserModal);
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") closeUserModal();
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
