(function () {
    let updateInterval = null;
    let answerChecked = false;
    let quizBusy = false;
    let questionRenderSeq = 0;
    let prefetchedQuestion = null;
    let prefetchAnchor = null;
    let prefetchPromise = null;

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

    function logPromptShown(prompt, details = {}) {
        fetch("/api/activity/prompt-shown", {
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
        return fetch("/api/notices/dismiss", {
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
        if (!feedback || feedback.hidden) return;
        const isCorrect = feedback.classList.contains("is-correct");
        playFeedbackSound(isCorrect);
    }

    function applyOnlineCount(data) {
        if (data?.online === undefined || data.online === null) return;
        setText("online-count", data.online);
    }

    function escapeHtml(text) {
        const container = document.createElement("div");
        container.textContent = text ?? "";
        return container.innerHTML;
    }

    function getAntiForgeryToken() {
        return document.querySelector('#quiz-answer-form input[name="__RequestVerificationToken"]')?.value ?? "";
    }

    function setQuizLoading(on) {
        const container = document.querySelector(".quiz-container");
        if (container) container.classList.toggle("quiz-loading", on);
    }

    function setQuizBusy(on) {
        quizBusy = on;
        setQuizLoading(on);
        const nextBtn = document.getElementById("next-question-btn");
        if (nextBtn) nextBtn.disabled = on;
        document.querySelectorAll("#answers-grid .answer-btn").forEach((btn) => {
            if (!answerChecked) btn.disabled = on;
        });
    }

    function setQuizSwapping(on) {
        const container = document.querySelector(".quiz-container");
        if (container) container.classList.toggle("quiz-swapping", on);
    }

    function preloadImage(url, timeoutMs = 12000) {
        return new Promise((resolve) => {
            if (!url) {
                resolve();
                return;
            }
            const img = new Image();
            let done = false;
            const finish = () => {
                if (done) return;
                done = true;
                resolve();
            };
            const timer = setTimeout(finish, timeoutMs);
            img.onload = () => {
                clearTimeout(timer);
                finish();
            };
            img.onerror = () => {
                clearTimeout(timer);
                finish();
            };
            img.src = url;
        });
    }

    async function preloadQuestionImages(data) {
        const urls = [
            data.questionImageUrl,
            ...(data.answers?.map((a) => a.imageUrl) ?? [])
        ].filter(Boolean);
        await Promise.all(urls.map((url) => preloadImage(url)));
    }

    function invalidatePrefetchCache() {
        prefetchedQuestion = null;
        prefetchAnchor = null;
        prefetchPromise = null;
    }

    function getCurrentQuestionAnchor() {
        return document.getElementById("quiz-question-image")?.value ?? "";
    }

    async function fetchNextQuestionData() {
        const res = await fetch("/Index?handler=PrefetchNextQuestion");
        if (res.status === 204 || !res.ok) return null;
        const data = await res.json();
        return data?.questionImage ? data : null;
    }

    function storePrefetchedQuestion(anchor, data) {
        prefetchAnchor = anchor;
        prefetchedQuestion = data;
    }

    async function loadPrefetchNextQuestion() {
        try {
            const anchor = getCurrentQuestionAnchor();
            if (!anchor) return null;

            const data = await fetchNextQuestionData();
            if (!data) return null;

            await preloadQuestionImages(data);
            if (getCurrentQuestionAnchor() !== anchor) return null;

            storePrefetchedQuestion(anchor, data);
            return data;
        } catch {
            return null;
        } finally {
            prefetchPromise = null;
        }
    }

    function fetchPrefetchNextQuestion() {
        if (prefetchPromise) return prefetchPromise;
        prefetchPromise = loadPrefetchNextQuestion();
        return prefetchPromise;
    }

    function schedulePrefetchNextQuestion() {
        invalidatePrefetchCache();
        window.setTimeout(() => {
            fetchPrefetchNextQuestion();
        }, 0);
    }

    function getCachedNextQuestion() {
        const current = document.getElementById("quiz-question-image")?.value ?? "";
        if (!prefetchedQuestion || prefetchAnchor !== current) return null;
        return prefetchedQuestion;
    }

    function prefersReducedMotion() {
        return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    let viewportAdjustTimer = null;

    function getQuizScrollMargins(viewportH) {
        const margin = 8;
        const minTop = margin;
        const maxBottom = viewportH - 16;
        const topBar = document.querySelector(".top-bar");
        if (!topBar) return { minTop, maxBottom };

        const barRect = topBar.getBoundingClientRect();
        if (barRect.top <= margin && barRect.bottom > barRect.top) {
            return { minTop: Math.ceil(barRect.bottom) + margin, maxBottom };
        }
        return { minTop, maxBottom };
    }

    function isQuizFullyVisible(rect, minTop, maxBottom) {
        return rect.top >= minTop - 3 && rect.bottom <= maxBottom + 3;
    }

    function computeDeltaWhenFits(rect, minTop, maxBottom) {
        if (rect.top < minTop) return rect.top - minTop;
        if (rect.bottom > maxBottom) return rect.bottom - maxBottom;
        return 0;
    }

    function computeQuizScrollDelta(rect, viewportH) {
        const { minTop, maxBottom } = getQuizScrollMargins(viewportH);
        const available = maxBottom - minTop;

        if (isQuizFullyVisible(rect, minTop, maxBottom)) return 0;
        if (rect.height <= available) return computeDeltaWhenFits(rect, minTop, maxBottom);
        if (rect.top < minTop) return rect.top - minTop;
        if (rect.bottom > maxBottom) return rect.bottom - maxBottom;
        return 0;
    }

    function scrollQuizIntoView() {
        const container = document.querySelector(".quiz-container");
        if (!container) return;

        const rect = container.getBoundingClientRect();
        if (rect.height <= 0) return;

        const deltaY = computeQuizScrollDelta(rect, window.innerHeight);
        if (Math.abs(deltaY) <= 3) return;
        window.scrollTo({
            top: Math.max(0, window.scrollY + deltaY),
            behavior: prefersReducedMotion() ? "auto" : "smooth"
        });
    }

    function scheduleQuizViewportAdjust() {
        if (viewportAdjustTimer) clearTimeout(viewportAdjustTimer);
        viewportAdjustTimer = setTimeout(() => {
            viewportAdjustTimer = null;
            adjustQuizViewport();
        }, 150);
    }

    function getReservedBelowQuestion(answers, buttonRow, feedback) {
        const feedbackHeight = feedback && !feedback.hidden ? feedback.offsetHeight : 0;
        return (answers?.offsetHeight ?? 0) +
            (buttonRow?.offsetHeight ?? 0) +
            feedbackHeight +
            32;
    }

    function applyQuestionImageMaxHeight(mainImg, availableForQuestion, viewportH) {
        if (availableForQuestion <= 96) return;
        const cssCap = viewportH * 0.4;
        const newMax = `${Math.floor(Math.min(cssCap, availableForQuestion))}px`;
        if (mainImg.style.maxHeight !== newMax) {
            mainImg.style.maxHeight = newMax;
        }
    }

    function adjustQuizViewport() {
        const container = document.querySelector(".quiz-container");
        const mainImg = document.getElementById("main-question-image");
        const answers = document.getElementById("answers-grid");
        const feedback = document.getElementById("answer-feedback");
        const buttonRow = container?.querySelector(".button-row");
        if (!container || !mainImg) return;

        const viewportH = window.innerHeight;
        const { minTop } = getQuizScrollMargins(viewportH);
        const containerRect = container.getBoundingClientRect();
        const reservedBelowQuestion = getReservedBelowQuestion(answers, buttonRow, feedback);
        const availableForQuestion = viewportH - Math.max(minTop, containerRect.top) - reservedBelowQuestion;
        applyQuestionImageMaxHeight(mainImg, availableForQuestion, viewportH);

        requestAnimationFrame(() => {
            requestAnimationFrame(scrollQuizIntoView);
        });
    }

    function bindQuestionImageLoad(img, onLoad) {
        if (!img) return;
        if (img.complete) {
            onLoad();
            return;
        }
        img.addEventListener("load", onLoad, { once: true });
        img.addEventListener("error", onLoad, { once: true });
    }

    function ensureStreakBadgeElement(stack) {
        const existing = document.getElementById("streak-badge");
        if (existing) return existing;
        if (!stack) return null;
        const badge = document.createElement("span");
        badge.id = "streak-badge";
        badge.className = "streak-badge";
        stack.appendChild(badge);
        return badge;
    }

    function updateStreakBadge(streak) {
        const badge = document.getElementById("streak-badge");
        if (streak > 0) {
            const activeBadge = ensureStreakBadgeElement(document.querySelector(".practice-mode-stack"));
            if (activeBadge) {
                activeBadge.hidden = false;
                activeBadge.textContent = `${streak} 🔥`;
            }
            return;
        }
        if (badge) badge.hidden = true;
    }

    function updatePracticeModeBadge(data) {
        const badge = document.getElementById("practice-mode-badge");
        if (!badge || !data.practiceModeLabel) return;
        if (data.practiceMode === "daily") {
            badge.innerHTML = `${escapeHtml(data.practiceModeLabel)} <span class="practice-mode-daily">${data.dailyProgress}/${data.dailyTotal}</span>`;
        } else {
            badge.textContent = data.practiceModeLabel;
        }
    }

    function showAchievementToast(achievements) {
        if (!achievements?.length) return;
        let toast = document.getElementById("achievement-toast");
        if (!toast) {
            toast = document.createElement("div");
            toast.id = "achievement-toast";
            toast.className = "achievement-toast";
            const quizContainer = document.querySelector(".quiz-container");
            quizContainer?.parentNode?.insertBefore(toast, quizContainer);
        }
        toast.classList.remove("achievement-toast-hide");
        toast.innerHTML = achievements.map((a) =>
            `<p class="achievement-toast-item">${escapeHtml(a.emoji)} הישג חדש: <strong>${escapeHtml(a.title)}</strong> — ${escapeHtml(a.description)}</p>`
        ).join("");
        setTimeout(() => toast.classList.add("achievement-toast-hide"), 6000);
    }

    function answerFileFromUrl(url) {
        if (!url) return "";
        try {
            const pathname = new URL(url, window.location.origin).pathname;
            return decodeURIComponent(pathname.split("/").pop() || "");
        } catch {
            return url.split("/").pop()?.split("?")[0]?.split("#")[0] ?? "";
        }
    }

    function filesMatchCorrectAnswer(file, imgFile, correctAnswerFile) {
        if (!correctAnswerFile) return false;
        return (file && file === correctAnswerFile) ||
            (imgFile && imgFile === correctAnswerFile);
    }

    function matchesCorrectAnswerFromList(file, imgFile, data) {
        if (!data.answers?.length || !data.correctKey) return false;
        const correct = data.answers.find((a) => a.key === data.correctKey);
        if (!correct?.fileName) return false;
        return file === correct.fileName || imgFile === correct.fileName;
    }

    function matchesCorrectAnswerUrl(imgFile, data) {
        if (!data.correctAnswerUrl) return false;
        const correctFile = answerFileFromUrl(data.correctAnswerUrl);
        return Boolean(correctFile && imgFile && correctFile === imgFile);
    }

    function isAnswerButtonCorrect(btn, data) {
        const key = btn.value;
        const img = btn.querySelector("img");
        const file = img?.dataset?.answerFile ?? "";
        const imgFile = answerFileFromUrl(img?.src ?? "");

        const matchers = [
            () => data.correctKey && key === data.correctKey,
            () => filesMatchCorrectAnswer(file, imgFile, data.correctAnswerFile),
            () => matchesCorrectAnswerUrl(imgFile, data),
            () => matchesCorrectAnswerFromList(file, imgFile, data)
        ];
        return matchers.some((match) => match());
    }

    function styleAnswerButtons(grid, data) {
        if (!grid) return;
        grid.querySelectorAll(".answer-btn").forEach((btn) => {
            btn.disabled = true;
            btn.classList.remove("correct", "incorrect", "answer-pulse", "answer-shake", "answer-reveal-correct");

            if (isAnswerButtonCorrect(btn, data)) {
                btn.classList.add("correct", data.isCorrect ? "answer-pulse" : "answer-reveal-correct");
            } else if (btn.value === data.selectedKey) {
                btn.classList.add("incorrect", "answer-shake");
            }
        });
    }

    function showAnswerFeedback(data) {
        const feedback = document.getElementById("answer-feedback");
        if (!feedback) return;
        feedback.hidden = false;
        feedback.classList.toggle("is-correct", data.isCorrect);
        feedback.classList.toggle("is-incorrect", !data.isCorrect);
        feedback.textContent = data.isCorrect ? "תשובה נכונה!" : "תשובה שגויה";
    }

    function setInputValue(input, value) {
        if (input) input.value = value;
    }

    function resolveReportQuestionImage(data) {
        const quizQuestion = document.getElementById("quiz-question-image")?.value;
        return quizQuestion || data.questionImageOriginalName || data.questionImage || "";
    }

    function buildReportAnswersJson(answers) {
        if (!answers?.length) return "";
        const dict = {};
        for (const answer of answers) {
            if (answer.key) dict[answer.key] = answer.fileName ?? "";
        }
        return JSON.stringify(dict);
    }

    function syncReportFormFromAnswerResult(data) {
        const form = document.getElementById("report-form");
        if (!form || !data) return;

        setInputValue(form.querySelector('input[name="questionImage"]'), resolveReportQuestionImage(data));
        setInputValue(form.querySelector('input[name="selectedAnswer"]'), data.selectedKey ?? "");
        setInputValue(form.querySelector('input[name="correctAnswer"]'), data.correctAnswerFile ?? "");
        setInputValue(form.querySelector('input[name="answers"]'), buildReportAnswersJson(data.answers));
    }

    function applyAnswerResult(data) {
        styleAnswerButtons(document.getElementById("answers-grid"), data);
        showAnswerFeedback(data);

        playFeedbackSound(data.isCorrect);
        if (data.stats) applyStatsData(data.stats);
        updateStreakBadge(data.stats?.streak ?? 0);
        showAchievementToast(data.achievements);

        if (data.showFeedbackPrompt && data.feedbackCampaignId) {
            openFeedbackModal(data.feedbackCampaignId, data.feedbackMilestone);
        } else if (data.showGitHubStarPrompt) {
            openGitHubStarModal(data.githubStarMilestone, data.githubStarUrl);
        }

        syncReportFormFromAnswerResult(data);
        scheduleQuizViewportAdjust();
        schedulePrefetchNextQuestion();
    }

    function setImageSource(img, url) {
        if (img && url) img.src = url;
    }

    function updateQuestionImages(mainImg, modalImg, data) {
        const url = data.questionImageUrl;
        if (mainImg) mainImg.style.maxHeight = "";
        setImageSource(mainImg, url);
        setImageSource(modalImg, url);
    }

    function renderAnswerButtons(grid, answers) {
        grid.innerHTML = "";
        answers.forEach((a) => {
            const btn = document.createElement("button");
            btn.type = "submit";
            btn.name = "answer";
            btn.value = a.key;
            btn.className = "answer-btn";
            const img = document.createElement("img");
            img.src = a.imageUrl;
            img.alt = "תשובה";
            if (a.fileName) img.dataset.answerFile = a.fileName;
            btn.appendChild(img);
            grid.appendChild(btn);
        });
    }

    function clearAnswerFeedback(feedback) {
        if (!feedback) return;
        feedback.hidden = true;
        feedback.classList.remove("is-correct", "is-incorrect");
        feedback.textContent = "";
    }

    function syncReportFormForQuestion(data) {
        const form = document.getElementById("report-form");
        if (!form) return;

        setInputValue(
            form.querySelector('input[name="questionImage"]'),
            data.questionImageOriginalName ?? data.questionImage ?? ""
        );
        setInputValue(form.querySelector('input[name="selectedAnswer"]'), "");
        setInputValue(form.querySelector('input[name="correctAnswer"]'), "");
        setInputValue(form.querySelector('input[name="answers"]'), "");
    }

    function setHiddenQuestionImage(questionImage) {
        const hidden = document.getElementById("quiz-question-image");
        if (hidden) hidden.value = questionImage ?? "";
    }

    function renderQuestionAnswersGrid(data) {
        const grid = document.getElementById("answers-grid");
        if (!grid || !data.answers?.length) return;
        renderAnswerButtons(grid, data.answers);
    }

    function applyRenderedQuestionDom(data) {
        updateQuestionImages(
            document.getElementById("main-question-image"),
            document.getElementById("modal-img"),
            data
        );
        setHiddenQuestionImage(data.questionImage);
        renderQuestionAnswersGrid(data);
        clearAnswerFeedback(document.getElementById("answer-feedback"));
        syncReportFormForQuestion(data);
        if (data.practiceMode === "daily") updatePracticeModeBadge(data);
    }

    async function renderQuestion(data, seq, options = {}) {
        if (!options.imagesPreloaded) {
            await preloadQuestionImages(data);
        }
        if (seq !== questionRenderSeq) return;

        applyRenderedQuestionDom(data);
        scheduleQuizViewportAdjust();
        schedulePrefetchNextQuestion();
    }

    function getQuizAnswerPayload(e, form) {
        const submitter = e.submitter;
        if (!submitter || submitter.name !== "answer") return null;
        const questionImage = form.querySelector('input[name="questionImage"]')?.value;
        if (!questionImage) return { missingQuestion: true };
        return { questionImage, answer: submitter.value, submitter };
    }

    async function submitQuizAnswer(questionImage, answer, token) {
        const res = await fetch("/Index?handler=SubmitAnswer", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: token
            },
            body: JSON.stringify({ questionImage, answer })
        });
        const data = await res.json();
        return { res, data };
    }

    function lockAnswerButtonsAfterCheck() {
        document.querySelectorAll("#answers-grid .answer-btn").forEach((btn) => {
            btn.disabled = true;
        });
    }

    function processQuizAnswerResponse(res, data) {
        if (data.redirect) {
            window.location.assign(data.redirect);
            return;
        }
        if (!res.ok) throw new Error("submit failed");
        applyAnswerResult(data);
        answerChecked = true;
    }

    async function recoverQuizAnswerSubmitError(responseReceived, form, submitter) {
        if (!responseReceived) {
            form.requestSubmit(submitter);
            return;
        }
        if (window.showAppAlert) {
            await window.showAppAlert("שגיאה בשליחת התשובה. נסה שוב.");
        }
    }

    async function alertMissingQuestionImage() {
        if (window.showAppAlert) {
            await window.showAppAlert("שגיאה: השאלה לא נטענה. לחץ «שאלה הבאה» ונסה שוב.");
        }
    }

    async function runQuizAnswerRequest(payload, form) {
        let responseReceived = false;
        try {
            const { res, data } = await submitQuizAnswer(
                payload.questionImage,
                payload.answer,
                getAntiForgeryToken()
            );
            responseReceived = true;
            processQuizAnswerResponse(res, data);
        } catch {
            await recoverQuizAnswerSubmitError(responseReceived, form, payload.submitter);
        }
    }

    async function submitQuizAnswerWithBusyState(payload, form) {
        setQuizBusy(true);
        try {
            await runQuizAnswerRequest(payload, form);
        } finally {
            setQuizBusy(false);
            if (answerChecked) lockAnswerButtonsAfterCheck();
        }
    }

    async function handleQuizAnswerSubmit(e, form) {
        e.preventDefault();
        if (answerChecked || quizBusy) return;

        const payload = getQuizAnswerPayload(e, form);
        if (!payload) return;
        if (payload.missingQuestion) {
            await alertMissingQuestionImage();
            return;
        }

        await submitQuizAnswerWithBusyState(payload, form);
    }

    function bindQuizAnswerForm() {
        const form = document.getElementById("quiz-answer-form");
        if (!form) return;
        form.addEventListener("submit", (e) => {
            handleQuizAnswerSubmit(e, form);
        });
    }

    async function fetchNextQuestionResponse() {
        const res = await fetch("/Index?handler=NextQuestion");
        const data = await res.json();
        return { res, data };
    }

    async function applyNextQuestion(data, mySeq, cached) {
        const imagesPreloaded = cached && cached.questionImage === data.questionImage;
        await renderQuestion(data, mySeq, { imagesPreloaded });
        invalidatePrefetchCache();
        if (mySeq === questionRenderSeq) answerChecked = false;
    }

    async function processNextQuestionResponse(res, data, mySeq, cached) {
        if (data.redirect) {
            window.location.assign(data.redirect);
            return;
        }
        if (!res.ok) throw new Error("next failed");
        await applyNextQuestion(data, mySeq, cached);
    }

    async function recoverNextQuestionSubmitError(responseReceived, form) {
        if (!responseReceived) {
            form.submit();
            return;
        }
        if (window.showAppAlert) {
            await window.showAppAlert("שגיאה בטעינת השאלה הבאה. נסה שוב.");
        }
    }

    async function handleNextQuestionSubmit(e, form) {
        e.preventDefault();
        if (quizBusy) return;

        const mySeq = ++questionRenderSeq;
        const cached = getCachedNextQuestion();
        setQuizSwapping(true);
        setQuizBusy(true);
        let responseReceived = false;
        try {
            const { res, data } = await fetchNextQuestionResponse();
            responseReceived = true;
            await processNextQuestionResponse(res, data, mySeq, cached);
        } catch {
            await recoverNextQuestionSubmitError(responseReceived, form);
        } finally {
            if (mySeq === questionRenderSeq) setQuizSwapping(false);
            setQuizBusy(false);
        }
    }

    function bindNextQuestion() {
        const form = document.getElementById("next-question-form");
        if (!form) return;
        form.addEventListener("submit", (e) => {
            handleNextQuestionSubmit(e, form);
        });
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

    const REPORT_FORM_DATA_KEYS = new Set([
        "questionImage",
        "explanation",
        "selectedAnswer",
        "correctAnswer",
        "answers"
    ]);

    function buildReportFormPayload(formData) {
        const data = {};
        formData.forEach((value, key) => {
            if (key === "__RequestVerificationToken") return;
            if (REPORT_FORM_DATA_KEYS.has(key)) data[key] = value;
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
            await window.showAppAlert("הדיווח נשלח בהצלחה!");
            form.reset();
            return;
        }
        await window.showAppAlert("אירעה שגיאה בשליחת הדיווח.");
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

    document.addEventListener("DOMContentLoaded", () => {
        const pageData = document.getElementById("quiz-page-data");
        answerChecked = pageData?.dataset.answerChecked === "1";

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
        bindQuizAnswerForm();
        bindNextQuestion();
        bindReportForm();
        bindAppNotice();
        bindFeedbackModal();
        bindGitHubStarModal();
        bindDifficultyChoices();
        bindSoundToggle();
        bindAchievementToast();
        bindAnswerFeedback();
        bindStatsAdvancedPanel();

        bindQuestionImageLoad(document.getElementById("main-question-image"), scheduleQuizViewportAdjust);
        window.addEventListener("resize", scheduleQuizViewportAdjust);
        schedulePrefetchNextQuestion();

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
