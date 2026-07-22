# Standard object reservoir

Use only object records that actually exist under `#OBJECTS` in these active ISO-8859-2 area files:

| File | Declared interval | Intended content |
| --- | --- | --- |
| `rezerwua.are` | 3001–3399 | common armor, weapons, containers, drinks, tools, and miscellaneous objects |
| `rezerwuc.are` | 4361–4460 | clothing |
| `rezerwuf.are` | 2450–2499 | food and drink |
| `rezerwuj.are` | 9120–9199 | jewelry |
| `rezerwum.are` | 2350–2399 | furniture and related containers |

Treat intervals as lookup boundaries, not lists of valid objects. Parse or inspect the chosen `#Vnum` record and confirm its type, values, material, flags, wear location, level implications, weight, and description fit the intended use. Check comparable active resets to confirm how the object is normally loaded.

Reference a selected object without duplicating it:

```json
{
  "kind": "object",
  "sourceType": "standard-reservoir",
  "sourceFile": "rezerwuf.are",
  "vnum": 2450,
  "reason": "Żelazna racja wydawana patrolom przez kwatermistrza"
}
```

Do not use a reservoir item merely because its name is convenient. Reject it when its mechanics, power, flags, material, or description conflict with the area. Keep unique keys, named artifacts, quest evidence, faction insignia, and distinctive boss rewards local unless an exact reservoir record was explicitly designed for that role.
