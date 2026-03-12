using UnityEngine;

namespace HalcyonAcademy
{
    /// <summary>
    /// Extension methods for ActivityDef ScriptableObjects.
    /// 
    /// These match the fields in your existing ActivityDef:
    ///   - string activityName
    ///   - float minPressureDelta, maxPressureDelta
    ///   - bool isRootwork, isAcademic, isGreenwork
    ///   - string locationPartner
    ///   - string narrativeDefault, narrativeHighPressure, narrativeLowPressure
    ///   - string perceptiveInsight
    /// 
    /// Fields added by this file (add to your ActivityDef class):
    ///   - TimeOfDay[] availableSlots   (which time slots this activity appears in)
    ///   - bool isFreeAction            (true for Vapeur — doesn't cost a slot)
    ///   - Sprite icon                  (optional icon for the button UI)
    /// </summary>
    public static class ActivityDefExtensions
    {
        /// <summary>
        /// Roll a pressure delta within the activity's min/max range.
        /// Applies the "bad days are more valuable" multiplier when relevant.
        /// </summary>
        public static float RollPressureDelta(this ActivityDef activity)
        {
            float baseDelta = Random.Range(activity.minPressureDelta, activity.maxPressureDelta);
            float currentPressure = PressureSystem.Instance.Pressure;

            // High-pressure bonuses: Rootwork and relationship activities
            // gain effectiveness when pressure is high (above 60)
            if (currentPressure > 60f && activity.BenefitsFromHighPressure())
            {
                float bonus = Mathf.Lerp(1f, 1.4f, (currentPressure - 60f) / 40f);
                if (baseDelta < 0) baseDelta *= bonus;
            }

            return baseDelta;
        }

        /// <summary>
        /// Activities tagged as Rootwork or with a relationship partner benefit
        /// from high pressure ("bad days are more valuable").
        /// Uses the boolean flags and locationPartner field on your ActivityDef.
        /// </summary>
        public static bool BenefitsFromHighPressure(this ActivityDef activity)
        {
            return activity.isRootwork ||
                   !string.IsNullOrEmpty(activity.locationPartner);
        }

        /// <summary>
        /// Whether this activity is available in the given time slot.
        /// Requires adding a TimeOfDay[] availableSlots field to ActivityDef.
        /// If the field doesn't exist yet or is empty, the activity is available anytime.
        /// </summary>
        public static bool IsAvailableAt(this ActivityDef activity, TimeOfDay time)
        {
            if (activity.availableSlots == null || activity.availableSlots.Length == 0)
                return true;

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
            string minSign = activity.minPressureDelta >= 0 ? "+" : "";
            string maxSign = activity.maxPressureDelta >= 0 ? "+" : "";
            return $"{minSign}{activity.minPressureDelta:F0} to {maxSign}{activity.maxPressureDelta:F0}";
        }

        /// <summary>
        /// Returns a color hint for the UI based on whether this activity
        /// raises or lowers pressure on average.
        /// </summary>
        public static Color GetPressureColor(this ActivityDef activity)
        {
            float avg = (activity.minPressureDelta + activity.maxPressureDelta) / 2f;
            if (avg > 2f) return new Color(0.85f, 0.35f, 0.35f, 1f);   // Red — raises pressure
            if (avg < -2f) return new Color(0.4f, 0.75f, 0.55f, 1f);   // Teal — lowers pressure
            return new Color(0.82f, 0.73f, 0.55f, 1f);                  // Gold — neutral/variable
        }

        /// <summary>
        /// Get the appropriate narrative text based on current pressure.
        /// Convenience wrapper around the existing GetNarrativeText method.
        /// </summary>
        public static string GetNarrative(this ActivityDef activity)
        {
            var ps = PressureSystem.Instance;
            return activity.GetNarrativeText(ps.Pressure, ps.CurrentZone);
        }
    }
}