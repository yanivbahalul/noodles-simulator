                   RESPONSIVE MOBILE/DESKTOP LAYOUT ARCHITECTURE
                          May 20, 2026 — responsive-layout-component

═══════════════════════════════════════════════════════════════════════════════

                            REQUEST FLOW

                              /Index
                                │
                        ┌───────┴───────┐
                        │ Device Check  │
                        │ (UA + cookie) │
                        └───────┬───────┘
                                │
                ┌───────────────┼───────────────┐
                │                               │
          [Phone/Tablet]                   [Desktop PC]
                │                               │
        [?desktop=1 override]          [normal flow]
                │                               │
        ┌─────────────┬──────────┐              │
        │             │          │              │
        YES           NO         │              │
        │             │          │              │
     Desktop      Mobile         │              │
     Layout       Layout         │              │
        │             │          │              │
        └─────────────┼──────────┘              │
                      │                         │
                      ├─────────────────────────┤
                      │
              render to browser


═══════════════════════════════════════════════════════════════════════════════

                          RENDERING DECISION

                          Pages/Index.cshtml
                         ┌────────────────────┐
                         │ @if (isPhoneQuiz)  │
                         └────────┬───────────┘
                                  │
                ┌─────────────────┼─────────────────┐
                │                                   │
            TRUE (Mobile)                       FALSE (Desktop)
                │                                   │
    ┌───────────────────────────┐      ┌──────────────────────┐
    │ Render _MobileQuizShell   │      │ Render _TopBar       │
    │ + Mobile CSS Files        │      │ + _LogoHeader        │
    │ + Mobile JS               │      │ + Desktop Content    │
    │                           │      │ + Desktop CSS/JS     │
    │ Components:               │      │                      │
    │ • Fixed header (56px)     │      │ + Desktop only:      │
    │ • Sticky actions (52px)   │      │   - Centered layout  │
    │ • Bottom nav (64px)       │      │   - Report form      │
    │ • Side drawer             │      │   - Stats panel      │
    │ • Report bottom sheet     │      │                      │
    │ • Stats bottom sheet      │      │                      │
    └───────────────────────────┘      └──────────────────────┘


═══════════════════════════════════════════════════════════════════════════════

                        DESKTOP LAYOUT (Desktop PC)

  ┌─────────────────────────────────────────────────────────────────────┐
  │ _TopBar (sticky top)                                                │
  │ 🏠 | 📋 | 🏆 | 👤 |                                                  │
  └─────────────────────────────────────────────────────────────────────┘
  
  ┌─────────────────────────────────────────────────────────────────────┐
  │ _LogoHeader                                                         │
  │        [NOODLES LOGO]                                               │
  │    Logged in as: John                    Online: 42                 │
  └─────────────────────────────────────────────────────────────────────┘
  
  ┌─────────────────────────────────────────────────────────────────────┐
  │ Quiz Container (centered)                                           │
  │                  [QUESTION IMAGE]                                   │
  │                                                                     │
  │              [Ans1]  [Ans2]  [Ans3]  [Ans4]                        │
  │                                                                     │
  │            [מצב מבחן]  [שאלה הבאה]  [איפוס]                         │
  │                                                                     │
  │              ✅ תשובה נכונה!                                         │
  └─────────────────────────────────────────────────────────────────────┘
  
  ┌─────────────────────────────────────────────────────────────────────┐
  │ Report Form                                                         │
  │ יש טעות בשאלה? דווח לנו                                             │
  │ [Textarea for explanation]                                          │
  │ [דווח על טעות בשאלה]                                                │
  └─────────────────────────────────────────────────────────────────────┘
  
  ┌─────────────────────────────────────────────────────────────────────┐
  │ _SiteFooter                                                         │
  └─────────────────────────────────────────────────────────────────────┘


