# Noodles Simulator — Mobile Preview / Mobile Quiz UI Reference

**Project:** `/Users/yaniv/noodles-simulator`  
**Stack:** ASP.NET Core 8 Razor Pages, `site.css` (desktop), additive mobile CSS/JS  
**Last updated:** May 2026 (conversation state)

---

## 1. What “Mobile Preview” is today

Originally there was a separate `/mobile-preview` page (and static `wwwroot/mobile-preview.html`). **That is merged into the main quiz.**

| URL | Behavior |
|-----|----------|
| `/Index` | Main quiz — **desktop OR mobile** layout depending on device/cookie |
| `/mobile-preview` | **Legacy redirect** → `/Index` (query string preserved) |

There is **no separate mobile-only quiz route** anymore. Mobile UX is a **shell layer** on `/Index` when the server detects a phone.

---

## 2. How mobile vs desktop is chosen

**Service:** `Services/MobileLayoutPreference.cs`

| Input | Result |
|-------|--------|
| User-Agent contains `iphone`, `ipod`, `android`, `ipad`, `tablet`, `mobile` | Mobile |
| Query `?mobile=1` | Mobile (+ sets cookie `noodles_layout=mobile` for 30 days) |
| Query `?desktop=1` | Desktop (+ sets cookie `noodles_layout=desktop`) |
| Cookie `noodles_layout=desktop` | Desktop |
| Cookie `noodles_layout=mobile` | Mobile |
| Otherwise | UA detection |

**In `Pages/Index.cshtml`:**

```csharp
var useMobileLayout = MobileLayoutPreference.ShouldUseMobileQuiz(Request);
var desktopLayoutPref = Request.Query.ContainsKey("desktop") 
    || cookie == DesktopValue;
var isPhoneQuiz = useMobileLayout && !desktopLayoutPref;
```

- **Desktop browser (PC):** `isPhoneQuiz = false` → original layout only  
- **Phone:** `isPhoneQuiz = true` → mobile shell + mobile CSS  
- **Phone + “desktop site”:** open `/Index?desktop=1` or use drawer link **תצוגת מחשב**

`QuizPageRoute()` always returns `"Index"` (login redirects to Index).

---

## 3. File map (all mobile-related)

### Razor / backend

| File | Role |
|------|------|
| `Pages/Index.cshtml` | Branches markup/CSS/JS on `isPhoneQuiz` |
| `Pages/Index.cshtml.cs` | Quiz logic, `OnGet` virtual, `QuizPageRoute`, `ApplyLayoutQuery` |
| `Pages/MobilePreview.cshtml` | Empty shell; route `/mobile-preview` |
| `Pages/MobilePreview.cshtml.cs` | `OnGet` → `Redirect("/Index" + query)` |
| `Pages/Shared/_MobileQuizShell.cshtml` | Mobile chrome partial (header, nav, sheets) |
| `Services/MobileLayoutPreference.cs` | UA + cookie + query layout detection |
| `Pages/Login.cshtml.cs` | After auth → `MobileLayoutPreference.QuizPageRoute` |

### CSS (do **not** edit `site.css` for mobile — additive only)

| File | Loaded when | Purpose |
|------|-------------|---------|
| `wwwroot/css/site.css` | Always | Desktop base styles |
| `wwwroot/css/quiz-index-layout.css` | Always on Index | Center quiz column + horizontal desktop buttons (RTL fix) |
| `wwwroot/css/mobile-shell.css` | Phone only | Component styles (drawer, bottom nav, sheets); `@import` mobile rules under `@media (max-width: 1023.98px)` |
| `wwwroot/css/mobile-shell-mobile.css` | Phone only | Layout toggles: hide desktop chrome, show mobile chrome, answer feedback colors |

### JavaScript

| File | Loaded when | Purpose |
|------|-------------|---------|
| `wwwroot/js/index-page.js` | Always | Core quiz: report (`#report-form`), difficulty modal, online count |
| `wwwroot/js/mobile-shell.js` | Phone only | Drawer, bottom sheets, scroll lock, sticky bar padding, `body.has-mobile-*` classes |
| `wwwroot/js/mobile-preview-page.js` | Phone only | Stats/online polling, image modal, difficulty modal, report POST to `NOODLES_QUIZ_PAGE` |

