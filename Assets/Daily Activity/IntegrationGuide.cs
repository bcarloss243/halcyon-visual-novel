using UnityEngine;

namespace HalcyonAcademy
{
    /// <summary>
    /// ═══════════════════════════════════════════════════════════════════
    ///  HALCYON ACADEMY — DAILY ACTIVITY SELECTOR: INTEGRATION GUIDE
    /// ═══════════════════════════════════════════════════════════════════
    ///
    /// This file documents how to wire the new scripts into your existing
    /// PressureSystem + ActivitySystem. It's a MonoBehaviour you can drop
    /// into your scene as a reference, or just read the comments.
    ///
    /// ─── FILE MANIFEST ────────────────────────────────────────────────
    ///
    ///  Scripts/Systems/DayCycleManager.cs
    ///    Singleton. Manages day progression, slot advancement, morning
    ///    pressure calculation, panic attack detection. Fires events that
    ///    the UI subscribes to.
    ///
    ///  Scripts/Systems/ActivityDefExtensions.cs
    ///    Extension methods for your existing ActivityDef ScriptableObject.
    ///    Adds: RollPressureDelta(), BenefitsFromHighPressure(),
    ///    IsAvailableAt(), GetPressureForecastText(), GetPressureColor().
    ///
    ///  Scripts/UI/DailyScheduleUI.cs
    ///    The main schedule panel. Subscribes to DayCycleManager events.
    ///    Spawns ActivityButtonUI instances from a prefab.
    ///    Also contains SlotIndicator and SlotState.
    ///
    ///  Scripts/UI/ActivityButtonUI.cs
    ///    Prefab script for individual activity choice cards.
    ///    Hover effects, staggered entrance animation, high-pressure
    ///    bonus badge, pressure forecast display.
    ///
    ///  HalcyonDailySelector.jsx
    ///    Interactive React prototype showing the full visual design
    ///    and interaction flow. Use as a reference when building the
    ///    Unity UI in Canvas.
    ///
    /// ─── ASSUMPTIONS ABOUT YOUR EXISTING CODE ─────────────────────────
    ///
    ///  PressureSystem.Instance
    ///    • .CurrentPressure (float, 0–100)
    ///    • .ApplyDelta(float delta, string source)
    ///    If your API differs, update DayCycleManager.cs and
    ///    ActivityDefExtensions.cs accordingly.
    ///
    ///  ActivityDef (ScriptableObject)
    ///    Expected fields (add any you're missing):
    ///    • string activityId
    ///    • string displayName
    ///    • string narrativeSnippet
    ///    • float pressureMin, pressureMax
    ///    • TimeOfDay[] availableSlots
    ///    • bool costsFreeSlot
    ///    • Sprite icon (optional)
    ///
    ///  TimeOfDay enum
    ///    If you already have this in ActivitySystem.cs, delete the one
    ///    in DayCycleManager.cs.
    ///
    /// ─── SCENE SETUP ──────────────────────────────────────────────────
    ///
    ///  1. MANAGERS GAMEOBJECT
    ///     Create an empty GO called "Managers" (or use your existing one).
    ///     Add: DayCycleManager component.
    ///     This should persist across scenes (DontDestroyOnLoad if needed).
    ///
    ///  2. CANVAS HIERARCHY
    ///     Canvas (Screen Space - Overlay or Camera)
    ///     └── SchedulePanel (Image, full-screen, deep purple #1a0f2e)
    ///         ├── CanvasGroup (for fade transitions)
    ///         ├── Header
    ///         │   ├── DayLabel (TMP) — "Day 1"
    ///         │   ├── TimeOfDayLabel (TMP) — "Morning"
    ///         │   └── WeatherLabel (TMP) — "☁ Pressure building"
    ///         ├── SlotIndicatorRow (Horizontal Layout)
    ///         │   └── 4x SlotIndicator (Image pip + glow ring + label)
    ///         ├── ActivityButtonContainer (Vertical Layout Group)
    ///         │   └── (ActivityButtonUI prefabs spawned here at runtime)
    ///         ├── NarrativePanel
    ///         │   ├── NarrativeText (TMP)
    ///         │   └── PressureChangeText (TMP)
    ///         ├── VapeurButton (Button + TMP child)
    ///         ├── MorningReportPanel (initially inactive)
    ///         │   ├── MorningReportText (TMP)
    ///         │   └── DismissButton
    ///         └── PanicOverlay (initially inactive)
    ///             └── PanicText (TMP)
    ///
    ///  3. ACTIVITY BUTTON PREFAB
    ///     Create a prefab with:
    ///     - Button + CanvasGroup + ActivityButtonUI script
    ///     - Child: NameLabel (TMP), PressureForecast (TMP),
    ///       FlavorText (TMP), HighPressureBonus (GO with TMP child),
    ///       BorderGlow (Image), PressureColorBar (Image)
    ///     Save as Assets/Prefabs/UI/ActivityButton.prefab
    ///
    ///  4. ACTIVITY DATABASE
    ///     Create ActivityDef ScriptableObjects for each activity:
    ///     Assets > Create > Halcyon > Activity Definition
    ///     Populate the DailyScheduleUI's activityDatabase list in Inspector.
    ///
    ///  5. WIRE REFERENCES
    ///     Select SchedulePanel, assign all serialized fields on DailyScheduleUI.
    ///     DailyScheduleUI auto-subscribes to DayCycleManager events in OnEnable.
    ///
    ///  6. START THE GAME
    ///     Somewhere in your game startup, call:
    ///       DayCycleManager.Instance.BeginGame();
    ///
    /// ─── KEY DESIGN DECISIONS ─────────────────────────────────────────
    ///
    ///  "Bad days are more valuable":
    ///    When pressure > 60, activities flagged as rootwork/relationship
    ///    get a 1.0x–1.4x multiplier on their pressure-reducing effect.
    ///    The UI shows a "✧ Insight bonus" badge on those cards.
    ///
    ///  Vapeur:
    ///    Free action (no slot cost). Once per day. Handled via
    ///    DayCycleManager.ExecuteFreeAction() which bypasses slot advance.
    ///
    ///  Panic Attack:
    ///    Triggers at 95+ pressure after any activity resolves.
    ///    Ends day immediately. Remaining slots marked as Lost.
    ///
    ///  Ward Locks:
    ///    The PopulateActivityButtons method in DailyScheduleUI.cs has
    ///    a TODO comment where you should add Greenwork lock checks.
    ///    This is where Alaric's block goes.
    ///
    /// ─── ART DECO STYLE GUIDE (from prototype) ───────────────────────
    ///
    ///  Colors:
    ///    Deep Purple BG:    #1a0f2e
    ///    Mid Purple:        #2d1b4e
    ///    Brass/Gold:        #c9a84c (primary accent)
    ///    Muted Gold Text:   #d4a843
    ///    Warm Cream Text:   #e8dcc8
    ///    Teal (positive):   #6bb88c
    ///    Red (negative):    #d4635a
    ///
    ///  Typography:
    ///    Headers:  Playfair Display (or similar Art Deco serif)
    ///    Body:     Cormorant Garamond (elegant serif)
    ///    Numbers:  JetBrains Mono (monospace for pressure values)
    ///
    ///  Decorative Elements:
    ///    - Corner accents (curved L-shapes with dot, like the prototype)
    ///    - Diamond-dot horizontal rules between sections
    ///    - Brass border glow on hover (0.2 → 0.8 alpha)
    ///    - Gradient color bars under activity names (matches pressure dir)
    ///    - Slot pips with inner glow on active slot
    ///
    /// ═══════════════════════════════════════════════════════════════════
    /// </summary>
    public class IntegrationGuide : MonoBehaviour
    {
        // This script exists only as documentation.
        // You can safely delete it from your project.

        [Header("Read the comments in this file for setup instructions.")]
        [SerializeField] private bool youGotThis = true;
    }
}
