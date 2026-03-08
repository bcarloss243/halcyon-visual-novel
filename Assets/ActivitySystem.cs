using UnityEngine;
using System;

namespace HalcyonAcademy
{
    /// <summary>
    /// Manages the daily activity loop: morning → afternoon → evening.
    /// Each activity has a base pressure adjustment modified by context.
    /// Fires events for the narrative system and UI to respond.
    /// </summary>
    public class ActivitySystem : MonoBehaviour
    {
        public static ActivitySystem Instance { get; private set; }

        // ── Events ─────────────────────────────────────────────────────
        public event Action<ActivityResult> OnActivityCompleted;
        public event Action<int> OnDayStarted;              // day number
        public event Action<TimeOfDay> OnTimeChanged;
        public event Action OnDayEnded;
        public event Action OnPanicEndedDayEarly;

        // ── State ──────────────────────────────────────────────────────
        [SerializeField] private int _activitySlotsPerDay = 4;
        [SerializeField] private int _slotsUsedToday = 0;

        private TimeOfDay _currentTime = TimeOfDay.Morning;
        public TimeOfDay CurrentTime => _currentTime;
        public int SlotsRemaining => _activitySlotsPerDay - _slotsUsedToday;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            if (PressureSystem.Instance != null)
                PressureSystem.Instance.OnPanicAttack.AddListener(HandlePanicAttack);
        }

        void OnDisable()
        {
            if (PressureSystem.Instance != null)
                PressureSystem.Instance.OnPanicAttack.RemoveListener(HandlePanicAttack);
        }

        // ================================================================
        //  PUBLIC API
        // ================================================================

        /// <summary>
        /// Execute an activity. Returns the result including actual pressure delta.
        /// </summary>
        public ActivityResult DoActivity(ActivityDef activity)
        {
            if (_slotsUsedToday >= _activitySlotsPerDay)
                return new ActivityResult { success = false, narrativeText = "The day is over. There's nothing left." };

            var ps = PressureSystem.Instance;
            if (ps == null)
                return new ActivityResult { success = false };

            // Roll the actual delta within the activity's range
            float baseDelta = UnityEngine.Random.Range(activity.minPressureDelta, activity.maxPressureDelta);

            // Context modifiers
            float contextMod = 0f;

            // "Bad Days Are More Valuable" bonuses
            float bonusMultiplier = 1f;
            if (activity.isRootwork)
                bonusMultiplier = ps.RootworkMultiplier;
            if (activity.isAcademic)
                bonusMultiplier = ps.AcademicMultiplier;

            // Apply relationship partner microclimate
            if (!string.IsNullOrEmpty(activity.locationPartner))
                ps.SetLocationPartner(activity.locationPartner);

            // Calculate final delta
            float finalDelta = baseDelta * bonusMultiplier + contextMod;
            ps.AdjustPressure(finalDelta, activity.activityName);

            // Record sheltering moment if it's a relationship activity on a hard day
            if (!string.IsNullOrEmpty(activity.locationPartner) && ps.IsHighPressureDay)
                ps.RecordShelteringMoment(activity.locationPartner);

            // Clear location partner after activity
            ps.SetLocationPartner(null);

            _slotsUsedToday++;
            AdvanceTime();

            // Build result
            var result = new ActivityResult
            {
                success = true,
                activityName = activity.activityName,
                pressureDelta = finalDelta,
                narrativeText = activity.GetNarrativeText(ps.Pressure, ps.CurrentZone),
                perceptiveInsight = ps.PerceptiveInsightsAvailable ? activity.perceptiveInsight : null,
                bonusMultiplier = bonusMultiplier
            };

            OnActivityCompleted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Start a new day. Resets slots, calculates morning pressure.
        /// </summary>
        public void StartNewDay(float weatherModifier = 0f, float relationshipModifier = 0f)
        {
            _slotsUsedToday = 0;
            _currentTime = TimeOfDay.Morning;
            PressureSystem.Instance?.StartNewDay(weatherModifier, relationshipModifier);
            OnDayStarted?.Invoke(PressureSystem.Instance?.Day ?? 1);
            OnTimeChanged?.Invoke(_currentTime);
        }

        /// <summary>
        /// Take Vapeur as a distinct action (costs no activity slot).
        /// </summary>
        public void TakeVapeur()
        {
            PressureSystem.Instance?.TakeVapeur();
        }

        // ================================================================
        //  INTERNALS
        // ================================================================

        private void AdvanceTime()
        {
            if (_slotsUsedToday == 1)
                _currentTime = TimeOfDay.Afternoon;
            else if (_slotsUsedToday == 2)
                _currentTime = TimeOfDay.Afternoon; // still afternoon
            else if (_slotsUsedToday == 3)
                _currentTime = TimeOfDay.Evening;
            else if (_slotsUsedToday >= _activitySlotsPerDay)
            {
                _currentTime = TimeOfDay.Night;
                OnDayEnded?.Invoke();
            }

            OnTimeChanged?.Invoke(_currentTime);
        }

        private void HandlePanicAttack()
        {
            // Day ends early — remaining slots are lost
            _slotsUsedToday = _activitySlotsPerDay;
            _currentTime = TimeOfDay.Night;

            // +5 base pressure penalty for next morning
            PressureSystem.Instance?.ShiftBasePressure(5f);

            OnPanicEndedDayEarly?.Invoke();
            OnDayEnded?.Invoke();
        }
    }

    // ================================================================
    //  DATA TYPES
    // ================================================================

    public enum TimeOfDay
    {
        Morning,
        Afternoon,
        Evening,
        Night
    }

    /// <summary>
    /// Definition of a single activity. Create as ScriptableObject or inline data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewActivity", menuName = "Halcyon/Activity Definition")]
    public class ActivityDef : ScriptableObject
    {
        public string activityName;

        [Header("Pressure Impact")]
        public float minPressureDelta;
        public float maxPressureDelta;

        [Header("Context")]
        public string locationPartner;  // who's present (for microclimates)
        public bool isRootwork;
        public bool isAcademic;
        public bool isGreenwork;

        [Header("Narrative")]
        [TextArea(2, 4)] public string narrativeDefault;
        [TextArea(2, 4)] public string narrativeHighPressure;
        [TextArea(2, 4)] public string narrativeLowPressure;

        [Header("Perceptive Insight (only at pressure > 65)")]
        [TextArea(2, 4)] public string perceptiveInsight;

        public string GetNarrativeText(float pressure, PressureZone zone)
        {
            if (pressure > 65f && !string.IsNullOrEmpty(narrativeHighPressure))
                return narrativeHighPressure;
            if (pressure < 30f && !string.IsNullOrEmpty(narrativeLowPressure))
                return narrativeLowPressure;
            return narrativeDefault;
        }
    }

    /// <summary>
    /// Result of performing an activity — passed to UI and narrative systems.
    /// </summary>
    public struct ActivityResult
    {
        public bool success;
        public string activityName;
        public float pressureDelta;
        public string narrativeText;
        public string perceptiveInsight;
        public float bonusMultiplier;
    }
}
