using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace HalcyonAcademy
{
    /// <summary>
    /// Renders the Art Deco pressure gauge as a Unity UI element.
    /// Subscribes to PressureSystem events and animates the needle.
    /// 
    /// Arc orientation (matching gauge art):
    ///   0   (Clarity) = bottom-left  (7 o'clock)
    ///   100 (Crisis)  = bottom-right (5 o'clock)
    ///   Arc sweeps clockwise over the top.
    /// 
    /// Zone thresholds: 0–25, 25–50, 50–75, 75–100
    /// </summary>
    public class PressureGaugeUI : MonoBehaviour
    {
        // ── References ─────────────────────────────────────────────────
        [Header("Gauge Components")]
        [SerializeField] private RectTransform _needlePivot;
        [SerializeField] private Image _gaugeBackground;
        [SerializeField] private Image _glassOverlay;
        [SerializeField] private Image _crackOverlay;
        [SerializeField] private Image _vapeurTintOverlay;
        [SerializeField] private Image[] _zoneArcs;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI _zoneLabel;
        [SerializeField] private TextMeshProUGUI _forecastText;
        [SerializeField] private TextMeshProUGUI _halcyonText;

        [Header("Config")]
        [SerializeField] private ZoneConfig _zoneConfig;

        [Header("Animation")]
        [SerializeField] private float _needleSmoothTime = 0.8f;
        [SerializeField] private AnimationCurve _needleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Effects")]
        [SerializeField] private CanvasGroup _gaugeGlow;
        [SerializeField] private float _crisisShakeIntensity = 2f;

        // ── Zone Glow System ───────────────────────────────────────────
        [Header("Zone Glow")]
        [Tooltip("Image overlays for each zone's glow. Order: Clarity, Manageable, Elevated, Crisis. Sprites should have colors baked in.")]
        [SerializeField] private Image[] _zoneGlowOverlays;
        [SerializeField] private float _glowPulseSpeed = 1.5f;
        [SerializeField] private float _glowMinAlpha = 0.1f;
        [SerializeField] private float _glowMaxAlpha = 0.8f;
        [SerializeField] private float _glowTransitionSpeed = 3f;

        // ── Arc Geometry ───────────────────────────────────────────────
        private const float ARC_START_ANGLE = 150f;
        private const float ARC_END_ANGLE = -150f;

        // ── State ──────────────────────────────────────────────────────
        private float _targetAngle;
        private float _currentAngle;
        private float _angleVelocity;
        private PressureZone _displayedZone;
        private PressureZone _previousZone;
        private Coroutine _forecastRoutine;
        private Vector2 _originalNeedlePos;
        private bool _isShaking;
        private bool _subscribed = false;

        // Glow state
        private float[] _glowTargetAlpha = new float[4];
        private float[] _glowCurrentAlpha = new float[4];
        private float _currentPressure;

        // ================================================================
        //  LIFECYCLE
        // ================================================================

        void OnEnable()
        {
            TrySubscribe();
            if (_needlePivot != null)
                _originalNeedlePos = _needlePivot.anchoredPosition;
        }

        void Start()
        {
            TrySubscribe();

            // Initialize all glow overlays to white with zero alpha
            if (_zoneGlowOverlays != null)
            {
                for (int i = 0; i < _zoneGlowOverlays.Length; i++)
                {
                    if (_zoneGlowOverlays[i] != null)
                        _zoneGlowOverlays[i].color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }

        private void TrySubscribe()
        {
            if (_subscribed || PressureSystem.Instance == null) return;
            PressureSystem.Instance.OnPressureChanged.AddListener(OnPressureChanged);
            PressureSystem.Instance.OnZoneChanged.AddListener(OnZoneChanged);
            PressureSystem.Instance.OnVapeurTaken.AddListener(OnVapeurTaken);
            PressureSystem.Instance.OnVapeurWoreOff.AddListener(OnVapeurWoreOff);
            PressureSystem.Instance.OnStormHarvest.AddListener(OnStormHarvest);
            PressureSystem.Instance.OnPanicAttack.AddListener(OnPanicAttack);
            _subscribed = true;
        }

        void OnDisable()
        {
            if (PressureSystem.Instance != null)
            {
                PressureSystem.Instance.OnPressureChanged.RemoveListener(OnPressureChanged);
                PressureSystem.Instance.OnZoneChanged.RemoveListener(OnZoneChanged);
                PressureSystem.Instance.OnVapeurTaken.RemoveListener(OnVapeurTaken);
                PressureSystem.Instance.OnVapeurWoreOff.RemoveListener(OnVapeurWoreOff);
                PressureSystem.Instance.OnStormHarvest.RemoveListener(OnStormHarvest);
                PressureSystem.Instance.OnPanicAttack.RemoveListener(OnPanicAttack);
            }
            _subscribed = false;
        }

        void Update()
        {
            if (!_subscribed) TrySubscribe();

            // Smooth needle animation
            _currentAngle = Mathf.SmoothDamp(
                _currentAngle, _targetAngle,
                ref _angleVelocity, _needleSmoothTime
            );

            if (_needlePivot != null)
                _needlePivot.localRotation = Quaternion.Euler(0, 0, _currentAngle);

            // Crisis shake
            if (_isShaking && _needlePivot != null)
            {
                float shake = Random.Range(-_crisisShakeIntensity, _crisisShakeIntensity);
                _needlePivot.anchoredPosition = _originalNeedlePos + new Vector2(shake * 0.3f, shake * 0.3f);
            }

            // Zone Glow
            UpdateZoneGlow();
        }

        // ================================================================
        //  ZONE GLOW SYSTEM
        // ================================================================

        private void UpdateZoneGlow()
        {
            if (_zoneGlowOverlays == null || _zoneGlowOverlays.Length < 4) return;

            float zoneDepth = GetZoneDepth(_currentPressure);

            // Breathing pulse
            float pulse = Mathf.Sin(Time.time * _glowPulseSpeed) * 0.5f + 0.5f;

            // Intensity scales with depth into the zone
            float pulseAlpha = Mathf.Lerp(_glowMinAlpha, _glowMaxAlpha, zoneDepth);
            pulseAlpha *= Mathf.Lerp(0.7f, 1.0f, pulse);

            // Crisis gets aggressive pulsing
            if (_displayedZone == PressureZone.Crisis)
            {
                float crisisIntensity = Mathf.InverseLerp(75f, 100f, _currentPressure);
                float fastPulse = Mathf.Sin(Time.time * _glowPulseSpeed * 2f) * 0.5f + 0.5f;
                pulseAlpha = Mathf.Lerp(pulseAlpha, _glowMaxAlpha * 1.2f, crisisIntensity * fastPulse);
            }

            // Only the active zone glows — white tint preserves sprite colors
            for (int i = 0; i < 4; i++)
            {
                _glowTargetAlpha[i] = ((int)_displayedZone == i) ? pulseAlpha : 0f;

                _glowCurrentAlpha[i] = Mathf.MoveTowards(
                    _glowCurrentAlpha[i],
                    _glowTargetAlpha[i],
                    Time.deltaTime * _glowTransitionSpeed
                );

                if (_zoneGlowOverlays[i] != null)
                {
                    _zoneGlowOverlays[i].color = new Color(1f, 1f, 1f, _glowCurrentAlpha[i]);
                }
            }
        }

        private float GetZoneDepth(float pressure)
        {
            switch (_displayedZone)
            {
                case PressureZone.Clarity:
                    return Mathf.InverseLerp(25f, 0f, pressure);
                case PressureZone.Manageable:
                    return 0.3f + 0.7f * Mathf.InverseLerp(50f, 25f, pressure);
                case PressureZone.Elevated:
                    return Mathf.InverseLerp(50f, 75f, pressure);
                case PressureZone.Crisis:
                    return Mathf.InverseLerp(75f, 100f, pressure);
                default:
                    return 0f;
            }
        }

        // ================================================================
        //  EVENT HANDLERS
        // ================================================================

        private void OnPressureChanged(float pressure)
        {
            _currentPressure = pressure;

            // Needle: 0 → bottom-left (150°), 100 → bottom-right (-150°)
            float t = pressure / 100f;
            _targetAngle = Mathf.Lerp(ARC_START_ANGLE, ARC_END_ANGLE, t);

            // Crack overlay above 90
            if (_crackOverlay != null)
            {
                float crackAlpha = Mathf.InverseLerp(88f, 95f, pressure);
                var c = _crackOverlay.color;
                _crackOverlay.color = new Color(c.r, c.g, c.b, crackAlpha);
            }

            // Clarity glow below 25
            if (_gaugeGlow != null)
                _gaugeGlow.alpha = Mathf.InverseLerp(25f, 5f, pressure) * 0.4f;

            // Crisis shake
            _isShaking = pressure > 85f;
            if (!_isShaking && _needlePivot != null)
                _needlePivot.anchoredPosition = _originalNeedlePos;
        }

        private void OnZoneChanged(PressureZone zone)
        {
            _previousZone = _displayedZone;
            _displayedZone = zone;

            if (_zoneConfig == null) return;
            var data = _zoneConfig.GetZoneData(zone);

            if (_zoneLabel != null)
            {
                _zoneLabel.text = PressureSystem.Instance.VapeurActive
                    ? "VAPEUR ACTIVE"
                    : data.displayName.ToUpper();
                _zoneLabel.color = PressureSystem.Instance.VapeurActive
                    ? new Color(0.54f, 0.44f, 0.69f)
                    : data.color;
            }

            UpdateForecast(data);
        }

        private void OnVapeurTaken()
        {
            if (_vapeurTintOverlay != null)
                StartCoroutine(FadeTint(_vapeurTintOverlay, 0.15f, 0.6f));

            if (_zoneLabel != null)
            {
                _zoneLabel.text = "VAPEUR ACTIVE";
                _zoneLabel.color = new Color(0.54f, 0.44f, 0.69f);
            }

            if (_forecastText != null)
            {
                _forecastText.text = "The Vapeur hits. The hum fades. Everything smooths out.";
                _forecastText.color = new Color(0.54f, 0.44f, 0.69f);
            }
        }

        private void OnVapeurWoreOff()
        {
            if (_vapeurTintOverlay != null)
                StartCoroutine(FadeTint(_vapeurTintOverlay, 0f, 1.2f));

            if (_forecastText != null)
            {
                _forecastText.text = "The clarity was borrowed. The debt comes due.";
                _forecastText.color = new Color(0.55f, 0.40f, 0.13f);
            }
        }

        private void OnStormHarvest(float spike)
        {
            StartCoroutine(StormFlickerEffect());
        }

        private void OnPanicAttack()
        {
            if (_forecastText != null)
            {
                _forecastText.text = "Everything narrows. You can\u2019t\u2009\u2014";
                _forecastText.color = new Color(0.55f, 0.13f, 0.13f);
            }
        }

        // ================================================================
        //  EFFECTS
        // ================================================================

        private void UpdateForecast(ZoneConfig.ZoneData data)
        {
            if (_forecastText == null || _zoneConfig == null) return;

            if (_forecastRoutine != null) StopCoroutine(_forecastRoutine);
            _forecastRoutine = StartCoroutine(TypeForecast(
                _zoneConfig.GetRandomForecast(_displayedZone),
                data.color
            ));
        }

        private IEnumerator TypeForecast(string text, Color color)
        {
            _forecastText.color = color;
            _forecastText.text = "";
            foreach (char c in text)
            {
                _forecastText.text += c;
                yield return new WaitForSeconds(0.02f);
            }
        }

        private IEnumerator FadeTint(Image overlay, float targetAlpha, float duration)
        {
            float startAlpha = overlay.color.a;
            float elapsed = 0f;
            Color c = overlay.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                overlay.color = c;
                yield return null;
            }
            c.a = targetAlpha;
            overlay.color = c;
        }

        private IEnumerator StormFlickerEffect()
        {
            if (_gaugeBackground == null) yield break;

            Color original = _gaugeBackground.color;
            Color bright = Color.Lerp(original, Color.white, 0.3f);

            for (int i = 0; i < 3; i++)
            {
                _gaugeBackground.color = bright;
                yield return new WaitForSeconds(0.05f);
                _gaugeBackground.color = original;
                yield return new WaitForSeconds(0.1f);
            }
        }

        // ================================================================
        //  PUBLIC: for narrative triggers
        // ================================================================

        public void ShowNarrativeForecast(string text, Color? color = null, float duration = 4f)
        {
            if (_forecastRoutine != null) StopCoroutine(_forecastRoutine);

            _forecastText.color = color ?? new Color(0.54f, 0.49f, 0.38f);
            _forecastRoutine = StartCoroutine(NarrativeForecastSequence(text, duration));
        }

        private IEnumerator NarrativeForecastSequence(string text, float duration)
        {
            _forecastText.text = "";
            foreach (char c in text)
            {
                _forecastText.text += c;
                yield return new WaitForSeconds(0.025f);
            }

            yield return new WaitForSeconds(duration);

            if (_zoneConfig != null)
                UpdateForecast(_zoneConfig.GetZoneData(_displayedZone));
        }
    }
}