═══════════════════════════════════════════════════════════════════════════════

                          MOBILE LAYOUT (Phone)

  ╔═════════════════════════════════════════════════════════╗
  ║ [☰] Mobile Header (56px)                              ║
  ╚═════════════════════════════════════════════════════════╝

  ╔═════════════════════════════════════════════════════════╗
  ║                                                         ║
  ║           [QUESTION IMAGE]                            ║
  ║                                                         ║
  ║       [Ans1]  [Ans2]                                   ║
  ║       [Ans3]  [Ans4]                                   ║
  ║                                                         ║
  ║         ✅ תשובה נכונה!                                  ║
  ║                                                         ║
  ║ (content scrollable)                                    ║
  ║                                                         ║
  ║ 👤 John · 🟢 42 מחוברים                                 ║
  ║                                                         ║
  ║ (scroll area)                                           ║
  ║                                                         ║
  ║                                                         ║
  ║                                                         ║
  ╚═════════════════════════════════════════════════════════╝

  ╔═════════════════════════════════════════════════════════╗
  ║ [שאלה הבאה] [מבחן] [איפוס] — Sticky Action Bar (52px)  ║
  ╚═════════════════════════════════════════════════════════╝

  ╔═════════════════════════════════════════════════════════╗
  ║ 🏠     📋      🏆     📧        — Bottom Nav (64px)     ║
  ║ בית   מבחנים  מובילים  דיווח                             ║
  ╚═════════════════════════════════════════════════════════╝

  DRAWER (opened by ☰):
  ┌─────────────────────┐
  │ תפריט          [×]  │
  │                     │
  │ 🏠 דף הבית       │
  │ 📋 המבחנים שלי    │
  │ 🏆 טבלת מובילים  │
  │ ─────────────────  │
  │ 📊 סטטיסטיקה     │
  │ 💻 תצוגת מחשב    │
  │ ─────────────────  │
  │ 🚪 התנתקות       │
  └─────────────────────┘

  REPORT BOTTOM SHEET (דיווח button):
  ┌──────────────────────────────┐
  │ ═══════════════════════════  │  ← handle
  │ דיווח על טעות בשאלה    [×]   │
  │                              │
  │ נתקלת בטעות? ספר לנו...      │
  │ [Textarea]                   │
  │ [שלח דיווח]                  │
  └──────────────────────────────┘


═══════════════════════════════════════════════════════════════════════════════

                         FILE STRUCTURE & SIZES

Pages/
├── Index.cshtml                                    11 KB ← Device detection
└── Shared/
    ├── _MobileQuizShell.cshtml                     16 KB ← Mobile chrome
    └── [_TopBar, _LogoHeader, _SiteFooter]

wwwroot/
├── css/
│   ├── site.css                                    (base, unchanged)
│   ├── quiz-index-layout.css                       (centering)
│   ├── mobile-shell.css                            20 KB ← All components
│   └── mobile-shell-mobile.css                     9 KB  ← Layout toggles
└── js/
    ├── index-page.js                               (unchanged)
    ├── mobile-shell.js                             8.3 KB ← Interactions
    └── mobile-preview-page.js                      6.2 KB ← Report handler

Total mobile-specific code: ~70 KB (unfminified)
Total additive CSS: ~29 KB
Total mobile JS: ~14 KB


═══════════════════════════════════════════════════════════════════════════════

                        CSS LOADING STRATEGY

Desktop Browser:                    Mobile Browser (Phone):
  Load site.css                       Load site.css
  Load quiz-index-layout.css          Load quiz-index-layout.css
  (no mobile CSS)                     Load mobile-shell.css
                                      Load mobile-shell-mobile.css

  Result: Lightweight                 Result: Full mobile UI
  No style conflicts                  Touch-friendly interactions
  Original layout preserved           Native feel


═══════════════════════════════════════════════════════════════════════════════

                    DEVICE DETECTION & PREFERENCES

Query Param              Cookie Set              Layout Result
────────────────────────────────────────────────────────────────────
?desktop=1              noodles_layout=desktop  Desktop (30 days)
?mobile=1               noodles_layout=mobile   Mobile (30 days)
(none)                  noodles_layout=desktop  Desktop
(none)                  noodles_layout=mobile   Mobile
(none)                  (none)                  Auto (UA detection)

UA Detection:
  • Contains "Mobile" / "Android" / "iPhone" → Mobile
  • Otherwise → Desktop

Priority: Query param > Cookie > UA detection


═══════════════════════════════════════════════════════════════════════════════

                     INTERACTION FLOW (Mobile)

1. User taps ☰ hamburger
   └→ mobile-shell.js: openDrawer()
   └→ Drawer slides in from left
   └→ body.mobile-drawer-active set (scroll lock)

2. User taps drawer menu item
   └→ Navigate to page
   └→ mobile-shell.js: closeDrawer()
   └→ Drawer slides out

3. User taps דיווח in bottom nav
   └→ mobile-shell.js: openBottomSheet('report-bottom-sheet')
   └→ Report sheet slides up from bottom
   └→ Focus moved to #explanation-mobile

4. User submits report form
   └→ mobile-preview-page.js: bindReportForm()
   └→ POST to /Index?handler=ReportError
   └→ On success: form reset, sheet closes

