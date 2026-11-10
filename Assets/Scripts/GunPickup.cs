using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// The twin gun, floating at waist height in level 6's safe zone. Walking into it hands it to the player,
    /// who can then switch to it with F. It has no collider, so shots and the player pass straight through it.
    /// </summary>
    public class GunPickup : MonoBehaviour
    {
        [Tooltip("Size of the area around the pickup the player's hitbox has to touch to take it.")]
        public Vector2 reach = new Vector2(0.9f, 1.4f);
        public float bobHeight = 0.08f;
        public float bobSpeed = 3f;

        static readonly Color GlowColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color GripColor = new Color(0.15f, 0.15f, 0.18f);
        const float BarrelLength = 0.6f;
        const int SortingOrder = 3; // behind the player, in front of the safe zone door

        Vector2 m_Rest;
        Transform m_Art;
        SpriteRenderer m_Glow;

        void Awake()
        {
            m_Rest = transform.position;

            var lib = Art.Lib;
            if (lib != null && lib.twinPickup != null && lib.pickupGlow != null)
            {
                // The glow sprite (tinted gold, pulsing) behind the twin marker itself, centred on the pickup.
                m_Glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
                m_Glow.transform.SetParent(transform, false);
                m_Glow.sprite = lib.pickupGlow;
                SortLayers.Set(m_Glow, SortLayers.Effects, 0);
                m_Art = new GameObject("Twin Gun").transform;
                m_Art.SetParent(transform, false);
                var marker = new GameObject("Marker").AddComponent<SpriteRenderer>();
                marker.transform.SetParent(m_Art, false);
                // Centred on the pickup: the sprite's pivot is its grip, so shift by the grip's offset from the middle.
                marker.transform.localPosition = -(Vector2)lib.twinPickup.bounds.center;
                marker.sprite = lib.twinPickup;
                SortLayers.Set(marker, SortLayers.Effects, 1);
                return;
            }

            m_Glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            m_Glow.transform.SetParent(transform, false);
            m_Glow.transform.localScale = Vector3.one * 1.1f;
            m_Glow.sprite = Shapes.Circle;
            m_Glow.sortingOrder = SortingOrder - 1;
            var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(transform, false);
            ring.transform.localScale = Vector3.one * 1.1f;
            ring.sprite = Shapes.Ring;
            ring.color = GlowColor;
            ring.sortingOrder = SortingOrder - 1;

            // A grip and receiver behind the player's own twin barrels, centred on the pickup.
            m_Art = new GameObject("Twin Gun").transform;
            m_Art.SetParent(transform, false);
            var gun = new GameObject("Offset").transform;
            gun.SetParent(m_Art, false);
            gun.localPosition = new Vector2(-BarrelLength * 0.45f, 0.05f);
            Shapes.Box(gun, "Grip", GripColor, new Vector2(0.02f, -0.17f), new Vector2(0.12f, 0.26f), SortingOrder);
            Shapes.Box(gun, "Receiver", GripColor, new Vector2(0.06f, 0f), new Vector2(0.24f, 0.24f), SortingOrder);
            PlayerController.BuildBarrels(gun, PlayerGun.Twin, BarrelLength, SortingOrder);
        }

        void Update()
        {
            m_Art.localPosition = Vector2.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            var glow = GlowColor;
            glow.a = 0.3f + 0.15f * Mathf.Sin(Time.time * bobSpeed * 1.5f);
            m_Glow.color = glow;

            var player = PlayerController.Instance;
            if (player == null || player.IsDead || player.hasTwinGun)
                return;
            Bounds box = player.HitBox;
            Vector2 half = reach * 0.5f;
            if (Mathf.Abs(box.center.x - m_Rest.x) > box.extents.x + half.x || Mathf.Abs(box.center.y - m_Rest.y) > box.extents.y + half.y)
                return;

            player.hasTwinGun = true;
            if (Art.Lib != null)
            {
                SpriteFx.Play(Art.Lib.armFlash, m_Rest, 11, false, GlowColor);
                Sfx.Play(Art.Lib.gunSwitch, 0.8f, 1.2f, 0f);
            }
            Destroy(gameObject);
        }
    }
}
