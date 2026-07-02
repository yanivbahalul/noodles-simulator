(function () {
    const prefetch = window.IndexQuizPrefetch;
    const feedback = window.IndexQuizFeedback;

    let answerChecked = false;
    let quizBusy = false;
    let questionRenderSeq = 0;

    function getAntiForgeryToken() {
        return document.querySelector('#quiz-answer-form input[name="__RequestVerificationToken"]')?.value ?? "";
    }

    function setQuizLoading(on) {
        const container = document.querySelector(".quiz-container");
        if (container) container.classList.toggle("quiz-loading", on);
    }

    function setQuizBusy(on, options = {}) {
        quizBusy = on;
        window.RequestChannels?.notifyQuizBusy(on);
        if (!options.silent) setQuizLoading(on);
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

    function scheduleQuizViewportAdjust() {
        window.QuizViewport?.scheduleQuizViewportAdjust?.();
    }

    function updatePracticeModeBadge(data) {
        const badge = document.getElementById("practice-mode-badge");
        if (!badge || !data.practiceModeLabel) return;
        if (data.practiceMode === "daily") {
            badge.innerHTML = `${window.escapeHtml(data.practiceModeLabel)} <span class="practice-mode-daily">${data.dailyProgress}/${data.dailyTotal}</span>`;
        } else {
            badge.textContent = data.practiceModeLabel;
        }
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
        feedback?.styleAnswerButtons?.(document.getElementById("answers-grid"), data);
        feedback?.showAnswerFeedback?.(data);

        window.IndexPage?.playFeedbackSound?.(data.isCorrect);
        feedback?.triggerHaptic?.(data.isCorrect);
        window.IndexPage?.applyAnswerSideEffects?.(data);

        syncReportFormFromAnswerResult(data);
        const questionId = document.getElementById("quiz-question-image")?.value ?? "";
        if (questionId && data.hasExplanation) {
            window.QuestionExplanation?.showIfAvailable?.(questionId, !!data.isCorrect, true);
        } else {
            window.QuestionExplanation?.reset?.();
        }
        scheduleQuizViewportAdjust();
        prefetch?.schedulePrefetchNextQuestion?.();
    }

    function updateQuestionImages(mainImg, modalImg, data) {
        window.QuizDisplay?.setQuestionImage?.(mainImg, data.questionImageUrl, modalImg);
    }

    function renderAnswerButtons(grid, answers) {
        window.QuizAnswers?.renderAnswerButtons(grid, answers, { eager: true });
    }

    function clearAnswerFeedback(el) {
        if (!el) return;
        el.hidden = true;
        el.classList.remove("is-correct", "is-incorrect");
        el.textContent = "";
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
        window.QuestionExplanation?.reset?.();
        syncReportFormForQuestion(data);
        if (data.practiceMode === "daily") updatePracticeModeBadge(data);
    }

    async function renderQuestion(data, seq, options = {}) {
        if (!options.imagesPreloaded) {
            await prefetch?.preloadQuestionImages?.(data, true);
        }
        if (seq !== questionRenderSeq) return;

        applyRenderedQuestionDom(data);
        scheduleQuizViewportAdjust();
        prefetch?.schedulePrefetchNextQuestion?.();
    }

    function getQuizAnswerPayload(e, form) {
        const submitter = e.submitter;
        if (!submitter || submitter.name !== "answer") return null;
        const questionImage = form.querySelector('input[name="questionImage"]')?.value;
        if (!questionImage) return { missingQuestion: true };
        return { questionImage, answer: submitter.value, submitter };
    }

    async function submitQuizAnswer(questionImage, answer, token) {
        const res = await window.RequestChannels.quizFetch("/Index?handler=SubmitAnswer", {
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
        if (window.showAppToast) {
            window.showAppToast("שגיאה בשליחת התשובה. נסה שוב.");
        } else if (window.showAppAlert) {
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
        setQuizBusy(true, { silent: true });
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

        feedback?.markAnswerPending?.(payload.submitter);
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
        const res = await window.RequestChannels.quizFetch("/Index?handler=NextQuestion");
        const data = await res.json();
        return { res, data };
    }

    async function applyNextQuestion(data, mySeq, cached) {
        const imagesPreloaded = cached && cached.questionImage === data.questionImage;
        await renderQuestion(data, mySeq, { imagesPreloaded });
        prefetch?.invalidatePrefetchCache?.();
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
        if (window.showAppToast) {
            window.showAppToast("שגיאה בטעינת השאלה הבאה. נסה שוב.");
        } else if (window.showAppAlert) {
            await window.showAppAlert("שגיאה בטעינת השאלה הבאה. נסה שוב.");
        }
    }

    async function handleNextQuestionSubmit(e, form) {
        e.preventDefault();
        if (quizBusy) return;

        const mySeq = ++questionRenderSeq;
        const cached = prefetch?.getCachedNextQuestion?.();
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

    window.IndexQuiz = {
        bindQuizAnswerForm,
        bindNextQuestion,
        schedulePrefetchNextQuestion: () => prefetch?.schedulePrefetchNextQuestion?.(),
        setAnswerChecked: (value) => { answerChecked = value; },
        isAnswerChecked: () => answerChecked,
        isQuizBusy: () => quizBusy,
        syncStreakBadgeFromPage: () => {
            const stat = document.getElementById("stat-streak");
            const parsed = parseInt(stat?.textContent ?? "0", 10);
            window.IndexPage?.updateStreakBadge?.(Number.isFinite(parsed) ? parsed : 0);
        }
    };
})();
