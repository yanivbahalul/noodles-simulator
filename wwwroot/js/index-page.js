(function () {
    let updateInterval = null;

    function openImageModal() {
        const modal = document.getElementById("image-modal");
        const modalImg = document.getElementById("modal-img");
        const mainImg = document.getElementById("main-question-image");
        if (!modal || !modalImg || !mainImg) return;
        modal.classList.add("modal-open");
        modalImg.src = mainImg.src;
    }

    function closeImageModal() {
        const modal = document.getElementById("image-modal");
        if (modal) modal.classList.remove("modal-open");
    }

    function closeAppDialog() {
        const dialog = document.getElementById("app-dialog");
        if (dialog) dialog.classList.remove("notice-modal-open");
    }

    function dismissExamFixNotice() {
        const modal = document.getElementById("exam-fix-notice-modal");
        const prompt = document.getElementById("exam-fix-notice-prompt");
        if (!modal) return;
        modal.classList.remove("difficulty-modal-open");
        const noticeId = prompt?.dataset.noticeId;
        if (!noticeId) return;
        fetch("/api/notices/dismiss", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ noticeId })
        }).catch(ignoreDismissError);
    }

    function ignoreDismissError() {
        // Best-effort dismiss after the modal is already closed.
    }

    function openDifficultyModal() {
        closeImageModal();
        closeAppDialog();
        dismissExamFixNotice();
        const modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.add("difficulty-modal-open");
    }

    function closeDifficultyModal() {
        const modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    function setText(id, value) {
        const el = document.getElementById(id);
        if (el) el.textContent = value;
    }

    function applyStatsData(data) {
        if (data?.correct === undefined) return;
        setText("stat-correct", data.correct);
        setText("stat-total", data.total);
        setText("stat-success", `${data.successRate}%`);
    }

    function applyOnlineCount(data) {
        if (data?.online === undefined || data.online === null) return;
        setText("online-count", data.online);
    }

    async function fetchStats() {
        try {
            const res = await fetch(`/Stats?_=${Date.now()}`);
            if (!res.ok) throw new Error("stats fetch failed");
            applyStatsData(await res.json());
        } catch {
            // keep existing values
        }
    }

    async function fetchOnlineCount() {
        try {
            const res = await fetch(`/api/online-count?_=${Date.now()}`);
            if (!res.ok) throw new Error("online fetch failed");
            applyOnlineCount(await res.json());
        } catch {
            // keep existing values
        }
    }

    function toggleStats() {
        const panel = document.getElementById("stats-panel");
        const toggle = document.getElementById("footer-stats-toggle");
        if (!panel) return;
        const isOpen = !panel.classList.contains("hidden");
        panel.classList.toggle("hidden");
        if (toggle) {
            toggle.classList.toggle("footer-stats-toggle-open", !isOpen);
            toggle.classList.toggle("footer-stats-toggle-closed", isOpen);
        }
        if (!isOpen) {
            fetchStats();
            fetchOnlineCount();
        }
    }

    function startAutoUpdate() {
        updateInterval = setInterval(() => {
            fetchStats();
            fetchOnlineCount();
        }, 5000);
    }

    function stopAutoUpdate() {
        if (updateInterval) {
            clearInterval(updateInterval);
            updateInterval = null;
        }
    }

    function bindDismissHandler(element, dismiss) {
        if (element) element.addEventListener("click", dismiss);
    }

    function bindExamFixNotice() {
        const prompt = document.getElementById("exam-fix-notice-prompt");
        if (!prompt) return;

        const noticeId = prompt.dataset.noticeId;
        const modal = document.getElementById("exam-fix-notice-modal");
        if (!modal || !noticeId) return;

        modal.classList.add("difficulty-modal-open");

        const dismiss = () => dismissExamFixNotice();

        bindDismissHandler(document.getElementById("exam-fix-notice-dismiss-btn"), dismiss);
        bindDismissHandler(document.getElementById("close-exam-fix-notice-btn"), dismiss);
        modal.addEventListener("click", (e) => {
            if (e.target === modal) dismiss();
        });
    }

    function bindDifficultyChoices() {
        document.querySelectorAll(".difficulty-btn[data-difficulty]").forEach((btn) => {
            btn.addEventListener("click", (e) => {
                e.preventDefault();
                const level = btn.getAttribute("data-difficulty");
                if (!level) return;
                window.location.assign(`/Test?start=1&difficulty=${encodeURIComponent(level)}`);
            });
        });
    }

    function bindReportForm() {
        const form = document.getElementById("report-form");
        if (!form) return;
        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            const formData = new FormData(form);
            const token = formData.get("__RequestVerificationToken");
            const data = {};
            formData.forEach((value, key) => {
                if (key === "__RequestVerificationToken") return;
                data[key === "answersJson" ? "answers" : key] = value;
            });

            try {
                const res = await fetch("/Index?handler=ReportError", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        RequestVerificationToken: token
                    },
                    body: JSON.stringify(data)
                });
                if (res.ok) {
                    await window.showAppAlert("הדיווח נשלח בהצלחה!");
                    form.reset();
                } else {
                    await window.showAppAlert("אירעה שגיאה בשליחת הדיווח.");
                }
            } catch {
                await window.showAppAlert("אירעה שגיאה בשליחת הדיווח.");
            }
        });
    }

    function bindModalDismiss(modalId, closeFn) {
        const modal = document.getElementById(modalId);
        if (!modal) return;
        modal.addEventListener("click", (e) => {
            if (e.target === modal) closeFn();
        });
    }

    function bindClick(id, handler) {
        const el = document.getElementById(id);
        if (el) el.addEventListener("click", handler);
    }

    document.addEventListener("DOMContentLoaded", () => {
        bindClick("main-question-image", openImageModal);
        bindClick("open-difficulty-modal-btn", (e) => {
            e.preventDefault();
            openDifficultyModal();
        });
        bindClick("close-difficulty-modal-btn", closeDifficultyModal);
        bindClick("close-image-modal-btn", closeImageModal);
        bindModalDismiss("image-modal", closeImageModal);
        bindModalDismiss("difficulty-modal", closeDifficultyModal);
        bindClick("footer-stats-toggle", toggleStats);
        bindReportForm();
        bindExamFixNotice();
        bindDifficultyChoices();

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") closeDifficultyModal();
        });
    });

    window.addEventListener("load", () => {
        fetchStats();
        fetchOnlineCount();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });
})();
