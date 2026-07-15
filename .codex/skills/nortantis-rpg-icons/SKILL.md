---
name: nortantis-rpg-icons
description: Generate or simplify reference-driven, hand-drawn black-outline RPG map icons matching the Killer Mud v1/v2 fantasy-atlas style and package them as Nortantis art packs. Use when the user asks for Nortantis icons, symbolic map markers, cities, settlements, racial architecture, monsters, treasures, dangers, graves, caves, volcanoes, or a ZIP art pack, including requests to restyle an existing pack with fewer details.
---

# Nortantis RPG Icons

Create hand-drawn fantasy-atlas icons matching the approved Killer Mud v1/v2 line-art style. Keep them symbolic and readable at 40-80 px without turning them into solid pictograms.

## Workflow

1. Read [style-spec.md](references/style-spec.md) completely before composing prompts.
2. Inspect [style-reference.png](assets/style-reference.png) with `view_image`. Treat it as the authoritative visual reference for contour, proportions, black fill, perspective, and line density.
3. Inspect Nortantis built-in art and `tools/Nortantis/ArtPacks/bonus_art_v2.zip` only when category organization or scale is needed. Do not let those packs override the bundled style reference.
4. List the requested symbols and give each one a dominant silhouette plus only the structural details needed to identify it.
5. Generate each symbol separately with `imagegen`. Always pass `assets/style-reference.png` through `referenced_image_paths`; a text-only prompt is insufficient for this style. Use a flat chroma background and no scenery.
6. Inspect every result at full size and at 40 px. Reject and regenerate flat vector glyphs, solid silhouettes, realistic illustrations, coloring-book art, engraved art, or drawings denser than the reference. Apply the stricter architecture rules to elves and dwarves.
7. Convert accepted results to pure black RGBA, remove the chroma background, crop transparent margins, and normalize their canvas sizes. Preserve antialiasing in alpha. Keep generated assets outside the skill directory unless the user explicitly asks to update the skill itself.
8. Arrange processed PNG files using exactly `type/group/icon.png`, for example `cities/medieval buildings/inn.png` or `decorations/city roads/road_curve.png`. Do not add another directory below the group; Nortantis imports such ZIPs but does not index their images. Then run:

   `python .codex/skills/nortantis-rpg-icons/scripts/package_art_pack.py <staging-directory> <output.zip> --pack-name "Killer Mud RPG Art"`

9. Import the ZIP into Nortantis and verify several dense and sparse symbols on parchment at normal map scale.

## Non-negotiable style rules

- Match the bundled reference: bold, slightly irregular black outer contour; thinner sparse interior strokes; open transparent interiors; occasional solid-black cavities or accents.
- Use one symbol and one visual idea per file.
- Use rounded line joins and caps with a subtle hand-drawn wobble. Avoid sterile vector geometry.
- Keep the outer contour roughly twice as heavy as interior marks.
- Use solid black mainly for deep openings, eyes, or one focal accent. Never fill the entire subject as a silhouette.
- Allow simple three-quarter perspective for objects and buildings when the reference uses it; keep flat front views for markers and emblems.
- Preserve quiet, unmarked areas inside the shape. Interior marks must describe construction, not create texture.
- Do not use gradients, color, gray wash, hatching, cross-hatching, stippling, realistic texture, dense masonry, decorative foliage, elaborate armor, labels, borders, scenic backgrounds, or cast shadows.
- Treat the existing v2 elven and dwarven icons as too detailed. New racial architecture must be simpler than those files.
- Communicate race or faction with one or two architectural cues only. Preserve the approved contour style while reducing ornament.
- Do not solve weak readability by making a filled glyph or by adding detail. Strengthen the outlined silhouette instead.

## Acceptance check

Before packaging, view every icon at 40 px and answer yes to all of these:

- Is the subject recognizable in under one second?
- Does it visibly belong beside the examples in `style-reference.png`?
- Is the subject mostly outlined line art rather than a solid pictogram?
- Does the outer contour carry most of the meaning while inner lines explain only major form?
- Would deleting one more detail improve the icon? If yes, delete it.
- Does it remain distinct from other icons in the same pack?

Do not package icons that fail this check.
