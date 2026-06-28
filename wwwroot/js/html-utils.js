(function () {
    window.escapeHtml = function escapeHtml(text) {
        const div = document.createElement("div");
        div.textContent = text ?? "";
        return div.innerHTML;
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
