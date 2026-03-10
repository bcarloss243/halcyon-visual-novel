using System;
using System.Collections;
using UnityEngine;

namespace HalcyonAcademy
{
    /// <summary>
    /// Manages the day/night cycle, time slot progression, and morning pressure calculation.
    /// Singleton — attach to a persistent GameObject in your scene.
    /// 
    /// Wiring:
    ///   - Reads/writes PressureSystem.Instance for pressure values
    ///   - Reads ActivitySystem for panic-attack threshold
    ///   - Fires events that DailyScheduleUI listens to
    /// </summary>
    public class DayCycleManager : MonoBehaviour
    {
        public static DayCycleManager Instance { get; private set; }

        // ── Serialized Config ──────────────────────────────────────────
        [Header("Day Structure")]
        [SerializeField] private int slotsPerDay = 4;

        [Header("Morning Pressure Calculation")]
        [Tooltip("Base overnight recovery (subtracted from pressure)")]
        [SerializeField] private float overnightRecoveryBase = 12f;
        [SerializeField] private float overnightRecoveryVariance = 4f;

        [Tooltip("Weather modifier range applied each morning")]
        [SerializeField] private float weatherModifierMin = -5f;
        [SerializeField] private float weatherModifierMax = 10f;

        [Tooltip("Pressure threshold that triggers a panic attack and ends the day")]
        [SerializeField] private float panicThreshold = 95f;

        [Header("Transition Timing")]
        [SerializeField] private float slotTransitionDelay = 0.6f;
        [SerializeField] private float dayTransitionDelay = 1.5f;

        // ── Runtime State ──────────────────────────────────────────────
        public int CurrentDay { get; private set; } = 1;
        public int CurrentSlotIndex { get; private set; } = 0;
        public TimeOfDay CurrentTimeOfDay => _slotOrder[Mathf.Clamp(CurrentSlotIndex, 0, _slotOrder.Length - 1)];
        public int SlotsPerDay => slotsPerDay;
        public bool DayInProgress { get; private set; }
        public float TodayWeatherModifier { get; private set; }

        private readonly TimeOfDay[] _slotOrder = {
            TimeOfDay.Morning,
            TimeOfDay.Afternoon,
            TimeOfDay.Evening,
            TimeOfDay.Night
        };

        // ── Events ─────────────────────────────────────────────────────
        /// <summary>Fired at the start of each new day, after morning pressure is calculated.</summary>
        public event Action<int> OnDayStarted;                    // dayNumber

        /// <summary>Fired when a new time slot becomes active.</summary>
        public event Action<int, TimeOfDay> OnSlotBegan;          // slotIndex, timeOfDay

        /// <summary>Fired after the player confirms an activity and its effects resolve.</summary>
        public event Action<int, TimeOfDay> OnSlotCompleted;      // slotIndex, timeOfDay

        /// <summary>Fired when the day ends (either naturally or via panic attack).</summary>
        public event Action<int, bool> OnDayEnded;                // dayNumber, wasPanicAttack

        /// <summary>Fired when a panic attack triggers, before the day-end event.</summary>
        public event Action OnPanicAttack;

        /// <summary>Fired with the morning pressure calculation breakdown for UI display.</summary>
        public event Action<MorningPressureReport> OnMorningPressureCalculated;

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Call this to kick off day 1 (or call StartNewDay directly).</summary>
        public void BeginGame()
        {
            CurrentDay = 0; // StartNewDay increments
            StartNewDay();
        }

        // ── Day Flow ───────────────────────────────────────────────────

