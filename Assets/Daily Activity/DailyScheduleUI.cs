using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HalcyonAcademy
{
    /// <summary>
    /// The main Daily Schedule UI panel. Displays available activities for the
    /// current time slot and lets the player choose one.
    ///
    /// Scene Setup:
    ///   1. Create a Canvas with a full-screen panel (the schedule backdrop)
    ///   2. Assign child elements in the Inspector (see serialized fields)
    ///   3. Create an ActivityButton prefab (see ActivityButtonUI.cs)
    ///   4. Populate the activityDatabase with your ActivityDef ScriptableObjects
    ///
    /// This script subscribes to DayCycleManager events automatically.
    /// </summary>
    public class DailyScheduleUI : MonoBehaviour
    {
        // ── References ─────────────────────────────────────────────────
        [Header("Panel References")]
        [SerializeField] private GameObject schedulePanel;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Day Header")]
        [SerializeField] private TextMeshProUGUI dayLabel;
        [SerializeField] private TextMeshProUGUI timeOfDayLabel;
        [SerializeField] private TextMeshProUGUI weatherLabel;

        [Header("Slot Indicators")]
        [SerializeField] private SlotIndicator[] slotIndicators;  // 4 elements

        [Header("Activity Grid")]
        [SerializeField] private Transform activityButtonContainer;
        [SerializeField] private ActivityButtonUI activityButtonPrefab;

        [Header("Narrative Feedback")]
        [SerializeField] private TextMeshProUGUI narrativeText;
        [SerializeField] private TextMeshProUGUI pressureChangeText;

        [Header("Vapeur Button (always visible, free action)")]
        [SerializeField] private Button vapeurButton;
        [SerializeField] private TextMeshProUGUI vapeurCooldownText;

        [Header("Morning Report")]
        [SerializeField] private GameObject morningReportPanel;
        [SerializeField] private TextMeshProUGUI morningReportText;
        [SerializeField] private Button morningDismissButton;

        [Header("Panic Attack Overlay")]
        [SerializeField] private GameObject panicOverlay;
        [SerializeField] private TextMeshProUGUI panicText;

        [Header("Activity Database")]
        [SerializeField] private List<ActivityDef> activityDatabase;

        [Header("Vapeur Activity (special — no slot cost)")]
        [SerializeField] private ActivityDef vapeurActivity;

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private float narrativeDisplayTime = 2.5f;
        [SerializeField] private float buttonStaggerDelay = 0.08f;

        // ── Runtime ────────────────────────────────────────────────────
        private readonly List<ActivityButtonUI> _spawnedButtons = new();
        private bool _waitingForChoice;
        private bool _vapeurOnCooldown;
        private Coroutine _narrativeCoroutine;

        // ── Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            var dc = DayCycleManager.Instance;
            if (dc == null) return;

            dc.OnDayStarted += HandleDayStarted;
            dc.OnSlotBegan += HandleSlotBegan;
            dc.OnSlotCompleted += HandleSlotCompleted;
            dc.OnDayEnded += HandleDayEnded;
            dc.OnPanicAttack += HandlePanicAttack;
            dc.OnMorningPressureCalculated += HandleMorningReport;

            if (vapeurButton != null)
                vapeurButton.onClick.AddListener(OnVapeurPressed);

            if (morningDismissButton != null)
                morningDismissButton.onClick.AddListener(DismissMorningReport);
        }

        private void OnDisable()
        {
            var dc = DayCycleManager.Instance;
            if (dc == null) return;

            dc.OnDayStarted -= HandleDayStarted;
            dc.OnSlotBegan -= HandleSlotBegan;
            dc.OnSlotCompleted -= HandleSlotCompleted;
            dc.OnDayEnded -= HandleDayEnded;
            dc.OnPanicAttack -= HandlePanicAttack;
            dc.OnMorningPressureCalculated -= HandleMorningReport;
        }

        // ── Event Handlers ─────────────────────────────────────────────

        private void HandleDayStarted(int dayNumber)
        {
            dayLabel.text = $"Day {dayNumber}";
            _vapeurOnCooldown = false;
            UpdateVapeurButton();

            // Reset slot indicators
            for (int i = 0; i < slotIndicators.Length; i++)
            {
                slotIndicators[i].SetState(SlotState.Upcoming);
            }

            if (panicOverlay != null) panicOverlay.SetActive(false);
            narrativeText.text = "";
            pressureChangeText.text = "";
        }

        private void HandleMorningReport(MorningPressureReport report)
        {
            if (morningReportPanel == null) return;

            morningReportPanel.SetActive(true);
            _waitingForChoice = false;

            string recoverySign = report.OvernightRecovery > 0 ? "-" : "+";
            string weatherSign = report.WeatherModifier >= 0 ? "+" : "";

            morningReportText.text =
                $"<size=120%><color=#C9A84C>Dawn over the Cloche</color></size>\n\n" +
                $"Last night's rest: {recoverySign}{report.OvernightRecovery:F0} pressure\n" +
                $"Weather: {report.WeatherDescription}\n" +
                $"  ({weatherSign}{report.WeatherModifier:F0} pressure)\n\n" +
                $"<size=110%>Starting pressure: <color=#D4A843>{report.FinalPressure:F0}</color></size>";
        }

        private void DismissMorningReport()
        {
            if (morningReportPanel != null)
                morningReportPanel.SetActive(false);

            // Now show the first slot's activities
            HandleSlotBegan(DayCycleManager.Instance.CurrentSlotIndex, DayCycleManager.Instance.CurrentTimeOfDay);
        }

        private void HandleSlotBegan(int slotIndex, TimeOfDay timeOfDay)
        {
            // Update header
            timeOfDayLabel.text = GetTimeOfDayDisplayName(timeOfDay);
            weatherLabel.text = GetWeatherHint();

            // Update slot indicators
            for (int i = 0; i < slotIndicators.Length; i++)
            {
                if (i < slotIndex)
                    slotIndicators[i].SetState(SlotState.Completed);
                else if (i == slotIndex)
                    slotIndicators[i].SetState(SlotState.Active);
                else
                    slotIndicators[i].SetState(SlotState.Upcoming);
            }

            // Populate activity choices
            PopulateActivityButtons(timeOfDay);
            _waitingForChoice = true;

            StartCoroutine(FadePanel(true));
        }

        private void HandleSlotCompleted(int slotIndex, TimeOfDay timeOfDay)
        {
            _waitingForChoice = false;
            slotIndicators[slotIndex].SetState(SlotState.Completed);
        }

        private void HandleDayEnded(int dayNumber, bool wasPanic)
        {
            ClearActivityButtons();
            _waitingForChoice = false;

            // Mark remaining slots as lost
            for (int i = 0; i < slotIndicators.Length; i++)
            {
                if (slotIndicators[i].CurrentState == SlotState.Upcoming ||
                    slotIndicators[i].CurrentState == SlotState.Active)
                {
                    slotIndicators[i].SetState(wasPanic ? SlotState.Lost : SlotState.Completed);
                }
            }

            if (!wasPanic)
            {
                ShowNarrative("The Cloche dims as night settles. Time to rest.", "");
            }
        }

        private void HandlePanicAttack()
        {
            ClearActivityButtons();
            _waitingForChoice = false;

            if (panicOverlay != null)
            {
                panicOverlay.SetActive(true);
                panicText.text = "The pressure crests—\n\nMolly's vision tunnels. The hum of the Cloche " +
                                 "drowns out everything else. She needs to stop. She needs to breathe.\n\n" +
                                 "<size=80%><color=#C9A84C>Remaining activities lost.</color></size>";
            }
        }

        // ── Activity Button Population ─────────────────────────────────

        private void PopulateActivityButtons(TimeOfDay timeOfDay)
        {
            ClearActivityButtons();

            List<ActivityDef> available = new();
            foreach (var def in activityDatabase)
            {
                if (def.costsFreeSlot) continue; // Vapeur handled separately
                if (!def.IsAvailableAt(timeOfDay)) continue;
                // TODO: Add ward-lock checks here (e.g., Greenwork locked by Alaric)
                available.Add(def);
            }

            for (int i = 0; i < available.Count; i++)
            {
                var btn = Instantiate(activityButtonPrefab, activityButtonContainer);
                btn.Initialize(available[i], OnActivityChosen);
                btn.AnimateIn(i * buttonStaggerDelay);
                _spawnedButtons.Add(btn);
            }
        }

        private void ClearActivityButtons()
        {
            foreach (var btn in _spawnedButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            _spawnedButtons.Clear();
        }

        // ── Player Actions ─────────────────────────────────────────────

        private void OnActivityChosen(ActivityDef chosen)
        {
            if (!_waitingForChoice) return;
            _waitingForChoice = false;

            // Disable all buttons during resolution
            foreach (var btn in _spawnedButtons) btn.SetInteractable(false);

            // Roll and apply pressure
            float delta = chosen.RollPressureDelta();
            PressureSystem.Instance.ApplyDelta(delta, chosen.activityId);

            // Show narrative feedback
            string deltaSign = delta >= 0 ? "+" : "";
            string deltaColor = delta >= 0 ? "#D4635A" : "#6BB88C";
            ShowNarrative(
                chosen.narrativeSnippet,
                $"<color={deltaColor}>{deltaSign}{delta:F0} pressure</color>"
            );

            // After narrative display, advance the day cycle
            StartCoroutine(ResolveActivityThenAdvance());
        }

        private void OnVapeurPressed()
        {
            if (_vapeurOnCooldown || vapeurActivity == null) return;

            _vapeurOnCooldown = true;
            UpdateVapeurButton();

            // Vapeur is a free action — doesn't consume a slot
            DayCycleManager.Instance.ExecuteFreeAction(vapeurActivity);

            float delta = vapeurActivity.pressureMin; // Vapeur has a fixed effect typically
            string sign = delta >= 0 ? "+" : "";
            ShowNarrative(
                "The Vapeur settles over her like a cool cloth. The edges soften.",
                $"<color=#6BB88C>{sign}{delta:F0} pressure</color>\n<color=#C9A84C>No slot used</color>"
            );
        }

        private void UpdateVapeurButton()
        {
            if (vapeurButton == null) return;
            vapeurButton.interactable = !_vapeurOnCooldown;
            if (vapeurCooldownText != null)
                vapeurCooldownText.text = _vapeurOnCooldown ? "Used today" : "Take Vapeur";
        }

        // ── Narrative Display ──────────────────────────────────────────

        private void ShowNarrative(string narrative, string pressureInfo)
        {
            if (_narrativeCoroutine != null) StopCoroutine(_narrativeCoroutine);
            narrativeText.text = narrative;
            pressureChangeText.text = pressureInfo;
        }

        private IEnumerator ResolveActivityThenAdvance()
        {
            yield return new WaitForSeconds(narrativeDisplayTime);
            ClearActivityButtons();
            DayCycleManager.Instance.CompleteCurrentSlot();
        }

        // ── Panel Animation ────────────────────────────────────────────

        private IEnumerator FadePanel(bool fadeIn)
        {
            if (panelCanvasGroup == null) yield break;

            float start = fadeIn ? 0f : 1f;
            float end = fadeIn ? 1f : 0f;
            float elapsed = 0f;

            panelCanvasGroup.alpha = start;
            schedulePanel.SetActive(true);

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                panelCanvasGroup.alpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
                yield return null;
            }

            panelCanvasGroup.alpha = end;
            if (!fadeIn) schedulePanel.SetActive(false);
        }

        // ── Helpers ────────────────────────────────────────────────────

        private string GetTimeOfDayDisplayName(TimeOfDay tod) => tod switch
        {
            TimeOfDay.Morning => "Morning",
            TimeOfDay.Afternoon => "Afternoon",
            TimeOfDay.Evening => "Evening",
            TimeOfDay.Night => "Night",
            _ => tod.ToString()
        };

        private string GetWeatherHint()
        {
            float mod = DayCycleManager.Instance.TodayWeatherModifier;
            if (mod > 6f) return "⚡ Storm beyond the walls";
            if (mod > 2f) return "☁ Pressure building";
            if (mod > -2f) return "○ Calm";
            return "✧ Clear skies";
        }
    }

    // ── Slot Indicator ─────────────────────────────────────────────────

    public enum SlotState { Upcoming, Active, Completed, Lost }

    /// <summary>
    /// A single slot pip in the day tracker. Attach to a small UI element
    /// with an Image component. Can also have a child TextMeshProUGUI for labels.
    /// </summary>
    [Serializable]
    public class SlotIndicator
    {
        [SerializeField] private Image pip;
        [SerializeField] private Image glowRing;
        [SerializeField] private TextMeshProUGUI label;

        [Header("Colors")]
        [SerializeField] private Color upcomingColor = new(0.35f, 0.28f, 0.45f, 0.6f);
        [SerializeField] private Color activeColor = new(0.82f, 0.73f, 0.55f, 1f);
        [SerializeField] private Color completedColor = new(0.55f, 0.48f, 0.68f, 1f);
        [SerializeField] private Color lostColor = new(0.65f, 0.3f, 0.3f, 0.5f);

        public SlotState CurrentState { get; private set; }

        private static readonly string[] SlotLabels = { "Morn", "Aft", "Eve", "Night" };

        public void SetState(SlotState state)
        {
            CurrentState = state;
            Color c = state switch
            {
                SlotState.Active => activeColor,
                SlotState.Completed => completedColor,
                SlotState.Lost => lostColor,
                _ => upcomingColor
            };

            if (pip != null) pip.color = c;
            if (glowRing != null)
            {
                glowRing.enabled = state == SlotState.Active;
                glowRing.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.4f);
            }
        }
    }
}
