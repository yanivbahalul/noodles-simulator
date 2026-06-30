(function () {
    const dashboard = window.Dashboard;
    if (!dashboard) return;

    function passesReportFilter(report) {
        if (dashboard.state.reportFilter === "all") return true;
        return report.status === dashboard.state.reportFilter;
    }

    function reportEmptyMessage(cacheLength) {
        return cacheLength
            ? "אין דיווחים בסינון הנוכחי"
            : "עדיין אין דיווחי שאלות — יופיעו כאן אחרי דיווח מהאתר";
    }

    function buildQuestionReportRowHtml(report) {
        const isOpen = report.status === "open";
        const statusLabel = isOpen ? "פתוח" : "טופל";
        const statusClass = isOpen ? "dashboard-badge-warn" : "dashboard-badge-ok";
        const viewUrl = `/QuestionView?id=${encodeURIComponent(report.questionId)}&from=dashboard`;
        const actionBtn = isOpen
            ? `<button type="button" class="dashboard-action-btn dashboard-action-btn-sm" data-report-action="resolve" data-report-id="${window.escapeHtml(report.id)}">סמן טופל</button>`
            : `<button type="button" class="dashboard-action-btn dashboard-action-btn-sm" data-report-action="reopen" data-report-id="${window.escapeHtml(report.id)}">פתח מחדש</button>`;
        return `<tr data-report-id="${window.escapeHtml(report.id)}" data-report-status="${window.escapeHtml(report.status)}">
            <td>${dashboard.formatClock(report.createdAtIso)}</td>
            <td>${window.escapeHtml(report.username)}</td>
            <td class="difficulty-question-cell">
                <span class="difficulty-question-content">
                    <span class="difficulty-question-text" title="${window.escapeHtml(report.questionId)}">${window.escapeHtml(window.formatQuestionLabel(report.questionId))}</span>
                    <a href="${viewUrl}" target="_blank" rel="noopener" class="difficulty-question-link" title="הצג שאלה">🔍</a>
                </span>
            </td>
            <td>${window.escapeHtml(report.explanation || "—")}</td>
            <td><span class="dashboard-badge ${statusClass}">${statusLabel}</span></td>
            <td>${actionBtn}</td>
        </tr>`;
    }

    function bindReportActionButtons(tbody) {
        tbody.querySelectorAll("[data-report-action]").forEach((btn) => {
            btn.addEventListener("click", () => dashboard.runReportAction(btn.dataset.reportAction, btn.dataset.reportId));
        });
    }

    function showEmptyQuestionReports(tbody, emptyHint, cacheLength) {
        tbody.innerHTML = "";
        if (!emptyHint) return;
        emptyHint.hidden = false;
        emptyHint.textContent = reportEmptyMessage(cacheLength);
    }

    dashboard.renderQuestionReports = function renderQuestionReports(reports) {
        dashboard.state.questionReportsCache = reports || [];
        const tbody = document.getElementById("question-reports-tbody");
        const emptyHint = document.getElementById("question-reports-empty");
        if (!tbody) return;

        const filtered = dashboard.state.questionReportsCache.filter(passesReportFilter);
        if (!filtered.length) {
            showEmptyQuestionReports(tbody, emptyHint, dashboard.state.questionReportsCache.length);
            return;
        }
        if (emptyHint) emptyHint.hidden = true;

        tbody.innerHTML = filtered.map(buildQuestionReportRowHtml).join("");
        bindReportActionButtons(tbody);
    };

    dashboard.runReportAction = async function runReportAction(action, id) {
        const status = action === "resolve" ? "resolved" : "open";
        try {
            const res = await fetch("/api/dashboard-report-status", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ id, status })
            });
            if (!res.ok) throw new Error("failed");
            await dashboard.fetchDashboardData(true);
        } catch {
            await dashboard.showDashboardError("עדכון סטטוס הדיווח נכשל");
        }
    };

    dashboard.renderProblematicQuestions = function renderProblematicQuestions(items) {
        const tbody = document.getElementById("problematic-questions-tbody");
        if (!tbody) return;
        if (!items?.length) {
            tbody.innerHTML = '<tr><td colspan="7" class="dashboard-empty-cell">אין שאלות בעייתיות כרגע</td></tr>';
            return;
        }
        tbody.innerHTML = items.map((q) => {
            const diff = dashboard.DIFFICULTY_TEXT[q.difficulty] || q.difficulty || "—";
            const viewUrl = `/QuestionView?id=${encodeURIComponent(q.questionId)}&from=dashboard`;
            return `<tr>
                <td class="difficulty-question-cell"><span class="difficulty-question-text" title="${window.escapeHtml(q.questionId)}">${window.escapeHtml(window.formatQuestionLabel(q.questionId))}</span></td>
                <td>${window.escapeHtml(q.reason)}</td>
                <td>${window.escapeHtml(diff)}</td>
                <td>${q.successRate}%</td>
                <td>${q.totalAttempts}</td>
                <td>${q.openReports}</td>
                <td><a href="${viewUrl}" target="_blank" rel="noopener" class="difficulty-question-link" title="הצג שאלה">🔍</a></td>
            </tr>`;
        }).join("");
    };

    dashboard.expireExam = async function expireExam(token) {
        if (!token) return;
        const confirmed = await dashboard.confirmDashboardAction("לסיים את המבחן הפעיל? המשתמש לא יוכל להמשיך לענות.");
        if (!confirmed) return;
        try {
            const res = await fetch("/api/dashboard-exam-expire", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ token })
            });
            if (!res.ok) throw new Error("failed");
            await dashboard.fetchDashboardData(true);
        } catch {
            await dashboard.showDashboardError("סיום המבחן נכשל");
        }
    };

    dashboard.renderActiveExams = function renderActiveExams(exams) {
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
            userCell.querySelector(".dashboard-user-link").addEventListener("click", () => dashboard.openUserDetail(exam.username));

            [dashboard.formatClock(exam.startedIso), dashboard.formatRelativeTime(exam.updatedIso), qDisplay, `${exam.score}/${exam.maxScore}`, `${exam.remainingMinutes} דק׳`]
                .forEach((text) => { row.insertCell().textContent = text; });

            const actionCell = row.insertCell();
            actionCell.innerHTML = `<button type="button" class="dashboard-action-btn dashboard-action-btn-sm" data-expire-token="${window.escapeHtml(exam.token)}">סיים מבחן</button>`;
            actionCell.querySelector("[data-expire-token]").addEventListener("click", () => dashboard.expireExam(exam.token));
        });
    };

    dashboard.bindReportUi = function bindReportUi() {
        document.querySelectorAll("#report-status-filters [data-report-filter]").forEach((btn) => {
            btn.addEventListener("click", () => {
                document.querySelectorAll("#report-status-filters [data-report-filter]").forEach((b) => b.classList.remove("is-active"));
                btn.classList.add("is-active");
                dashboard.state.reportFilter = btn.dataset.reportFilter || "open";
                dashboard.renderQuestionReports(dashboard.state.questionReportsCache);
            });
        });
    };
})();
