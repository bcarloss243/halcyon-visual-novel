# Halcyon Academy — Decision Log

Brief record of meaningful design and technical decisions.
Format: **Date | Decision | Why | What we considered**

---

### 2025 — Phase 1: Core Loop Lock

**Pressure Gauge over traditional health/mana**
Chose a single Art Deco brass instrument (0–100) tracking Molly's emotional/physiological state instead of separate HP/MP/stress bars. Why: the gauge IS the game — it's not a resource to manage, it's Molly's lived experience made visible. Simpler for the player, richer narratively.

**"Bad days are more valuable" inversion**
High pressure boosts Rootwork and relationship gains instead of punishing the player. Why: rejects the "optimize for calm" pattern in most wellness games. Molly's worst days are when she grows the most. Mechanical expression of the game's thesis.

**Purple-forward color palette over brass/mint**
Overrode the "safer" Art Deco brass-and-mint scheme for a purple-dominant palette on the gauge. Why: felt right. Purple carries the mysticism and femininity of the world better than mint. Trusted creative instinct over convention.

**Individual zone segment sprites over programmatic fill**
Chose hand-crafted sprites per pressure zone instead of a simpler color-fill approach. Why: each zone deserves its own visual identity. More work upfront, better feel.

**Daily activity selector as core loop (not map-first)**
Built the day cycle → activity card → pressure change loop before building the Cloche map. Why: the loop IS the game. Map is navigation chrome. Get the feel right first, then give it a world to live in.

**Vapeur as opt-in activity, not automatic**
Vapeur card is visually separated at the bottom of the activity selector rather than integrated with other activities. Why: it's a loaded choice — the thing everyone else relies on that doesn't work for Molly. Separating it spatially reinforces the weight of choosing it.

**Perspective camera with real Z-depth for map (specced, not built)**
Chose perspective camera with sprites at real Z positions over fake parallax scrolling. Why: automatic parallax, natural scale shift, real depth-of-field via URP post-processing. More setup, but the Cloche deserves to feel like a place with depth.

**Ink for dialogue over custom system**
Using Inkle's Ink scripting language. Why: battle-tested, good branching/conditional support, clean integration with Unity, lets narrative work happen in .ink files separate from C# systems code.

**Lola is not Glinda**
Deliberately engineered the Molly/Lola dynamic to avoid popular-girl-rescues-outcast. Both have genuine conflicts and incompatible needs. Why: mutual presence, not rescue. The relationship is built on showing up, not fixing each other.

**Syncretic magic system (not lineage-locked)**
Traiteur, rootwork, vodou, and folk healing traditions blend across racial and cultural lines in the Cloche, reflecting historical reality. Why: New Orleans magical traditions are syncretic by nature. Isolating them by lineage would be historically inaccurate and narratively limiting.

---

### Template for new entries

Copy and fill in:

**[Short name for the decision]**
Chose [X] over [Y]. Why: [reasoning]. What we considered: [alternatives, if worth noting].
