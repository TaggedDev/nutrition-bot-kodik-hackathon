# Frontend Guidelines

## Color System

- Base surfaces: use white and soft lavender-white surfaces for app panels, cards, inputs, and popovers.
- Nuance colors: use lavender borders, muted violet text, and low-opacity purple shadows for hierarchy.
- Accent color: use purple as the primary action and selection color.
- Macro colors: protein is purple, fat is orange, carbs are teal.
- Avoid green as a primary UI color.
- Avoid red or yellow as dominant UI colors; reserve red for destructive hover/error states.
- Do not show confidence score in the chat or meal context UI.

## Layout Rules

- The main chat screen uses three functional zones: left navigation, central chat, right meal context panel.
- Right sidebar is a meal context panel, not a generic form. It should contain header, basket, total, day progress, and recommendations.
- Cards should have clear ownership and should not be nested inside decorative section cards.
- Prefer compact cards with 16px radius for repeated items.
- Use 22-24px radius only for major panels or premium recommendation cards.
- Use fixed or constrained dimensions for toolbars, thumbnails, calendar cells, and action buttons so hover/active states do not shift layout.
- Text must fit inside its container on desktop and mobile. Truncate product names in compact rows.

## Typography

- Use the same app font stack everywhere: Manrope, Inter, system UI, sans-serif.
- Numeric summary values use strong but smooth weight: 800, letter-spacing 0, line-height near 1.
- Header values should reuse the same visual tone as meal total values: dark text, smooth antialiasing, no pixelated heavy weight.
- Avoid oversized headings inside compact panels.

## Button States

- Idle primary action: purple gradient fill, white foreground, subtle purple shadow.
- Idle secondary action: white or light lavender background, purple foreground, lavender border.
- Hover secondary action: light lavender background and stronger lavender border.
- Selected/add state: pastel purple foreground, light fill, bright purple 2px outline.
- Remove hover state: red foreground, white or very light red fill, saturated red outline.
- Click state: keep layout stable; use small translate or brightness changes only when it does not move neighboring content.
- Icon buttons should use recognizable symbols instead of text when the action is common.

## Calendar Popovers

- Calendar popovers start weeks on Monday.
- Show every day of the visible month.
- Header contains previous/next month buttons and the current month name.
- Footer shows the year.
- Clicking a day changes the selected app date and refreshes date-bound data.

## Meal Context Behavior

- Selected meal type controls the right panel basket and where new chat results are added.
- Date selection controls the loaded day, current meal totals, and day progress.
- Empty current meal state should be explicit and helpful, not a broken empty card.
- Removing an item immediately recalculates meal total and progress.
