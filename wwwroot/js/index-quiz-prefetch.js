(function () {
    let prefetchedQuestion = null;
    let prefetchAnchor = null;
    let prefetchPromise = null;

    function preloadImage(url, timeoutMs = 12000) {
        return new Promise((resolve) => {
            if (!url) {
                resolve();
                return;
            }
            const img = new Image();
            let done = false;
            const finish = () => {
                if (done) return;
                done = true;
                resolve();
            };
            const timer = setTimeout(finish, timeoutMs);
            img.onload = () => {
                clearTimeout(timer);
                finish();
            };
            img.onerror = () => {
                clearTimeout(timer);
                finish();
            };
            img.src = url;
        });
    }

    async function preloadQuestionImages(data) {
        const urls = [
            data.questionImageUrl,
            ...(data.answers?.map((a) => a.imageUrl) ?? [])
        ].filter(Boolean);
        await Promise.all(urls.map((url) => preloadImage(url)));
    }

    function invalidatePrefetchCache() {
        prefetchedQuestion = null;
        prefetchAnchor = null;
        prefetchPromise = null;
    }

    function getCurrentQuestionAnchor() {
        return document.getElementById("quiz-question-image")?.value ?? "";
    }

    async function fetchNextQuestionData() {
        const res = await window.RequestChannels.quizFetch("/Index?handler=PrefetchNextQuestion");
        if (res.status === 204 || !res.ok) return null;
        const data = await res.json();
        return data?.questionImage ? data : null;
    }

    function storePrefetchedQuestion(anchor, data) {
        prefetchAnchor = anchor;
        prefetchedQuestion = data;
    }

    async function loadPrefetchNextQuestion() {
        try {
            const anchor = getCurrentQuestionAnchor();
            if (!anchor) return null;

            const data = await fetchNextQuestionData();
            if (!data) return null;

            await preloadQuestionImages(data);
            if (getCurrentQuestionAnchor() !== anchor) return null;

            storePrefetchedQuestion(anchor, data);
            return data;
        } catch {
            return null;
        } finally {
            prefetchPromise = null;
        }
    }

    function fetchPrefetchNextQuestion() {
        if (prefetchPromise) return prefetchPromise;
        prefetchPromise = loadPrefetchNextQuestion();
        return prefetchPromise;
    }

    function schedulePrefetchNextQuestion() {
        invalidatePrefetchCache();
        window.setTimeout(() => {
            fetchPrefetchNextQuestion();
        }, 0);
    }

    function getCachedNextQuestion() {
        const current = document.getElementById("quiz-question-image")?.value ?? "";
        if (!prefetchedQuestion || prefetchAnchor !== current) return null;
        return prefetchedQuestion;
    }

    window.IndexQuizPrefetch = {
        preloadQuestionImages,
        schedulePrefetchNextQuestion,
        getCachedNextQuestion,
        invalidatePrefetchCache
    };
})();
