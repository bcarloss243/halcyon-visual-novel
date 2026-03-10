using UnityEngine;
using UnityEngine.Events;
using System;

namespace HalcyonAcademy
{
    /// <summary>
    /// Core pressure simulation. Owns the float, runs the math, fires events.
    /// No visuals — this is pure data. Attach to a persistent GameObject.
    /// </summary>
    public class PressureSystem : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────
        public static PressureSystem Instance { get; private set; }

        // ── Events ─────────────────────────────────────────────────────
        [Header("Events")]
        public UnityEvent<float> OnPressureChanged;
        public UnityEvent<PressureZone> OnZoneChanged;
        public UnityEvent OnPanicAttack;
        public UnityEvent OnVapeurTaken;
        public UnityEvent OnVapeurWoreOff;
        public UnityEvent<float> OnStormHarvest;

        // ── Pressure State ─────────────────────────────────────────────
        [Header("Pressure")]
        [SerializeField, Range(0f, 100f)]
        private float _pressure = 55f;
        public float Pressure
        {
            get => _pressure;
            private set
            {
                float prev = _pressure;
                _pressure = Mathf.Clamp(value, 0f, 100f);

                // Vapeur floor: can't go below 20 while active
                if (_vapeurActive && _pressure < 20f)
                    _pressure = 20f;

                if (!Mathf.Approximately(prev, _pressure))
                    OnPressureChanged?.Invoke(_pressure);

                // Zone transition check
                PressureZone newZone = GetZone(_pressure);
                if (newZone != _currentZone)
                {
                    _currentZone = newZone;
                    OnZoneChanged?.Invoke(_currentZone);
                }

                // Panic attack at 95+
                if (_pressure >= 95f && prev < 95f)
                    OnPanicAttack?.Invoke();
            }
        }

        // ── Zone ───────────────────────────────────────────────────────
        private PressureZone _currentZone;
        public PressureZone CurrentZone => _currentZone;

        // ── Vapeur ─────────────────────────────────────────────────────
        [Header("Vapeur State")]
        [SerializeField] private bool _vapeurActive;
        [SerializeField] private float _vapeurDebt;
        [SerializeField] private int _consecutiveVapeurDays;

        public bool VapeurActive => _vapeurActive;
        public float VapeurDebt => _vapeurDebt;
        public int ConsecutiveVapeurDays => _consecutiveVapeurDays;

        // ── Day State ──────────────────────────────────────────────────
        [Header("Day")]
        [SerializeField] private int _day = 1;
        [SerializeField] private float _basePressure = 55f;
        [SerializeField] private float _weatherModifier;

        public int Day => _day;
        public float BasePressure => _basePressure;

        // ── Relationships / Microclimates ──────────────────────────────
        [Header("Microclimates")]
        [SerializeField] private float _lolaMicroclimate = 0f;
        [SerializeField] private float _alaricMicroclimate = 0.2f;
        private string _currentLocationPartner = null;

        // ── Noise ──────────────────────────────────────────────────────
        private float _microNoise;
        private float _noiseTimer;

