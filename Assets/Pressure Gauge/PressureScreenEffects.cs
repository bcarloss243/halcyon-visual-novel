using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HalcyonAcademy
{
    /// <summary>
    /// Drives URP post-processing effects based on pressure.
    /// Requires a Volume component on the same GameObject (or a global Volume in the scene).
    /// 
    /// Pressure → Visual mapping:
    ///   0–20  (Clarity):   Cool tones, sharpened, dust mote particles
    ///   20–45 (Manageable): Neutral baseline
    ///   45–70 (Elevated):  Warm/yellow shift, subtle vignette
    ///   70–90 (Crisis):    Desaturation, noticeable vignette, camera sway
    ///   90+   (Near Panic): Heavy vignette, chromatic aberration, pulse
    /// </summary>
    public class PressureScreenEffects : MonoBehaviour
    {
        [Header("Volume Reference")]
        [SerializeField] private Volume _postProcessVolume;

        [Header("Optional: Camera for Sway")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Camera Sway (Crisis)")]
        [SerializeField] private float _maxSwayAngle = 1.5f;
        [SerializeField] private float _swaySpeed = 0.8f;

        [Header("Pulse (Near Panic)")]
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseIntensity = 0.08f;

        // Post-processing overrides
        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;
        private ColorAdjustments _colorAdjustments;
        private WhiteBalance _whiteBalance;

        // State
        private float _currentPressure;
        private Quaternion _baseCameraRotation;
        private float _swayTimer;

        void Start()
        {
            if (_postProcessVolume == null)
                _postProcessVolume = GetComponent<Volume>();

            if (_postProcessVolume != null && _postProcessVolume.profile != null)
            {
                _postProcessVolume.profile.TryGet(out _vignette);
                _postProcessVolume.profile.TryGet(out _chromaticAberration);
                _postProcessVolume.profile.TryGet(out _colorAdjustments);
                _postProcessVolume.profile.TryGet(out _whiteBalance);
            }

            if (_cameraTransform != null)
                _baseCameraRotation = _cameraTransform.localRotation;
        }

        void OnEnable()
        {
            if (PressureSystem.Instance != null)
                PressureSystem.Instance.OnPressureChanged.AddListener(OnPressureChanged);
        }

        void OnDisable()
        {
            if (PressureSystem.Instance != null)
                PressureSystem.Instance.OnPressureChanged.RemoveListener(OnPressureChanged);
        }

        void Update()
        {
            // Camera sway in crisis
            if (_cameraTransform != null && _currentPressure > 70f)
            {
                _swayTimer += Time.deltaTime * _swaySpeed;
                float swayAmount = Mathf.InverseLerp(70f, 100f, _currentPressure) * _maxSwayAngle;
                float swayX = Mathf.Sin(_swayTimer * 1.3f) * swayAmount;
                float swayZ = Mathf.Cos(_swayTimer * 0.9f) * swayAmount * 0.5f;
                _cameraTransform.localRotation = _baseCameraRotation * Quaternion.Euler(swayX, 0, swayZ);
            }

            // Near-panic pulse effect on vignette
            if (_vignette != null && _currentPressure > 90f)
            {
                float pulse = Mathf.Sin(Time.time * _pulseSpeed) * _pulseIntensity;
                _vignette.intensity.value = GetVignetteIntensity(_currentPressure) + pulse;
            }
        }

        private void OnPressureChanged(float pressure)
        {
            _currentPressure = pressure;

            // ── Vignette ───────────────────────────────────────────
            if (_vignette != null)
            {
                _vignette.intensity.Override(GetVignetteIntensity(pressure));

                // Tint shifts: warm at high pressure, cool at low
                if (pressure < 20f)
                    _vignette.color.Override(new Color(0.1f, 0.2f, 0.3f)); // cool teal tint
                else if (pressure > 70f)
                    _vignette.color.Override(new Color(0.15f, 0.05f, 0.05f)); // dark red
                else
                    _vignette.color.Override(Color.black);
            }

            // ── Chromatic Aberration (90+) ─────────────────────────
            if (_chromaticAberration != null)
            {
                float ca = pressure > 88f ? Mathf.InverseLerp(88f, 100f, pressure) * 0.6f : 0f;
                _chromaticAberration.intensity.Override(ca);
            }

            // ── Color Adjustments ──────────────────────────────────
            if (_colorAdjustments != null)
            {
                // Saturation: normal at mid, slightly boosted at clarity, desaturated at crisis
                float sat;
                if (pressure < 20f)
                    sat = 10f; // slightly vivid
                else if (pressure > 70f)
                    sat = Mathf.Lerp(0f, -40f, Mathf.InverseLerp(70f, 100f, pressure));
                else
                    sat = 0f;
                _colorAdjustments.saturation.Override(sat);

                // Contrast: slightly higher in clarity
                float contrast = pressure < 20f ? 10f : 0f;
                _colorAdjustments.contrast.Override(contrast);
            }

            // ── White Balance (color temperature) ──────────────────
            if (_whiteBalance != null)
            {
                // Warm shift at elevated+, cool at clarity
                float temp;
                if (pressure < 20f)
                    temp = -15f; // cool
                else if (pressure > 45f)
                    temp = Mathf.Lerp(0f, 20f, Mathf.InverseLerp(45f, 100f, pressure)); // warm
                else
                    temp = 0f;
                _whiteBalance.temperature.Override(temp);
            }
        }

        private float GetVignetteIntensity(float pressure)
        {
            if (pressure < 45f) return 0f;
            if (pressure < 70f) return Mathf.InverseLerp(45f, 70f, pressure) * 0.15f;
            if (pressure < 90f) return Mathf.Lerp(0.15f, 0.35f, Mathf.InverseLerp(70f, 90f, pressure));
            return Mathf.Lerp(0.35f, 0.55f, Mathf.InverseLerp(90f, 100f, pressure));
        }
    }
}
