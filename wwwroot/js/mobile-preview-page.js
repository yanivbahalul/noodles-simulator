(function () {
    "use strict";

    var quizPage = window.NOODLES_QUIZ_PAGE || "/MobilePreview";
    var updateInterval;

    function openImageModal() {
        var modal = document.getElementById("image-modal");
        var modalImg = document.getElementById("modal-img");
        var mainImg = document.getElementById("main-question-image");
        if (!modal || !modalImg || !mainImg) return;
        modal.classList.add("modal-open");
        modalImg.src = mainImg.src;
    }

    function closeImageModal() {
        var modal = document.getElementById("image-modal");
        if (modal) modal.classList.remove("modal-open");
    }

    function openDifficultyModal() {
        var modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.add("difficulty-modal-open");
    }

    function closeDifficultyModal() {
        var modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    function setText(id, value) {
        var el = document.getElementById(id);
        if (el) el.textContent = value;
    }

    function setTextAll(ids, value) {
        ids.forEach(function (id) {
            setText(id, value);
        });
    }

    async function fetchStats() {
        try {
            var res = await fetch("/Stats?_=" + new Date().getTime());
            if (!res.ok) throw new Error("stats fetch failed");
            var data = await res.json();
            if (data && data.correct !== undefined) {
                setTextAll(["stat-correct", "stat-correct-mobile"], data.correct);
                setTextAll(["stat-total", "stat-total-mobile"], data.total);
                setTextAll(["stat-success", "stat-success-mobile"], data.successRate + "%");
            }
            if (data && data.online !== undefined && data.online !== null) {
                setTextAll(["online-count", "online-count-mobile", "online-count-drawer"], data.online);
            }
        } catch {
            // keep existing values
        }
    }

    async function fetchOnlineCount() {
        try {
            var res = await fetch("/api/online-count?_=" + new Date().getTime());
            if (!res.ok) throw new Error("online fetch failed");
            var data = await res.json();
            if (data && data.online !== undefined && data.online !== null) {
                setTextAll(["online-count", "online-count-mobile", "online-count-drawer"], data.online);
            }
        } catch {
            // keep existing values
        }
    }

    function startAutoUpdate() {
        updateInterval = setInterval(function () {
            fetchStats();
            fetchOnlineCount();
        }, 5000);
    }

    function stopAutoUpdate() {
        if (updateInterval) {
            clearInterval(updateInterval);
            updateInterval = null;
        }
    }

    function bindSingleReportForm(form) {
        if (!form) return;
        form.addEventListener("submit", async function (e) {
            e.preventDefault();
            var formData = new FormData(form);
            var token = formData.get("__RequestVerificationToken");
            var data = {};
            formData.forEach(function (value, key) {
                if (key === "__RequestVerificationToken") return;
                data[key === "answersJson" ? "answers" : key] = value;
            });

            try {
                var res = await fetch(quizPage + "?handler=ReportError", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        RequestVerificationToken: token
                    },
                    body: JSON.stringify(data)
                });
                if (res.ok) {
                    alert("הדיווח נשלח בהצלחה!");
                    form.reset();
                    if (window.mobileShell && window.mobileShell.closeBottomSheet) {
                        window.mobileShell.closeBottomSheet("report-bottom-sheet");
                    }
                } else {
                    alert("אירעה שגיאה בשליחת הדיווח.");
                }
            } catch {
                alert("אירעה שגיאה בשליחת הדיווח.");
            }
        });
    }

    function bindReportForm() {
        // Bind both desktop form (#report-form) and mobile bottom-sheet form (#report-form-mobile)
        bindSingleReportForm(document.getElementById("report-form"));
        bindSingleReportForm(document.getElementById("report-form-mobile"));
    }

    document.addEventListener("DOMContentLoaded", function () {
        var mainQuestionImage = document.getElementById("main-question-image");
        if (mainQuestionImage) mainQuestionImage.addEventListener("click", openImageModal);

        document.querySelectorAll("#open-difficulty-modal-btn, .open-difficulty-modal-btn").forEach(function (btn) {
            btn.addEventListener("click", openDifficultyModal);
        });

        var closeDifficultyBtn = document.getElementById("close-difficulty-modal-btn");
        if (closeDifficultyBtn) closeDifficultyBtn.addEventListener("click", closeDifficultyModal);

        var closeImageBtn = document.getElementById("close-image-modal-btn");
        if (closeImageBtn) closeImageBtn.addEventListener("click", closeImageModal);

        var modal = document.getElementById("image-modal");
        if (modal) {
            modal.addEventListener("click", function (e) {
                if (e.target === modal) closeImageModal();
            });
        }

        var diffModal = document.getElementById("difficulty-modal");
        if (diffModal) {
            diffModal.addEventListener("click", function (e) {
                if (e.target === diffModal) closeDifficultyModal();
            });
        }

        bindReportForm();
    });

    window.__fetchMobileStats = function () {
        fetchStats();
        fetchOnlineCount();
    };

    window.addEventListener("load", function () {
        fetchStats();
        fetchOnlineCount();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", function () {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });
})();