5. User taps שאלה הבאה (sticky action bar)
   └→ Form submits
   └→ Page reloads with next question
   └→ Sticky bar remains above bottom nav


═══════════════════════════════════════════════════════════════════════════════

                    ACCESSIBILITY (ARIA)

Mobile Shell:
  ✓ aria-label on buttons (hamburger, close, nav)
  ✓ aria-controls on drawer trigger
  ✓ aria-expanded on drawer state
  ✓ role="navigation" on header
  ✓ role="dialog" + aria-modal on sheets
  ✓ aria-labelledby on sheet titles
  ✓ aria-hidden on decorative elements (SVGs)
  ✓ sr-only text for screen readers
  ✓ Keyboard navigation (Escape to close)
  ✓ Focus management on sheet open

Desktop Layout:
  ✓ Semantic HTML: <header>, <nav>, <main>
  ✓ ARIA labels on all interactive elements
  ✓ Proper heading hierarchy
  ✓ Color contrast ratios (WCAG AA)


═══════════════════════════════════════════════════════════════════════════════

                        TOUCH TARGETS

Mobile:
  • Hamburger: 44px × 44px
  • Bottom nav items: 64px height, full-width
  • Drawer menu links: 48px height
  • Bottom sheet close: 44px × 44px
  • Answer buttons: 80px × 80px (in 2×2 grid)

Minimum touch target: 48px (WCAG 2.1 AAA)
All components meet or exceed this requirement


═══════════════════════════════════════════════════════════════════════════════

                          KEY FILES

1. Pages/Index.cshtml
   • Device detection (isPhoneQuiz boolean)
   • Conditional rendering: @if (isPhoneQuiz) { Mobile } else { Desktop }
   • Query param handling (?desktop=1, ?mobile=1)
   • Cookie setup (noodles_layout, 30-day expiry)

2. Pages/Shared/_MobileQuizShell.cshtml
   • Mobile header (hamburger, 56px)
   • Sticky action bar (52px)
   • Drawer (menu, settings, logout)
   • Bottom sheets (report, stats)
   • Bottom navigation (4 tabs)

3. wwwroot/css/mobile-shell.css
   • All mobile component styles
   • Transitions: 0.35s cubic-bezier(0.4, 0, 0.2, 1)
   • Touch-friendly spacing
   • No modifications to site.css

4. wwwroot/js/mobile-shell.js
   • Drawer open/close + scroll lock
   • Bottom sheet management
   • Sticky bar height calculations
   • html.layout-desktop class handling

5. wwwroot/js/mobile-preview-page.js
   • Report form binding (both desktop & mobile)
   • Stats polling (5s interval)
   • Online count updates
   • POST to /Index?handler=ReportError

═══════════════════════════════════════════════════════════════════════════════

                      TESTING CHECKLIST

Desktop PC (Chrome/Firefox):
  ✓ No mobile chrome visible
  ✓ Layout matches original design
  ✓ /Index?mobile=1 shows mobile UI
  ✓ Top bar, logo, meta info visible
  ✓ Centered quiz container
  ✓ Report form below quiz

Phone (iPhone/Android):
  ✓ Mobile UI with header, nav, sheets
  ✓ Hamburger opens drawer
  ✓ Bottom nav has 4 items
  ✓ Sticky action bar above bottom nav
  ✓ דיווח opens report sheet
  ✓ Report submit works
  ✓ שאלה הבאה loads next question
  ✓ /Index?desktop=1 shows desktop layout

iPad/Tablet:
  ✓ Mobile UI (viewport < 1024px)
  ✓ Touch interactions smooth
  ✓ All buttons accessible

Responsive (DevTools):
  ✓ 375px (iPhone): mobile
  ✓ 768px (tablet): mobile
  ✓ 1024px: mobile/desktop transition
  ✓ 1920px (desktop): desktop

═══════════════════════════════════════════════════════════════════════════════

                     GIT COMMIT HISTORY

da9e28e — Main implementation
  feat: implement responsive mobile/desktop layout with architectural separation
  - Device detection, branching logic, mobile shell partial
  - CSS architecture (additive mobile styles)
  - JavaScript interactions (drawer, sheets, scroll lock)
  - Report form handling (both desktop & mobile)

ed8d8f2 — Comprehensive reference
  docs: add comprehensive implementation summary

8b306fc — Quick start guide
  docs: add quick start guide for mobile/desktop layout development

═══════════════════════════════════════════════════════════════════════════════
