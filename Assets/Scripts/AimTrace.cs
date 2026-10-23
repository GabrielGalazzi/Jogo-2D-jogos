using UnityEngine;

namespace RicochetPoC
{
    /// <summary>Short-lived polyline that fades out — used to visualise the path an enemy's aim solver planned.</summary>
    public class AimTrace : MonoBehaviour
    {
        const float Width = 0.05f;

        SpriteRenderer[] m_Segments;
        Color m_Color;
        float m_Duration;
        float m_Age;

        public static void Show(Color color, float duration, params Vector2[] points)
        {
            var go = new GameObject("Aim Trace");
            var trace = go.AddComponent<AimTrace>();
            trace.m_Color = color;
            trace.m_Duration = duration;
            trace.m_Segments = new SpriteRenderer[points.Length - 1];
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 d = b - a;
                var sr = Shapes.Box(go.transform, "Segment", color, (a + b) * 0.5f, new Vector2(d.magnitude, Width), 15);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                trace.m_Segments[i] = sr;
            }
        }

        void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= m_Duration)
            {
                Destroy(gameObject);
                return;
            }
            var c = m_Color;
            c.a *= 1f - m_Age / m_Duration;
            foreach (var sr in m_Segments)
                sr.color = c;
        }
    }
}
