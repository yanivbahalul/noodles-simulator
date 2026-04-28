(function () {
    let updateInterval;

    function openImageModal() {
        const modal = document.getElementById("image-modal");
        const modalImg = document.getElementById("modal-img");
        const mainImg = document.getElementById("main-question-image");
        if (!modal || !modalImg || !mainImg) return;
        modal.classList.add("modal-open");
        modalImg.src = mainImg.src;
    }

    function closeImageModal() {
        const modal = document.getElementById("image-modal");
        if (modal) modal.classList.remove("modal-open");
    }

    function openDifficultyModal() {
        const modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.add("difficulty-modal-open");
    }

    function closeDifficultyModal() {
        const modal = document.getElementById("difficulty-modal");
        if (modal) modal.classList.remove("difficulty-modal-open");
    }

    async function fetchStats() {
        try {
            const res = await fetch("/Stats?_=" + new Date().getTime());
            if (!res.ok) throw new Error("stats fetch failed");
            const data = await res.json();
            if (data && data.correct !== undefined) {
                document.getElementById("stat-correct").innerText = data.correct;
                document.getElementById("stat-total").innerText = data.total;
                document.getElementById("stat-success").innerText = data.successRate + "%";
            }
            if (data && data.online !== undefined && data.online !== null) {
                document.getElementById("online-count").innerText = data.online;
            }
        } catch {
            // keep existing values
        }
    }

    async function fetchOnlineCount() {
        try {
            const res = await fetch("/api/online-count?_=" + new Date().getTime());
            if (!res.ok) throw new Error("online fetch failed");
            const data = await res.json();
            if (data && data.online !== undefined && data.online !== null) {
                document.getElementById("online-count").innerText = data.online;
            }
        } catch {
            // keep existing values
        }
    }

    function toggleStats() {
        const panel = document.getElementById("stats-panel");
        const toggle = document.getElementById("footer-stats-toggle");
        if (!panel) return;
        const isOpen = !panel.classList.contains("hidden");
        panel.classList.toggle("hidden");
        if (toggle) {
            toggle.classList.toggle("footer-stats-toggle-open", !isOpen);
            toggle.classList.toggle("footer-stats-toggle-closed", isOpen);
        }
        if (!isOpen) {
            fetchStats();
            fetchOnlineCount();
        }
    }

    function startAutoUpdate() {
        updateInterval = setInterval(() => {
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

    function bindReportForm() {
        const form = document.getElementById("report-form");
        if (!form) return;
        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            const formData = new FormData(form);
            const token = formData.get("__RequestVerificationToken");
            const data = {};
            formData.forEach((value, key) => {
                if (key === "__RequestVerificationToken") return;
                data[key === "answersJson" ? "answers" : key] = value;
            });

            try {
                const res = await fetch("/Index?handler=ReportError", {
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
                } else {
                    alert("אירעה שגיאה בשליחת הדיווח.");
                }
            } catch {
                alert("אירעה שגיאה בשליחת הדיווח.");
            }
        });
    }

    document.addEventListener("DOMContentLoaded", () => {
        const mainQuestionImage = document.getElementById("main-question-image");
        if (mainQuestionImage) mainQuestionImage.addEventListener("click", openImageModal);

        const openDifficultyBtn = document.getElementById("open-difficulty-modal-btn");
        if (openDifficultyBtn) openDifficultyBtn.addEventListener("click", openDifficultyModal);

        const closeDifficultyBtn = document.getElementById("close-difficulty-modal-btn");
        if (closeDifficultyBtn) closeDifficultyBtn.addEventListener("click", closeDifficultyModal);

        const closeImageBtn = document.getElementById("close-image-modal-btn");
        if (closeImageBtn) closeImageBtn.addEventListener("click", closeImageModal);

        const modal = document.getElementById("image-modal");
        if (modal) {
            modal.addEventListener("click", (e) => {
                if (e.target === modal) closeImageModal();
            });
        }

        const diffModal = document.getElementById("difficulty-modal");
        if (diffModal) {
            diffModal.addEventListener("click", (e) => {
                if (e.target === diffModal) closeDifficultyModal();
            });
        }

        const footerStatsToggle = document.getElementById("footer-stats-toggle");
        if (footerStatsToggle) footerStatsToggle.addEventListener("click", toggleStats);

        bindReportForm();
    });

    window.addEventListener("load", () => {
        fetchStats();
        fetchOnlineCount();
        startAutoUpdate();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) stopAutoUpdate();
        else startAutoUpdate();
    });
})();