### Deleted / legacy

- `wwwroot/mobile-preview.html` — removed (was static demo)

---

## 4. Desktop layout (`isPhoneQuiz = false`)

**Goal:** Match pre-mobile refactor; **no** `mobile-shell.css` on desktop.

**Structure (DOM order):**

1. `_TopBar` — sticky top icon bar (home, exams, leaderboard, logout)
2. `_LogoHeader` — noodles logo (not inside top bar)
3. `quiz-meta-user` — “Logged in as **username**”
4. `quiz-meta-online` — “מחוברים כעת: N”
5. `.quiz-container` — question image, answers grid, feedback, **button-row** (מצב מבחן | שאלה הבאה | איפוס שאלות)
6. Difficulty modal, image modal
7. `.report-container` — centered report form below card
8. `#stats-panel` — floating stats (footer toggle)
9. `_SiteFooter`

**CSS notes:**

- `quiz-index-layout.css` centers column (`body.quiz-page { align-items: center }`) and forces **horizontal centered** action buttons (overrides `site.css` tablet rule that stacked forms at `width: 100%` below 768px).

---

## 5. Mobile layout (`isPhoneQuiz = true`)

**Goal:** Mobile-first shell; real Supabase questions via same `IndexModel` as desktop.

**Structure:**

1. `_MobileQuizShell` (see section 6)
2. `_LogoHeader` — larger logo under black header bar
3. Combined meta line: `👤 username · 🟢 N מחוברים` (`#online-count-mobile`)
4. `.quiz-container` — same quiz content; **no** in-card desktop `button-row`
5. Modals (difficulty, image) — difficulty opens as bottom-sheet style when mobile CSS applies
6. **No** desktop `report-container` or `#stats-panel` in HTML
7. Footer (site footer hidden on mobile via CSS)

**Sticky action bar** (in shell, not in quiz card):

- שאלה הבאה | מבחן | איפוס — fixed above bottom nav

**Bottom nav:** בית | מבחנים | מובילים | דיווח

---

## 6. `_MobileQuizShell.cshtml` components

| Component | IDs / classes | Behavior |
|-----------|---------------|----------|
| Mobile header | `.mobile-header`, `#mobile-header-menu-btn` | Hamburger, **physical left** (`direction: ltr` on bar) |
| Sticky actions | `.sticky-action-bar`, `.sticky-action-bar-placeholder` | Fixed above bottom nav; padding via `--sticky-actions-offset` |
| Report bottom sheet | `#report-bottom-sheet`, `#report-form-mobile`, `#explanation-mobile` | Open from bottom nav “דיווח” |
| Drawer | `#mobile-drawer`, `#mobile-drawer-backdrop` | Menu: home, desktop view, exams, leaderboard, stats, logout |
| Stats bottom sheet | `#stats-bottom-sheet`, `#stat-*-mobile`, `#online-count-drawer` | Open from drawer “סטטיסטיקה” |
| Bottom nav | `.bottom-nav` | Four tabs |

**Drawer link:** `/Index?desktop=1` — forces classic desktop UI on phone.

---

## 7. CSS architecture

### Breakpoint

- Mobile shell active: **viewport &lt; 1024px** OR **phone UA** (extra stylesheet loaded server-side)
- Desktop chrome hidden: **`@media (min-width: 1024px)`** in `mobile-shell.css`

### Phone-only load (avoids breaking desktop)

On **desktop PC**, only `site.css` + `quiz-index-layout.css` load.  
On **phone**, also `mobile-shell.css` + `mobile-shell-mobile.css` (no reliance on `@import` alone for iOS “desktop site” 1024px viewport).

### Answer feedback (mobile)

After submit, image answers get:

- **Correct:** yellow overlay + green outline (like desktop text tiles)
- **Incorrect:** red outline + tint
- Unselected disabled answers: dimmed grayscale

Defined in `mobile-shell-mobile.css` (`.answer-btn.correct`, `.incorrect`, etc.).

### `html.layout-desktop`

