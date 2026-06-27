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

    function ensureQuizNotifyStack() {
        let stack = document.getElementById("quiz-notify-stack");
        if (stack) return stack;

        stack = document.createElement("div");
        stack.id = "quiz-notify-stack";
        stack.className = "quiz-notify-stack";
        stack.setAttribute("aria-live", "polite");
        stack.setAttribute("aria-relevant", "additions");
        document.body.appendChild(stack);
        return stack;
    }

    function dismissQuizNotifyCard(card) {
        if (!card || card.dataset.dismissing === "1") return;
        card.dataset.dismissing = "1";
        card.classList.remove("quiz-notify-card--visible");
        card.classList.add("quiz-notify-card--hide");
        setTimeout(() => card.remove(), 280);
    }

    window.pushQuizNotify = function pushQuizNotify(options = {}) {
        const {
            type = "info",
            title = "",
            message = "",
            durationMs = 5500
        } = options;
        const text = (message ?? "").trim();
        if (!text && !title) return null;

        const stack = ensureQuizNotifyStack();
        const card = document.createElement("div");
        card.className = `quiz-notify-card quiz-notify-card--${type}`;
        card.innerHTML = title
            ? `<p class="quiz-notify-title">${title}</p><p class="quiz-notify-message">${message ?? ""}</p>`
            : `<p class="quiz-notify-message">${message ?? ""}</p>`;

        stack.appendChild(card);
        requestAnimationFrame(() => card.classList.add("quiz-notify-card--visible"));

        const maxVisible = 6;
        while (stack.children.length > maxVisible) {
            dismissQuizNotifyCard(stack.firstElementChild);
        }

        const timer = setTimeout(() => dismissQuizNotifyCard(card), durationMs);
        card.addEventListener("click", () => {
            clearTimeout(timer);
            dismissQuizNotifyCard(card);
        });

        return card;
    };

    window.showAppToast = function showAppToast(message, durationMs = 3200) {
        let toast = document.getElementById("app-toast");
        if (!toast) {
            toast = document.createElement("div");
            toast.id = "app-toast";
            toast.className = "app-toast";
            document.body.appendChild(toast);
        }
        toast.textContent = message ?? "";
        toast.classList.add("app-toast-visible");
        clearTimeout(window.__appToastTimer);
        window.__appToastTimer = setTimeout(() => {
            toast.classList.remove("app-toast-visible");
        }, durationMs);
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
