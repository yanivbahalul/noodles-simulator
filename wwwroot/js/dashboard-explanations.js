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
                            ${window.escapeHtml?.(item.questionLabel || item.questionFile) ?? (item.questionLabel || item.questionFile)}
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

    function starsDisplay(avg) {
        const n = Math.max(0, Math.min(5, Math.round(Number(avg) || 0)));
        return "★".repeat(n) + "☆".repeat(5 - n);
    }

    dashboard.loadExplanationRatings = async function loadExplanationRatings() {
        const tbody = document.getElementById("explanation-ratings-tbody");
        if (!tbody) return;

        try {
            const res = await fetch("/api/question-explanation-ratings", { credentials: "same-origin" });
            if (!res.ok) throw new Error("ratings fetch failed");
            const data = await res.json();
            if (!data.enabled) {
                tbody.innerHTML = '<tr><td colspan="6">דירוגים לא זמינים (הרץ SQL ב-Supabase)</td></tr>';
                return;
            }
            const items = Array.isArray(data.items) ? data.items : [];
            if (items.length === 0) {
                tbody.innerHTML = '<tr><td colspan="6">עדיין אין דירוגים ממשתמשים</td></tr>';
                return;
            }

            tbody.innerHTML = items.map((item, idx) => {
                const urgent = Number(item.avgStars) <= 3 || Number(item.lowCount) > 0;
                const feedback = (item.recentFeedback || []).filter(Boolean).join(" · ") || "—";
                const viewUrl = `/QuestionView?id=${encodeURIComponent(item.questionFile)}&from=dashboard`;
                return `<tr>
                    <td class="${urgent ? "explanation-rating-urgent" : ""}">${idx + 1}</td>
                    <td class="difficulty-question-cell">
                        <span class="difficulty-question-content">
                            <span class="difficulty-question-text" title="${window.escapeHtml?.(item.questionFile) ?? item.questionFile}">
                                ${window.escapeHtml?.(item.questionLabel || item.questionFile) ?? (item.questionLabel || item.questionFile)}
                            </span>
                            <a href="${viewUrl}" target="_blank" title="הצג שאלה" class="difficulty-question-link">🔍</a>
                        </span>
                    </td>
                    <td><span class="explanation-rating-stars-display" title="${item.avgStars}">${starsDisplay(item.avgStars)}</span> (${item.avgStars})</td>
                    <td>${item.ratingCount ?? 0}</td>
                    <td>${item.lowCount ?? 0}</td>
                    <td>${window.escapeHtml?.(feedback) ?? feedback}</td>
                </tr>`;
            }).join("");
        } catch {
            tbody.innerHTML = '<tr><td colspan="6">שגיאה בטעינת דירוגים</td></tr>';
        }
    };
})();
