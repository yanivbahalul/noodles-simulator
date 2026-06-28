(function () {
    const panel = () => document.getElementById("question-explanation-panel");
    const btn = () => document.getElementById("question-explanation-btn");
    const video = () => document.getElementById("question-explanation-video");

    let loadToken = 0;

    function reset() {
        loadToken += 1;
        const p = panel();
        const b = btn();
        const v = video();
        if (p) p.hidden = true;
        if (b) {
            b.hidden = false;
            b.disabled = false;
            b.textContent = "למה טעיתי? ▶";
        }
        if (v) {
            v.hidden = true;
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

    async function playExplanation(questionId) {
        const b = btn();
        const v = video();
        if (!b || !v || !questionId) return;

        const token = ++loadToken;
        b.disabled = true;
        b.textContent = "טוען הסבר...";

        try {
            const url = await fetchExplanationUrl(questionId);
            if (token !== loadToken) return;
            if (!url) {
                b.textContent = "אין הסבר לשאלה זו";
                return;
            }
            b.hidden = true;
            v.hidden = false;
            v.src = url;
            await v.play();
        } catch {
            if (token === loadToken) {
                b.disabled = false;
                b.textContent = "שגיאה בטעינה — נסה שוב";
            }
        }
    }

    function showForWrongAnswer(questionId) {
        reset();
        const p = panel();
        const b = btn();
        if (!p || !b || !questionId) return;
        p.hidden = false;
        b.onclick = () => playExplanation(questionId);
    }

    window.QuestionExplanation = {
        reset,
        showForWrongAnswer,
        playExplanation
    };
})();
