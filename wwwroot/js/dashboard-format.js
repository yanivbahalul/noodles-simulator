(function () {
    const D = window.Dashboard = window.Dashboard || {};

    D.DIFFICULTY_TEXT = { easy: "קל", medium: "בינוני", hard: "קשה" };
    D.DIFFICULTY_LABELS = { easy: "קלות", medium: "בינוניות", hard: "קשות" };

    function formatRelativeSeconds(diffSec) {
        if (diffSec < 60) return "לפני פחות מדקה";
        if (diffSec < 3600) return `לפני ${Math.floor(diffSec / 60)} דק׳`;
        if (diffSec < 86400) return `לפני ${Math.floor(diffSec / 3600)} שע׳`;
        return null;
    }

    D.formatRelativeTime = function formatRelativeTime(iso) {
        if (!iso) return "—";
        const then = new Date(iso);
        if (Number.isNaN(then.getTime())) return "—";
        const diffSec = Math.floor((Date.now() - then.getTime()) / 1000);
        const relative = formatRelativeSeconds(diffSec);
        if (relative) return relative;
        return then.toLocaleString("he-IL", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
    };

    D.formatClock = function formatClock(iso) {
        if (!iso) return "—";
        const date = new Date(iso);
        if (Number.isNaN(date.getTime())) return "—";
        return date.toLocaleString("he-IL", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
    };

    D.formatQuestionLabel = function formatQuestionLabel(questionId) {
        if (!questionId) return "—";
        const name = String(questionId).split("/").pop().replace(/\.(png|jpg|jpeg|webp)$/i, "");
        const screenshotMatch = name.match(/^Screenshot at (\w{3}) (\d{1,2}) (\d{2})-(\d{2})-(\d{2})$/i);
        if (screenshotMatch) {
            const months = { Jan: "01", Feb: "02", Mar: "03", Apr: "04", May: "05", Jun: "06", Jul: "07", Aug: "08", Sep: "09", Oct: "10", Nov: "11", Dec: "12" };
            const mon = months[screenshotMatch[1]] || screenshotMatch[1];
            return `${screenshotMatch[2].padStart(2, "0")}/${mon} ${screenshotMatch[3]}:${screenshotMatch[4]}`;
        }
        if (name.length > 28) return `${name.slice(0, 25)}…`;
        return name;
    };

    D.formatExamStatus = function formatExamStatus(status) {
        const map = { active: "פעיל", completed: "הושלם", expired: "פג תוקף" };
        return map[status] || status || "—";
    };

    D.formatAttemptScore = function formatAttemptScore(correct) {
        return String(correct);
    };

    D.formatAttemptTotal = function formatAttemptTotal(attempts) {
        return String(attempts);
    };

    D.resultBadge = function resultBadge(wasCorrect) {
        return wasCorrect
            ? '<span class="dashboard-result-badge dashboard-result-badge-ok">נכון</span>'
            : '<span class="dashboard-result-badge dashboard-result-badge-bad">שגוי</span>';
    };
})();
