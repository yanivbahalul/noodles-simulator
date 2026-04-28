(function () {
    document.addEventListener("DOMContentLoaded", () => {
        const buttons = document.querySelectorAll(".confirm-end-test-btn");
        buttons.forEach((btn) => {
            btn.addEventListener("click", (e) => {
                const ok = window.confirm("האם אתה בטוח שברצונך לסיים את המבחן? התוצאות יישמרו.");
                if (!ok) e.preventDefault();
            });
        });
    });
})();
