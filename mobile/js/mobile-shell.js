(function () {
    "use strict";

    var DRAWER_OPEN_CLASS = "mobile-drawer-open";
    var BODY_DRAWER_CLASS = "mobile-drawer-active";
    var BOTTOM_SHEET_OPEN_CLASS = "bottom-sheet-open";
    var BODY_SHEET_CLASS = "bottom-sheet-active";
    var scrollLockY = 0;

    function isMobileShell() {
        if (document.documentElement.classList.contains("layout-desktop")) {
            return false;
        }
        if (document.body.getAttribute("data-mobile-device") === "true") {
            return true;
        }
        return window.matchMedia("(max-width: 1023.98px)").matches;
    }

    function getDrawer() {
        return document.getElementById("mobile-drawer");
    }

    function getBackdrop() {
        return document.getElementById("mobile-drawer-backdrop");
    }

    function setDrawerOpen(open) {
        var drawer = getDrawer();
        var backdrop = getBackdrop();
        if (!drawer || !backdrop) return;

        if (open) {
            lockPageScroll();
        }
        drawer.classList.toggle(DRAWER_OPEN_CLASS, open);
        backdrop.classList.toggle(DRAWER_OPEN_CLASS, open);
        document.body.classList.toggle(BODY_DRAWER_CLASS, open);
        drawer.setAttribute("aria-hidden", open ? "false" : "true");
        backdrop.setAttribute("aria-hidden", open ? "false" : "true");
        if (!open) {
            unlockPageScroll();
        }

        document.querySelectorAll("#mobile-header-menu-btn").forEach(function (btn) {
            btn.setAttribute("aria-expanded", open ? "true" : "false");
        });
    }

    function openDrawer() {
        setDrawerOpen(true);
    }

    function closeDrawer() {
        setDrawerOpen(false);
    }

    function toggleDrawer() {
        var drawer = getDrawer();
        if (!drawer) return;
        setDrawerOpen(!drawer.classList.contains(DRAWER_OPEN_CLASS));
    }

    function lockPageScroll() {
        if (document.body.style.top) {
            return;
        }
        scrollLockY = window.scrollY || window.pageYOffset || 0;
        document.body.style.top = "-" + scrollLockY + "px";
    }

    function unlockPageScroll() {
        if (document.body.classList.contains(BODY_DRAWER_CLASS) ||
            document.body.classList.contains(BODY_SHEET_CLASS)) {
            return;
        }
        document.body.style.top = "";
        window.scrollTo(0, scrollLockY);
    }

    function openBottomSheet(sheetId) {
        var sheet = document.getElementById(sheetId);
        var backdrop = document.getElementById(sheetId + "-backdrop");
        if (!sheet) return;
        lockPageScroll();
        sheet.classList.add(BOTTOM_SHEET_OPEN_CLASS);
        if (backdrop) backdrop.classList.add(BOTTOM_SHEET_OPEN_CLASS);
        document.body.classList.add(BODY_SHEET_CLASS);
        sheet.setAttribute("aria-hidden", "false");
    }

    function closeBottomSheet(sheetId) {
        var sheet = document.getElementById(sheetId);
        var backdrop = document.getElementById(sheetId + "-backdrop");
        if (!sheet) return;
        sheet.classList.remove(BOTTOM_SHEET_OPEN_CLASS);
        if (backdrop) backdrop.classList.remove(BOTTOM_SHEET_OPEN_CLASS);
        document.body.classList.remove(BODY_SHEET_CLASS);
        sheet.setAttribute("aria-hidden", "true");
        unlockPageScroll();
    }

    function closeAllBottomSheets() {
        document.querySelectorAll(".bottom-sheet." + BOTTOM_SHEET_OPEN_CLASS).forEach(function (sheet) {
            closeBottomSheet(sheet.id);
        });
    }

    function bindDrawer() {
        var headerMenuBtn = document.getElementById("mobile-header-menu-btn");
        var closeBtn = document.getElementById("mobile-drawer-close");
        var backdrop = getBackdrop();

        if (headerMenuBtn) headerMenuBtn.addEventListener("click", toggleDrawer);
        if (closeBtn) closeBtn.addEventListener("click", closeDrawer);
        if (backdrop) backdrop.addEventListener("click", closeDrawer);

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape") {
                closeDrawer();
                closeAllBottomSheets();
            }
        });
    }

    function bindBottomSheets() {
        document.querySelectorAll("[data-bottom-sheet]").forEach(function (trigger) {
            trigger.addEventListener("click", function () {
                var sheetId = trigger.getAttribute("data-bottom-sheet");
                if (!sheetId) return;
                openBottomSheet(sheetId);
                var focusId = trigger.getAttribute("data-bottom-sheet-focus");
                if (focusId) {
                    setTimeout(function () {
                        var el = document.getElementById(focusId);
                        if (el) el.focus();
                    }, 350);
                }
            });
        });

        document.querySelectorAll("[data-bottom-sheet-close]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var sheetId = btn.getAttribute("data-bottom-sheet-close");
                if (sheetId) closeBottomSheet(sheetId);
            });
        });

        document.querySelectorAll(".bottom-sheet-backdrop").forEach(function (backdrop) {
            backdrop.addEventListener("click", function () {
                var sheetId = backdrop.getAttribute("data-sheet-id");
                if (sheetId) closeBottomSheet(sheetId);
            });
        });
    }

    function bindMobileStats() {
        var mobileStatsTrigger = document.getElementById("mobile-stats-trigger");
        if (mobileStatsTrigger) {
            mobileStatsTrigger.addEventListener("click", function () {
                closeDrawer();
                openBottomSheet("stats-bottom-sheet");
                if (typeof window.__fetchMobileStats === "function") {
                    window.__fetchMobileStats();
                }
            });
        }
    }

    function syncShellClasses() {
        var mobile = isMobileShell();
        document.body.classList.toggle(
            "has-mobile-nav",
            mobile && !!document.querySelector(".bottom-nav")
        );
        document.body.classList.toggle(
            "has-mobile-header",
            mobile && !!document.querySelector(".mobile-header")
        );
        document.body.classList.toggle(
            "has-sticky-actions",
            mobile && !!document.querySelector(".sticky-action-bar")
        );
        if (!mobile) {
            document.body.classList.remove(BODY_DRAWER_CLASS, BODY_SHEET_CLASS);
        }
    }

    function handleViewportChange() {
        syncShellClasses();
        if (!isMobileShell()) {
            closeDrawer();
            closeAllBottomSheets();
        }
    }

    function bindStickyActionBars() {
        var bars = document.querySelectorAll(".sticky-action-bar");
        if (!bars.length) return;

        function updatePadding() {
            if (!isMobileShell()) {
                document.documentElement.style.removeProperty("--sticky-actions-offset");
                return;
            }
            var maxHeight = 0;
            bars.forEach(function (bar) {
                if (bar.offsetParent !== null) {
                    maxHeight = Math.max(maxHeight, bar.offsetHeight);
                }
            });
            document.documentElement.style.setProperty(
                "--sticky-actions-offset",
                maxHeight + "px"
            );
            document.querySelectorAll(".sticky-action-bar-placeholder").forEach(function (el) {
                el.style.height = maxHeight + "px";
            });
        }

        updatePadding();
        window.addEventListener("resize", updatePadding);
        if (typeof ResizeObserver !== "undefined") {
            bars.forEach(function (bar) {
                new ResizeObserver(updatePadding).observe(bar);
            });
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        syncShellClasses();
        bindDrawer();
        bindBottomSheets();
        bindMobileStats();
        bindStickyActionBars();
        handleViewportChange();
    });

    window.addEventListener("resize", handleViewportChange);

    window.mobileShell = {
        openDrawer: openDrawer,
        closeDrawer: closeDrawer,
        openBottomSheet: openBottomSheet,
        closeBottomSheet: closeBottomSheet,
        isMobileShell: isMobileShell
    };
})();
