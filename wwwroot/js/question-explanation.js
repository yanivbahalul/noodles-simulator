(function () {
    const panel = () => document.getElementById("question-explanation-panel");
    const btn = () => document.getElementById("question-explanation-btn");
    const btnText = () => document.getElementById("question-explanation-btn-text");
    const videoWrap = () => document.getElementById("question-explanation-video-wrap");
    const video = () => document.getElementById("question-explanation-video");

    const BTN_WRONG = "למה טעיתי? ▶";
    const BTN_CORRECT = "צפה בהסבר ▶";

    let loadToken = 0;
    let clickHandler = null;

    function scheduleViewport() {
        window.QuizViewport?.scheduleQuizViewportAdjust?.();
    }

    function setBtnText(text) {
        const el = btnText();
        if (el) el.textContent = text;
    }

    function reset() {
        loadToken += 1;
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
        setBtnText(BTN_WRONG);
        if (wrap) wrap.hidden = true;
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
        p.classList.add("is-loading-video");
        scheduleViewport();

        try {
            const url = await fetchExplanationUrl(questionId);
            if (token !== loadToken) return;
            if (!url) {
                wrap.hidden = true;
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
        p.classList.remove("is-playing", "is-loading-video");
        b.hidden = false;
        b.disabled = false;
        if (wrap) wrap.hidden = true;
        if (v) {
            v.pause();
            v.removeAttribute("src");
            v.load();
        }
        setBtnText(isCorrect ? BTN_CORRECT : BTN_WRONG);

        if (clickHandler) b.removeEventListener("click", clickHandler);
        clickHandler = (e) => {
            e.preventDefault();
            playExplanation(questionId, isCorrect);
        };
        b.addEventListener("click", clickHandler);
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
        showIfAvailable,
        showAfterAnswer,
        showForWrongAnswer,
        playExplanation
    };
})();
