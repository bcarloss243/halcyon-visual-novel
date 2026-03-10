using UnityEngine;

namespace HalcyonAcademy
{
    /// <summary>
    /// Data container for pressure zone visuals and text.
    /// Create one asset: Right-click > Create > Halcyon > Zone Config.
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneConfig", menuName = "Halcyon/Zone Config")]
    public class ZoneConfig : ScriptableObject
    {
        [System.Serializable]
        public class ZoneData
        {
            public PressureZone zone;
            public string displayName;
            public Color color;
            public Color gaugeArcColor;
            [TextArea(2, 4)]
            public string[] forecasts;
        }

        public ZoneData[] zones = new ZoneData[]
        {
            new ZoneData
            {
                zone = PressureZone.Clarity,
                displayName = "Clarity",
                color = new Color(0.10f, 0.29f, 0.42f),         // #1a4a6b
                gaugeArcColor = new Color(0.10f, 0.29f, 0.42f),
                forecasts = new string[]
                {
                    "The hum resolves into something almost musical. You can feel the edges of something vast.",
                    "Quiet. Not silence\u2009\u2014\u2009something deeper. Like hearing the ocean through a wall.",
                    "For a moment, the static clears and you can almost see through it."
                }
            },
            new ZoneData
            {
                zone = PressureZone.Manageable,
                displayName = "Manageable",
                color = new Color(0.16f, 0.42f, 0.29f),         // #2a6b4a
                gaugeArcColor = new Color(0.16f, 0.42f, 0.29f),
                forecasts = new string[]
                {
                    "The hum is background noise today. You can function. You can breathe.",
                    "A decent day. The static is there but it's not in charge.",
                    "The needle sits steady. Not good, not bad. Just a day."
                }
            },
            new ZoneData
            {
                zone = PressureZone.Elevated,
                displayName = "Elevated",
                color = new Color(0.55f, 0.40f, 0.13f),         // #8b6520
                gaugeArcColor = new Color(0.55f, 0.40f, 0.13f),
                forecasts = new string[]
                {
                    "The hum is persistent today. Everything costs a little more.",
                    "Heavy. Like walking through water. The lights feel too bright.",
                    "You can push through this. You always push through this. But it takes something."
                }
            },
            new ZoneData
            {
                zone = PressureZone.Crisis,
                displayName = "Crisis",
                color = new Color(0.55f, 0.13f, 0.13f),         // #8b2020
                gaugeArcColor = new Color(0.55f, 0.13f, 0.13f),
                forecasts = new string[]
                {
                    "The hum is deafening. Your hands are shaking. The walls are too close.",
                    "You can\u2019t think. You can\u2019t breathe. Everything is frequency and pressure and noise.",
                    "The world is static. You are static. There is nothing else."
                }
            }
        };

        public ZoneData GetZoneData(PressureZone zone)
        {
            foreach (var z in zones)
                if (z.zone == zone) return z;
            return zones[zones.Length - 1];
        }

        public string GetRandomForecast(PressureZone zone)
        {
            var data = GetZoneData(zone);
            if (data.forecasts == null || data.forecasts.Length == 0) return "";
            return data.forecasts[Random.Range(0, data.forecasts.Length)];
        }
    }
}
