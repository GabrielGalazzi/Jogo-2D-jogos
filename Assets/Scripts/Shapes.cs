using UnityEngine;

namespace RicochetPoC
{
    /// <summary>Runtime-generated placeholder art, so the PoC needs no imported assets.</summary>
    public static class Shapes
    {
        static Sprite s_Square;
        static Sprite s_Circle;

        /// <summary>1×1 unit white square, pivot in the centre.</summary>
        public static Sprite Square
        {
            get
            {
                if (s_Square == null)
                {
                    var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, name = "PoC Square" };
                    var pixels = new Color32[16];
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    s_Square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                }
                return s_Square;
            }
        }

        /// <summary>1 unit diameter white circle, pivot in the centre.</summary>
        public static Sprite Circle
        {
            get
            {
                if (s_Circle == null)
                {
                    const int size = 32;
                    var tex = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, name = "PoC Circle" };
                    float r = size * 0.5f;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                    }
                    tex.Apply();
                    s_Circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                }
                return s_Circle;
            }
        }

        public static SpriteRenderer Box(Transform parent, string name, Color color, Vector2 localPosition, Vector2 size, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
