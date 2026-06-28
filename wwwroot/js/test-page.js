(function () {
    let testBusy = false;
    let currentIndex = 0;
    let totalQuestions = 0;

    function bindClick(id, handler) {
        const el = document.getElementById(id);
        if (el) el.addEventListener("click", handler);
    }

    function getAntiForgeryToken() {
        return document.querySelector('#test-answer-form input[name="__RequestVerificationToken"]')?.value ?? "";
    }

    function getTestToken() {
        return document.querySelector('#test-answer-form input[name="token"]')?.value ?? "";
    }

    function setTestBusy(on) {
        testBusy = on;
        const container = document.querySelector(".quiz-container");
        if (container) container.classList.toggle("quiz-loading", on);
        document.querySelectorAll("#answers-grid .answer-btn").forEach((btn) => {
            btn.disabled = on;
        });
        document.querySelectorAll(".test-question-pill, .test-nav-arrow").forEach((btn) => {
            btn.disabled = on;
        });
    }

    function scheduleQuizViewportAdjust() {
        window.QuizViewport?.scheduleQuizViewportAdjust?.();
    }

    function updateTestProgress(data) {
        const fill = document.getElementById("test-progress-fill");
        const label = document.getElementById("test-progress-label");
        const questionNumber = document.getElementById("test-question-number");
        const answered = data.answeredCount ?? 0;
        const total = data.totalQuestions ?? totalQuestions;
        const displayNum = data.displayQuestionNumber ?? (currentIndex + 1);
        if (fill) fill.style.width = `${data.progressPercent ?? 0}%`;
        if (label) {
            label.textContent = `התקדמות: ענית על ${answered} / ${total} · שאלה ${displayNum}`;
        }
        if (questionNumber) questionNumber.textContent = String(displayNum);
    }

    function updateQuestionPills(data) {
        const pills = document.querySelectorAll(".test-question-pill");
        const answered = data.answeredByIndex ?? [];
        const idx = data.currentIndex ?? currentIndex;
        currentIndex = idx;
        pills.forEach((pill) => {
            const pillIndex = parseInt(pill.dataset.questionIndex ?? "-1", 10);
            const isAnswered = Boolean(answered[pillIndex]);
            const isCurrent = pillIndex === idx;
            pill.classList.toggle("is-answered", isAnswered);
            pill.classList.toggle("is-current", isCurrent);
            pill.setAttribute("aria-selected", isCurrent ? "true" : "false");
        });
        const prevBtn = document.getElementById("test-prev-btn");
        const nextBtn = document.getElementById("test-next-btn");
        if (prevBtn) prevBtn.disabled = testBusy || idx <= 0;
        if (nextBtn) nextBtn.disabled = testBusy || idx >= totalQuestions - 1;
    }

    function highlightSelectedAnswer(selectedKey) {
        document.querySelectorAll("#answers-grid .answer-btn").forEach((btn) => {
            const selected = Boolean(selectedKey && btn.value === selectedKey);
            btn.classList.toggle("is-selected", selected);
            btn.setAttribute("aria-pressed", selected ? "true" : "false");
        });
    }

    function renderAnswerButtons(grid, answers) {
        window.QuizAnswers?.renderAnswerButtons(grid, answers);
    }

    function renderTestQuestion(data) {
        const mainImg = document.getElementById("main-question-image");
        const modalImg = document.getElementById("modal-img");
        const grid = document.getElementById("answers-grid");
        if (!grid) return;

        if (mainImg) {
            mainImg.style.maxHeight = "";
            mainImg.src = data.questionImageUrl;
            mainImg.addEventListener("load", scheduleQuizViewportAdjust, { once: true });
        }
        if (modalImg) modalImg.src = data.questionImageUrl;

        renderAnswerButtons(grid, data.answers);
        highlightSelectedAnswer(data.selectedAnswerKey);
        if (data.currentIndex !== undefined) currentIndex = data.currentIndex;
        if (data.totalQuestions) totalQuestions = data.totalQuestions;
        updateTestProgress(data);
        updateQuestionPills(data);
        syncCountdown(data.remainingSeconds);
        scheduleQuizViewportAdjust();
    }

    async function submitTestAnswer(token, answer) {
        const res = await fetch("/Test?handler=SubmitAnswer", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: getAntiForgeryToken()
            },
            body: JSON.stringify({ token, answer })
        });
        const data = await res.json();
        return { res, data };
    }

    async function navigateTestQuestion(token, index) {
        const res = await fetch("/Test?handler=Navigate", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                RequestVerificationToken: getAntiForgeryToken()
            },
            body: JSON.stringify({ token, index })
        });
        const data = await res.json();
        return { res, data };
    }

    async function showTestSubmitError() {
        if (window.showAppToast) {
            window.showAppToast("שגיאה בשליחת התשובה. נסה שוב.");
        } else if (window.showAppAlert) {
            await window.showAppAlert("שגיאה בשליחת התשובה. נסה שוב.");
        }
    }

    async function showTestNavigateError() {
        if (window.showAppToast) {
            window.showAppToast("שגיאה במעבר לשאלה. נסה שוב.");
        } else if (window.showAppAlert) {
            await window.showAppAlert("שגיאה במעבר לשאלה. נסה שוב.");
        }
    }

    async function handleTestResponse(res, data, onError) {
        if (data.redirect) {
            window.location.assign(data.redirect);
            return true;
        }
        if (!res.ok) {
            await onError();
            return true;
        }
        renderTestQuestion(data);
        return false;
    }

    async function handleTestAnswerSubmit(e, form) {
        e.preventDefault();
        if (testBusy) return;

        const submitter = e.submitter;
        if (!submitter || submitter.name !== "answer") return;

        const token = getTestToken();
        const answer = submitter.value;
        if (!token || !answer) return;

        setTestBusy(true);
        let responseReceived = false;
        try {
            const { res, data } = await submitTestAnswer(token, answer);
            responseReceived = true;
            if (await handleTestResponse(res, data, showTestSubmitError)) return;
        } catch {
            if (!responseReceived) {
                form.requestSubmit(submitter);
                return;
            }
            await showTestSubmitError();
        } finally {
            setTestBusy(false);
        }
    }

    async function goToQuestion(index) {
        if (testBusy || index === currentIndex) return;
        const token = getTestToken();
        if (!token) return;

        setTestBusy(true);
        try {
            const { res, data } = await navigateTestQuestion(token, index);
            await handleTestResponse(res, data, showTestNavigateError);
        } catch {
            await showTestNavigateError();
        } finally {
            setTestBusy(false);
        }
    }

    function bindTestAnswerForm() {
        const form = document.getElementById("test-answer-form");
        if (!form) return;
        form.addEventListener("submit", (e) => {
            handleTestAnswerSubmit(e, form);
        });
    }

    function bindTestNavigation() {
        document.querySelectorAll(".test-question-pill").forEach((pill) => {
            pill.addEventListener("click", () => {
                const index = parseInt(pill.dataset.questionIndex ?? "-1", 10);
                if (Number.isFinite(index) && index >= 0) goToQuestion(index);
            });
        });
        bindClick("test-prev-btn", () => {
            if (currentIndex > 0) goToQuestion(currentIndex - 1);
        });
        bindClick("test-next-btn", () => {
            if (currentIndex < totalQuestions - 1) goToQuestion(currentIndex + 1);
        });
    }

    function formatCountdown(totalSeconds) {
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;
        const pad = (n) => (n < 10 ? `0${n}` : String(n));
        return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    }

    let countdownRemaining = null;
    let countdownTimer = null;

    function updateCountdownDisplay() {
        const el = document.getElementById("countdown");
        if (el && countdownRemaining !== null) {
            el.textContent = formatCountdown(countdownRemaining);
        }
    }

    function syncCountdown(remainingSeconds) {
        if (remainingSeconds === undefined || remainingSeconds === null) return;
        countdownRemaining = Math.max(0, Math.floor(remainingSeconds));
        updateCountdownDisplay();
    }

    function startCountdown(initialSeconds) {
        countdownRemaining = Math.max(0, Math.floor(initialSeconds));
        if (countdownTimer) clearTimeout(countdownTimer);

        function tick() {
            updateCountdownDisplay();
            if (countdownRemaining <= 0) {
                window.location.reload();
                return;
            }
            countdownRemaining--;
            countdownTimer = setTimeout(tick, 1000);
        }
        tick();
    }

    function initTestPage(remainingSeconds) {
        const currentPill = document.querySelector(".test-question-pill.is-current");
        if (currentPill) {
            currentIndex = parseInt(currentPill.dataset.questionIndex ?? "0", 10);
        }
        totalQuestions = document.querySelectorAll(".test-question-pill").length || 17;

        bindClick("main-question-image", () => {
            const mainImg = document.getElementById("main-question-image");
            window.ImageModal?.openImageModal(mainImg?.src || "");
        });
        bindClick("close-image-modal-btn", () => window.ImageModal?.closeImageModal());
        const imageModal = document.getElementById("image-modal");
        if (imageModal) {
            imageModal.addEventListener("click", (e) => {
                if (e.target === imageModal) window.ImageModal?.closeImageModal();
            });
        }
        bindTestAnswerForm();
        bindTestNavigation();

        if (window.bindConfirmEndTestButtons) {
            window.bindConfirmEndTestButtons("האם אתה בטוח שברצונך לסיים את המבחן? התוצאות יישמרו.");
        }

        window.QuizViewport?.bindQuizViewportHandlers();
        startCountdown(remainingSeconds);
    }

    document.addEventListener("DOMContentLoaded", () => {
        const data = document.getElementById("test-page-data");
        if (!data) return;
        const remaining = parseInt(data.dataset.testRemainingSeconds ?? "0", 10);
        initTestPage(Number.isFinite(remaining) ? remaining : 0);
    });
})();
