using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// Short-lived sprite effects: flipbooks that play once (sparks, bursts, splashes), paint splat decals left on surfaces,
    /// and fading after-images for trails. None of them has a collider, so nothing here affects play.
    /// Everything spawned lives under the current level, so it goes when the level is torn down.
    /// </summary>
    public class SpriteFx : MonoBehaviour
    {
        /// <summary>Most splat decals kept at once; the oldest goes first.</summary>
        const int MaxDecals = 80;

        static readonly Queue<GameObject> s_Decals = new Queue<GameObject>();

        /// <summary>Where effects are parented: the current level, so they're cleared with it.</summary>
        public static Transform Root;

        Flipbook m_Book;
        SpriteRenderer m_Renderer;
        float m_Age;
        float m_FadeTime;
        float m_StartAlpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Decals.Clear();
            Root = null;
        }

        static SpriteRenderer Make(string name, Vector2 position, Sprite sprite, string layer, int order)
        {
            var go = new GameObject(name);
            if (Root != null)
                go.transform.SetParent(Root, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            SortLayers.Set(sr, layer, order);
            return sr;
        }

        /// <summary>Plays <paramref name="book"/> once at <paramref name="position"/>, then removes it.</summary>
        public static void Play(Flipbook book, Vector2 position, int order = 10, bool flipX = false, Color? tint = null)
        {
            if (book == null || !book.IsValid)
                return;
            var sr = Make("FX", position, book.frames[0], SortLayers.Effects, order);
            sr.flipX = flipX;
            if (tint.HasValue)
                sr.color = tint.Value;
            var fx = sr.gameObject.AddComponent<SpriteFx>();
            fx.m_Book = book;
            fx.m_Renderer = sr;
        }

        /// <summary>
        /// Leaves a paint splat on a surface. <paramref name="normal"/> points out of the surface; the splat sits just inside
        /// it, on the object's face, rotated a random quarter turn so the pixels stay on the grid.
        /// </summary>
        public static void Splat(Sprite[] variants, Vector2 contact, Vector2 normal)
        {
            if (variants == null || variants.Length == 0)
                return;
            var sprite = variants[Random.Range(0, variants.Length)];
            if (sprite == null)
                return;
            float inset = Mathf.Min(sprite.bounds.extents.x, sprite.bounds.extents.y) * 0.6f;
            var sr = Make("Splat", contact - normal.normalized * inset, sprite, SortLayers.Objects, 5);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
            s_Decals.Enqueue(sr.gameObject);
            while (s_Decals.Count > MaxDecals)
            {
                var old = s_Decals.Dequeue();
                if (old != null)
                    Destroy(old);
            }
        }

        /// <summary>A copy of <paramref name="sprite"/> at <paramref name="position"/> that fades out over <paramref name="time"/> seconds.</summary>
        public static void Ghost(Sprite sprite, Vector2 position, float time, float alpha, int order)
        {
            if (sprite == null)
                return;
            var sr = Make("Trail", position, sprite, SortLayers.Effects, order);
            sr.color = new Color(1f, 1f, 1f, alpha);
            var fx = sr.gameObject.AddComponent<SpriteFx>();
            fx.m_Renderer = sr;
            fx.m_FadeTime = time;
            fx.m_StartAlpha = alpha;
        }

        /// <summary>
        /// The death animation of a character that has just been eliminated: its art, left where it fell (same position,
        /// rotation and facing), plays its Dead state, holds, then fades away. Gameplay is already done with the character.
        /// </summary>
        public static void Corpse(CharacterArt art, Transform body, Vector2 feetLocal, bool flipX, float hold = 0.9f, float fade = 0.5f)
        {
            if (art == null || !art.IsValid)
                return;
            var root = new GameObject("Corpse").transform;
            if (Root != null)
                root.SetParent(Root, false);
            root.SetPositionAndRotation(body.position, body.rotation);
            var visual = CharacterVisual.Create(root, art, feetLocal.y, 0);
            visual.Face(!flipX);
            visual.Animator.SetBool(CharacterVisual.Dead, true);
            var fx = visual.gameObject.AddComponent<SpriteFx>();
            fx.m_Renderer = visual.Renderer;
            fx.m_Hold = hold;
            fx.m_FadeTime = fade;
            fx.m_StartAlpha = 1f;
            fx.m_DestroyRoot = root.gameObject;
        }

        float m_Hold;
        GameObject m_DestroyRoot;

        void Update()
        {
            if (m_Hold > 0f)
            {
                m_Hold -= Time.deltaTime;
                return;
            }
            m_Age += Time.deltaTime;
            if (m_Book != null)
            {
                int frame = Mathf.FloorToInt(m_Age * m_Book.fps);
                if (frame >= m_Book.frames.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                m_Renderer.sprite = m_Book.frames[frame];
                return;
            }
            if (m_FadeTime > 0f)
            {
                if (m_Age >= m_FadeTime)
                {
                    Destroy(m_DestroyRoot != null ? m_DestroyRoot : gameObject);
                    return;
                }
                var c = m_Renderer.color;
                c.a = m_StartAlpha * (1f - m_Age / m_FadeTime);
                m_Renderer.color = c;
            }
        }
    }
}
