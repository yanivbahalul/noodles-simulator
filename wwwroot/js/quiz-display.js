(function () {
    /** Demo pattern: onload → adjust, then set src, then complete check. */
    function setQuestionImage(mainImg, url, modalImg) {
        if (!mainImg || !url) return;

        mainImg.style.removeProperty("max-height");
        const adjust = () => window.QuizViewport?.scheduleQuizViewportAdjust?.();
        mainImg.onload = adjust;
        mainImg.onerror = adjust;
        mainImg.src = url;
        if (modalImg) modalImg.src = url;
        if (mainImg.complete) adjust();
    }

    window.QuizDisplay = { setQuestionImage };
})();
