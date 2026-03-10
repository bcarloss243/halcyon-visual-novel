using UnityEngine;

namespace HalcyonAcademy
{
    /// <summary>
    /// Extension methods and helpers for ActivityDef ScriptableObjects.
    /// 
    /// These assume your existing ActivityDef has at minimum:
    ///   - string activityId
    ///   - string displayName
    ///   - TimeOfDay[] availableSlots
    ///   - float pressureMin, pressureMax (the delta range)
    ///   - bool costsFreeSlot (true for Vapeur)
    ///   - Sprite icon (optional)
    ///   - string narrativeSnippet (short flavor text)
    ///
    /// If your ActivityDef already has a RollPressureDelta method, remove
    /// the extension method below and use yours instead.
    /// </summary>
    public static class ActivityDefExtensions
    {
        /// <summary>
        /// Roll a pressure delta within the activity's min/max range.
        /// Applies the "bad days are more valuable" multiplier when relevant.
        /// </summary>
        public static float RollPressureDelta(this ActivityDef activity)
        {
            float baseDelta = Random.Range(activity.pressureMin, activity.pressureMax);
            float currentPressure = PressureSystem.Instance.CurrentPressure;

            // High-pressure bonuses: Rootwork and relationship activities
            // gain effectiveness when pressure is high (above 60)
            if (currentPressure > 60f && activity.BenefitsFromHighPressure())
            {
                float bonus = Mathf.Lerp(1f, 1.4f, (currentPressure - 60f) / 40f);
                // For negative deltas (pressure-reducing), multiply the magnitude
                if (baseDelta < 0) baseDelta *= bonus;
            }

            return baseDelta;
        }

        /// <summary>
        /// Activities tagged as Rootwork or Relationship benefit from high pressure.
        /// Checks the activity's category tag. Adjust the string checks to match
        /// your ActivityDef's category system.
        /// </summary>
        public static bool BenefitsFromHighPressure(this ActivityDef activity)
        {
            // Adjust these checks to match your ActivityDef's tag/category system
            string id = activity.activityId.ToLowerInvariant();
            return id.Contains("rootwork") ||
                   id.Contains("lola") ||
                   id.Contains("relationship") ||
                   id.Contains("bayou");
        }

        /// <summary>
        /// Whether this activity is available in the given time slot.
        /// </summary>
        public static bool IsAvailableAt(this ActivityDef activity, TimeOfDay time)
        {
            if (activity.availableSlots == null || activity.availableSlots.Length == 0)
                return true; // Available anytime if no restrictions

            foreach (var slot in activity.availableSlots)
            {
                if (slot == time) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a forecast string like "+8 to +12" or "-10 to -15".
        /// </summary>
        public static string GetPressureForecastText(this ActivityDef activity)
        {
            string minSign = activity.pressureMin >= 0 ? "+" : "";
            string maxSign = activity.pressureMax >= 0 ? "+" : "";
            return $"{minSign}{activity.pressureMin:F0} to {maxSign}{activity.pressureMax:F0}";
        }

        /// <summary>
        /// Returns a color hint for the activity based on whether it raises or lowers pressure.
        /// Use this for UI tinting.
        /// </summary>
        public static Color GetPressureColor(this ActivityDef activity)
        {
            float avg = (activity.pressureMin + activity.pressureMax) / 2f;
            if (avg > 2f) return new Color(0.85f, 0.35f, 0.35f, 1f);  // Red-ish — raises pressure
            if (avg < -2f) return new Color(0.4f, 0.75f, 0.55f, 1f);  // Teal — lowers pressure
            return new Color(0.82f, 0.73f, 0.55f, 1f);                // Gold — neutral/variable
        }
    }
}
