(function () {
    function renderAnswerButtons(grid, answers, options = {}) {
        const eager = options.eager === true;
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
            img.loading = eager ? "eager" : "lazy";
            if (eager) img.fetchPriority = "high";
            if (a.fileName) img.dataset.answerFile = a.fileName;
            btn.appendChild(img);
            grid.appendChild(btn);
        });
    }

    window.QuizAnswers = { renderAnswerButtons };
})();
