# Quick Start — Mobile/Desktop Layout

## For Users

### Force Mobile Layout (on Desktop)
Visit: `/Index?mobile=1`  
This will set a cookie and remember your preference for 30 days.

### Force Desktop Layout (on Phone)
Visit: `/Index?desktop=1` OR use the drawer menu link "תצוגת מחשב"  
This will set a cookie and remember your preference for 30 days.

### Clear Preference
Delete the `noodles_layout` cookie, or visit `/Index` without any query params.

---

## For Developers

### Key Files Overview

| File | Purpose |
|------|---------|
| `Pages/Index.cshtml` | Device detection (`isPhoneQuiz` boolean) and layout branching |
| `Pages/Shared/_MobileQuizShell.cshtml` | Mobile chrome (header, drawer, bottom nav, sheets) |
| `wwwroot/css/mobile-shell.css` | Mobile component styles (all interactive components) |
| `wwwroot/css/mobile-shell-mobile.css` | Mobile layout toggles and answer feedback colors |
| `wwwroot/js/mobile-shell.js` | Drawer/sheet interaction logic and scroll lock |
| `wwwroot/js/mobile-preview-page.js` | Report form handler (both desktop & mobile) |

### Adding a Mobile Feature

1. **Add HTML** in `Pages/Shared/_MobileQuizShell.cshtml`
   - Use `.mobile-only` or `mobile-*` class naming
   - Include ARIA labels for accessibility

2. **Add CSS** in `wwwroot/css/mobile-shell.css` or `mobile-shell-mobile.css`
   - **Never modify `site.css`** for mobile
   - Use `@media (max-width: 1023.98px)` for mobile-specific rules
   - Ensure desktop `@media (min-width: 1024px)` hides mobile chrome

3. **Add JS** in `wwwroot/js/mobile-shell.js` if needed
   - Register event listeners
   - Use `isMobileShell()` to check if mobile UI is active
   - Call `updateStickyBarHeight()` if adding fixed elements

4. **Test on both desktop and phone**
   - Desktop: no mobile chrome visible
   - Phone: mobile UI appears
   - `/Index?desktop=1`: desktop UI on phone
   - `/Index?mobile=1`: mobile UI on desktop

### Common Tasks

#### Add a new drawer menu item
```html
<a href="/path/to/page" class="mobile-drawer-link">
    <svg aria-hidden="true"><!-- icon --></svg>
    Link text
</a>
```

#### Add a new bottom sheet
```html
<aside class="bottom-sheet mobile-only" id="my-sheet">
    <div class="bottom-sheet-handle"></div>
    <div class="bottom-sheet-header">
        <h3 class="bottom-sheet-title">Title</h3>
        <button data-bottom-sheet-close="my-sheet">Close</button>
    </div>
    <div class="bottom-sheet-body">Content</div>
</aside>
```

Then in `mobile-shell.js`, add trigger:
```javascript
document.getElementById('my-trigger').addEventListener('click', () => {
    window.mobileShell.openBottomSheet('my-sheet');
});
```

#### Hide something on mobile only
```html
<div class="desktop-only">Desktop only content</div>
```

#### Hide something on desktop only
```html
<div class="mobile-only">Mobile only content</div>
```

### Debugging

**Check if mobile UI is active:**
```javascript
console.log(window.mobileShell?.isMobileShell?.());
```

**Check active layout:**
```javascript
console.log(document.documentElement.classList.toString());
// Should contain 'layout-mobile' or 'layout-desktop'
```

**Manual toggle (browser console):**
```javascript
// Force mobile
document.documentElement.classList.add('layout-mobile');
document.documentElement.classList.remove('layout-desktop');

// Force desktop
document.documentElement.classList.add('layout-desktop');
document.documentElement.classList.remove('layout-mobile');
```

### Responsive Breakpoint

- **Mobile:** < 1024px viewport OR phone UA detection
- **Desktop:** ≥ 1024px AND desktop UA

The breakpoint is defined in `mobile-shell.css`:
```css
@media (max-width: 1023.98px) {
    /* Mobile rules */
}

@media (min-width: 1024px) {
    /* Desktop rules */
}
```

### CSS Priority

1. `site.css` — Base desktop styles (never touch for mobile)
2. `quiz-index-layout.css` — Quiz-specific layout (centering, button layout)
3. `mobile-shell.css` — Mobile component styles (added on phones only)
4. `mobile-shell-mobile.css` — Mobile layout & feedback colors (added on phones only)

**Important:** Mobile CSS files are **only loaded on phones** to prevent style conflicts on desktop.

---

## File Size Reference

| File | Size | Min lines |
|------|------|-----------|
| `Pages/Index.cshtml` | 11 KB | ~350 |
| `_MobileQuizShell.cshtml` | 16 KB | ~300 |
| `mobile-shell.css` | 20 KB | ~750 |
| `mobile-shell-mobile.css` | 9 KB | ~300 |
| `mobile-shell.js` | 8.3 KB | ~250 |
| `mobile-preview-page.js` | 6.2 KB | ~160 |

All files are optimized for production. No minification applied (use your build pipeline).

---

## Known Limitations

- **iOS "Request Desktop Website"** — still shows mobile UI (UA-based detection)
  - Users can force desktop with `/Index?desktop=1`
- **Landscape mode on phone** — same mobile shell (not tablet-optimized)
  - Future work: add tablet breakpoint (1024px - 1366px)
- **Drag handle on bottom sheets** — visual only (mobile sheets use native scroll)

---

## References

- Full reference: `noodles-simulator-mobile-preview-reference.md`
- Implementation details: `IMPLEMENTATION_SUMMARY.md`
- Original commit: `da9e28e` (git log --oneline)
