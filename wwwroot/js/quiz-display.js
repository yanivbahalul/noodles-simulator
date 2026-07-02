(function () {
    /** Same pattern as demo/normalize-preview: set src, remeasure on load — never clear maxHeight inline. */
    function setQuestionImage(mainImg, url, modalImg) {
        if (!mainImg || !url) return;

        const adjust = () => window.QuizViewport?.scheduleQuizViewportAdjust?.();
        window.QuizViewport?.bindQuestionImageLoad?.(mainImg, adjust);
        mainImg.src = url;
        if (modalImg) modalImg.src = url;
    }

    window.QuizDisplay = { setQuestionImage };
})();
