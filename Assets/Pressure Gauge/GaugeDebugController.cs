using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace HalcyonAcademy
{
    /// <summary>
    /// Dev-only controller for testing the Pressure Gauge without the full game.
    /// Provides keyboard shortcuts and an optional debug overlay.
    /// 
    /// Remove or disable this script before shipping.
    /// 
    /// Controls:
    ///   ↑/↓         Adjust pressure ±5
    ///   Shift+↑/↓   Adjust pressure ±15
    ///   V            Take Vapeur
    ///   R            Resolve Vapeur (simulate end of day)
    ///   S            Trigger Storm Harvest
    ///   N            Start New Day
    ///   L            Toggle Lola's presence (microclimate)
    ///   A            Toggle Alaric's presence
    ///   C            Trigger Alaric conflict
    ///   P            Practice Rootwork
    ///   G            Visit Greenhouse
    ///   F            Attend Ironwork (Forge)
    ///   Tab          Toggle debug overlay
    /// </summary>
    public class GaugeDebugController : MonoBehaviour
    {
        [Header("UI References (optional)")]
        [SerializeField] private TextMeshProUGUI _debugText;
        [SerializeField] private PressureGaugeUI _gaugeUI;

        [Header("Settings")]
        [SerializeField] private bool _showDebugOverlay = true;

        private bool _lolaPresent = false;
        private bool _alaricPresent = false;
        private float _weatherModifier = 0f;
        private int _dayWeatherIndex = 0;

        // Simulated weather modifiers per day (early game → late game)
        private readonly float[] _weatherSchedule = new float[]
        {
            0f, 2f, -3f, 5f, 1f, -2f, 8f, 3f, // early: small fluctuations
            10f, 12f, 8f, 15f, 12f, 18f, 20f, 25f // late: relentless rise
        };

        void Update()
        {
            var ps = PressureSystem.Instance;
            if (ps == null) return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Pressure adjustments
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                float delta = shift ? 15f : 5f;
                ps.AdjustPressure(delta, "debug");
                ShowFeedback($"+{delta} pressure");
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                float delta = shift ? -15f : -5f;
                ps.AdjustPressure(delta, "debug");
                ShowFeedback($"{delta} pressure");
            }

            // Vapeur
            if (Input.GetKeyDown(KeyCode.V))
            {
                ps.TakeVapeur();
                ShowFeedback("Vapeur taken");
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                ps.ResolveVapeur();
                ShowFeedback("Vapeur resolved — rebound hit");
            }

            // Storm Harvest
            if (Input.GetKeyDown(KeyCode.S))
            {
                ps.TriggerStormHarvest();
                ShowFeedback("⚡ Storm harvest!");
            }

            // New Day
            if (Input.GetKeyDown(KeyCode.N))
            {
                _weatherModifier = _dayWeatherIndex < _weatherSchedule.Length
                    ? _weatherSchedule[_dayWeatherIndex++]
                    : Random.Range(15f, 25f); // late game default

                float relMod = _lolaPresent ? -5f : 0f;
                ps.StartNewDay(_weatherModifier, relMod);
                ShowFeedback($"Day {ps.Day} — weather: {_weatherModifier:+#;-#;0}");
            }

            // Lola presence toggle
            if (Input.GetKeyDown(KeyCode.L))
            {
                _lolaPresent = !_lolaPresent;
                ps.SetLocationPartner(_lolaPresent ? "Lola" : null);
                ShowFeedback(_lolaPresent ? "Lola is nearby" : "Lola left");
            }

            // Alaric presence toggle
            if (Input.GetKeyDown(KeyCode.A))
            {
                _alaricPresent = !_alaricPresent;
                ps.SetLocationPartner(_alaricPresent ? "Alaric" : null);
                ShowFeedback(_alaricPresent ? "Alaric is nearby" : "Alaric left");
            }

            // Alaric conflict
            if (Input.GetKeyDown(KeyCode.C))
            {
                ps.RecordConflict("Alaric", 0.15f);
                ps.AdjustPressure(12f, "alaric_conflict");
                ShowFeedback("Conflict with Alaric — microclimate damaged");
            }

            // Rootwork practice
            if (Input.GetKeyDown(KeyCode.P))
            {
                float base_delta = Random.Range(-5f, -15f);
                float effective = base_delta * ps.RootworkMultiplier;
                ps.AdjustPressure(effective, "rootwork");
                ShowFeedback($"Rootwork: {effective:F1} (×{ps.RootworkMultiplier:F1})");
            }

            // Greenhouse visit
            if (Input.GetKeyDown(KeyCode.G))
            {
                float delta = Random.Range(-10f, -15f);
                ps.AdjustPressure(delta, "greenhouse");
                if (_gaugeUI != null)
                    _gaugeUI.ShowNarrativeForecast(
                        "The greenhouse air wraps around you like a warm hand.",
                        new Color(0.16f, 0.42f, 0.29f)
                    );
                ShowFeedback($"Greenhouse: {delta:F1}");
            }

            // Forge visit
            if (Input.GetKeyDown(KeyCode.F))
            {
                float delta = Random.Range(8f, 12f);
                ps.AdjustPressure(delta, "forge");
                if (_gaugeUI != null)
                    _gaugeUI.ShowNarrativeForecast(
                        "The Forge hums at a frequency that makes your teeth ache.",
                        new Color(0.55f, 0.40f, 0.13f)
                    );
                ShowFeedback($"Forge: +{delta:F1}");
            }

            // Toggle debug overlay
            if (Input.GetKeyDown(KeyCode.Tab))
                _showDebugOverlay = !_showDebugOverlay;

            // Update debug text
            if (_debugText != null && _showDebugOverlay)
                UpdateDebugOverlay(ps);
            else if (_debugText != null)
                _debugText.text = "";
        }

        private void ShowFeedback(string msg)
        {
            Debug.Log($"[Halcyon Debug] {msg}");
        }

        private void UpdateDebugOverlay(PressureSystem ps)
        {
            _debugText.text = $@"<size=10><color=#6a6050>── PRESSURE GAUGE DEBUG ──
Pressure: {ps.Pressure:F1} ({ps.CurrentZone})
Base: {ps.BasePressure:F1}  |  Day: {ps.Day}
Vapeur: {(ps.VapeurActive ? "ACTIVE" : "off")}  Debt: {ps.VapeurDebt:F1}  Streak: {ps.ConsecutiveVapeurDays}
Weather: {_weatherModifier:+#.#;-#.#;0}
High Day: {ps.IsHighPressureDay}  |  Low Day: {ps.IsLowPressureDay}
Rootwork ×{ps.RootworkMultiplier:F1}  Academic ×{ps.AcademicMultiplier:F1}
Perceptive Insights: {(ps.PerceptiveInsightsAvailable ? "AVAILABLE" : "locked")}
Lola: {(_lolaPresent ? "present" : "—")}  Alaric: {(_alaricPresent ? "present" : "—")}

↑↓ pressure  |  V vapeur  |  S storm  |  N new day
L lola  |  A alaric  |  C conflict  |  P rootwork
G greenhouse  |  F forge  |  Tab hide</color></size>";
        }
    }
}
