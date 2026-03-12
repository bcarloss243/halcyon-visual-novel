using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace HalcyonAcademy
{
    /// <summary>
    /// UI component for a single activity choice button.
    /// Prefab structure:
    ///   ActivityButton (Button + Image + CanvasGroup)
    ///   ├── Icon (Image)            — optional activity icon
    ///   ├── NameLabel (TMP)         — activity name
    ///   ├── PressureForecast (TMP)  — e.g., "+8 to +12"
    ///   ├── FlavorText (TMP)        — narrative snippet
    ///   ├── HighPressureBonus (GameObject, enabled when applicable)
    ///   │   └── BonusLabel (TMP)    — "✧ Insight bonus"
    ///   ├── BorderGlow (Image)      — Art Deco border, fades on hover
    ///   └── PressureBar (Image)     — small color bar indicating pressure direction
    /// </summary>
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(CanvasGroup))]
    public class ActivityButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // ── Serialized ─────────────────────────────────────────────────
        [Header("Content")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI pressureForecastLabel;
        [SerializeField] private TextMeshProUGUI flavorLabel;

        [Header("High Pressure Bonus")]
        [SerializeField] private GameObject highPressureBonusObj;
        [SerializeField] private TextMeshProUGUI bonusLabel;

        [Header("Visual Accents")]
        [SerializeField] private Image borderGlow;
        [SerializeField] private Image pressureColorBar;

        [Header("Animation")]
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float hoverBorderAlpha = 0.8f;
        [SerializeField] private float animateInDuration = 0.35f;

        // ── Runtime ────────────────────────────────────────────────────
        private Button _button;
        private CanvasGroup _canvasGroup;
        private RectTransform _rect;
        private ActivityDef _activityDef;
        private Action<ActivityDef> _onChosen;
        private Vector3 _baseScale;
        private Coroutine _hoverCoroutine;

        // ── Initialization ─────────────────────────────────────────────

        private void Awake()
        {
            _button = GetComponent<Button>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _rect = GetComponent<RectTransform>();
            _baseScale = _rect.localScale;
        }

        /// <summary>
        /// Called by DailyScheduleUI when spawning this button.
        /// Uses actual ActivityDef field names.
        /// </summary>
        public void Initialize(ActivityDef def, Action<ActivityDef> onChosen)
        {
            _activityDef = def;
            _onChosen = onChosen;

            // Content — using actual ActivityDef fields
            if (nameLabel != null) nameLabel.text = def.activityName;
            if (pressureForecastLabel != null) pressureForecastLabel.text = def.GetPressureForecastText();
            if (flavorLabel != null) flavorLabel.text = def.GetNarrative();
            if (iconImage != null && def.icon != null) iconImage.sprite = def.icon;

            // Pressure color bar
            if (pressureColorBar != null)
                pressureColorBar.color = def.GetPressureColor();

            // High pressure bonus indicator
            UpdateHighPressureBonus();

            // Border starts dim
            if (borderGlow != null)
            {
                Color c = borderGlow.color;
                c.a = 0.2f;
                borderGlow.color = c;
            }

            // Button click
            _button.onClick.AddListener(() => _onChosen?.Invoke(_activityDef));
        }

        private void UpdateHighPressureBonus()
        {
            if (highPressureBonusObj == null) return;

            float pressure = PressureSystem.Instance.Pressure;
            bool showBonus = pressure > 60f && _activityDef.BenefitsFromHighPressure();
            highPressureBonusObj.SetActive(showBonus);

            if (showBonus && bonusLabel != null)
            {
                float multiplier = Mathf.Lerp(1f, 1.4f, (pressure - 60f) / 40f);
                bonusLabel.text = $"✧ Insight bonus ×{multiplier:F1}";
            }
        }

        // ── Animate In ─────────────────────────────────────────────────

        public void AnimateIn(float delay)
        {
            StartCoroutine(DoAnimateIn(delay));
        }

        private IEnumerator DoAnimateIn(float delay)
        {
            _canvasGroup.alpha = 0f;
            _rect.localScale = _baseScale * 0.92f;

            yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            while (elapsed < animateInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animateInDuration;
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                _canvasGroup.alpha = ease;
                _rect.localScale = Vector3.Lerp(_baseScale * 0.92f, _baseScale, ease);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _rect.localScale = _baseScale;
        }

        // ── Hover Effects ──────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            if (_hoverCoroutine != null) StopCoroutine(_hoverCoroutine);
            _hoverCoroutine = StartCoroutine(HoverTransition(true));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_hoverCoroutine != null) StopCoroutine(_hoverCoroutine);
            _hoverCoroutine = StartCoroutine(HoverTransition(false));
        }

        private IEnumerator HoverTransition(bool entering)
        {
            float duration = 0.15f;
            float elapsed = 0f;

            Vector3 startScale = _rect.localScale;
            Vector3 targetScale = entering ? _baseScale * hoverScale : _baseScale;
            float startAlpha = borderGlow != null ? borderGlow.color.a : 0.2f;
            float targetAlpha = entering ? hoverBorderAlpha : 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _rect.localScale = Vector3.Lerp(startScale, targetScale, t);

                if (borderGlow != null)
                {
                    Color c = borderGlow.color;
                    c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
                    borderGlow.color = c;
                }
                yield return null;
            }

            _rect.localScale = targetScale;
        }

        // ── Public Controls ────────────────────────────────────────────

        public void SetInteractable(bool interactable)
        {
            _button.interactable = interactable;
            _canvasGroup.alpha = interactable ? 1f : 0.5f;
        }
    }
}