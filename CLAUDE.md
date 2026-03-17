# Halcyon Academy — Project Context for Claude Code

## What This Is

Halcyon Academy is a narrative RPG built in **Unity 2022.3 LTS (Universal 2D)**. You play as Molly, a student at a retrofuturistic academy inside a domed city (the Cloche) built over flooded New Orleans. The core mechanic is a **Pressure Gauge** that tracks Molly's emotional/physiological state — what the world calls anxiety is actually atmospheric weather sensitivity inherited from her grandmother. The game's central tension: the medication everyone takes (Vapeur) doesn't work on her, and the journey is discovering why.

This is a solo-dev capstone project. The developer (Bergen) is learning Unity while building. She needs clear explanations of *why* things work, not just code. When making changes, explain your reasoning.

## Critical Rules

1. **All scripts use the `HalcyonAcademy` C# namespace.** Every class, enum, and struct lives in `namespace HalcyonAcademy { }`. No exceptions.
2. **Match existing field names exactly.** The biggest source of past bugs has been Claude guessing field names instead of reading the actual code. Always `view` the file before writing code that references it.
3. **Implement first, style later.** Get things working with placeholder art/UI before polishing. Bergen iterates on visuals separately.
4. **Explain what you're doing.** Bergen is building her Unity knowledge. Don't silently make changes — say what you changed and why.
5. **Don't contradict the existing architecture.** If you're unsure how something is structured, read the file. Don't assume.

## Architecture Overview

### Core Systems (Singletons)

**PressureSystem** — The central game state manager.
- `PressureSystem.Instance.Pressure` — current pressure (float, 0–100)
- `PressureSystem.Instance.AdjustPressure(float delta)` — apply pressure change
- `PressureSystem.GetZone(float pressure)` → returns `PressureZone` enum
- Zone thresholds: 0–25 (Clarity), 25–50 (Manageable), 50–75 (Elevated), 75–100 (Crisis)
- Panic attack triggers at 95+ pressure
- Key properties: `IsHighPressureDay` (>65), `IsLowPressureDay` (<30), `RootworkMultiplier`, `AcademicMultiplier`, `RelationshipMultiplier`, `PerceptiveInsightsAvailable`
- Microclimate system: Lola reduces pressure, Alaric increases it

**DayCycleManager** — Manages day/night cycle and time slot progression.
- Time slots: Morning, Afternoon, Evening, Night (uses `TimeOfDay` enum)
- `DayCycleManager.Instance.BeginGame()` — starts the loop
- Events: `OnDayBegan`, `OnSlotBegan`, `OnDayEnded`, `OnPanicAttack`
- Morning pressure calculation: previous pressure + overnight recovery + weather modifier
- `MorningPressureReport` struct for UI display

### Data Model

**ActivityDef** (ScriptableObject, `Create > Halcyon > Activity Definition`)
- `activityName` (string) — NOT `activityId`
- `minPressureDelta` / `maxPressureDelta` (float) — NOT `pressureMin`/`pressureMax`
- `narrativeDefault`, `narrativeHighPressure`, `narrativeLowPressure` (string, TextArea)
- `perceptiveInsight` (string, TextArea) — only shown at pressure > 65
- `GetNarrativeText(float pressure, PressureZone zone)` — returns context-appropriate text
- `locationPartner` (string) — "Lola", "Alaric", etc. for microclimate calculation
- `isRootwork`, `isAcademic`, `isGreenwork` (bool)
- `availableSlots` (TimeOfDay[]) — when this activity can be chosen
- `isFreeAction` (bool) — doesn't consume a time slot
- `icon` (Sprite)

**ActivityResult** (struct) — returned after performing an activity
- `success`, `activityName`, `pressureDelta`, `narrativeText`, `perceptiveInsight`, `bonusMultiplier`

**ZoneConfig** (ScriptableObject, `Create > Halcyon > Zone Config`)
- Array of `ZoneData`: zone enum, displayName, color, gaugeArcColor, forecasts[]
- Purple-forward palette: cool blue → soft lavender → dusty rose → deep crimson-pink

### Enums

```csharp
public enum PressureZone { Clarity, Manageable, Elevated, Crisis }
public enum TimeOfDay { Morning, Afternoon, Evening, Night }
```

### UI Scripts

**PressureGaugeUI** — Renders the Art Deco gauge. Needle pivot, zone arcs, glow overlays (white tint), forecast text. Arc sweeps clockwise: Clarity at bottom-left (7 o'clock), Crisis at bottom-right (5 o'clock).

**DailyScheduleUI** — Shows activity cards for the current time slot. References `activityDatabase` (ActivityDef[]) and `vapeurActivity` (separate ActivityDef).

**ActivityButtonUI** — Individual activity card with hover effects, pressure forecast, high-pressure bonus indicator.

### Seven Core Activities
1. Ironwork Class
2. Greenhouse
3. Visit Lola
4. Rest
5. Rootwork Practice
6. Dinner with Alaric
7. Take Vapeur (handled separately as a free action)

## Design Principles

- **"Bad days are more valuable"** — High pressure (>65) boosts Rootwork and relationship gains via multipliers. The game rewards engaging with difficulty, not avoiding it.
- **Vapeur is a trap** — It temporarily lowers pressure but causes rebound spikes. The player is meant to gradually discover this.
- **The gauge IS the revelation** — What everyone thinks is an anxiety meter is actually measuring atmospheric pressure. The mechanic is the plot twist.

## Known Issues / Deferred Work
- Morning report auto-show has a timing bug (deferred)
- SchedulePanel overlaps the gauge visually (layout fix needed)
- Ink integration not yet started (planned for activity scene buildout)

## Project Structure

```
Assets/
├── Scripts/
│   ├── Systems/        (PressureSystem, DayCycleManager, ActivityDefExtensions)
│   ├── UI/             (PressureGaugeUI, DailyScheduleUI, ActivityButtonUI)
│   └── Debug/          (GaugeDebugController)
├── Data/
│   ├── Activities/     (7 ActivityDef ScriptableObjects)
│   └── Config/         (ZoneConfig asset)
├── Prefabs/
│   └── ActivityButton  (prefab with ActivityButtonUI)
├── Art/
│   └── Gauge/          (gauge face, needle, center cap, zone segment sprites)
└── Scenes/
```

## Git

- Repo: `github.com/bcarloss243/halcyon-visual-novel` (private)
- Branch: `main`
- Always use meaningful commit messages that describe what changed and why

## Tech Stack Context

- **Dialogue scripting:** Ink by Inkle (not yet integrated, planned)
- **Art pipeline:** Midjourney with `--sref` for backgrounds, Scenario.gg for character sprites, Photoshop for compositing
- **Visual benchmark:** Jen Zee's Hades art; structural reference: Citizen Sleeper
- **Art direction:** Painterly retro-futurist comic illustration — Studio Ghibli softness meets French bande dessinée linework, atomic-age pastel grittiness
