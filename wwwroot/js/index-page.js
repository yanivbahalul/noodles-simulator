(function () {
    let updateInterval = null;

    function bindClick(id, handler) {
        const el = document.getElementById(id);
        if (el) el.addEventListener("click", handler);
    }

    function bindImageModal() {
        const open = () => {
            const mainImg = document.getElementById("main-question-image");
            window.ImageModal?.openImageModal(mainImg?.src || "");
        };
        const close = () => window.ImageModal?.closeImageModal();
        bindClick("main-question-image", open);
        bindClick("close-image-modal-btn", close);
        bindModalDismiss("image-modal", close);
    }

    function closeAppDialog() {
        const dialog = document.getElementById("app-dialog");
        if (dialog) dialog.classList.remove("notice-modal-open");
    }

    function closeImageModal() {
        window.ImageModal?.closeImageModal();
    }


    function setText(id, value) {
        const el = document.getElementById(id);
        if (el) el.textContent = value;
    }

    function applyLevelFields(level) {
        setText("live-level", level);
        setText("stat-level-value", level);
        setText("stat-level-label", level);
    }

    function applyXpProgressFields(data, options) {
        if (data?.xpProgressPercent !== undefined) {
            const fill = document.getElementById("live-xp-fill");
            if (fill) {
                const nextWidth = `${data.xpProgressPercent}%`;
                if (fill.style.getPropertyValue("--xp-progress") !== nextWidth) {
                    fill.style.setProperty("--xp-progress", nextWidth);
                }
            }
        }
        if (data?.xpToNextLevel !== undefined) {
            const meta = document.getElementById("live-xp-meta");
            if (meta) meta.textContent = `נותר ${data.xpToNextLevel} XP לרמה ההבאה`;
        }
        if (data?.xp !== undefined) setText("stat-xp-value", data.xp);
        if (options.pulse && data?.xpGain > 0) pulseLevelBar();
    }

    function applyLevelProgressLive(data, options = {}) {
        if (data?.level !== undefined) applyLevelFields(data.level);
        applyXpProgressFields(data, options);
    }

    function enableLevelProgressTransitions() {
        const fill = document.getElementById("live-xp-fill");
        if (!fill) return;
        fill.classList.add("level-progress-live-fill--animate");
    }

    async function initLevelProgressLive() {
        try {
            await fetchStats();
        } catch {
            // keep server-rendered values
        }
        enableLevelProgressTransitions();
    }

    function pulseLevelBar() {
        const fill = document.getElementById("live-xp-fill");
        if (!fill) return;
        fill.classList.remove("level-progress-live-fill--pulse");
        window.replayCssAnimation?.(fill);
        fill.classList.add("level-progress-live-fill--pulse");
    }

    function applyStatsData(data, options = {}) {
        if (!data) return;
        if (data.correct !== undefined) {
            setText("stat-correct-panel", data.correct);
            setText("stat-total-panel", data.total);
            setText("stat-success-panel", `${data.successRate}%`);
        }
        if (data.streak !== undefined) {
            setText("stat-streak", data.streak);
            if (!options.skipStreakUpdate) {
                updateStreakBadge(data.streak, { pulse: options.streakPulse });
            }
        }
        applyLevelProgressLive(data, options);
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
        toast.querySelectorAll(".achievement-toast-item").forEach((item, index) => {
            const text = item.textContent?.trim();
            if (!text) return;
            setTimeout(() => {
                window.pushQuizNotify?.({
                    type: "achievement",
                    message: text,
                    durationMs: 6000
                });
            }, index * 120);
        });
        toast.remove();
    }

    function bindAnswerFeedback() {
        const feedback = document.getElementById("answer-feedback");
        if (!feedback || feedback.hidden) return;
        const isCorrect = feedback.classList.contains("is-correct");
        playFeedbackSound(isCorrect);
        if (!isCorrect) {
            const questionId = document.getElementById("quiz-question-image")?.value ?? "";
            window.QuestionExplanation?.showForWrongAnswer?.(questionId);
        }
    }

    function applyOnlineCount(data) {
        if (data?.online === undefined || data.online === null) return;
        setText("online-count", data.online);
    }

    async function fetchStats() {
        try {
            const res = await window.RequestChannels.backgroundFetch(`/api/stats-data?_=${Date.now()}`);
            if (!res.ok) throw new Error("stats fetch failed");
            applyStatsData(await res.json());
        } catch {
            // keep existing values
        }
    }

    async function fetchOnlineCount() {
        try {
            const res = await window.RequestChannels.backgroundFetch(`/api/online-count?_=${Date.now()}`);
            if (!res.ok) throw new Error("online fetch failed");
            applyOnlineCount(await res.json());
        } catch {
            // keep existing values
        }
    }

    function startAutoUpdate() {
        stopAutoUpdate();
        updateInterval = setInterval(fetchOnlineCount, 30000);
    }

    function stopAutoUpdate() {
        if (updateInterval) {
            clearInterval(updateInterval);
            updateInterval = null;
        }
    }

    function buildReportFormPayload(formData) {
        const keys = new Set([
            "questionImage",
            "explanation",
            "selectedAnswer",
            "correctAnswer",
            "answers"
        ]);
        const data = {};
        formData.forEach((value, key) => {
            if (key === "__RequestVerificationToken") return;
            if (keys.has(key)) data[key] = value;
        });
        return data;
    }

    async function postReportForm(form, payload, token) {
        const res = await fetch("/Index?handler=ReportError", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token
            },
            body: JSON.stringify(payload)
        });
        if (res.ok) {
            if (window.showAppToast) {
                window.showAppToast("תודה, קיבלנו את הדיווח! 🙏");
            } else {
                await window.showAppAlert("תודה, קיבלנו את הדיווח!");
            }
            form.reset();
            return;
        }
        if (window.showAppToast) {
            window.showAppToast("אירעה שגיאה בשליחת הדיווח.");
        } else {
            await window.showAppAlert("אירעה שגיאה בשליחת הדיווח.");
        }
    }

    function bindReportForm() {
        const form = document.getElementById("report-form");
        if (!form) return;
        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            const formData = new FormData(form);
            const token = formData.get("__RequestVerificationToken");
            try {
                await postReportForm(form, buildReportFormPayload(formData), token);
            } catch {
                if (window.showAppToast) {
                    window.showAppToast("אירעה שגיאה בשליחת הדיווח.");
                } else {
                    await window.showAppAlert("אירעה שגיאה בשליחת הדיווח.");
                }
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

        resetForm?.addEventListener("submit", async (e) => {
            e.preventDefault();
            const confirmed = await window.showAppConfirm(
                "לאפס את כל ההתקדמות?\n\n" +
                "יסיר: סטטיסטיקה, XP, רמה, רצף, הישגים ונתוני תרגול.\n" +
                "לא ניתן לבטל."
            );
            if (confirmed) resetForm.submit();
        });
    }



    function showActiveStreakBadge(badge, text, value, options) {
        badge.hidden = false;
        if (text) text.textContent = String(value);
        badge.classList.toggle("streak-badge--hot", value >= 7);
        if (options.pulse) {
            badge.classList.remove("streak-badge--burst");
            window.replayCssAnimation?.(badge);
            badge.classList.add("streak-badge--burst");
            setTimeout(() => badge.classList.remove("streak-badge--burst"), 450);
        }
    }

    function hideStreakBadge(badge) {
        badge.hidden = true;
        badge.classList.remove("streak-badge--hot", "streak-badge--burst");
    }

    function updateStreakBadge(streak, options = {}) {
        const badge = document.getElementById("streak-badge");
        const text = document.getElementById("streak-badge-text");
        if (!badge) return;

        const value = Number(streak) || 0;
        if (value > 0) {
            showActiveStreakBadge(badge, text, value, options);
            return;
        }
        hideStreakBadge(badge);
    }

    function syncStreakBadgeFromPage() {
        const stat = document.getElementById("stat-streak");
        const parsed = parseInt(stat?.textContent ?? "0", 10);
        updateStreakBadge(Number.isFinite(parsed) ? parsed : 0);
    }

    function showAchievementToast(achievements) {
        if (!achievements?.length || !window.pushQuizNotify) return;
        achievements.forEach((a, index) => {
            setTimeout(() => {
                window.pushQuizNotify({
                    type: "achievement",
                    title: "הישג חדש",
                    message: `${a.emoji} <strong>${window.escapeHtml(a.title)}</strong> — ${window.escapeHtml(a.description)}`,
                    durationMs: 6000
                });
            }, index * 120);
        });
    }

    function showLevelUpToast(level) {
        window.pushQuizNotify?.({
            type: "levelUp",
            title: "רמה חדשה!",
            message: `עלית לרמה ${level}`,
            durationMs: 5000
        });
    }

    function showDailyCompleteModal(score, total) {
        const modal = document.getElementById("daily-complete-modal");
        const scoreEl = document.getElementById("daily-complete-score");
        if (scoreEl) scoreEl.textContent = `${score}/${total}`;
        if (modal) modal.classList.add("difficulty-modal-open");
    }

    function closeDailyCompleteModal() {
        const modal = document.getElementById("daily-complete-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    function isTypingTarget(el) {
        if (!el) return false;
        const tag = el.tagName?.toLowerCase();
        return tag === "input" || tag === "textarea" || tag === "select" || el.isContentEditable;
    }

    function isQuizShortcutBlocked() {
        return Boolean(document.querySelector(".difficulty-modal-open, .modal-open, .notice-modal-open"));
    }

    function tryAdvanceToNextQuestion(e, quizBusy) {
        if (e.key !== "Enter" || quizBusy) return false;
        const nextBtn = document.getElementById("next-question-btn");
        if (!nextBtn || nextBtn.disabled) return false;
        e.preventDefault();
        nextBtn.click();
        return true;
    }

    function trySelectAnswerByKey(e, answerChecked, quizBusy) {
        if (answerChecked || quizBusy) return false;
        const idx = parseInt(e.key, 10);
        if (idx < 1 || idx > 4) return false;
        const btn = document.querySelectorAll("#answers-grid .answer-btn")[idx - 1];
        if (!btn || btn.disabled) return false;
        e.preventDefault();
        btn.click();
        return true;
    }

    function bindKeyboardShortcuts() {
        document.addEventListener("keydown", (e) => {
            if (isTypingTarget(document.activeElement)) return;
            if (isQuizShortcutBlocked()) return;

            const answerChecked = window.IndexQuiz?.isAnswerChecked?.() ?? false;
            const quizBusy = window.IndexQuiz?.isQuizBusy?.() ?? false;

            if (tryAdvanceToNextQuestion(e, quizBusy)) return;
            trySelectAnswerByKey(e, answerChecked, quizBusy);
        });
    }

    function bindDailyCompleteModal() {
        bindClick("daily-complete-dismiss-btn", closeDailyCompleteModal);
        bindModalDismiss("daily-complete-modal", closeDailyCompleteModal);
    }

    function applyAnswerStats(data) {
        if (!data.stats && !data.feedback?.levelUpTo) return;
        const statsPayload = {
            ...(data.stats ?? {}),
            xpGain: data.feedback?.xpGain ?? 0
        };
        if (data.feedback?.levelUpTo) {
            statsPayload.level = Math.max(statsPayload.level ?? 1, data.feedback.levelUpTo);
        }
        const shouldPulse = Boolean(
            data.isCorrect && (data.feedback?.xpGain > 0 || data.feedback?.levelUpTo)
        );
        applyStatsData(statsPayload, {
            pulse: shouldPulse,
            skipStreakUpdate: true
        });
    }

    function applyAnswerPrompts(data) {
        if (data.showFeedbackPrompt && data.feedbackCampaignId) {
            window.IndexModals?.openFeedbackModal?.(data.feedbackCampaignId, data.feedbackMilestone);
            return;
        }
        if (data.showGitHubStarPrompt) {
            window.IndexModals?.openGitHubStarModal?.(data.githubStarMilestone, data.githubStarUrl);
        }
    }

    function applyAnswerSideEffects(data) {
        applyAnswerStats(data);
        updateStreakBadge(data.stats?.streak ?? 0, { pulse: Boolean(data.isCorrect) });
        if (data.feedback?.levelUpTo) showLevelUpToast(data.feedback.levelUpTo);
        showAchievementToast(data.achievements);
        if (data.feedback?.dailyComplete) {
            showDailyCompleteModal(data.feedback.dailyScore ?? 0, data.feedback.dailyTotal ?? 10);
        }
        applyAnswerPrompts(data);
    }

    window.IndexPage = { applyAnswerSideEffects, playFeedbackSound, updateStreakBadge, closeImageModal, closeAppDialog };

    document.addEventListener("DOMContentLoaded", () => {
        const pageData = document.getElementById("quiz-page-data");
        window.IndexQuiz?.setAnswerChecked(pageData?.dataset.answerChecked === "1");

        bindImageModal();
        bindClick("open-difficulty-modal-btn", (e) => {
            e.preventDefault();
            window.IndexModals?.openDifficultyModal?.();
        });
        bindClick("open-practice-options-btn", (e) => {
            e.preventDefault();
            window.IndexModals?.openPracticeOptionsModal?.();
        });
        bindClick("close-difficulty-modal-btn", () => window.IndexModals?.closeDifficultyModal?.());
        bindClick("close-practice-options-btn", () => window.IndexModals?.closePracticeOptionsModal?.());
        bindModalDismiss("difficulty-modal", () => window.IndexModals?.closeDifficultyModal?.());
        bindModalDismiss("practice-options-modal", () => window.IndexModals?.closePracticeOptionsModal?.());
        window.IndexQuiz?.bindQuizAnswerForm?.();
        window.IndexQuiz?.bindNextQuestion?.();
        bindReportForm();
        window.IndexModals?.bindWelcomeModal?.();
        window.IndexModals?.bindFeedbackModal?.();
        window.IndexModals?.bindGitHubStarModal?.();
        window.IndexModals?.bindDifficultyChoices?.();
        bindSoundToggle();
        syncStreakBadgeFromPage();
        bindAchievementToast();
        bindAnswerFeedback();
        bindStatsAdvancedPanel();
        bindDailyCompleteModal();
        bindKeyboardShortcuts();

        initLevelProgressLive().catch(window.ignoreBackgroundError);

        window.QuizViewport?.bindQuizViewportHandlers?.();
        window.IndexQuiz?.schedulePrefetchNextQuestion?.();

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") {
                window.IndexModals?.closeDifficultyModal?.();
                window.IndexModals?.closePracticeOptionsModal?.();
            }
        });
    });

    function scheduleBackgroundRefresh() {
        window.RequestChannels?.scheduleBackground(fetchOnlineCount);
    }

    window.addEventListener("load", () => {
        scheduleBackgroundRefresh();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });

    window.applyLevelProgressLive = applyLevelProgressLive;
})();
