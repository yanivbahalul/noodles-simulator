(function () {
    let viewportAdjustTimer = null;

    function prefersReducedMotion() {
        return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

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

    function getVisibleHeight(el) {
        return el && !el.hidden ? el.offsetHeight : 0;
    }

    function getExplanationPanelHeight(panel) {
        if (!panel || panel.hidden) return 0;
        // ponytail: fixed reserve while video plays — rating form expand must not shrink question image / scroll
        if (panel.classList.contains("is-playing")) {
            const wrap = document.getElementById("question-explanation-video-wrap");
            const videoH = wrap && !wrap.hidden ? wrap.offsetHeight : 0;
            return videoH + 200;
        }
        return panel.offsetHeight;
    }

    function getReservedBelowQuestion(answers, feedback) {
        const explanationPanel = document.getElementById("question-explanation-panel");
        // ponytail: demo only reserves answers (+ visible post-answer UI) — not button row / keyboard hint
        return (answers?.offsetHeight ?? 0) +
            getVisibleHeight(feedback) +
            getExplanationPanelHeight(explanationPanel) +
            24;
    }

    function isPostAnswerLayout() {
        const feedback = document.getElementById("answer-feedback");
        if (feedback && !feedback.hidden && feedback.textContent?.trim()) return true;
        const panel = document.getElementById("question-explanation-panel");
        return Boolean(panel && !panel.hidden);
    }

    function displayWidth(mainImg) {
        const w = mainImg.offsetWidth || mainImg.clientWidth;
        if (w > 1) return w;
        return mainImg.parentElement?.clientWidth || 0;
    }

    function naturalDisplayHeight(mainImg, cssCap) {
        const nw = mainImg.naturalWidth;
        const nh = mainImg.naturalHeight;
        if (!nw || !nh) return cssCap;
        const w = displayWidth(mainImg);
        if (!w) return cssCap;
        return Math.min(cssCap, Math.ceil((nh * w) / nw));
    }

    function applyQuestionImageMaxHeight(mainImg, availableForQuestion, viewportH) {
        // ponytail: pre-answer sizing is CSS-only (width 100% + max-height) — inline maxHeight here caused load-time jump
        if (!isPostAnswerLayout()) {
            mainImg.style.removeProperty("max-height");
            return;
        }
        if (!mainImg.complete || !mainImg.naturalWidth || !mainImg.naturalHeight) return;

        const cssCap = Math.floor(viewportH * 0.58);
        let cap = Math.min(cssCap, naturalDisplayHeight(mainImg, cssCap));
        if (availableForQuestion > 96) {
            cap = Math.min(cap, availableForQuestion);
        }
        if (cap <= 96) return;
        const newMax = `${cap}px`;
        if (mainImg.style.maxHeight !== newMax) {
            mainImg.style.maxHeight = newMax;
        }
    }

    function adjustQuizViewport() {
        const container = document.querySelector(".quiz-container");
        const mainImg = document.getElementById("main-question-image");
        const answers = document.getElementById("answers-grid");
        const feedback = document.getElementById("answer-feedback");
        if (!container || !mainImg) return;
        if (!mainImg.complete) return;

        const viewportH = window.innerHeight;
        const { minTop } = getQuizScrollMargins(viewportH);
        const containerRect = container.getBoundingClientRect();
        const reservedBelowQuestion = getReservedBelowQuestion(answers, feedback);
        const availableForQuestion = viewportH - Math.max(minTop, containerRect.top) - reservedBelowQuestion;
        applyQuestionImageMaxHeight(mainImg, availableForQuestion, viewportH);

        requestAnimationFrame(() => {
            requestAnimationFrame(scrollQuizIntoView);
        });
    }

    function scheduleQuizViewportAdjust() {
        if (viewportAdjustTimer) clearTimeout(viewportAdjustTimer);
        viewportAdjustTimer = setTimeout(() => {
            viewportAdjustTimer = null;
            adjustQuizViewport();
        }, 150);
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

    function bindQuizViewportHandlers() {
        bindQuestionImageLoad(
            document.getElementById("main-question-image"),
            scheduleQuizViewportAdjust
        );
        window.addEventListener("resize", scheduleQuizViewportAdjust);
    }

    window.QuizViewport = {
        scheduleQuizViewportAdjust,
        bindQuizViewportHandlers,
        bindQuestionImageLoad
    };
})();
