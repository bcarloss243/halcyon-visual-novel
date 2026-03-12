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
    ///   - Fires events that DailyScheduleUI listens to
    ///   - Uses TimeOfDay enum from ActivitySystem.cs (not duplicated here)
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
        public event Action<int> OnDayStarted;
        public event Action<int, TimeOfDay> OnSlotBegan;
        public event Action<int, TimeOfDay> OnSlotCompleted;
        public event Action<int, bool> OnDayEnded;
        public event Action OnPanicAttack;
        public event Action<MorningPressureReport> OnMorningPressureCalculated;

        // ── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void BeginGame()
        {
            CurrentDay = 0;
            StartNewDay();
        }

        // ── Day Flow ───────────────────────────────────────────────────

        public void StartNewDay()
        {
            CurrentDay++;
            CurrentSlotIndex = 0;
            DayInProgress = true;

            float previousPressure = PressureSystem.Instance.Pressure;
            float recovery = overnightRecoveryBase + UnityEngine.Random.Range(-overnightRecoveryVariance, overnightRecoveryVariance);
            TodayWeatherModifier = UnityEngine.Random.Range(weatherModifierMin, weatherModifierMax);
            float morningDelta = -recovery + TodayWeatherModifier;

            PressureSystem.Instance.AdjustPressure(morningDelta, "morning_calculation");

            var report = new MorningPressureReport
            {
                PreviousPressure = previousPressure,
                OvernightRecovery = recovery,
                WeatherModifier = TodayWeatherModifier,
                FinalPressure = PressureSystem.Instance.Pressure
            };
            OnMorningPressureCalculated?.Invoke(report);
            OnDayStarted?.Invoke(CurrentDay);
            OnSlotBegan?.Invoke(CurrentSlotIndex, CurrentTimeOfDay);
        }

        public void CompleteCurrentSlot()
        {
            TimeOfDay completedTime = CurrentTimeOfDay;
            OnSlotCompleted?.Invoke(CurrentSlotIndex, completedTime);

            if (PressureSystem.Instance.Pressure >= panicThreshold)
            {
                StartCoroutine(HandlePanicAttack());
                return;
            }

            CurrentSlotIndex++;

            if (CurrentSlotIndex >= slotsPerDay)
                StartCoroutine(HandleDayEnd(false));
            else
                StartCoroutine(TransitionToNextSlot());
        }

        public void ExecuteFreeAction(ActivityDef activity)
        {
            float delta = UnityEngine.Random.Range(activity.minPressureDelta, activity.maxPressureDelta);
            PressureSystem.Instance.AdjustPressure(delta, activity.activityName);

            if (PressureSystem.Instance.Pressure >= panicThreshold)
                StartCoroutine(HandlePanicAttack());
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

    // NOTE: TimeOfDay enum lives in ActivitySystem.cs — not duplicated here.
}