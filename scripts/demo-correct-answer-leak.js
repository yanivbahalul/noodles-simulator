// Demo: this script no longer finds value="correct" after the anti-cheat fix.
(function () {
  const btn = document.querySelector('button[name="answer"][value="correct"]');
  if (!btn) return;
  btn.style.outline = "4px solid #ffd700";
})();
