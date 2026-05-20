# Responsive Layout Component — Test Plan

## Pre-Deployment Testing Checklist

### 1. Desktop Layout (1024px and above)
- [ ] Visit `/Index` on desktop browser (Chrome, Firefox, Safari, Edge)
- [ ] Verify **NO mobile chrome is visible**:
  - No hamburger menu header
  - No bottom navigation bar
  - No drawer or bottom sheets
  - All `.mobile-only` elements are hidden
- [ ] Verify desktop layout is displayed:
  - Top bar with logo
  - Quiz content centered
  - "מצב מבחן" (Test Mode), "שאלה הבאה" (Next), "איפוס שאלות" (Reset) buttons in a horizontal row
  - Desktop buttons should have proper styling and spacing
- [ ] Check responsive breakpoint — resize to 1023px and verify mobile chrome appears
- [ ] Verify all desktop styles from `site.css` and `quiz-index-layout.css` are applied

### 2. Mobile Layout (< 1024px or on actual phone)
- [ ] Visit `/Index` on mobile browser or emulate with DevTools
- [ ] Verify **mobile chrome is visible**:
  - [ ] Fixed header with hamburger menu (48×48 touch target)
  - [ ] Sticky action bar above bottom navigation
  - [ ] Bottom navigation with 4 tabs (Test Mode, Next, Report, Reset)
  - [ ] All `.desktop-only` elements are hidden
- [ ] Tap hamburger menu:
  - [ ] Drawer slides in from left
  - [ ] Backdrop appears behind drawer
  - [ ] Drawer has menu items (logout, desktop mode link, etc.)
  - [ ] Tapping outside drawer closes it
  - [ ] ESC key closes drawer
  - [ ] Scroll is locked while drawer is open
- [ ] Bottom sheets:
  - [ ] "Report" button opens report form bottom sheet
  - [ ] Bottom sheet has drag handle, header, and form
  - [ ] Form submission closes sheet and shows success message
  - [ ] Clicking outside sheet or header close button closes it
  - [ ] Scroll is locked while sheet is open
- [ ] Answer selection:
  - [ ] Answer buttons are 100% width on mobile (easier to tap)
  - [ ] Correct/incorrect feedback is styled properly
  - [ ] Feedback colors are applied from `mobile-shell-mobile.css`

### 3. Mobile Layout Override Query Params & Cookies
- [ ] Desktop → Mobile override:
  - [ ] Visit `/Index?mobile=1` on desktop
  - [ ] Mobile chrome appears on desktop viewport
  - [ ] Verify cookie `noodles_layout=mobile` is set (30-day expiry)
  - [ ] Refresh page — mobile chrome persists
  - [ ] Visit `/Index` normally — mobile chrome still visible (until cookie expires)

- [ ] Mobile → Desktop override:
  - [ ] Visit `/Index?desktop=1` on phone or mobile viewport
  - [ ] Desktop layout appears instead of mobile chrome
  - [ ] Verify cookie `noodles_layout=desktop` is set (30-day expiry)
  - [ ] Refresh page — desktop layout persists
  - [ ] Visit `/Index` normally — desktop layout still visible (until cookie expires)

- [ ] Clear preference:
  - [ ] Delete `noodles_layout` cookie
  - [ ] Visit `/Index` — device detection kicks back in (mobile on phone, desktop on desktop)

### 4. Responsive Breakpoint Verification
- [ ] Desktop browser:
  - [ ] 1024px+ viewport → desktop layout
  - [ ] Resize to 1023px → mobile layout appears
  - [ ] Verify smooth transition (CSS transitions enabled)
- [ ] Mobile browser:
  - [ ] Mobile viewport (<1024px) → mobile layout
  - [ ] DevTools device emulation changes properly

### 5. CSS & JavaScript Functionality
- [ ] Drawer interaction:
  - [ ] Smooth slide-in animation
  - [ ] Backdrop fades in
  - [ ] Body scroll is locked (no background scroll while drawer open)
  - [ ] Tab key focus remains within drawer (accessibility)
- [ ] Bottom sheets:
  - [ ] Smooth slide-up animation
  - [ ] Backdrop fades in
  - [ ] Body scroll is locked
  - [ ] Close button and backdrop click close the sheet
- [ ] Sticky action bar:
  - [ ] Stays visible while scrolling
  - [ ] Doesn't overlap with bottom nav or other content
  - [ ] Placeholder prevents layout shift
- [ ] Mobile header:
  - [ ] Hamburger button is easily tappable (48×48)
  - [ ] Button hover/active states are clear
- [ ] Bottom nav:
  - [ ] All 4 tabs are equally spaced
  - [ ] Touch targets are 44px minimum height
  - [ ] Icons are crisp and properly sized (24px)
  - [ ] Active state is visually distinct

### 6. Form Submission (Report Form)
- [ ] Desktop:
  - [ ] Report form works on desktop
  - [ ] Form submission is processed correctly
- [ ] Mobile:
  - [ ] Report button opens bottom sheet with form
  - [ ] Form submission closes sheet
  - [ ] Success message appears
  - [ ] Form is cleared on success
- [ ] Both:
  - [ ] Anti-forgery token is included
  - [ ] Error handling works (test with invalid data)
  - [ ] Network errors are handled gracefully

### 7. Accessibility (a11y)
- [ ] Semantic HTML:
  - [ ] `<header>`, `<nav>`, `<aside>`, `<main>` tags used appropriately
  - [ ] Button elements used for interactive controls (not `<div>` with onclick)
