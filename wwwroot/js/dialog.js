(function () {
    let dialogRoot = null;

    function ensureDialog() {
        if (dialogRoot) return dialogRoot;

        dialogRoot = document.createElement("div");
        dialogRoot.id = "app-dialog";
        dialogRoot.className = "notice-modal";
        dialogRoot.innerHTML = `
            <div class="notice-modal-content app-dialog-content" role="dialog" aria-modal="true">
                <p class="notice-modal-text app-dialog-message"></p>
                <div class="app-dialog-actions"></div>
            </div>`;
        document.body.appendChild(dialogRoot);
        return dialogRoot;
    }

    function closeDialog() {
        if (dialogRoot) dialogRoot.classList.remove("notice-modal-open");
    }

    function openDialog(message, buttons) {
        const root = ensureDialog();
        const messageEl = root.querySelector(".app-dialog-message");
        const actionsEl = root.querySelector(".app-dialog-actions");
        if (!messageEl || !actionsEl) return Promise.resolve(false);

        messageEl.textContent = message;
        actionsEl.innerHTML = "";

        return new Promise((resolve) => {
            buttons.forEach(({ label, primary, value }) => {
                const btn = document.createElement("button");
                btn.type = "button";
                btn.textContent = label;
                btn.className = primary ? "notice-modal-btn" : "app-dialog-btn-secondary";
                btn.addEventListener("click", () => {
                    closeDialog();
                    resolve(value);
                });
                actionsEl.appendChild(btn);
            });
            root.classList.add("notice-modal-open");
        });
    }

    window.showAppAlert = function showAppAlert(message) {
        return openDialog(message, [{ label: "אישור", primary: true, value: true }]);
    };

    window.showAppConfirm = function showAppConfirm(message) {
        return openDialog(message, [
            { label: "אישור", primary: true, value: true },
            { label: "ביטול", primary: false, value: false }
        ]);
    };

    window.bindConfirmEndTestButtons = function bindConfirmEndTestButtons(message) {
        document.querySelectorAll(".confirm-end-test-btn").forEach((btn) => {
            btn.addEventListener("click", async (e) => {
                e.preventDefault();
                const ok = await window.showAppConfirm(message);
                if (ok) {
                    const form = btn.closest("form");
                    if (form) form.requestSubmit ? form.requestSubmit() : form.submit();
                }
            });
        });
    };
})();
