(function () {
    const dashboard = window.Dashboard = window.Dashboard || {};

    function formatIso(iso) {
        if (!iso) return "—";
        try {
            return new Date(iso).toLocaleString("he-IL", { dateStyle: "short", timeStyle: "short" });
        } catch {
            return iso;
        }
    }

    function statusLabel(status) {
        switch ((status || "").toLowerCase()) {
            case "ready": return "מוכן";
            case "pending": return "בתהליך";
            case "failed": return "נכשל";
            case "needs_review": return "לבדיקה";
            default: return status || "—";
        }
    }

    dashboard.loadExplanationVideos = async function loadExplanationVideos() {
        const tbody = document.getElementById("explanation-videos-tbody");
        if (!tbody) return;

        try {
            const res = await fetch("/api/question-explanations-status", { credentials: "same-origin" });
            if (!res.ok) throw new Error("status fetch failed");
            const data = await res.json();
            const s = data.summary ?? {};

            dashboard.setText?.("explanation-ready-count", s.ready ?? 0);
            dashboard.setText?.("explanation-missing-count", s.missing ?? 0);
            dashboard.setText?.("explanation-failed-count", s.failed ?? 0);
            dashboard.setText?.("explanation-review-count", s.needsReview ?? 0);

            const items = Array.isArray(data.items) ? data.items : [];
            if (!data.enabled) {
                tbody.innerHTML = '<tr><td colspan="4">שירות הסברים לא זמין (Supabase)</td></tr>';
                return;
            }
            if (items.length === 0) {
                tbody.innerHTML = '<tr><td colspan="4">עדיין לא נוצרו הסברים — הרץ את ה-pipeline</td></tr>';
                return;
            }

            tbody.innerHTML = items.map((item) => `
                <tr>
                    <td class="difficulty-question-cell">
                        <span class="difficulty-question-text" title="${window.escapeHtml?.(item.questionFile) ?? item.questionFile}">
                            ${window.escapeHtml?.(dashboard.formatQuestionLabel?.(item.questionFile) ?? item.questionFile) ?? item.questionFile}
                        </span>
                    </td>
                    <td>${window.escapeHtml?.(statusLabel(item.status)) ?? statusLabel(item.status)}</td>
                    <td>${formatIso(item.generatedAt)}</td>
                    <td>${window.escapeHtml?.(item.errorMessage || "—") ?? (item.errorMessage || "—")}</td>
                </tr>
            `).join("");
        } catch {
            tbody.innerHTML = '<tr><td colspan="4">שגיאה בטעינת סטטוס הסברים</td></tr>';
        }
    };
})();
