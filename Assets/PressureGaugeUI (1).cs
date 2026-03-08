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
    /// Setup: Place on a RectTransform (200×200). Child objects referenced
    /// below are created by the GaugePrefabBuilder editor script.
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
        [SerializeField] private Image[] _zoneArcs;          // 4 arcs: crisis, elevated, manageable, clarity

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI _zoneLabel;
        [SerializeField] private TextMeshProUGUI _forecastText;
        [SerializeField] private TextMeshProUGUI _halcyonText; // etched at bottom

        [Header("Config")]
        [SerializeField] private ZoneConfig _zoneConfig;

        [Header("Animation")]
        [SerializeField] private float _needleSmoothTime = 0.8f;
        [SerializeField] private AnimationCurve _needleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Effects")]
        [SerializeField] private CanvasGroup _gaugeGlow;      // teal glow for clarity zone
        [SerializeField] private float _crisisShakeIntensity = 2f;

        // ── Arc Geometry ───────────────────────────────────────────────
        // Gauge spans 240° — from 210° (crisis/left) to -30° (clarity/right)
        // Unity UI rotation: 0° = up, positive = clockwise
        private const float ARC_START_ANGLE = 150f;  // crisis side (7 o'clock)
        private const float ARC_END_ANGLE = -150f;   // clarity side (5 o'clock)
        private const float ARC_SPAN = 300f;         // total sweep

        // ── State ──────────────────────────────────────────────────────
        private float _targetAngle;
        private float _currentAngle;
        private float _angleVelocity;
        private PressureZone _displayedZone;
        private Coroutine _forecastRoutine;
        private Vector2 _originalNeedlePos;
        private bool _isShaking;

        // ================================================================
        //  LIFECYCLE
        // ================================================================

        private bool _subscribed = false;

        void OnEnable()
        {
            TrySubscribe();
            if (_needlePivot != null)
                _originalNeedlePos = _needlePivot.anchoredPosition;
        }

        void Start()
        {
            // Retry subscription in Start in case PressureSystem wasn't ready in OnEnable
            TrySubscribe();
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
            // Retry subscription if PressureSystem wasn't ready earlier
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
        }

        // ================================================================
        //  EVENT HANDLERS
        // ================================================================

        private void OnPressureChanged(float pressure)
        {
            // Map pressure 0–100 to needle angle
            // 0 (clarity) = far right = ARC_END_ANGLE
            // 100 (crisis) = far left = ARC_START_ANGLE
            float t = pressure / 100f;
            _targetAngle = Mathf.Lerp(ARC_END_ANGLE, ARC_START_ANGLE, t);

            // Crack overlay above 90
            if (_crackOverlay != null)
            {
                float crackAlpha = Mathf.InverseLerp(88f, 95f, pressure);
                var c = _crackOverlay.color;
                _crackOverlay.color = new Color(c.r, c.g, c.b, crackAlpha);
            }

            // Clarity glow below 20
            if (_gaugeGlow != null)
            {
                _gaugeGlow.alpha = Mathf.InverseLerp(25f, 10f, pressure) * 0.4f;
            }

            // Crisis shake
            _isShaking = pressure > 85f;
            if (!_isShaking && _needlePivot != null)
                _needlePivot.anchoredPosition = _originalNeedlePos;
        }

        private void OnZoneChanged(PressureZone zone)
        {
            _displayedZone = zone;

            if (_zoneConfig == null) return;
            var data = _zoneConfig.GetZoneData(zone);

            // Update zone label
            if (_zoneLabel != null)
            {
                _zoneLabel.text = PressureSystem.Instance.VapeurActive
                    ? "VAPEUR ACTIVE"
                    : data.displayName.ToUpper();
                _zoneLabel.color = PressureSystem.Instance.VapeurActive
                    ? new Color(0.54f, 0.44f, 0.69f) // purple
                    : data.color;
            }

            // Update forecast text
            UpdateForecast(data);
        }

        private void OnVapeurTaken()
        {
            // Purple tint overlay
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
                _forecastText.text = "Everything narrows. You can't\u2009\u2014";
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

        /// <summary>
        /// Show a custom narrative forecast line (e.g., after choosing an activity).
        /// Reverts to zone-based forecast after duration seconds.
        /// </summary>
        public void ShowNarrativeForecast(string text, Color? color = null, float duration = 4f)
        {
            if (_forecastRoutine != null) StopCoroutine(_forecastRoutine);

            _forecastText.color = color ?? new Color(0.54f, 0.49f, 0.38f);
            _forecastRoutine = StartCoroutine(NarrativeForecastSequence(text, duration));
        }

        private IEnumerator NarrativeForecastSequence(string text, float duration)
        {
            // Type out the narrative text
            _forecastText.text = "";
            foreach (char c in text)
            {
                _forecastText.text += c;
                yield return new WaitForSeconds(0.025f);
            }

            yield return new WaitForSeconds(duration);

            // Revert to zone forecast
            if (_zoneConfig != null)
                UpdateForecast(_zoneConfig.GetZoneData(_displayedZone));
        }
    }
}
