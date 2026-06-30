(function () {
    const dashboard = window.Dashboard = window.Dashboard || {};

    dashboard.DIFFICULTY_TEXT = { easy: "קל", medium: "בינוני", hard: "קשה" };
    dashboard.DIFFICULTY_LABELS = { easy: "קלות", medium: "בינוניות", hard: "קשות" };

    function formatRelativeSeconds(diffSec) {
        if (diffSec < 60) return "לפני פחות מדקה";
        if (diffSec < 3600) return `לפני ${Math.floor(diffSec / 60)} דק׳`;
        if (diffSec < 86400) return `לפני ${Math.floor(diffSec / 3600)} שע׳`;
        return null;
    }

    dashboard.formatRelativeTime = function formatRelativeTime(iso) {
        if (!iso) return "—";
        const then = new Date(iso);
        if (Number.isNaN(then.getTime())) return "—";
        const diffSec = Math.floor((Date.now() - then.getTime()) / 1000);
        const relative = formatRelativeSeconds(diffSec);
        if (relative) return relative;
        return then.toLocaleString("he-IL", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
    };

    dashboard.formatClock = function formatClock(iso) {
        if (!iso) return "—";
        const date = new Date(iso);
        if (Number.isNaN(date.getTime())) return "—";
        return date.toLocaleString("he-IL", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
    };

    dashboard.formatExamStatus = function formatExamStatus(status) {
        const map = { active: "פעיל", completed: "הושלם", expired: "פג תוקף" };
        return map[status] || status || "—";
    };

    dashboard.formatAttemptScore = function formatAttemptScore(correct) {
        return String(correct);
    };

    dashboard.formatAttemptTotal = function formatAttemptTotal(attempts) {
        return String(attempts);
    };

    dashboard.resultBadge = function resultBadge(wasCorrect) {
        return wasCorrect
            ? '<span class="dashboard-result-badge dashboard-result-badge-ok">נכון</span>'
            : '<span class="dashboard-result-badge dashboard-result-badge-bad">שגוי</span>';
    };
})();
