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
        const d = document.createElement("div");
        d.textContent = text ?? "";
        return d.innerHTML;
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

    async function fetchPrefetchNextQuestion() {
        if (prefetchPromise) return prefetchPromise;

        prefetchPromise = (async () => {
            try {
                const anchor = document.getElementById("quiz-question-image")?.value ?? "";
                if (!anchor) return null;

                const res = await fetch("/Index?handler=PrefetchNextQuestion");
                if (res.status === 204 || !res.ok) return null;

                const data = await res.json();
                if (!data?.questionImage) return null;

                await preloadQuestionImages(data);
                if (document.getElementById("quiz-question-image")?.value !== anchor) return null;

                prefetchAnchor = anchor;
                prefetchedQuestion = data;
                return data;
            } catch {
                return null;
            } finally {
                prefetchPromise = null;
            }
        })();

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

    function scrollQuizIntoView() {
        const container = document.querySelector(".quiz-container");
        if (!container) return;

        const rect = container.getBoundingClientRect();
        if (rect.height <= 0) return;

        const marginTop = 8;
        const marginBottom = 16;
        const viewportH = window.innerHeight;
        const minTop = marginTop;
        const maxBottom = viewportH - marginBottom;
        const available = viewportH - marginTop - marginBottom;

        const topVisible = rect.top >= minTop - 3;
        const bottomVisible = rect.bottom <= maxBottom + 3;
        if (topVisible && bottomVisible) return;

        let deltaY = 0;

        if (rect.height <= available) {
            if (rect.top < minTop) {
                deltaY = rect.top - minTop;
            } else if (rect.bottom > maxBottom) {
                deltaY = rect.bottom - maxBottom;
            }
        } else {
            deltaY = rect.top - minTop;
        }

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

    function adjustQuizViewport() {
        const container = document.querySelector(".quiz-container");
        const mainImg = document.getElementById("main-question-image");
        const answers = document.getElementById("answers-grid");
        const feedback = document.getElementById("answer-feedback");
        const buttonRow = container?.querySelector(".button-row");
        if (!container || !mainImg) return;

        const marginTop = 8;
        const viewportH = window.innerHeight;

        const feedbackHeight = feedback && !feedback.hidden ? feedback.offsetHeight : 0;
        const reservedBelowQuestion =
            (answers?.offsetHeight ?? 0) +
            (buttonRow?.offsetHeight ?? 0) +
            feedbackHeight +
            32;

        const availableForQuestion = viewportH - marginTop - reservedBelowQuestion;
        if (availableForQuestion > 96) {
            const cssCap = viewportH * 0.4;
            const newMax = `${Math.floor(Math.min(cssCap, availableForQuestion))}px`;
            if (mainImg.style.maxHeight !== newMax) {
                mainImg.style.maxHeight = newMax;
            }
        }

        requestAnimationFrame(scrollQuizIntoView);
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

    function updateStreakBadge(streak) {
        let badge = document.getElementById("streak-badge");
        const stack = document.querySelector(".practice-mode-stack");
        if (streak > 0) {
            if (!badge && stack) {
                badge = document.createElement("span");
                badge.id = "streak-badge";
                badge.className = "streak-badge";
                stack.appendChild(badge);
            }
            if (badge) {
                badge.hidden = false;
                badge.textContent = `${streak} 🔥`;
            }
        } else if (badge) {
            badge.hidden = true;
        }
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

    function isAnswerButtonCorrect(btn, data) {
        const key = btn.value;
        const img = btn.querySelector("img");
        const file = img?.dataset?.answerFile ?? "";
        const imgFile = answerFileFromUrl(img?.src ?? "");

        if (data.correctKey && key === data.correctKey) return true;
        if (data.correctAnswerFile && file && file === data.correctAnswerFile) return true;
        if (data.correctAnswerFile && imgFile && imgFile === data.correctAnswerFile) return true;
        if (data.correctAnswerUrl) {
            const correctFile = answerFileFromUrl(data.correctAnswerUrl);
            if (correctFile && imgFile && correctFile === imgFile) return true;
        }

        if (data.answers?.length) {
            const correct = data.answers.find((a) => a.key === data.correctKey);
            if (correct?.fileName && (file === correct.fileName || imgFile === correct.fileName)) return true;
        }

        return false;
    }

    function applyAnswerResult(data) {
        const grid = document.getElementById("answers-grid");
        if (grid) {
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

        const feedback = document.getElementById("answer-feedback");
        if (feedback) {
            feedback.hidden = false;
            feedback.classList.toggle("is-correct", data.isCorrect);
            feedback.classList.toggle("is-incorrect", !data.isCorrect);
            feedback.textContent = data.isCorrect ? "תשובה נכונה!" : "תשובה שגויה";
        }

        playFeedbackSound(data.isCorrect);
        if (data.stats) applyStatsData(data.stats);
        updateStreakBadge(data.stats?.streak ?? 0);
        showAchievementToast(data.achievements);

        const selectedInput = document.querySelector('#report-form input[name="selectedAnswer"]');
        if (selectedInput) selectedInput.value = data.selectedKey ?? "";
        scheduleQuizViewportAdjust();
        schedulePrefetchNextQuestion();
    }

    async function renderQuestion(data, seq, options = {}) {
        if (!options.imagesPreloaded) {
            await preloadQuestionImages(data);
        }
        if (seq !== questionRenderSeq) return;

        const mainImg = document.getElementById("main-question-image");
        const modalImg = document.getElementById("modal-img");
        if (mainImg) mainImg.style.maxHeight = "";
        if (mainImg && data.questionImageUrl) {
            mainImg.src = data.questionImageUrl;
        }
        if (modalImg && data.questionImageUrl) modalImg.src = data.questionImageUrl;

        const hidden = document.getElementById("quiz-question-image");
        if (hidden) hidden.value = data.questionImage ?? "";

        const grid = document.getElementById("answers-grid");
        if (grid && data.answers?.length) {
            grid.innerHTML = "";
            data.answers.forEach((a) => {
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

        const feedback = document.getElementById("answer-feedback");
        if (feedback) {
            feedback.hidden = true;
            feedback.classList.remove("is-correct", "is-incorrect");
            feedback.textContent = "";
        }

        const qInput = document.querySelector('#report-form input[name="questionImage"]');
        const sInput = document.querySelector('#report-form input[name="selectedAnswer"]');
        if (qInput) qInput.value = data.questionImageOriginalName ?? data.questionImage ?? "";
        if (sInput) sInput.value = "";

        if (data.practiceMode === "daily") {
            updatePracticeModeBadge(data);
        }
        scheduleQuizViewportAdjust();
        schedulePrefetchNextQuestion();
    }

    function bindQuizAnswerForm() {
        const form = document.getElementById("quiz-answer-form");
        if (!form) return;

        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            if (answerChecked || quizBusy) return;

            const submitter = e.submitter;
            if (!submitter || submitter.name !== "answer") return;

            const questionImage = form.querySelector('input[name="questionImage"]')?.value;
            const answer = submitter.value;
            const token = getAntiForgeryToken();

            if (!questionImage) {
                if (window.showAppAlert) await window.showAppAlert("שגיאה: השאלה לא נטענה. לחץ «שאלה הבאה» ונסה שוב.");
                return;
            }

            setQuizBusy(true);
            let responseReceived = false;
            try {
                const res = await fetch("/Index?handler=SubmitAnswer", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        RequestVerificationToken: token
                    },
                    body: JSON.stringify({ questionImage, answer })
                });
                responseReceived = true;
                const data = await res.json();
                if (data.redirect) {
                    window.location.assign(data.redirect);
                    return;
                }
                if (!res.ok) throw new Error("submit failed");
                applyAnswerResult(data);
                answerChecked = true;
            } catch {
                if (!responseReceived) {
                    form.requestSubmit(submitter);
                } else if (window.showAppAlert) {
                    await window.showAppAlert("שגיאה בשליחת התשובה. נסה שוב.");
                }
            } finally {
                setQuizBusy(false);
                if (answerChecked) {
                    document.querySelectorAll("#answers-grid .answer-btn").forEach((btn) => {
                        btn.disabled = true;
                    });
                }
            }
        });
    }

    function bindNextQuestion() {
        const form = document.getElementById("next-question-form");
        if (!form) return;

        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            if (quizBusy) return;

            const mySeq = ++questionRenderSeq;
            const cached = getCachedNextQuestion();
            setQuizSwapping(true);
            setQuizBusy(true);
            let responseReceived = false;
            try {
                const res = await fetch("/Index?handler=NextQuestion");
                responseReceived = true;
                const data = await res.json();
                if (data.redirect) {
                    window.location.assign(data.redirect);
                    return;
                }
                if (!res.ok) throw new Error("next failed");

                const imagesPreloaded = cached && cached.questionImage === data.questionImage;
                await renderQuestion(data, mySeq, { imagesPreloaded });
                invalidatePrefetchCache();

                if (mySeq === questionRenderSeq) {
                    answerChecked = false;
                }
            } catch {
                if (!responseReceived) {
                    form.submit();
                } else if (window.showAppAlert) {
                    await window.showAppAlert("שגיאה בטעינת השאלה הבאה. נסה שוב.");
                }
            } finally {
                if (mySeq === questionRenderSeq) {
                    setQuizSwapping(false);
                }
                setQuizBusy(false);
            }
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
                if (key === "questionImage" || key === "explanation" || key === "selectedAnswer")
                    data[key] = value;
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
