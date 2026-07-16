# Presentation language and predicates

Write all authored, human-readable lore in Polish. This includes `name`, `aliases`, `summary`, `description`, `tags`, string claim `value`, time `label`, constraint `rule` and `reason`, style-profile prose arrays, and article titles and content. Preserve canonical proper names and exact source excerpts; do not translate technical IDs, enum values, file paths, or source field names.

Treat `id`, `recordType`, `entityType`, `domains`, `predicate`, relation `direction`, statuses, article `category`, article section `type`, and similar schema values as machine-facing. Never copy them into presentation prose or use a humanized technical token as a label.

Use only the predicates accepted by `scripts/validate_lore.py`. Every accepted predicate must have an explicit Polish label in `LoreCatalogLoader.PredicateLabel` or Polish forward and inverse labels in `LoreCatalogLoader.RelationLabel`. Do not rely on replacing underscores with spaces. When a genuinely new predicate is necessary:

1. Add precise Polish display labels to `LoreCatalogLoader`.
2. Add the predicate to the corresponding validator allowlist.
3. Add or update a Killeropedia test proving the technical token is not displayed.
4. Run lore validation and the application tests before accepting the records.

Prefer an existing predicate when its meaning matches exactly. Do not reuse a near match merely to bypass validation.

Use only article categories accepted by `scripts/validate_lore.py`. Each accepted category must have an explicit Polish heading in the Markdown exporter and a Polish category assignment in Killeropedia. Add those presentation mappings before extending the allowlist.