When cookie/query sets desktop on phone, rules in `mobile-shell.css` hide all `.mobile-only` chrome and show `.desktop-only` if present.

---

## 8. JavaScript behavior

### `mobile-shell.js`

- `isMobileShell()`: `data-mobile-device="true"` OR viewport ≤ 1023.98px (unless `layout-desktop` on html)
- Toggles `body.has-mobile-nav`, `has-mobile-header`, `has-sticky-actions`
- Drawer open/close + scroll lock (`body.mobile-drawer-active`)
- Bottom sheets + `window.mobileShell.closeBottomSheet(id)`
- `ResizeObserver` on sticky bar for bottom padding

### `mobile-preview-page.js`

- `window.NOODLES_QUIZ_PAGE` — set in Index to `"/Index"` (legacy default was `"/MobilePreview"`)
- Polls `/Stats` and `/api/online-count` every 5s; updates `stat-*`, `online-count-mobile`, `online-count-drawer`
- `window.__fetchMobileStats()` — called when opening stats sheet
- Report: binds **`#report-form` only** — mobile uses `#report-form-mobile` (verify binding if report submit fails on phone)
- POST: `{quizPage}?handler=ReportError` with antiforgery header

### `index-page.js`

- Desktop report form, footer stats toggle, difficulty button `#open-difficulty-modal-btn`
- Also syncs online count IDs including mobile variants when present

---

## 9. Important element IDs (avoid duplicates on same page)

| Desktop | Mobile |
|---------|--------|
| `#online-count` | `#online-count-mobile`, `#online-count-drawer` |
| `#report-form`, `#explanation` | `#report-form-mobile`, `#explanation-mobile` |
| `#open-difficulty-modal-btn` | `#open-difficulty-modal-btn-mobile` |
| `#stat-correct` etc. | `#stat-correct-mobile` etc. |

---

## 10. Running locally

```bash
cd /Users/yaniv/noodles-simulator
set -a && source ./env && set +a
dotnet run --urls "http://localhost:5001"
```

- Quiz: http://localhost:5001/Index  
- Legacy: http://localhost:5001/mobile-preview → redirects to Index  
- Force mobile cookie: `/Index?mobile=1`  
- Force desktop on phone: `/Index?desktop=1`  

**Env:** Supabase keys in `./env` (not committed).

---

## 11. Known issues / fixes applied in conversation

| Issue | Cause | Fix |
|-------|-------|-----|
| Mobile design not visible on phone | iOS “Request Desktop Website” → 1024px viewport; desktop CSS won last | Phone-only CSS file + server UA detection |
| Desktop layout “moved” / stuck right | RTL + flex column; mobile CSS on desktop; `desktop-only` wrappers | Split `isPhoneQuiz` markup; desktop doesn’t load mobile CSS |
| Buttons stacked on side | `site.css` `@media (max-width: 767px)` sets forms to `width: 100%` | `quiz-index-layout.css` forces horizontal centered row on Index |
| Report form floating right | Same centering issue | `quiz-index-layout.css` centers `.report-container` |

---

## 12. User constraints (keep for future work)

1. **Do not modify `wwwroot/css/site.css`** for mobile — only additive files.  
2. **Desktop and mobile must stay visually distinct** — don’t load mobile shell CSS on desktop.  
3. Logo stays **below** header on mobile, **not** inside top bar.  
4. Mobile answer highlighting: **yellow + green** correct, red incorrect.  
5. Real questions from **Supabase** on mobile (same `IndexModel` pipeline).

---

## 13. Quick checklist for changes

- [ ] Phone: hamburger left, bottom nav, sticky actions, report sheet  
- [ ] Desktop: top bar, separate meta lines, in-card buttons, centered report form  
- [ ] `/mobile-preview` still redirects to `/Index`  
- [ ] Cache-bust CSS (`?v=N`) after CSS changes  
- [ ] Hard refresh on phone; disable “Request Desktop Website” if layout wrong  

---

## 14. Related git reference

Original desktop-only `Index.cshtml` (before mobile merge): commit `f9cc142` — use `git show f9cc142:Pages/Index.cshtml` to compare.
