# Symbolic style specification

## Visual target

Match `../assets/style-reference.png` as closely as possible. The approved style is clean hand-drawn fantasy-map line art:

- heavy, smooth but slightly imperfect outer contours;
- inner construction lines about half the outer stroke weight;
- transparent or parchment-colored open interiors;
- small solid-black areas only for deep openings, eyes, gaps, or a focal cavity;
- compact, centered compositions with mildly exaggerated readable proportions;
- front view for signs and emblems, simple three-quarter view for objects and some buildings;
- no grayscale shading and no etched texture.

This is not a solid pictogram, minimalist corporate vector icon, realistic illustration, coloring-book page, medieval engraving, or detailed concept-art miniature.

## Prompt template

Use this base and replace the bracketed subject description:

> Create one isolated [SUBJECT] icon in exactly the same hand-drawn fantasy-atlas line-art style as the attached Killer Mud reference sheet. Match its bold rounded black outer contour, thinner sparse inner construction lines, slightly organic ink wobble, compact proportions, and mostly open unfilled interior. Use solid black only for deep openings or one small focal accent. Keep the silhouette immediately readable at 40 px while retaining the same illustrated outline character as the reference. Center the complete object with generous separation from the canvas edge. Flat chroma-key green background (#00FF00), no transparency. No text, border, ground plane, scenery, grayscale, gradients, shading, hatching, stippling, dense texture, tiny ornament, realistic rendering, sterile vector geometry, or full solid-black silhouette.

For green subjects, replace the green background with flat magenta `#FF00FF`.

Always attach `assets/style-reference.png` to `imagegen` using `referenced_image_paths`. Add a subject-specific sentence naming the main parts and the details to omit. Do not add adjectives such as intricate, ornate, realistic, richly decorated, or highly detailed.

Finish every generation prompt with:

> The attached sheet controls style, line weight, black-to-empty balance, and level of simplification. Do not copy a subject from it; create the requested subject as a new icon in that exact visual family.

## Detail budget

- One isolated object, creature, marker, or compact settlement composition.
- Identifying cues: 1-3, with fewer cues for racial architecture.
- Interior strokes: enough to explain major form, but never enough to become surface texture.
- Repeated small elements: generally at most 3; a settlement may contain up to 4 simplified buildings.
- Weapons or held props on a creature: at most 1.
- Windows and doors: use only large readable openings. Avoid rows of tiny windows.
- Black fill: normally limited to openings and accents; keep most of the bounded interior open.

## Subject recipes

Use these as construction limits, not as mandatory compositions:

- Elven city: one tall central leaf-spire plus two smaller spires or arches. Retain thick rounded contour and a few graceful inner curves. No tree trunk, vine lattice, facade filigree, repeated windows, or hair-thin ornament. Simpler than v2 `elven_city.png`.
- Dwarven city or fortress: one mountain-shaped mass, one large angular gate, and at most two squat tower cues. Retain bold contour and a few rock or facade divisions. No individual bricks, runic panels, statues, axes, or nested ornaments. Simpler than v2 `dwarven_fortress.png`.
- Human city: one curved wall, one central gate, and up to three large tower shapes. Match the friendly rounded contour of the reference walled city.
- Village: one focal mill or hall and two or three huts. No fences, crop rows, smoke, people, or landscape.
- Inn: one readable gabled building and one hanging sign. A few major beams are acceptable; omit small roof boards, window grids, furniture, barrels, roads, and trees.
- Wizard tower: one crooked tower, one secondary round volume, and one star. No magic particles or masonry texture.
- Monster: one readable full-body contour with a few major anatomy divisions and at most one prop. Use the reference's cartoon-fantasy proportions, but omit fur strands, scale fields, armor texture, veins, wrinkles, and dense facial rendering.
- Treasure: one outlined chest in simple three-quarter view with a lock and up to three large loot shapes. No scattered loot scene or wood-grain texture field.
- Danger: one warning shape plus one threat cue, such as a skull or flame. Do not combine multiple threats.
- Graveyard: up to three headstones. No fence, trees, fog, bones, or landscape.
- Sarcophagus: one lid silhouette and one extremely simple face or crossed-arm mark.
- Cave or lair: one dark arch opening with one jagged rock cue. No cliff scene.
- Volcano: one triangular cone and one plume or three lava strokes. No mountain range or clouds.

## Regeneration language

If a result is too detailed, do not merely say "simpler." Explicitly constrain the next generation:

> Redraw in the exact attached Killer Mud line-art style. Keep the bold rounded outer contour and open interior. Keep only [named structural parts]. Remove [named unwanted details]. Reduce inner lines to major construction only. Do not turn it into a flat vector glyph or solid silhouette. It must read at 40 px and still look hand-drawn.

For elven and dwarven symbols, always include:

> Use the contour language of the attached sheet, but make this simpler than the existing Killer Mud RPG Art v2 racial city icons; those are the upper limit and still contain too much architectural detail.

## Technical output

- One icon per PNG.
- Transparent background after chroma removal.
- Pure black RGB for every visible line pixel; use alpha for antialiasing and transparent open areas.
- Tight crop with consistent transparent padding.
- Suggested maximum canvas: 280 x 280 px.
- Use snake_case ASCII filenames.
- Organize every file at exactly `type/group/icon.png`, for example `cities/human/inn.png`, `cities/dwarven/fortress.png`, or `decorations/creatures/goblin.png`. Never nest a subgroup below the group; Nortantis may import the ZIP while showing no assets from that nested path.
