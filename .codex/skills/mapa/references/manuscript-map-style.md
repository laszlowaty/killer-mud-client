# Manuscript map style

## Core prompt — use verbatim

Copy the complete block below into every image-generation prompt. Do not summarize, translate, shorten, or replace it.

```text
Create a highly detailed hand-drawn fantasy map inspired by late-medieval and early Renaissance manuscript cartography.

VISUAL STYLE:
- Antique parchment background with warm beige, ivory, tan and faded ochre tones.
- The paper must look naturally aged: subtle stains, faded areas, small speckles, uneven fibers, soft discoloration near the edges and occasional ink imperfections.
- Use black and dark brown ink for terrain, borders and symbols.
- Use restrained dark burgundy or oxblood-red ink only for major region names, decorative initials, borders and selected cartographic ornaments.
- The artwork must look entirely hand-drawn with a dip pen, fine nib, dry brush and stippling.
- Lines should be slightly irregular and organic rather than geometrically perfect.
- Avoid smooth digital gradients. Create shadows using cross-hatching, short ink strokes and dense stippling.

MAP CONSTRUCTION:
- Draw irregular coastlines with many natural bays, peninsulas, capes and narrow inlets.
- Use dense stippled dots along coastlines and selected sea areas.
- Represent mountain ranges as repeated individual triangular mountain illustrations, viewed from a slightly elevated side angle.
- Each mountain should have a dark shaded side made from short parallel ink strokes.
- Mountain chains should overlap slightly and follow believable geological arcs.
- Represent forests as dense clusters of many tiny hand-drawn trees rather than flat green shapes.
- Draw rivers as thin winding ink lines that begin in mountain regions, merge naturally and flow toward the sea.
- Add small settlements, towers, fortresses, ruins, bridges, roads and regional landmarks using miniature medieval symbols.
- Roads and borders should be subtle, broken or lightly dotted.
- Do not use satellite-map realism, contour lines or modern GIS symbols.

TYPOGRAPHY:
- Major kingdoms, seas and wilderness regions should use large decorative medieval calligraphy in faded burgundy ink.
- Cities, rivers, mountains and minor locations should use small black handwritten lettering.
- Letter placement may gently follow the curve of rivers, coastlines or mountain ranges.
- Typography should feel hand-lettered, slightly inconsistent and integrated into the illustration.
- Keep labels readable and prevent them from overlapping important terrain.

COMPOSITION:
- Use an old manuscript-map layout with a decorative border.
- Add a detailed compass rose in one corner using black, parchment white and dark burgundy.
- Optionally add a title cartouche, scale bar, small marginal annotations or ornamental symbols.
- Leave some open parchment areas so the composition does not feel digitally overcrowded.
- Create a clear hierarchy between continents, kingdoms, regions, settlements and minor landmarks.
- The map should feel ancient, mysterious, scholarly and believable.

RENDERING:
- Extremely detailed pen-and-ink illustration.
- Fine engraved texture.
- High-resolution.
- Matte surface.
- Subtle faded print effect.
- Slightly imperfect registration of red and black ink.
- No glossy lighting.
- No photorealistic 3D terrain.
- No modern UI elements.
- No neon colors.
- No clean vector-flat appearance.

IMPORTANT:
Create an original geography, original place names and an original composition. Do not reproduce an existing fictional world, map layout, symbols or copyrighted lettering.
```

## KillerMud prompt overrides

Append the applicable rules below after the verbatim core prompt. These rules resolve conflicts between a generic atlas prompt and a functional KillerMud map layer:

- Treat room coordinates, connections, sectors, markers, manual elements and the existing target as the project's original geography. Preserve them; never invent a replacement layout. Interpret the core prompt's originality requirement as a prohibition on copying outside fictional worlds.
- Use `old-continent-overview.png` as the mandatory visual style reference. Match its parchment color and fibers, black-to-burgundy ink balance, line weight, irregularity, engraving density, stippling, terrain treatment and faded-print character. Copy no geography or labels from it.
- For a local room backdrop, preserve exactly the same illustration style as the atlas while overriding only its functional typography and frame requests: generate no text, letters, numerals, pseudo-writing, decorative border, compass rose, cartouche, scale bar or marginal annotations unless the user or a marker explicitly requests exact content.
- For an atlas or overview, allow the border and cartographic ornaments. Generate labels only from a closed list of exact names supplied by the user or project data; prohibit all additional pseudo-text.
- Apply coastlines, mountain chains, forests and rivers only where the export's sectors, rooms and geography support them. Do not add them merely to satisfy the generic examples in the core prompt.
- Treat the room network as an invisible spatial skeleton. Render a continuous, organic illustrated place with the same visual density and landmark treatment as the atlas; never expose the network as a blueprint, orthographic architectural plan, rectangular dungeon grid, tilemap or collection of isolated icons.
- Represent interiors and underground rooms as organically integrated roofless landmarks, cutaways or terrain vignettes in the exact atlas style. Keep their corridors and connections readable without turning them into technical floor plans or forcing outdoor terrain into them.
- Default to a bright, readable daytime interpretation. If `generationPrompt` explicitly requires night, dusk or dawn, express it through ink density, parchment tone and restrained ornament rather than photorealistic scene lighting.
- Preserve the target aspect ratio and place all important geography inside safe crop margins. Never stretch the result.
