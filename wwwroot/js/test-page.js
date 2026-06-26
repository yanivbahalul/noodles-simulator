(function () {
    function openImageModal(imageUrl) {
        const modal = document.getElementById("image-modal");
        const modalImg = document.getElementById("modal-img");
        if (!modal || !modalImg) return;
        modalImg.src = imageUrl || "";
        modal.classList.add("modal-open");
    }

    function closeImageModal() {
        const modal = document.getElementById("image-modal");
        if (modal) modal.classList.remove("modal-open");
    }

    function bindModalDismiss(modalId, closeFn) {
        const modal = document.getElementById(modalId);
        if (!modal) return;
        modal.addEventListener("click", (e) => {
            if (e.target === modal) closeFn();
        });
    }

    function bindClick(id, handler) {
        const el = document.getElementById(id);
        if (el) el.addEventListener("click", handler);
    }

    function startCountdown(endUtc) {
        if (!endUtc) return;
        const end = new Date(endUtc).getTime();
        function tick() {
            const now = new Date().getTime();
            const diff = Math.max(0, end - now);
            const hours = Math.floor(diff / 3600000);
            const minutes = Math.floor((diff % 3600000) / 60000);
            const seconds = Math.floor((diff % 60000) / 1000);
            const pad = (n) => (n < 10 ? `0${n}` : String(n));
            const el = document.getElementById("countdown");
            if (el) el.textContent = `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
            if (diff <= 0) {
                window.location.reload();
                return;
            }
            setTimeout(tick, 1000);
        }
        tick();
    }

    function initTestPage(imageUrl, endUtc) {
        bindClick("main-question-image", () => openImageModal(imageUrl));
        bindClick("close-image-modal-btn", closeImageModal);
        bindModalDismiss("image-modal", closeImageModal);

        if (window.bindConfirmEndTestButtons) {
            window.bindConfirmEndTestButtons("האם אתה בטוח שברצונך לסיים את המבחן? התוצאות יישמרו.");
        }

        startCountdown(endUtc);
    }

    document.addEventListener("DOMContentLoaded", () => {
        const data = document.getElementById("test-page-data");
        if (!data) return;
        initTestPage(data.dataset.questionImageUrl || "", data.dataset.testEndUtc || "");
    });
})();