        // ================================================================
        //  LIFECYCLE
        // ================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            _currentZone = GetZone(_pressure);
            OnPressureChanged?.Invoke(_pressure);
            OnZoneChanged?.Invoke(_currentZone);
        }

        void Update()
        {
            // Micro-noise: the needle is never perfectly still
            _noiseTimer += Time.deltaTime;
            if (_noiseTimer >= 0.15f)
            {
                _noiseTimer = 0f;
                float intensity = Mathf.Lerp(0.2f, 1.5f, _pressure / 100f);
                _microNoise = UnityEngine.Random.Range(-intensity, intensity);
                OnPressureChanged?.Invoke(_pressure + _microNoise);
            }
        }

        // ================================================================
        //  PUBLIC API
        // ================================================================

        public void AdjustPressure(float delta, string source = "")
        {
            float adjusted = delta;

            // Microclimate: soften increases, amplify decreases
            if (_currentLocationPartner != null)
            {
                float mc = GetMicroclimate(_currentLocationPartner);
                if (adjusted > 0)
                    adjusted *= (1f - mc * 0.3f);
                else
                    adjusted *= (1f + mc * 0.2f);
            }

            // Vapeur debt tracking
            if (_vapeurActive && adjusted > 0)
                _vapeurDebt += adjusted;

            Pressure += adjusted;
        }

        public void TakeVapeur()
        {
            if (_vapeurActive) return;

            _vapeurDebt = Mathf.Max(0, _pressure - 25f);
            _vapeurActive = true;
            _consecutiveVapeurDays++;
            Pressure = 25f;
            OnVapeurTaken?.Invoke();
        }

        public void ResolveVapeur()
        {
            if (!_vapeurActive) return;

            float reboundMultiplier = 0.6f + (_consecutiveVapeurDays - 1) * 0.15f;
            reboundMultiplier = Mathf.Min(reboundMultiplier, 1.2f);

            float rebound = _vapeurDebt * reboundMultiplier;
            _vapeurActive = false;
            _vapeurDebt = 0f;
            Pressure += rebound;
            OnVapeurWoreOff?.Invoke();
        }

        public void StartNewDay(float weatherModifier = 0f, float relationshipModifier = 0f)
        {
            _day++;

            if (_vapeurActive)
                ResolveVapeur();
            else
                _consecutiveVapeurDays = 0;

            float vapeurMod = 0f;
            if (_consecutiveVapeurDays == 1) vapeurMod = 3f;
            else if (_consecutiveVapeurDays == 2) vapeurMod = 7f;
            else if (_consecutiveVapeurDays == 3) vapeurMod = 12f;
            else if (_consecutiveVapeurDays >= 4) vapeurMod = Mathf.Min(20f, 12f + (_consecutiveVapeurDays - 3) * 4f);

            float randomVariance = UnityEngine.Random.Range(-8f, 8f);

            _weatherModifier = weatherModifier;

            float morning = _basePressure
                          + relationshipModifier
                          + vapeurMod
                          + weatherModifier
                          + randomVariance;

            Pressure = morning;
        }

        public void TriggerStormHarvest()
        {
            float spike = UnityEngine.Random.Range(15f, 25f);
            Pressure += spike;
            OnStormHarvest?.Invoke(spike);
        }

        public void SetLocationPartner(string partnerName)
        {
            _currentLocationPartner = partnerName;
        }

        public void RecordShelteringMoment(string partner, float amount = 0.05f)
        {
            if (_pressure > 75f)
                amount *= 1.3f;

            if (partner == "Lola")
                _lolaMicroclimate = Mathf.Clamp01(_lolaMicroclimate + amount * 1.5f);
            else if (partner == "Alaric")
                _alaricMicroclimate = Mathf.Clamp(_alaricMicroclimate + amount, -0.5f, 1f);
        }

        public void RecordConflict(string partner, float amount = 0.1f)
        {
            if (partner == "Alaric")
                _alaricMicroclimate = Mathf.Clamp(_alaricMicroclimate - amount, -0.5f, 1f);
        }

        public void ShiftBasePressure(float delta)
        {
            _basePressure = Mathf.Clamp(_basePressure + delta, 40f, 65f);
        }

        // ── "Bad Days Are More Valuable" ───────────────────────────────
        public bool IsHighPressureDay => _pressure > 75f;
        public bool IsLowPressureDay => _pressure < 25f;

        public float RootworkMultiplier => IsHighPressureDay ? 1.5f : (IsLowPressureDay ? 0.5f : 1f);
        public float AcademicMultiplier => IsLowPressureDay ? 1.3f : 1f;
        public float RelationshipMultiplier => IsHighPressureDay ? 1.3f : 1f;
        public bool PerceptiveInsightsAvailable => _pressure > 75f;

        // ================================================================
        //  INTERNALS
        // ================================================================

        private float GetMicroclimate(string partner)
        {
            if (partner == "Lola") return _lolaMicroclimate;
            if (partner == "Alaric") return _alaricMicroclimate;
            return 0f;
        }

        public static PressureZone GetZone(float pressure)
        {
            if (pressure < 25f) return PressureZone.Clarity;
            if (pressure < 50f) return PressureZone.Manageable;
            if (pressure < 75f) return PressureZone.Elevated;
            return PressureZone.Crisis;
        }
    }

    public enum PressureZone
    {
        Clarity,    // 0–25   Deep teal
        Manageable, // 25–50  Verdigris
        Elevated,   // 50–75  Burnished gold
        Crisis      // 75–100 Deep crimson
    }
}
