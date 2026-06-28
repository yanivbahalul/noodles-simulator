(function () {
    function dismissAppNotice() {
        const modal = document.getElementById("app-notice-modal");
        const prompt = document.getElementById("app-notice-prompt");
        if (!modal) return;
        modal.classList.remove("difficulty-modal-open");
        const noticeId = prompt?.dataset.noticeId;
        if (!noticeId) return;
        window.RequestChannels.backgroundFetch("/api/notices/dismiss", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ noticeId })
        }).catch(ignoreDismissError);
    }

    function logPromptShown(prompt, details = {}) {
        window.RequestChannels.backgroundFetch("/api/activity/prompt-shown", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ prompt, ...details })
        }).catch(() => {});
    }

    function ensureFeedbackPrompt(campaignId, milestone) {
        let prompt = document.getElementById("feedback-prompt");
        if (!prompt) {
            prompt = document.createElement("div");
            prompt.id = "feedback-prompt";
            prompt.hidden = true;
            document.body.appendChild(prompt);
        }
        prompt.dataset.campaignId = campaignId;
        prompt.dataset.milestone = String(milestone ?? "");
        return prompt;
    }

    function openFeedbackModal(campaignId, milestone) {
        const modal = document.getElementById("feedback-modal");
        if (!modal || !campaignId) {
            openGitHubStarModalIfPending();
            return;
        }
        ensureFeedbackPrompt(campaignId, milestone);
        resetFeedbackStars(
            document.getElementById("feedback-stars"),
            document.getElementById("feedback-submit-btn")
        );
        modal.classList.add("difficulty-modal-open");
        logPromptShown("feedback", {
            campaignId,
            milestone: parseInt(milestone, 10) || 0
        });
    }

    function openFeedbackModalIfPending() {
        const prompt = document.getElementById("feedback-prompt");
        const modal = document.getElementById("feedback-modal");
        if (!prompt || !modal || !prompt.dataset.campaignId) {
            openGitHubStarModalIfPending();
            return;
        }
        modal.classList.add("difficulty-modal-open");
    }

    function closeFeedbackModal() {
        const modal = document.getElementById("feedback-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    function dismissGitHubStarNotice(noticeId) {
        return window.RequestChannels.backgroundFetch("/api/notices/dismiss", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ noticeId })
        }).then((res) => res.ok).catch(() => false);
    }

    function openGitHubStarModal(milestone, url) {
        const modal = document.getElementById("github-star-modal");
        if (!modal) return;
        modal.dataset.milestone = String(milestone ?? "");
        modal.dataset.repoUrl = url || "https://github.com/yanivbahalul/noodles-simulator";
        modal.classList.add("difficulty-modal-open");
        logPromptShown("github_star", { milestone: parseInt(milestone, 10) || 0 });
    }

    function closeGitHubStarModal() {
        const modal = document.getElementById("github-star-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    function openGitHubStarModalIfPending(skipFeedbackCheck = false) {
        const prompt = document.getElementById("github-star-prompt");
        if (!prompt) return;
        if (prompt.dataset.hasNotice === "1") return;
        if (!skipFeedbackCheck && prompt.dataset.hasFeedback === "1") return;
        openGitHubStarModal(
            parseInt(prompt.dataset.milestone, 10),
            prompt.dataset.repoUrl
        );
    }

    function ignoreDismissError() {
        // Best-effort dismiss after the modal is already closed.
    }

    function openDifficultyModal() {
        window.IndexPage?.closeImageModal?.();
        window.IndexPage?.closeAppDialog?.();
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
        window.IndexPage?.closeImageModal?.();
        window.IndexPage?.closeAppDialog?.();
        closeDifficultyModal();
        dismissAppNotice();
        const modal = document.getElementById("practice-options-modal");
        if (modal) modal.classList.add("difficulty-modal-open");
    }

    function closePracticeOptionsModal() {
        const modal = document.getElementById("practice-options-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
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
        logPromptShown("app_notice", { noticeId });

        const dismiss = () => {
            dismissAppNotice();
            openFeedbackModalIfPending();
            openGitHubStarModalIfPending();
        };

        bindDismissHandler(document.getElementById("app-notice-dismiss-btn"), dismiss);
        bindDismissHandler(document.getElementById("close-app-notice-btn"), dismiss);
        modal.addEventListener("click", (e) => {
            if (e.target === modal) dismiss();
        });
    }

    async function showAppAlertIfAvailable(message) {
        if (typeof window.showAppAlert !== "function") return;
        await window.showAppAlert(message);
    }

    function createFeedbackStarUpdater(starsEl, submitBtn, onRatingChange) {
        return (rating) => {
            onRatingChange(rating);
            starsEl.querySelectorAll(".feedback-star").forEach((star) => {
                const value = parseInt(star.dataset.rating, 10);
                star.classList.toggle("is-selected", value <= rating);
            });
            submitBtn.disabled = rating < 1;
        };
    }

    function bindFeedbackStarClicks(starsEl, updateStars) {
        starsEl.querySelectorAll(".feedback-star").forEach((star) => {
            star.addEventListener("click", () => {
                updateStars(parseInt(star.dataset.rating, 10));
            });
        });
    }

    async function recordFeedbackLater(prompt, laterBtn) {
        laterBtn.disabled = true;
        try {
            const res = await fetch("/api/feedback/later", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ campaignId: prompt.dataset.campaignId })
            });
            if (res.ok) {
                closeFeedbackModal();
                prompt.remove();
                openGitHubStarModalIfPending(true);
                return;
            }
        } catch {
            // keep prompt for retry
        }
        laterBtn.disabled = false;
    }

    async function submitFeedbackRating(prompt, selectedRating, messageEl, submitBtn) {
        if (selectedRating < 1) return;

        submitBtn.disabled = true;
        try {
            const res = await fetch("/api/feedback/submit", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    campaignId: prompt.dataset.campaignId,
                    rating: selectedRating,
                    message: messageEl?.value?.trim() || ""
                })
            });
            if (res.ok) {
                closeFeedbackModal();
                prompt.remove();
                await showAppAlertIfAvailable("תודה! המשוב נשמר.");
                return;
            }
            await showAppAlertIfAvailable("אירעה שגיאה בשליחת המשוב. נסו שוב.");
        } catch {
            await showAppAlertIfAvailable("אירעה שגיאה בשליחת המשוב. נסו שוב.");
        } finally {
            submitBtn.disabled = selectedRating < 1;
        }
    }

    let feedbackSelectedRating = 0;

    function resetFeedbackStars(starsEl, submitBtn) {
        feedbackSelectedRating = 0;
        if (!starsEl || !submitBtn) return;
        starsEl.querySelectorAll(".feedback-star").forEach((star) => star.classList.remove("is-selected"));
        submitBtn.disabled = true;
        const messageEl = document.getElementById("feedback-message");
        if (messageEl) messageEl.value = "";
    }

    function bindFeedbackModal() {
        const modal = document.getElementById("feedback-modal");
        const submitBtn = document.getElementById("feedback-submit-btn");
        const laterBtn = document.getElementById("feedback-later-btn");
        const starsEl = document.getElementById("feedback-stars");
        const messageEl = document.getElementById("feedback-message");
        if (!modal || !submitBtn || !laterBtn || !starsEl) return;

        const updateStars = createFeedbackStarUpdater(starsEl, submitBtn, (rating) => {
            feedbackSelectedRating = rating;
        });
        bindFeedbackStarClicks(starsEl, updateStars);

        laterBtn.addEventListener("click", () => {
            const prompt = document.getElementById("feedback-prompt");
            if (prompt) recordFeedbackLater(prompt, laterBtn);
        });

        modal.addEventListener("click", (e) => {
            if (e.target === modal) closeFeedbackModal();
        });

        submitBtn.addEventListener("click", () => {
            const prompt = document.getElementById("feedback-prompt");
            if (prompt) {
                submitFeedbackRating(prompt, feedbackSelectedRating, messageEl, submitBtn);
            }
        });

        const prompt = document.getElementById("feedback-prompt");
        if (prompt?.dataset.campaignId && prompt.dataset.hasNotice !== "1") {
            openFeedbackModalIfPending();
        } else {
            openGitHubStarModalIfPending();
        }
    }

    function bindGitHubStarModal() {
        const modal = document.getElementById("github-star-modal");
        const acceptBtn = document.getElementById("github-star-accept-btn");
        const laterBtn = document.getElementById("github-star-later-btn");
        if (!modal || !acceptBtn || !laterBtn) return;

        acceptBtn.addEventListener("click", async () => {
            const url = modal.dataset.repoUrl || "https://github.com/yanivbahalul/noodles-simulator";
            window.open(url, "_blank", "noopener,noreferrer");
            acceptBtn.disabled = true;
            laterBtn.disabled = true;
            const ok = await dismissGitHubStarNotice("github-star-opted-in");
            if (ok) {
                closeGitHubStarModal();
                document.getElementById("github-star-prompt")?.remove();
                return;
            }
            acceptBtn.disabled = false;
            laterBtn.disabled = false;
        });

        laterBtn.addEventListener("click", async () => {
            const milestone = parseInt(modal.dataset.milestone, 10);
            if (milestone <= 0) return;
            acceptBtn.disabled = true;
            laterBtn.disabled = true;
            const ok = await dismissGitHubStarNotice(`github-star-${milestone}`);
            if (ok) {
                closeGitHubStarModal();
                document.getElementById("github-star-prompt")?.remove();
                return;
            }
            acceptBtn.disabled = false;
            laterBtn.disabled = false;
        });

        modal.addEventListener("click", (e) => {
            if (e.target === modal) closeGitHubStarModal();
        });

        openGitHubStarModalIfPending();
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

    window.IndexModals = {
        openFeedbackModal,
        openGitHubStarModal,
        openDifficultyModal,
        closeDifficultyModal,
        openPracticeOptionsModal,
        closePracticeOptionsModal,
        bindAppNotice,
        bindFeedbackModal,
        bindGitHubStarModal,
        bindDifficultyChoices
    };
})();
