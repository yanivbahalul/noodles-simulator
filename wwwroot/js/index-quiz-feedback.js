(function () {
    function answerFileFromUrl(url) {
        if (!url) return "";
        try {
            const pathname = new URL(url, window.location.origin).pathname;
            return decodeURIComponent(pathname.split("/").pop() || "");
        } catch {
            return url.split("/").pop()?.split("?")[0]?.split("#")[0] ?? "";
        }
    }

    function filesMatchCorrectAnswer(file, imgFile, correctAnswerFile) {
        if (!correctAnswerFile) return false;
        return (file && file === correctAnswerFile) ||
            (imgFile && imgFile === correctAnswerFile);
    }

    function matchesCorrectAnswerFromList(file, imgFile, data) {
        if (!data.answers?.length || !data.correctKey) return false;
        const correct = data.answers.find((a) => a.key === data.correctKey);
        if (!correct?.fileName) return false;
        return file === correct.fileName || imgFile === correct.fileName;
    }

    function matchesCorrectAnswerUrl(imgFile, data) {
        if (!data.correctAnswerUrl) return false;
        const correctFile = answerFileFromUrl(data.correctAnswerUrl);
        return Boolean(correctFile && imgFile && correctFile === imgFile);
    }

    function isAnswerButtonCorrect(btn, data) {
        const key = btn.value;
        const img = btn.querySelector("img");
        const file = img?.dataset?.answerFile ?? "";
        const imgFile = answerFileFromUrl(img?.src ?? "");

        const matchers = [
            () => data.correctKey && key === data.correctKey,
            () => filesMatchCorrectAnswer(file, imgFile, data.correctAnswerFile),
            () => matchesCorrectAnswerUrl(imgFile, data),
            () => matchesCorrectAnswerFromList(file, imgFile, data)
        ];
        return matchers.some((match) => match());
    }

    function markAnswerPending(submitter) {
        const grid = document.getElementById("answers-grid");
        if (!grid || !submitter) return;
        grid.querySelectorAll(".answer-btn").forEach((btn) => {
            btn.disabled = true;
            btn.classList.remove("is-selected");
        });
        submitter.classList.add("is-selected");
    }

    function styleAnswerButtons(grid, data) {
        if (!grid) return;
        grid.querySelectorAll(".answer-btn").forEach((btn) => {
            btn.disabled = true;
            btn.classList.remove("correct", "incorrect", "answer-pulse", "answer-shake", "answer-reveal-correct", "is-selected");

            if (isAnswerButtonCorrect(btn, data)) {
                btn.classList.add("correct", data.isCorrect ? "answer-pulse" : "answer-reveal-correct");
            } else if (btn.value === data.selectedKey) {
                btn.classList.add("incorrect", "answer-shake");
            }
        });
    }

    function showAnswerFeedback(data) {
        const feedback = document.getElementById("answer-feedback");
        if (!feedback) return;

        feedback.hidden = false;
        feedback.classList.toggle("is-correct", data.isCorrect);
        feedback.classList.toggle("is-incorrect", !data.isCorrect);
        feedback.textContent = data.isCorrect ? "תשובה נכונה!" : "תשובה שגויה";
    }

    function triggerHaptic(isCorrect) {
        if (!isCorrect || !navigator.vibrate) return;
        try { navigator.vibrate(20); } catch { /* unsupported */ }
    }

    window.IndexQuizFeedback = {
        markAnswerPending,
        styleAnswerButtons,
        showAnswerFeedback,
        triggerHaptic
    };
})();
