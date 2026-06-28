(function () {
    const D = window.Dashboard;
    if (!D) return;

    function passesReportFilter(report) {
        if (D.state.reportFilter === "all") return true;
        return report.status === D.state.reportFilter;
    }

    D.renderQuestionReports = function renderQuestionReports(reports) {
        D.state.questionReportsCache = reports || [];
        const tbody = document.getElementById("question-reports-tbody");
        const emptyHint = document.getElementById("question-reports-empty");
        if (!tbody) return;

        const filtered = D.state.questionReportsCache.filter(passesReportFilter);
        if (!filtered.length) {
            tbody.innerHTML = "";
            if (emptyHint) {
                emptyHint.hidden = false;
                emptyHint.textContent = D.state.questionReportsCache.length
                    ? "אין דיווחים בסינון הנוכחי"
                    : "עדיין אין דיווחי שאלות — יופיעו כאן אחרי דיווח מהאתר";
            }
            return;
        }
        if (emptyHint) emptyHint.hidden = true;

        tbody.innerHTML = filtered.map((r) => {
            const isOpen = r.status === "open";
            const statusLabel = isOpen ? "פתוח" : "טופל";
            const statusClass = isOpen ? "dashboard-badge-warn" : "dashboard-badge-ok";
            const viewUrl = `/QuestionView?id=${encodeURIComponent(r.questionId)}&from=dashboard`;
            const actionBtn = isOpen
                ? `<button type="button" class="dashboard-action-btn dashboard-action-btn-sm" data-report-action="resolve" data-report-id="${window.escapeHtml(r.id)}">סמן טופל</button>`
                : `<button type="button" class="dashboard-action-btn dashboard-action-btn-sm" data-report-action="reopen" data-report-id="${window.escapeHtml(r.id)}">פתח מחדש</button>`;
            return `<tr data-report-id="${window.escapeHtml(r.id)}" data-report-status="${window.escapeHtml(r.status)}">
                <td>${D.formatClock(r.createdAtIso)}</td>
                <td>${window.escapeHtml(r.username)}</td>
                <td class="difficulty-question-cell">
                    <span class="difficulty-question-content">
                        <span class="difficulty-question-text" title="${window.escapeHtml(r.questionId)}">${window.escapeHtml(D.formatQuestionLabel(r.questionId))}</span>
                        <a href="${viewUrl}" target="_blank" rel="noopener" class="difficulty-question-link" title="הצג שאלה">🔍</a>
                    </span>
                </td>
                <td>${window.escapeHtml(r.explanation || "—")}</td>
                <td><span class="dashboard-badge ${statusClass}">${statusLabel}</span></td>
                <td>${actionBtn}</td>
            </tr>`;
        }).join("");

        tbody.querySelectorAll("[data-report-action]").forEach((btn) => {
            btn.addEventListener("click", () => D.runReportAction(btn.dataset.reportAction, btn.dataset.reportId));
        });
    };

    D.runReportAction = async function runReportAction(action, id) {
        const status = action === "resolve" ? "resolved" : "open";
        try {
            const res = await fetch("/api/dashboard-report-status", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ id, status })
            });
            if (!res.ok) throw new Error("failed");
            await D.fetchDashboardData(true);
        } catch {
            await D.showDashboardError("עדכון סטטוס הדיווח נכשל");
        }
    };

    D.renderProblematicQuestions = function renderProblematicQuestions(items) {
        const tbody = document.getElementById("problematic-questions-tbody");
        if (!tbody) return;
        if (!items?.length) {
            tbody.innerHTML = '<tr><td colspan="7" class="dashboard-empty-cell">אין שאלות בעייתיות כרגע</td></tr>';
            return;
        }
        tbody.innerHTML = items.map((q) => {
            const diff = D.DIFFICULTY_TEXT[q.difficulty] || q.difficulty || "—";
            const viewUrl = `/QuestionView?id=${encodeURIComponent(q.questionId)}&from=dashboard`;
            return `<tr>
                <td class="difficulty-question-cell"><span class="difficulty-question-text" title="${window.escapeHtml(q.questionId)}">${window.escapeHtml(D.formatQuestionLabel(q.questionId))}</span></td>
                <td>${window.escapeHtml(q.reason)}</td>
                <td>${window.escapeHtml(diff)}</td>
                <td>${q.successRate}%</td>
                <td>${q.totalAttempts}</td>
                <td>${q.openReports}</td>
                <td><a href="${viewUrl}" target="_blank" rel="noopener" class="difficulty-question-link" title="הצג שאלה">🔍</a></td>
            </tr>`;
        }).join("");
    };

    D.expireExam = async function expireExam(token) {
        if (!token) return;
        const confirmed = await D.confirmDashboardAction("לסיים את המבחן הפעיל? המשתמש לא יוכל להמשיך לענות.");
        if (!confirmed) return;
        try {
            const res = await fetch("/api/dashboard-exam-expire", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ token })
            });
            if (!res.ok) throw new Error("failed");
            await D.fetchDashboardData(true);
        } catch {
            await D.showDashboardError("סיום המבחן נכשל");
        }
    };

    D.renderActiveExams = function renderActiveExams(exams) {
        const table = document.getElementById("active-exams-table");
        if (!table) return;
        const headerRow = table.querySelector("tr");
        table.innerHTML = "";
        if (headerRow) table.appendChild(headerRow);

        if (!exams.length) {
            const row = table.insertRow();
            const cell = row.insertCell();
            cell.colSpan = 7;
            cell.className = "dashboard-empty-cell";
            cell.textContent = "אין מבחנים פעילים כרגע";
            return;
        }

        exams.forEach((exam) => {
            const row = table.insertRow();
            const qDisplay = exam.totalQuestions > 0
                ? `${exam.currentIndex + 1}/${exam.totalQuestions}`
                : `${exam.currentIndex + 1}`;

            const userCell = row.insertCell();
            userCell.innerHTML = `<button type="button" class="dashboard-user-link" data-username="${window.escapeHtml(exam.username)}">${window.escapeHtml(exam.username)}</button>`;
            userCell.querySelector(".dashboard-user-link").addEventListener("click", () => D.openUserDetail(exam.username));

            [D.formatClock(exam.startedIso), D.formatRelativeTime(exam.updatedIso), qDisplay, `${exam.score}/${exam.maxScore}`, `${exam.remainingMinutes} דק׳`]
                .forEach((text) => { row.insertCell().textContent = text; });

            const actionCell = row.insertCell();
            actionCell.innerHTML = `<button type="button" class="dashboard-action-btn dashboard-action-btn-sm" data-expire-token="${window.escapeHtml(exam.token)}">סיים מבחן</button>`;
            actionCell.querySelector("[data-expire-token]").addEventListener("click", () => D.expireExam(exam.token));
        });
    };

    D.bindReportUi = function bindReportUi() {
        document.querySelectorAll("#report-status-filters [data-report-filter]").forEach((btn) => {
            btn.addEventListener("click", () => {
                document.querySelectorAll("#report-status-filters [data-report-filter]").forEach((b) => b.classList.remove("is-active"));
                btn.classList.add("is-active");
                D.state.reportFilter = btn.dataset.reportFilter || "open";
                D.renderQuestionReports(D.state.questionReportsCache);
            });
        });
    };
})();