- [ ] ARIA labels:
  - [ ] Hamburger button has `aria-label="פתח תפריט"`
  - [ ] Drawer has `aria-hidden` attribute (correct state)
  - [ ] Form buttons have descriptive labels
- [ ] Keyboard navigation:
  - [ ] Tab through buttons and form fields
  - [ ] ESC closes drawer/sheets
  - [ ] Enter submits forms
- [ ] Screen reader:
  - [ ] Test with VoiceOver (Mac/iOS) or NVDA/JAWS (Windows)
  - [ ] All interactive elements are announced correctly
  - [ ] Hidden elements are properly marked

### 8. Performance
- [ ] CSS file sizes:
  - [ ] `mobile-shell.css` ≤ 25KB (test currently ~20KB ✓)
  - [ ] `mobile-shell-mobile.css` ≤ 15KB (test currently ~9KB ✓)
  - [ ] `quiz-index-layout.css` ≤ 5KB (test currently ~1.5KB ✓)
- [ ] JavaScript file sizes:
  - [ ] `mobile-shell.js` ≤ 12KB (test currently ~8.3KB ✓)
  - [ ] `mobile-preview-page.js` ≤ 10KB (test currently ~6.2KB ✓)
- [ ] Load time:
  - [ ] Mobile CSS loaded only on mobile devices (UA detection)
  - [ ] No render-blocking stylesheets for non-matching media queries
- [ ] Animation smoothness:
  - [ ] Drawer slides in smoothly (60fps)
  - [ ] Bottom sheets animate without jank
  - [ ] No layout thrashing during interactions

### 9. Browser & Device Compatibility
- [ ] Desktop browsers:
  - [ ] Chrome (latest)
  - [ ] Firefox (latest)
  - [ ] Safari (latest)
  - [ ] Edge (latest)
- [ ] Mobile browsers:
  - [ ] Chrome (iOS/Android)
  - [ ] Safari (iOS)
  - [ ] Firefox (Android)
- [ ] Device sizes:
  - [ ] Phone: 375×667 (iPhone SE)
  - [ ] Phone: 390×844 (iPhone 12)
  - [ ] Phone: 412×915 (Android standard)
  - [ ] Tablet: 768×1024 (iPad)
  - [ ] Landscape: 667×375

### 10. Dark Mode (if applicable)
- [ ] Desktop layout respects system dark mode preference
- [ ] Mobile layout respects system dark mode preference
- [ ] Colors are properly contrasted in both light and dark modes
- [ ] Drawer and bottom sheets render correctly in dark mode

### 11. Fallback Testing (Progressive Enhancement)
- [ ] JavaScript disabled:
  - [ ] Page still loads and displays content
  - [ ] Mobile layout is visible (based on UA)
  - [ ] Drawer doesn't open (expected without JS)
  - [ ] Forms are still submittable
- [ ] Old browser (IE11 if needed):
  - [ ] Basic layout works (might not have animations)
  - [ ] All critical functionality works

### 12. Edge Cases
- [ ] Double-tap on mobile buttons → no double submissions
- [ ] Fast clicks on drawer → drawer doesn't toggle multiple times
- [ ] Resize window → layout switches smoothly between mobile/desktop
- [ ] Navigate between pages → mobile state is preserved
- [ ] Long content:
  - [ ] Scroll works properly in drawer
  - [ ] Scroll works properly in bottom sheets
  - [ ] Sticky action bar stays visible

## Test Coverage Summary

| Component | Desktop | Mobile | Override | Accessibility | Performance |
|-----------|---------|--------|----------|----------------|-------------|
| Layout    | ✓       | ✓      | ✓        | ✓              | ✓           |
| Drawer    | ✗       | ✓      | ✓        | ✓              | ✓           |
| Bottom Nav| ✗       | ✓      | ✓        | ✓              | ✓           |
| Sheets    | ✗       | ✓      | ✓        | ✓              | ✓           |
| Forms     | ✓       | ✓      | ✓        | ✓              | ✓           |

## Testing Environment

### Local Testing (Before Commit)
```bash
# Build and run locally
dotnet restore
dotnet run

# Visit http://localhost:5000/Index
```

### Browser DevTools Testing
```javascript
// Force mobile layout on desktop
document.documentElement.classList.add('layout-mobile');
document.documentElement.classList.remove('layout-desktop');

// Check if mobile shell is active
console.log(window.mobileShell?.isMobileShell?.());

// Manually open drawer
window.mobileShell?.openDrawer?.();

// Manually close drawer
window.mobileShell?.closeDrawer?.();

// Check active layout
console.log(document.documentElement.classList.toString());
```

### Automated Testing (Future)
- [ ] Unit tests for `mobile-shell.js` drawer/sheet logic
- [ ] E2E tests for layout switching with Playwright or Cypress
- [ ] Visual regression tests for mobile vs. desktop
- [ ] Accessibility audit with axe-core

## Blockers & Known Limitations

1. **iOS "Request Desktop Website"** — still shows mobile UI (UA-based detection)
   - Workaround: Users can force desktop with `/Index?desktop=1`

2. **Landscape mode on phone** — uses mobile shell (not tablet-optimized)
   - Future work: add tablet breakpoint (1024px - 1366px)

3. **Drag handle on bottom sheets** — visual only (mobile sheets use native scroll)
   - Works as intended; future enhancement: swipe-to-close

## Sign-Off

- [ ] Dev: All tests pass locally
- [ ] QA: All tests pass on staging
- [ ] Product: Approve for production release

---

**Last Updated:** 2026-05-20  
**Status:** Ready for Testing