        public void StartNewDay()
        {
            CurrentDay++;
            CurrentSlotIndex = 0;
            DayInProgress = true;

            // Morning pressure calculation
            float previousPressure = PressureSystem.Instance.CurrentPressure;
            float recovery = overnightRecoveryBase + UnityEngine.Random.Range(-overnightRecoveryVariance, overnightRecoveryVariance);
            TodayWeatherModifier = UnityEngine.Random.Range(weatherModifierMin, weatherModifierMax);
            float morningDelta = -recovery + TodayWeatherModifier;

            PressureSystem.Instance.ApplyDelta(morningDelta, "morning_calculation");

            var report = new MorningPressureReport
            {
                PreviousPressure = previousPressure,
                OvernightRecovery = recovery,
                WeatherModifier = TodayWeatherModifier,
                FinalPressure = PressureSystem.Instance.CurrentPressure
            };
            OnMorningPressureCalculated?.Invoke(report);
            OnDayStarted?.Invoke(CurrentDay);

            // Begin first slot
            OnSlotBegan?.Invoke(CurrentSlotIndex, CurrentTimeOfDay);
        }

        /// <summary>
        /// Called by the UI after an activity resolves. Advances to the next slot
        /// or ends the day. Checks for panic attack after pressure changes.
        /// </summary>
        public void CompleteCurrentSlot()
        {
            TimeOfDay completedTime = CurrentTimeOfDay;
            OnSlotCompleted?.Invoke(CurrentSlotIndex, completedTime);

            // Check panic attack
            if (PressureSystem.Instance.CurrentPressure >= panicThreshold)
            {
                StartCoroutine(HandlePanicAttack());
                return;
            }

            CurrentSlotIndex++;

            if (CurrentSlotIndex >= slotsPerDay)
            {
                StartCoroutine(HandleDayEnd(false));
            }
            else
            {
                StartCoroutine(TransitionToNextSlot());
            }
        }

        /// <summary>
        /// Allows external systems (e.g., Vapeur) to trigger a slot action
        /// without consuming a slot.
        /// </summary>
        public void ExecuteFreeAction(ActivityDef activity)
        {
            // Apply the activity's pressure effect without advancing the slot
            float delta = activity.RollPressureDelta();
            PressureSystem.Instance.ApplyDelta(delta, activity.activityId);

            // Still check for panic
            if (PressureSystem.Instance.CurrentPressure >= panicThreshold)
            {
                StartCoroutine(HandlePanicAttack());
            }
        }

        // ── Coroutines ────────────────────────────────────────────────

        private IEnumerator TransitionToNextSlot()
        {
            yield return new WaitForSeconds(slotTransitionDelay);
            OnSlotBegan?.Invoke(CurrentSlotIndex, CurrentTimeOfDay);
        }

        private IEnumerator HandlePanicAttack()
        {
            DayInProgress = false;
            OnPanicAttack?.Invoke();
            yield return new WaitForSeconds(dayTransitionDelay);
            OnDayEnded?.Invoke(CurrentDay, true);
        }

        private IEnumerator HandleDayEnd(bool wasPanic)
        {
            DayInProgress = false;
            yield return new WaitForSeconds(dayTransitionDelay);
            OnDayEnded?.Invoke(CurrentDay, wasPanic);
        }
    }

    // ── Supporting Types ───────────────────────────────────────────────

    /// <summary>
    /// Breakdown of the morning pressure calculation for UI display.
    /// </summary>
    [Serializable]
    public struct MorningPressureReport
    {
        public float PreviousPressure;
        public float OvernightRecovery;
        public float WeatherModifier;
        public float FinalPressure;

        public string WeatherDescription
        {
            get
            {
                if (WeatherModifier > 6f) return "Storm surge detected beyond the Cloche";
                if (WeatherModifier > 2f) return "Unsettled weather pressing on the dome";
                if (WeatherModifier > -2f) return "Calm skies over the Cloche";
                return "Clear and still — the dome hums quietly";
            }
        }
    }

    /// <summary>
    /// Time-of-day enum. If you already have this in ActivitySystem.cs,
    /// delete this one and use yours.
    /// </summary>
    public enum TimeOfDay
    {
        Morning,
        Afternoon,
        Evening,
        Night
    }
}
