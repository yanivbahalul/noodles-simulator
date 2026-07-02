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

    function getReservedBelowQuestion(answers, buttonRow, feedback) {
        const hintHeight = document.getElementById("quiz-keyboard-hint")?.offsetHeight ?? 0;
        const explanationPanel = document.getElementById("question-explanation-panel");
        return (answers?.offsetHeight ?? 0) +
            (buttonRow?.offsetHeight ?? 0) +
            getVisibleHeight(feedback) +
            getExplanationPanelHeight(explanationPanel) +
            hintHeight +
            32;
    }

    function applyQuestionImageMaxHeight(mainImg, availableForQuestion, viewportH) {
        if (availableForQuestion <= 96) return;
        const cssCap = viewportH * 0.58;
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
        scheduleQuizViewportAdjust();
    }

    window.QuizViewport = {
        scheduleQuizViewportAdjust,
        bindQuizViewportHandlers,
        bindQuestionImageLoad
    };
})();
