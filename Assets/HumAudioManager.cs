using UnityEngine;

namespace HalcyonAcademy
{
    /// <summary>
    /// Manages the three-layer ambient hum audio system.
    /// Attach to the same GameObject as an AudioSource, or reference 3 child AudioSources.
    /// 
    /// Layer 1 — Low Drone:     always present, volume scales with pressure
    /// Layer 2 — Static:        fades in above pressure 40, dominant above 70
    /// Layer 3 — Signal/Music:  only audible below pressure 25, Molly's gift
    /// 
    /// Late Game: signalCeiling rises, letting the signal emerge at higher pressures.
    /// </summary>
    public class HumAudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _droneSource;    // Layer 1
        [SerializeField] private AudioSource _staticSource;   // Layer 2
        [SerializeField] private AudioSource _signalSource;   // Layer 3

        [Header("Drone (Layer 1)")]
        [SerializeField] private float _droneMinVolume = 0.1f;
        [SerializeField] private float _droneMaxVolume = 0.8f;
        [SerializeField] private float _dronePitchLow = 0.85f;  // at high pressure (heavier)
        [SerializeField] private float _dronePitchHigh = 1.0f;  // at low pressure

        [Header("Static (Layer 2)")]
        [SerializeField] private float _staticThreshold = 40f;   // fades in above this
        [SerializeField] private float _staticMaxVolume = 0.7f;

        [Header("Signal (Layer 3)")]
        [SerializeField] private float _signalCeiling = 25f;     // audible below this
        [SerializeField] private float _signalMaxVolume = 0.4f;

        [Header("Smoothing")]
        [SerializeField] private float _volumeSmoothSpeed = 3f;

        // ── Late-Game Progression ──────────────────────────────────────
        [Header("Late Game")]
        [Tooltip("As Molly's Rootwork develops, the signal emerges at higher pressures.")]
        [SerializeField] private float _signalCeilingMax = 80f;
        private float _rootworkProgression = 0f; // 0 = early game, 1 = climax

        // ── Targets ────────────────────────────────────────────────────
        private float _targetDroneVol;
        private float _targetDronePitch;
        private float _targetStaticVol;
        private float _targetSignalVol;

        // ── Storm Harvest ──────────────────────────────────────────────
        private float _harvestBoost = 0f;

        void OnEnable()
        {
            if (PressureSystem.Instance != null)
            {
                PressureSystem.Instance.OnPressureChanged.AddListener(OnPressureChanged);
                PressureSystem.Instance.OnStormHarvest.AddListener(OnStormHarvest);
            }
        }

        void OnDisable()
        {
            if (PressureSystem.Instance != null)
            {
                PressureSystem.Instance.OnPressureChanged.RemoveListener(OnPressureChanged);
                PressureSystem.Instance.OnStormHarvest.RemoveListener(OnStormHarvest);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime * _volumeSmoothSpeed;

            // Smooth volume transitions
            if (_droneSource != null)
            {
                _droneSource.volume = Mathf.Lerp(_droneSource.volume, _targetDroneVol + _harvestBoost, dt);
                _droneSource.pitch = Mathf.Lerp(_droneSource.pitch, _targetDronePitch, dt);
            }

            if (_staticSource != null)
                _staticSource.volume = Mathf.Lerp(_staticSource.volume, _targetStaticVol + _harvestBoost, dt);

            if (_signalSource != null)
                _signalSource.volume = Mathf.Lerp(_signalSource.volume, _targetSignalVol, dt);

            // Decay storm harvest boost
            if (_harvestBoost > 0)
                _harvestBoost = Mathf.MoveTowards(_harvestBoost, 0f, Time.deltaTime * 0.5f);
        }

        // ================================================================
        //  PRESSURE RESPONSE
        // ================================================================

        private void OnPressureChanged(float pressure)
        {
            float t = pressure / 100f; // 0–1

            // Layer 1 — Drone
            _targetDroneVol = Mathf.Lerp(_droneMinVolume, _droneMaxVolume, t);
            _targetDronePitch = Mathf.Lerp(_dronePitchHigh, _dronePitchLow, t);

            // Layer 2 — Static
            if (pressure > _staticThreshold)
            {
                float staticT = Mathf.InverseLerp(_staticThreshold, 100f, pressure);
                _targetStaticVol = staticT * _staticMaxVolume;
            }
            else
            {
                _targetStaticVol = 0f;
            }

            // Layer 3 — Signal
            float effectiveCeiling = Mathf.Lerp(_signalCeiling, _signalCeilingMax, _rootworkProgression);
            if (pressure < effectiveCeiling)
            {
                float signalT = Mathf.InverseLerp(effectiveCeiling, 0f, pressure);
                _targetSignalVol = signalT * _signalMaxVolume;
            }
            else
            {
                _targetSignalVol = 0f;
            }
        }

        private void OnStormHarvest(float spike)
        {
            // All layers spike momentarily
            _harvestBoost = 0.3f;
        }

        // ================================================================
        //  PUBLIC API
        // ================================================================

        /// <summary>
        /// Advance Molly's Rootwork progression (0–1).
        /// At 1.0, the signal is audible even at high pressure values.
        /// </summary>
        public void SetRootworkProgression(float progress)
        {
            _rootworkProgression = Mathf.Clamp01(progress);
        }
    }
}
