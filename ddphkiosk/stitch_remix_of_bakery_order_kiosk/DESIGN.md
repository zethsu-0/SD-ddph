# Design System: The Artisanal Kiosk

## 1. Overview & Creative North Star
**Creative North Star: "The Flourished Hearth"**

To design for a high-end bakery is to design for the senses. We are moving beyond the "digital interface" and into the realm of "digital hospitality." This system rejects the cold, clinical efficiency of standard kiosks in favor of a **High-End Editorial** experience that feels as tactile as a dusted loaf of sourdough and as curated as a boutique patisserie menu.

We achieve this through **Intentional Asymmetry** and **Tonal Depth**. By breaking the rigid, centered grid of typical self-service software, we create a sense of movement and discovery. Overlapping elements—such as a product image slightly breaking the bounds of its container—will simulate the layering of fine parchment and linen.

## 2. Colors: A Palette of Natural Ingredients
Our color strategy is rooted in "The No-Line Rule." We do not use borders to define space; we use the subtle shift of natural light and organic tones.

### The Palette
- **Primary (`#576342` - Sage):** Our "flourish" color. Used for moments of confirmation and key brand accents.
- **Secondary (`#8d4e36` - Terracotta/Blush):** Represents the warmth of the oven. Used for primary CTAs to evoke appetite and action.
- **Surface & Background (`#fbf9f5` - Cream):** The "flour" of our system. All interfaces start here.

### The Rules of Engagement
- **The "No-Line" Rule:** 1px solid borders are strictly prohibited for sectioning. Boundaries must be defined solely through background color shifts. For example, a `surface-container-low` sidebar sitting against a `surface` background provides all the definition a high-end user needs.
- **Surface Hierarchy & Nesting:** Treat the UI as a series of physical layers. Use the `surface-container` tiers (Lowest to Highest) to create "nested" depth. A product detail card (`surface-container-lowest`) should sit atop a category tray (`surface-container-low`) to create a soft, natural lift.
- **The "Glass & Gradient" Rule:** To avoid a flat, "templated" look, use Glassmorphism for floating elements (like a "View Cart" bar). Apply a semi-transparent `surface` color with a `backdrop-blur` of 12px-16px.
- **Signature Textures:** For hero backgrounds or large action buttons, use a subtle radial gradient transitioning from `primary` to `primary-container`. This adds a "visual soul" and three-dimensional depth that a flat hex code cannot achieve.

## 3. Typography: The Editorial Voice
We pair the authority of a serif with the modern clarity of a geometric sans-serif.

- **Display & Headlines (Noto Serif):** This is our "Brand Mark." Used in `display-lg` through `headline-sm`. These should be set with generous leading (1.2 - 1.4) to feel like a premium cookbook.
- **Titles & Body (Plus Jakarta Sans):** Our "Functional Voice." Used for navigation, product names, and descriptions. This typeface provides a clean, modern counter-balance to the warmth of the serif.
- **The Hierarchy Strategy:** Use high-contrast scale shifts. A `display-md` headline paired with a `body-md` description creates an editorial look that guides the eye through "The Artisan’s Path" (the user flow).

## 4. Elevation & Depth: Tonal Layering
In a tactile kiosk environment, depth signifies touchability. We move away from "drop shadows" toward "ambient light."

- **The Layering Principle:** Stack `surface-container` tiers. 
    *   *Base:* `surface`
    *   *Section:* `surface-container-low`
    *   *Interactive Card:* `surface-container-lowest` (White)
- **Ambient Shadows:** When an element must float (e.g., a modal or a floating action button), use an extra-diffused shadow: `blur: 40px`, `spread: 0`, `opacity: 6%`. The shadow color must be a tinted version of `on-surface` (`#31332f`), never pure black.
- **The "Ghost Border" Fallback:** If accessibility requires a stroke (e.g., a focused input), use the `outline-variant` token at 20% opacity. 100% opaque borders are considered a "system failure" in this aesthetic.

## 5. Components: Tactile Primitives

### Buttons
- **Primary (The Signature):** Uses `secondary` (Terracotta). 1.5rem (`xl`) corner radius. Subtle gradient from `secondary` to `secondary_dim`.
- **Secondary (The Ghost):** No background, no border. Uses `title-md` typography in `primary` (Sage) with a subtle `surface-container` hover state.

### Input Fields
- **The Floating Input:** Forbid traditional "box" inputs. Use a `surface-container-low` background with a generous top/bottom padding and a 0.5rem (`DEFAULT`) corner radius. The label should use `label-md` in `on-surface-variant`.

### Cards & Lists
- **The Editorial Card:** Forbid divider lines. Separate items using vertical white space (use the 24px/32px spacing increments). 
- **Tactile Interaction:** Product cards should use `surface-container-lowest` and feature a 1rem (`lg`) corner radius. When selected, the card should not "glow"; it should shift to a `primary-container` background.

### Custom Component: The "Ingredients Tray"
A horizontal scrolling area for modifiers (extra butter, toasted, etc.) using `selection chips`. These chips should use `surface-container-high` as a base and transition to `primary` when active, mimicking the physical act of "pressing" a button into dough.

## 6. Do's and Don’ts

### Do:
- **Use Intentional Asymmetry:** Offset images of pastries so they bleed off the edge of the container to create a "freshly prepared" feel.
- **Embrace White Space:** High-end brands breathe. If a screen feels "full," remove a container background and use spacing instead.
- **Prioritize Legibility:** Ensure `on-surface` text on `surface` backgrounds meets WCAG AA standards, even while maintaining the soft pastel palette.

### Don't:
- **No Hard Dividers:** Never use a 1px line to separate content. Use a 8px-16px gap or a background color shift.
- **No System Grays:** Use our tinted neutrals (`surface-dim`, `on-surface-variant`) to keep the "warm" bakery feel. Pure gray (`#808080`) will break the organic immersion.
- **No Aggressive Shadows:** If the shadow is the first thing you see, it’s too dark. It should be felt, not seen.