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
        closePracticeOptionsModal();
        dismissExamFixNotice();
        const modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.add("difficulty-modal-open");
    }

    function closeDifficultyModal() {
        const modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    function openPracticeOptionsModal() {
        closeImageModal();
        closeAppDialog();
        closeDifficultyModal();
        dismissExamFixNotice();
        const modal = document.getElementById("practice-options-modal");
        if (modal) modal.classList.add("difficulty-modal-open");
    }

    function closePracticeOptionsModal() {
        const modal = document.getElementById("practice-options-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    function setText(id, value) {
        const el = document.getElementById(id);
        if (el) el.textContent = value;
    }

    function applyStatsData(data) {
        if (data?.correct === undefined) return;
        setText("stat-correct-panel", data.correct);
        setText("stat-total-panel", data.total);
        setText("stat-success-panel", `${data.successRate}%`);
        if (data.streak !== undefined) setText("stat-streak", data.streak);
        if (data.level !== undefined) setText("stat-level-value", data.level);
        if (data.xp !== undefined) setText("stat-xp-value", data.xp);
    }

    function playFeedbackSound(isCorrect) {
        if (localStorage.getItem("quizSounds") === "off") return;
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.frequency.value = isCorrect ? 880 : 220;
            gain.gain.value = 0.08;
            osc.start();
            osc.stop(ctx.currentTime + (isCorrect ? 0.15 : 0.25));
        } catch { /* no audio */ }
    }

    function bindSoundToggle() {
        const toggle = document.getElementById("sound-toggle");
        if (!toggle) return;
        toggle.checked = localStorage.getItem("quizSounds") !== "off";
        toggle.addEventListener("change", () => {
            localStorage.setItem("quizSounds", toggle.checked ? "on" : "off");
        });
    }

    function bindAchievementToast() {
        const toast = document.getElementById("achievement-toast");
        if (!toast) return;
        setTimeout(() => toast.classList.add("achievement-toast-hide"), 6000);
    }

    function bindAnswerFeedback() {
        const feedback = document.getElementById("answer-feedback");
        if (!feedback) return;
        const isCorrect = feedback.classList.contains("is-correct");
        playFeedbackSound(isCorrect);
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
        const willOpen = !panel.classList.contains("is-open");
        panel.classList.toggle("is-open", willOpen);
        panel.setAttribute("aria-hidden", willOpen ? "false" : "true");
        if (toggle) {
            toggle.classList.toggle("footer-stats-toggle-open", willOpen);
            toggle.classList.toggle("footer-stats-toggle-closed", !willOpen);
            toggle.setAttribute("aria-expanded", willOpen ? "true" : "false");
        }
        if (willOpen) {
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
        bindClick("open-practice-options-btn", (e) => {
            e.preventDefault();
            openPracticeOptionsModal();
        });
        bindClick("close-difficulty-modal-btn", closeDifficultyModal);
        bindClick("close-practice-options-btn", closePracticeOptionsModal);
        bindClick("close-image-modal-btn", closeImageModal);
        bindModalDismiss("image-modal", closeImageModal);
        bindModalDismiss("difficulty-modal", closeDifficultyModal);
        bindModalDismiss("practice-options-modal", closePracticeOptionsModal);
        bindClick("footer-stats-toggle", toggleStats);
        bindReportForm();
        bindExamFixNotice();
        bindDifficultyChoices();
        bindSoundToggle();
        bindAchievementToast();
        bindAnswerFeedback();

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") {
                closeDifficultyModal();
                closePracticeOptionsModal();
            }
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
