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

    function startCountdown(endUtc) {
        if (!endUtc) return;
        const end = new Date(endUtc).getTime();
        function tick() {
            const now = new Date().getTime();
            const diff = Math.max(0, end - now);
            const h = Math.floor(diff / 3600000);
            const m = Math.floor((diff % 3600000) / 60000);
            const s = Math.floor((diff % 60000) / 1000);
            const pad = (n) => (n < 10 ? "0" + n : "" + n);
            const el = document.getElementById("countdown");
            if (el) el.textContent = pad(h) + ":" + pad(m) + ":" + pad(s);
            if (diff <= 0) {
                window.location.reload();
                return;
            }
            setTimeout(tick, 1000);
        }
        tick();
    }

    document.addEventListener("DOMContentLoaded", () => {
        const data = document.getElementById("test-page-data");
        if (!data) return;
        const imageUrl = data.dataset.questionImageUrl || "";
        const endUtc = data.dataset.testEndUtc || "";

        const mainImage = document.getElementById("main-question-image");
        if (mainImage) {
            mainImage.addEventListener("click", () => openImageModal(imageUrl));
        }

        const closeButton = document.getElementById("close-image-modal-btn");
        if (closeButton) closeButton.addEventListener("click", closeImageModal);

        const modal = document.getElementById("image-modal");
        if (modal) {
            modal.addEventListener("click", (e) => {
                if (e.target === modal) closeImageModal();
            });
        }

        const endTestBtn = document.querySelector(".confirm-end-test-btn");
        if (endTestBtn) {
            endTestBtn.addEventListener("click", (e) => {
                const ok = window.confirm("האם אתה בטוח שברצונך לסיים את המבחן? התוצאות יישמרו.");
                if (!ok) e.preventDefault();
            });
        }

        startCountdown(endUtc);
    });
})();
