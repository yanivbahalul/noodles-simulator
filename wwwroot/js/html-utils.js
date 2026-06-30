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

    window.replayCssAnimation = function replayCssAnimation(el) {
        if (!el) return;
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
    };
})();
