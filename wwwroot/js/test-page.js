(function () {
    let testBusy = false;

    function openImageModal(imageUrl) {
        const modal = document.getElementById("image-modal");
        const modalImg = document.getElementById("modal-img");
        if (!modal || !modalImg) return;
        modalImg.src = imageUrl || "";
        modal.classList.add("modal-open");
    }

    function closeImageModal() {
        const modal = document.getElementById("image-modal");
        if (modal) modal.classList.remove("modal-open");
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

    function getAntiForgeryToken() {
        return document.querySelector('#test-answer-form input[name="__RequestVerificationToken"]')?.value ?? "";
    }

    function setTestBusy(on) {
        testBusy = on;
        const container = document.querySelector(".quiz-container");
        if (container) container.classList.toggle("quiz-loading", on);
        document.querySelectorAll("#answers-grid .answer-btn").forEach((btn) => {
            btn.disabled = on;
        });
    }

    function scheduleQuizViewportAdjust() {
        window.QuizViewport?.scheduleQuizViewportAdjust();
    }

    function updateTestProgress(data) {
        const fill = document.getElementById("test-progress-fill");
        const label = document.getElementById("test-progress-label");
        const questionNumber = document.getElementById("test-question-number");
        if (fill) fill.style.width = `${data.progressPercent}%`;
        if (label) {
            label.textContent = `התקדמות: ${data.displayQuestionNumber} / ${data.totalQuestions}`;
        }
        if (questionNumber) questionNumber.textContent = String(data.displayQuestionNumber);
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
            img.loading = "lazy";
            btn.appendChild(img);
            grid.appendChild(btn);
        });
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
        updateTestProgress(data);
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

    async function handleTestAnswerSubmit(e, form) {
        e.preventDefault();
        if (testBusy) return;

        const submitter = e.submitter;
        if (!submitter || submitter.name !== "answer") return;

        const token = form.querySelector('input[name="token"]')?.value;
        const answer = submitter.value;
        if (!token || !answer) return;

        setTestBusy(true);
        let responseReceived = false;
        try {
            const { res, data } = await submitTestAnswer(token, answer);
            responseReceived = true;
            if (data.redirect) {
                window.location.assign(data.redirect);
                return;
            }
            if (!res.ok) throw new Error("submit failed");
            renderTestQuestion(data);
        } catch {
            if (!responseReceived) {
                form.requestSubmit(submitter);
                return;
            }
            if (window.showAppToast) {
                window.showAppToast("שגיאה בשליחת התשובה. נסה שוב.");
            } else if (window.showAppAlert) {
                await window.showAppAlert("שגיאה בשליחת התשובה. נסה שוב.");
            }
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
        bindClick("main-question-image", () => {
            const mainImg = document.getElementById("main-question-image");
            openImageModal(mainImg?.src || "");
        });
        bindClick("close-image-modal-btn", closeImageModal);
        bindModalDismiss("image-modal", closeImageModal);
        bindTestAnswerForm();

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
