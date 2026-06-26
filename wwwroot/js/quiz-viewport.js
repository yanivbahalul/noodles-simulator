(function () {
    let viewportAdjustTimer = null;

    function prefersReducedMotion() {
        return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
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
        const marginTop = 8;
        const marginBottom = 16;
        const minTop = marginTop;
        const maxBottom = viewportH - marginBottom;
        const available = viewportH - marginTop - marginBottom;

        if (isQuizFullyVisible(rect, minTop, maxBottom)) return 0;
        if (rect.height <= available) return computeDeltaWhenFits(rect, minTop, maxBottom);
        return rect.top - minTop;
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

        const marginTop = 8;
        const viewportH = window.innerHeight;
        const reservedBelowQuestion = getReservedBelowQuestion(answers, buttonRow, feedback);
        const availableForQuestion = viewportH - marginTop - reservedBelowQuestion;
        applyQuestionImageMaxHeight(mainImg, availableForQuestion, viewportH);

        requestAnimationFrame(scrollQuizIntoView);
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
        bindQuizViewportHandlers
    };
})();
