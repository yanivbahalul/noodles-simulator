(function () {
    let items = [];
    let currentIndex = 0;
    let totalQuestions = 0;

    function bindClick(id, handler) {
        const el = document.getElementById(id);
        if (el) el.addEventListener("click", handler);
    }

    function scheduleQuizViewportAdjust() {
        window.QuizViewport?.scheduleQuizViewportAdjust?.();
    }

    function decodeItems() {
        const el = document.getElementById("test-results-items-json");
        if (!el) return [];
        try {
            const parsed = JSON.parse(el.textContent || "[]");
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    }

    function statusText(item) {
        if (!item) return "—";
        if (item.isCorrect) return "✓ נכון";
        if (item.isAnswered) return "✗ לא נכון";
        return "לא ענית";
    }

    function statusClass(item) {
        if (!item) return "";
        if (item.isCorrect) return " is-correct";
        if (item.isAnswered) return " is-wrong";
        return " is-unanswered";
    }

    function renderReviewAnswers(grid, item) {
        if (!grid || !item) {
            if (grid) grid.innerHTML = "";
            return;
        }

        grid.innerHTML = "";
        const urls = item.answerUrls ?? {};
        Object.entries(urls).forEach(([key, url]) => {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.disabled = true;
            btn.className = "answer-btn";
            if (key === item.correctKey) btn.classList.add("correct");
            else if (key === item.selectedKey && !item.isCorrect) btn.classList.add("incorrect");

            const img = document.createElement("img");
            img.src = url;
            img.alt = "תשובה";
            img.loading = "lazy";
            btn.appendChild(img);
            grid.appendChild(btn);
        });
    }

    function updateQuestionPills() {
        document.querySelectorAll("#test-results-question-pills .test-question-pill").forEach((pill) => {
            const pillIndex = parseInt(pill.dataset.questionIndex ?? "-1", 10);
            const item = items[pillIndex];
            const isCurrent = pillIndex === currentIndex;
            pill.classList.toggle("is-current", isCurrent);
            pill.classList.toggle("is-correct", Boolean(item?.isCorrect));
            pill.classList.toggle("is-wrong", Boolean(item?.isAnswered && !item?.isCorrect));
            pill.classList.toggle("is-unanswered", Boolean(item && !item.isAnswered));
            pill.setAttribute("aria-selected", isCurrent ? "true" : "false");
        });

        const prevBtn = document.getElementById("test-results-prev-btn");
        const nextBtn = document.getElementById("test-results-next-btn");
        if (prevBtn) prevBtn.disabled = currentIndex <= 0;
        if (nextBtn) nextBtn.disabled = currentIndex >= totalQuestions - 1;
    }

    function showQuestion(index) {
        if (index < 0 || index >= totalQuestions) return;
        currentIndex = index;
        const item = items[index];

        const mainImg = document.getElementById("test-results-question-image");
        const modalImg = document.getElementById("modal-img");
        const grid = document.getElementById("test-results-answers-grid");
        const questionNumber = document.getElementById("test-results-question-number");
        const statusEl = document.getElementById("test-results-question-status");

        if (questionNumber) questionNumber.textContent = String(index + 1);
        if (statusEl) {
            statusEl.textContent = statusText(item);
            statusEl.className = `test-results-question-status${statusClass(item)}`;
        }

        if (mainImg) {
            if (item?.questionUrl) {
                mainImg.hidden = false;
                mainImg.src = item.questionUrl;
                mainImg.addEventListener("load", scheduleQuizViewportAdjust, { once: true });
            } else {
                mainImg.hidden = true;
                mainImg.removeAttribute("src");
            }
        }
        if (modalImg && item?.questionUrl) modalImg.src = item.questionUrl;

        renderReviewAnswers(grid, item);
        updateQuestionPills();
        updateExplanationPanel(item);
        scheduleQuizViewportAdjust();
    }

    function updateExplanationPanel(item) {
        if (!item) {
            window.QuestionExplanation?.reset?.();
            return;
        }
        const questionId = item.questionFile ?? "";
        if (item.isAnswered && !item.isCorrect && questionId) {
            window.QuestionExplanation?.showAfterAnswer?.(questionId, false);
        } else {
            window.QuestionExplanation?.reset?.();
        }
    }

    function bindNavigation() {
        document.querySelectorAll("#test-results-question-pills .test-question-pill").forEach((pill) => {
            pill.addEventListener("click", () => {
                const index = parseInt(pill.dataset.questionIndex ?? "-1", 10);
                if (Number.isFinite(index) && index >= 0) showQuestion(index);
            });
        });
        bindClick("test-results-prev-btn", () => {
            if (currentIndex > 0) showQuestion(currentIndex - 1);
        });
        bindClick("test-results-next-btn", () => {
            if (currentIndex < totalQuestions - 1) showQuestion(currentIndex + 1);
        });
    }

    function bindImageModal() {
        bindClick("test-results-question-image", () => {
            const mainImg = document.getElementById("test-results-question-image");
            window.ImageModal?.openImageModal(mainImg?.src || "");
        });
        bindClick("close-image-modal-btn", () => window.ImageModal?.closeImageModal());
        const imageModal = document.getElementById("image-modal");
        if (imageModal) {
            imageModal.addEventListener("click", (e) => {
                if (e.target === imageModal) window.ImageModal?.closeImageModal();
            });
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        const data = document.getElementById("test-results-page-data");
        if (!data) return;

        items = decodeItems();
        totalQuestions = items.length || parseInt(data.dataset.total ?? "0", 10) || 0;
        currentIndex = parseInt(data.dataset.initialIndex ?? "0", 10);
        if (!Number.isFinite(currentIndex) || currentIndex < 0) currentIndex = 0;
        if (totalQuestions > 0 && currentIndex >= totalQuestions) currentIndex = 0;

        bindNavigation();
        bindImageModal();
        window.QuizViewport?.bindQuizViewportHandlers();

        if (items.length > 0) showQuestion(currentIndex);
        else updateQuestionPills();
    });
})();
