(function () {
    window.escapeHtml = function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text ?? "";
        return div.innerHTML;
    };

    window.setText = function setText(id, value) {
        const el = document.getElementById(id);
        if (el) el.textContent = value ?? "";
    };

    // ponytail: keep in sync with Models/QuestionLabel.cs and tools/check_question_label_sync.py
    window.formatQuestionLabel = function formatQuestionLabel(questionId) {
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

    window.replayCssAnimation = function replayCssAnimation(el) {
        if (!el) return;
        // ponytail: offsetWidth read forces reflow so CSS animation restarts
        el.offsetWidth;
    };

    window.notifyAppError = async function notifyAppError(message) {
        if (window.showAppToast) {
            window.showAppToast(message);
            return;
        }
        if (window.showAppAlert) {
            await window.showAppAlert(message);
        }
    };

    window.confirmAppAction = async function confirmAppAction(message) {
        if (window.showAppConfirm) return await window.showAppConfirm(message);
        return window.confirm(message);
    };

    window.ignoreBackgroundError = function ignoreBackgroundError() {
        // ponytail: best-effort background telemetry — failures are non-fatal
    };
})();
