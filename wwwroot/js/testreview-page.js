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

    document.addEventListener("DOMContentLoaded", () => {
        const data = document.getElementById("test-review-data");
        const imageUrl = data ? data.dataset.questionImageUrl || "" : "";

        const mainImage = document.getElementById("main-question-image");
        if (mainImage) {
            mainImage.addEventListener("click", () => openImageModal(imageUrl));
        }

        const closeBtn = document.getElementById("close-image-modal-btn");
        if (closeBtn) closeBtn.addEventListener("click", closeImageModal);

        const modal = document.getElementById("image-modal");
        if (modal) {
            modal.addEventListener("click", (e) => {
                if (e.target === modal) closeImageModal();
            });
        }
    });
})();
