(function () {
    const panel = () => document.getElementById("question-explanation-panel");
    const btn = () => document.getElementById("question-explanation-btn");
    const btnText = () => document.getElementById("question-explanation-btn-text");
    const videoWrap = () => document.getElementById("question-explanation-video-wrap");
    const video = () => document.getElementById("question-explanation-video");
    const closeBtn = () => document.getElementById("question-explanation-close-btn");

    const BTN_LABEL = "הסבר";
    const RATED_KEY = "explanation-rated:";

    let loadToken = 0;
    let clickHandler = null;
    let currentQuestionId = "";
    let selectedStars = 0;

    const ratingPanel = () => document.getElementById("question-explanation-rating");
    const ratingThanks = () => document.getElementById("question-explanation-rating-thanks");
    const ratingFeedback = () => document.getElementById("question-explanation-feedback");
    const ratingSubmit = () => document.getElementById("question-explanation-rating-submit");
    const starBtns = () => document.querySelectorAll(".explanation-star-btn");

    function alreadyRated(questionId) {
        if (!questionId) return false;
        try { return localStorage.getItem(RATED_KEY + questionId) === "1"; } catch { return false; }
    }

    function markRated(questionId) {
        try { localStorage.setItem(RATED_KEY + questionId, "1"); } catch { /* ignore */ }
    }

    function clearRatingForm() {
        selectedStars = 0;
        const panel = ratingPanel();
        const thanks = ratingThanks();
        const fb = ratingFeedback();
        const submit = ratingSubmit();
        if (thanks) thanks.hidden = true;
        if (fb) { fb.hidden = true; fb.value = ""; }
        if (submit) { submit.hidden = true; submit.disabled = false; submit.textContent = "שלח דירוג"; }
        panel?.querySelector(".question-explanation-rating-prompt")?.removeAttribute("hidden");
        panel?.querySelector(".explanation-stars")?.removeAttribute("hidden");
        starBtns().forEach((btn) => {
            btn.classList.remove("is-selected", "is-active");
            btn.disabled = false;
        });
    }

    function resetRatingUi() {
        clearRatingForm();
        const panel = ratingPanel();
        if (panel) panel.hidden = true;
    }

    function paintStars(count) {
        starBtns().forEach((btn) => {
            const n = Number(btn.dataset.star || 0);
            btn.classList.toggle("is-active", n > 0 && n <= count);
        });
    }

    function showRatingPrompt(questionId) {
        if (!questionId || alreadyRated(questionId)) {
            resetRatingUi();
            return;
        }
        const panel = ratingPanel();
        if (!panel) return;
        clearRatingForm();
        panel.hidden = false;
        starBtns().forEach((btn) => {
            if (btn.dataset.bound === "1") return;
            btn.dataset.bound = "1";
            btn.addEventListener("click", (e) => {
                e.preventDefault();
                selectedStars = Number(btn.dataset.star || 0);
                paintStars(selectedStars);
                const fb = ratingFeedback();
                const submit = ratingSubmit();
                if (fb) {
                    fb.hidden = false;
                    fb.placeholder = selectedStars <= 3
                        ? "מה לא היה תקין? (מומלץ לפרט)"
                        : "מה לא היה תקין? (אופציונלי)";
                }
                if (submit) submit.hidden = false;
                btn.blur();
            });
        });
        const submit = ratingSubmit();
        if (submit && submit.dataset.bound !== "1") {
            submit.dataset.bound = "1";
            submit.addEventListener("click", () => submitRating(questionId));
        }
        scheduleViewport();
    }

    async function submitRating(questionId) {
        if (!questionId || selectedStars < 1) return;
        const submit = ratingSubmit();
        const fb = ratingFeedback();
        if (submit) submit.disabled = true;
        try {
            const res = await fetch("/api/question-explanation-rating", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    questionId,
                    stars: selectedStars,
                    feedback: fb?.value?.trim() || ""
                })
            });
            if (!res.ok) throw new Error("save failed");
            markRated(questionId);
            const panel = ratingPanel();
            const thanks = ratingThanks();
            if (panel) {
                panel.querySelector(".question-explanation-rating-prompt")?.setAttribute("hidden", "");
                panel.querySelector(".explanation-stars")?.setAttribute("hidden", "");
                if (fb) fb.hidden = true;
                if (submit) submit.hidden = true;
            }
            if (thanks) thanks.hidden = false;
            starBtns().forEach((btn) => { btn.disabled = true; });
        } catch {
            if (submit) {
                submit.disabled = false;
                submit.textContent = "שגיאה — נסה שוב";
            }
        }
    }

    function scheduleViewport() {
        window.QuizViewport?.scheduleQuizViewportAdjust?.();
    }

    function setBtnText(text) {
        const el = btnText();
        if (el) el.textContent = text;
    }

    function setCloseVisible(visible) {
        const c = closeBtn();
        if (c) c.hidden = !visible;
    }

    function bindCloseButton() {
        const c = closeBtn();
        if (!c || c.dataset.bound === "1") return;
        c.dataset.bound = "1";
        c.addEventListener("click", (e) => {
            e.preventDefault();
            closeExplanation();
        });
    }

    function closeExplanation() {
        const p = panel();
        const b = btn();
        const wrap = videoWrap();
        const v = video();
        if (!p) return;

        loadToken += 1;
        v?.pause();
        if (wrap) wrap.hidden = true;
        p.classList.remove("is-playing", "is-loading-video");
        resetRatingUi();
        setCloseVisible(false);
        if (b) {
            b.hidden = false;
            b.disabled = false;
        }
        setBtnText(BTN_LABEL);
        scheduleViewport();
    }

    function reset() {
        loadToken += 1;
        currentQuestionId = "";
        resetRatingUi();
        const p = panel();
        const b = btn();
        const wrap = videoWrap();
        const v = video();
        if (p) {
            p.hidden = true;
            p.classList.remove("is-playing", "is-loading-video");
        }
        if (b) {
            b.hidden = false;
            b.disabled = false;
            if (clickHandler) {
                b.removeEventListener("click", clickHandler);
                clickHandler = null;
            }
        }
        setBtnText(BTN_LABEL);
        if (wrap) wrap.hidden = true;
        setCloseVisible(false);
        if (v) {
            v.pause();
            v.removeAttribute("src");
            v.load();
        }
    }

    async function fetchExplanationUrl(questionId) {
        const res = await fetch(
            `/api/question-explanation?questionId=${encodeURIComponent(questionId)}`,
            { credentials: "same-origin" }
        );
        if (!res.ok) return null;
        const data = await res.json();
        return data?.hasExplanation && data?.videoUrl ? data.videoUrl : null;
    }

    function waitForVideoReady(v, timeoutMs) {
        return new Promise((resolve, reject) => {
            if (v.readyState >= HTMLMediaElement.HAVE_FUTURE_DATA) {
                resolve();
                return;
            }
            let settled = false;
            const finish = (ok) => {
                if (settled) return;
                settled = true;
                cleanup();
                ok ? resolve() : reject(new Error("video error"));
            };
            const timer = setTimeout(() => finish(false), timeoutMs);
            const cleanup = () => {
                clearTimeout(timer);
                v.removeEventListener("loadeddata", onReady);
                v.removeEventListener("canplay", onReady);
                v.removeEventListener("error", onErr);
            };
            const onReady = () => finish(true);
            const onErr = () => finish(false);
            v.addEventListener("loadeddata", onReady);
            v.addEventListener("canplay", onReady);
            v.addEventListener("error", onErr);
        });
    }

    async function playExplanation(questionId, isCorrect) {
        const p = panel();
        const b = btn();
        const wrap = videoWrap();
        const v = video();
        if (!p || !b || !wrap || !v || !questionId) return;

        const token = ++loadToken;
        b.disabled = true;
        setBtnText("טוען הסבר...");
        wrap.hidden = false;
        setCloseVisible(true);
        p.classList.add("is-loading-video");
        scheduleViewport();

        try {
            const url = await fetchExplanationUrl(questionId);
            if (token !== loadToken) return;
            if (!url) {
                wrap.hidden = true;
                setCloseVisible(false);
                p.classList.remove("is-loading-video");
                setBtnText("אין הסבר לשאלה זו");
                b.disabled = false;
                return;
            }

            v.pause();
            v.removeAttribute("src");
            v.src = url;
            v.load();
            await waitForVideoReady(v, 90000);
            if (token !== loadToken) return;

            p.classList.remove("is-loading-video");
            p.classList.add("is-playing");
            b.hidden = true;
            currentQuestionId = questionId;
            showRatingPrompt(questionId);
            scheduleViewport();
            try {
                await v.play();
            } catch {
                // autoplay blocked — native controls still work
            }
        } catch {
            if (token !== loadToken) return;
            p.classList.remove("is-loading-video");
            wrap.hidden = true;
            setCloseVisible(false);
            b.hidden = false;
            b.disabled = false;
            setBtnText("שגיאה בטעינה — נסה שוב");
            v.pause();
            v.removeAttribute("src");
            v.load();
        }
    }

    function showIfAvailable(questionId, isCorrect, hasExplanation) {
        if (!questionId || !hasExplanation) {
            reset();
            return;
        }

        const p = panel();
        const b = btn();
        const wrap = videoWrap();
        const v = video();
        if (!p || !b) return;

        p.hidden = false;
        currentQuestionId = questionId;
        p.classList.remove("is-playing", "is-loading-video");
        b.hidden = false;
        b.disabled = false;
        if (wrap) wrap.hidden = true;
        if (v) {
            v.pause();
            v.removeAttribute("src");
            v.load();
        }
        setBtnText(BTN_LABEL);

        if (clickHandler) b.removeEventListener("click", clickHandler);
        clickHandler = (e) => {
            e.preventDefault();
            playExplanation(questionId, isCorrect);
        };
        b.addEventListener("click", clickHandler);
        bindCloseButton();
        scheduleViewport();
    }

    async function showAfterAnswer(questionId, isCorrect) {
        if (!questionId) {
            reset();
            return;
        }
        try {
            const res = await fetch(
                `/api/question-explanation?questionId=${encodeURIComponent(questionId)}`,
                { credentials: "same-origin" }
            );
            if (!res.ok) {
                reset();
                return;
            }
            const data = await res.json();
            showIfAvailable(questionId, isCorrect, !!(data?.hasExplanation && data?.videoUrl));
        } catch {
            reset();
        }
    }

    function showForWrongAnswer(questionId) {
        return showAfterAnswer(questionId, false);
    }

    window.QuestionExplanation = {
        reset,
        closeExplanation,
        showIfAvailable,
        showAfterAnswer,
        showForWrongAnswer,
        playExplanation
    };
})();
