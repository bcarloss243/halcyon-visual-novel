using UnityEngine;
using UnityEngine.UI;

namespace HalcyonAcademy
{
    /// <summary>
    /// Procedurally generates an arc mesh for a single pressure zone on the gauge face.
    /// Attach to a UI GameObject with a RectTransform. It draws a filled arc segment
    /// like the colored bands on an analog instrument.
    /// 
    /// The gauge uses four of these, one per zone:
    ///   Crisis:     startAngle=150,  endAngle=75     (far left)
    ///   Elevated:   startAngle=75,   endAngle=0      (center-left)
    ///   Manageable: startAngle=0,    endAngle=-75     (center-right)
    ///   Clarity:    startAngle=-75,  endAngle=-150    (far right)
    /// 
    /// Angles: 0° = top, positive = clockwise (Unity UI convention).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GaugeArc : Graphic
    {
        [Header("Arc Geometry")]
        [SerializeField] private float _startAngle = 150f;
        [SerializeField] private float _endAngle = 75f;
        [SerializeField] private float _outerRadius = 90f;
        [SerializeField] private float _innerRadius = 78f;
        [SerializeField, Range(2, 64)] private int _segments = 24;

        [Header("Appearance")]
        [SerializeField, Range(0f, 1f)] private float _opacity = 0.55f;
        [SerializeField] private bool _useGradient = true;
        [SerializeField] private Color _gradientEnd = Color.white;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float startRad = _startAngle * Mathf.Deg2Rad;
            float endRad = _endAngle * Mathf.Deg2Rad;
            float step = (endRad - startRad) / _segments;

            Color baseColor = color;
            baseColor.a *= _opacity;
            Color endColor = _useGradient ? _gradientEnd : baseColor;
            endColor.a *= _opacity;

            for (int i = 0; i <= _segments; i++)
            {
                float angle = startRad + step * i;

                // Unity UI: angle measured from top, positive = CW
                // Convert to standard trig: x = sin(angle), y = cos(angle)
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                float t = (float)i / _segments;
                Color vertColor = Color.Lerp(baseColor, endColor, t);

                Vector2 outerPos = new Vector2(sin * _outerRadius, cos * _outerRadius);
                Vector2 innerPos = new Vector2(sin * _innerRadius, cos * _innerRadius);

                vh.AddVert(outerPos, vertColor, Vector2.zero);
                vh.AddVert(innerPos, vertColor, Vector2.zero);

                if (i > 0)
                {
                    int idx = i * 2;
                    vh.AddTriangle(idx - 2, idx - 1, idx);
                    vh.AddTriangle(idx - 1, idx + 1, idx);
                }
            }
        }

        // Allow the arc to be configured at runtime
        public void SetArc(float startAngle, float endAngle, Color arcColor, float opacity = 0.55f)
        {
            _startAngle = startAngle;
            _endAngle = endAngle;
            color = arcColor;
            _opacity = opacity;
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
