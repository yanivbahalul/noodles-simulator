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

    function dismissAppNotice() {
        const modal = document.getElementById("app-notice-modal");
        const prompt = document.getElementById("app-notice-prompt");
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
        dismissAppNotice();
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
        dismissAppNotice();
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

    const FEEDBACK_TONES = {
        correct: { frequency: 880, duration: 0.15 },
        incorrect: { frequency: 220, duration: 0.25 }
    };

    function isSoundEnabled() {
        return localStorage.getItem("quizSounds") !== "off";
    }

    function createAudioContext() {
        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        return new AudioCtx();
    }

    function playTone(ctx, frequency, duration) {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.frequency.value = frequency;
        gain.gain.value = 0.08;
        osc.start();
        osc.stop(ctx.currentTime + duration);
    }

    function playFeedbackTone(isCorrect) {
        const tone = FEEDBACK_TONES[isCorrect ? "correct" : "incorrect"];
        try {
            playTone(createAudioContext(), tone.frequency, tone.duration);
        } catch { /* no audio */ }
    }

    function playFeedbackSound(isCorrect) {
        if (!isSoundEnabled()) return;
        playFeedbackTone(isCorrect);
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
            const res = await fetch(`/api/stats-data?_=${Date.now()}`);
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

    function startAutoUpdate() {
        stopAutoUpdate();
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

    function bindAppNotice() {
        const prompt = document.getElementById("app-notice-prompt");
        if (!prompt) return;

        const noticeId = prompt.dataset.noticeId;
        const modal = document.getElementById("app-notice-modal");
        if (!modal || !noticeId) return;

        modal.classList.add("difficulty-modal-open");

        const dismiss = () => dismissAppNotice();

        bindDismissHandler(document.getElementById("app-notice-dismiss-btn"), dismiss);
        bindDismissHandler(document.getElementById("close-app-notice-btn"), dismiss);
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

    function bindStatsAdvancedPanel() {
        const toggle = document.getElementById("stats-advanced-toggle");
        const panel = document.getElementById("stats-advanced-panel");
        const statsPanel = document.getElementById("stats-panel");
        const resetForm = document.getElementById("reset-progress-form");
        if (!toggle || !panel) return;

        toggle.addEventListener("click", () => {
            const willOpen = panel.hidden;
            panel.hidden = !willOpen;
            toggle.setAttribute("aria-expanded", willOpen ? "true" : "false");
            statsPanel?.classList.toggle("is-advanced-open", willOpen);
        });

        resetForm?.addEventListener("submit", (e) => {
            const ok = window.confirm(
                "לאפס את כל ההתקדמות?\n\n" +
                "יסיר: סטטיסטיקה, XP, רמה, רצף, הישגים ונתוני תרגול.\n" +
                "לא ניתן לבטל."
            );
            if (!ok) e.preventDefault();
        });
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
        bindReportForm();
        bindAppNotice();
        bindDifficultyChoices();
        bindSoundToggle();
        bindAchievementToast();
        bindAnswerFeedback();
        bindStatsAdvancedPanel();

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
