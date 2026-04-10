# Design System Document: The Artisanal Interface

## 1. Overview & Creative North Star
**Creative North Star: "The Flourished Hearth"**

This design system moves away from the sterile, plastic feel of traditional Point-of-Sale (POS) software. Instead, it draws inspiration from high-end editorial food magazines and the tactile nature of a boutique bakery. The goal is "Warm Modernism"—a marriage of high-efficiency retail utility and the soft, inviting atmosphere of a morning kitchen.

We reject the "boxed-in" look of legacy software. By utilizing intentional asymmetry, oversized "editorial" typography, and a "No-Line" philosophy, we create a digital space that feels as airy and light as a fresh croissant. Elements shouldn't just sit on the screen; they should feel nested, layered, and curated.

---

## 2. Colors: Tonal Depth & The "No-Line" Rule
The palette is a sophisticated blend of creams and earths, accented by a botanical green (Tertiary) and a soft berry (Secondary).

### The "No-Line" Rule
**Explicit Instruction:** Do not use 1px solid borders to define sections. Layout boundaries must be defined exclusively through background color shifts. For example, a sidebar using `surface-container-low` should sit directly against a `background` canvas. 

### Surface Hierarchy & Nesting
Treat the UI as a physical stack of fine parchment. 
- **Base Canvas:** `background` (#fff8ef)
- **Primary Containers:** `surface-container` (#f5edde)
- **Interactive Elevated Cards:** `surface-container-lowest` (#ffffff)
- **Deep Recessed Areas:** `surface-dim` (#e1d9cb)

### The "Glass & Gradient" Rule
To add "soul" to the retail experience, use Glassmorphism for floating overlays (like "Order Summaries" or "Modals"). Use `surface` colors at 80% opacity with a `24px` backdrop-blur. 
**Signature Texture:** Apply a subtle linear gradient to Primary buttons, transitioning from `primary` (#765449) to `primary_container` (#916c60) at a 135-degree angle. This mimics the toasted gradient of a loaf of bread.

---

## 3. Typography: Editorial Utility
We pair **Plus Jakarta Sans** (Display/Headlines) for a modern, high-end feel with **Work Sans** (Body/Labels) for its exceptional legibility in high-pressure environments.

*   **Display (Plus Jakarta Sans):** Large, bold, and expressive. Use `display-lg` for daily specials or total amounts to create a clear visual anchor.
*   **Headlines (Plus Jakarta Sans):** Set at `headline-md` for category headers (e.g., "Sourdoughs," "Pastries"). The tight kerning provides a premium, "signed" look.
*   **Body & Titles (Work Sans):** Optimized for the "glance-test." Use `title-lg` for product names on buttons to ensure cashiers can identify items instantly under fluorescent lighting.
*   **Labels (Work Sans):** Reserved for technical data (SKUs, timestamps). Even at `label-sm`, the high x-height of Work Sans maintains clarity.

---

## 4. Elevation & Depth: Tonal Layering
Traditional drop shadows are often too "digital." Here, we use light and color to create presence.

*   **The Layering Principle:** Instead of a shadow, place a `surface-container-highest` element behind a `surface-container-lowest` card. The 6% difference in luminance is enough to define the edge naturally.
*   **Ambient Shadows:** For critical floating elements (e.g., a "Checkout" drawer), use an extra-diffused shadow: `offset-y: 12px, blur: 40px, color: rgba(30, 27, 19, 0.06)`. This uses the `on-surface` hue for a natural, ambient occlusion effect.
*   **The "Ghost Border" Fallback:** If accessibility requirements demand a border, use `outline_variant` at 15% opacity. Never use 100% opaque lines.
*   **Glassmorphism:** Use for "Quick View" overlays. It keeps the cashier grounded in the shop's workflow by allowing the warm background colors to bleed through the active task.

---

## 5. Components: Tactile & Generous
All components follow a generous **xl (1.5rem)** or **lg (1rem)** roundedness scale to mimic the soft edges of organic dough.

*   **Product Tiles (Cards):** Forbid divider lines. Use `surface-container-low` for the card body and `surface-container-lowest` for the price badge. Leave ample white space (24px+) between product name and price.
*   **Primary Action Buttons:** Use the signature bread-toast gradient. Sizing should be a minimum of 64px in height for desktop touch-point parity.
*   **Quantity Chips:** Use `secondary_container` (#fec1d6) for "Add" actions to make them feel cheerful and rewarding.
*   **Input Fields:** Ghost-style inputs. Use `surface-variant` backgrounds with no borders. On focus, transition the background to `surface-container-lowest` with a subtle `primary` glow.
*   **The "Order Ribbon" (List):** Replace standard list dividers with 8px vertical gaps. Group items using a `surface-container-high` background for the entire group.
*   **Status Indicators:** Use `tertiary` (#266931) for "In Oven" or "Available" states. The green should feel botanical and fresh, not "system-error" green.

---

## 6. Do's and Don'ts

### Do:
*   **Do** use asymmetrical layouts for the dashboard (e.g., a wide product grid with a narrower, overlapping order summary).
*   **Do** prioritize "breathing room." If an interface feels cramped, increase the surface-to-surface padding rather than adding a divider.
*   **Do** use `on_surface_variant` for secondary information to maintain a soft, low-contrast visual hierarchy that reduces eye strain over an 8-hour shift.

### Don't:
*   **Don't** use pure black (#000000). Always use `on_background` (#1e1b13) to maintain the warmth of the palette.
*   **Don't** use hard 90-degree corners. Everything in a bakery is organic; your UI should be too.
*   **Don't** use "Alert Red" for non-critical errors. Use `secondary` tones for warnings to keep the atmosphere "cheerful" and calm. Only use `error` (#ba1a1a) for catastrophic failures (e.g., Payment Declined).
*   **Don't** add anything related to stocks