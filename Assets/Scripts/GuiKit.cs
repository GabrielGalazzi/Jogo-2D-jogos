using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RicochetPoC
{
    /// <summary>
    /// Immediate-mode GUI drawing shared by the menus and the tutorial boards: rounded panels, buttons, keycaps and a mouse.
    /// Everything is tinted by <see cref="GUI.color"/>, so lowering its alpha fades whatever is drawn. Call only from OnGUI.
    /// </summary>
    public static class GuiKit
    {
        public enum ButtonKind
        {
            /// <summary>Filled with the accent colour: the main action, or the selected option.</summary>
            Primary,
            /// <summary>Dark, with an outline.</summary>
            Secondary,
            /// <summary>Just an outline: an option that isn't selected.</summary>
            Ghost,
        }

        public static readonly Color Accent = new Color(0.2f, 0.45f, 0.95f);
        public static readonly Color MutedText = new Color(0.72f, 0.75f, 0.82f);
        static readonly Color SecondaryFill = new Color(0.13f, 0.14f, 0.19f, 0.95f);
        static readonly Color KeyFace = new Color(0.92f, 0.93f, 0.95f);
        static readonly Color KeyEdge = new Color(0.5f, 0.53f, 0.6f);
        static readonly Color KeyText = new Color(0.12f, 0.13f, 0.16f);

        static readonly Dictionary<int, GUIStyle> s_Styles = new Dictionary<int, GUIStyle>();

        /// <summary>Scale for sizes laid out for the 1680×840 window: 1 at that size, smaller in a smaller window.</summary>
        public static float Unit => Mathf.Min(Screen.width / 1680f, Screen.height / 840f);

        /// <summary>A white label style of <paramref name="size"/> pixels with no padding. Cached, so don't modify it.</summary>
        public static GUIStyle Text(float size, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true)
        {
            int px = Mathf.Clamp(Mathf.RoundToInt(size), 8, 1023);
            int key = px | (int)fontStyle << 10 | (int)anchor << 13 | (wrap ? 1 << 18 : 0);
            if (!s_Styles.TryGetValue(key, out var style) || style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = px,
                    fontStyle = fontStyle,
                    alignment = anchor,
                    wordWrap = wrap,
                    padding = new RectOffset(),
                    margin = new RectOffset(),
                    clipping = TextClipping.Overflow,
                };
                style.normal.textColor = Color.white;
                s_Styles[key] = style;
            }
            return style;
        }

        public static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            var previous = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(rect, text, style);
            GUI.contentColor = previous;
        }

        /// <summary>Filled rectangle with corners rounded to <paramref name="radius"/> pixels.</summary>
        public static void Fill(Rect rect, Color color, float radius = 0f)
        {
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color * GUI.color, 0f, radius);
        }

        /// <summary>Filled rectangle with its own radius per corner: top-left, top-right, bottom-right, bottom-left.</summary>
        public static void Fill(Rect rect, Color color, Vector4 radii)
        {
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color * GUI.color, Vector4.zero, radii);
        }

        /// <summary>Rounded outline <paramref name="width"/> pixels thick, drawn inside <paramref name="rect"/>.</summary>
        public static void Outline(Rect rect, Color color, float width, float radius = 0f)
        {
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color * GUI.color, width, radius);
        }

        /// <summary>Straight line from <paramref name="a"/> to <paramref name="b"/>.</summary>
        public static void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y - width * 0.5f, d.magnitude, width), color);
            GUI.matrix = matrix;
        }

        /// <summary>Draws <paramref name="sprite"/> as large as fits in <paramref name="rect"/>, keeping its shape.</summary>
        public static void DrawSprite(Rect rect, Sprite sprite)
        {
            var texture = sprite.texture;
            // A tightly packed sprite has no rectangle on its atlas; its rect in the source image is the best there is.
            Rect source = sprite.packed && sprite.packingMode == SpritePackingMode.Tight ? sprite.rect : sprite.textureRect;
            float aspect = source.width / source.height;
            if (aspect > rect.width / rect.height)
            {
                float height = rect.width / aspect;
                rect = new Rect(rect.x, rect.y + (rect.height - height) * 0.5f, rect.width, height);
            }
            else
            {
                float width = rect.height * aspect;
                rect = new Rect(rect.x + (rect.width - width) * 0.5f, rect.y, width, rect.height);
            }
            var uv = new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, uv);
        }

        /// <summary>A button that lightens under the mouse and darkens while held. Returns true when it's clicked.</summary>
        public static bool Button(Rect rect, string text, ButtonKind kind, float fontSize)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            bool held = hover && Mouse.current != null && Mouse.current.leftButton.isPressed;
            float radius = rect.height * 0.2f;

            if (!PixelButton(rect, kind, hover, held))
            {
                Color fill = kind == ButtonKind.Primary ? Accent : kind == ButtonKind.Secondary ? SecondaryFill : new Color(1f, 1f, 1f, 0f);
                if (hover)
                    fill = kind == ButtonKind.Ghost ? new Color(1f, 1f, 1f, 0.08f) : Color.Lerp(fill, Color.white, 0.15f);
                if (held)
                    fill = Color.Lerp(fill, Color.black, 0.2f);
                Fill(rect, fill, radius);
                if (kind != ButtonKind.Primary)
                    Outline(rect, new Color(1f, 1f, 1f, hover ? 0.55f : 0.3f), Mathf.Max(1f, rect.height * 0.03f), radius);
            }

            Label(rect, text, Text(fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, false), kind == ButtonKind.Ghost && !hover ? MutedText : Color.white);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked && Art.Lib != null)
                Sfx.Play(Art.Lib.uiClick, 0.7f, 1f, 0f);
            return clicked;
        }

        static readonly Dictionary<Texture2D, GUIStyle> s_PixelButtons = new Dictionary<Texture2D, GUIStyle>();

        /// <summary>Draws the button as the art library's 9-sliced pixel panel. Returns false if there's no art for it.</summary>
        static bool PixelButton(Rect rect, ButtonKind kind, bool hover, bool held)
        {
            var lib = Art.Lib;
            if (lib == null)
                return false;
            var texture = kind == ButtonKind.Primary
                ? (held ? lib.buttonPressed : hover ? lib.buttonHover : lib.buttonNormal)
                : lib.buttonGhost;
            if (texture == null)
                return false;
            if (Event.current.type != EventType.Repaint)
                return true;
            if (!s_PixelButtons.TryGetValue(texture, out var style) || style == null)
            {
                style = new GUIStyle { border = new RectOffset(6, 6, 6, 6) };
                style.normal.background = texture;
                s_PixelButtons[texture] = style;
            }
            var previous = GUI.color;
            if (kind != ButtonKind.Primary && hover)
                GUI.color = previous * new Color(1.35f, 1.35f, 1.35f, 1f);
            style.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = previous;
            return true;
        }

        /// <summary>Width of the keycap <see cref="DrawKeycap"/> draws for <paramref name="label"/>.</summary>
        public static float KeycapWidth(string label, float height) =>
            Mathf.Max(height, KeycapStyle(height).CalcSize(new GUIContent(label)).x + height * 0.6f);

        /// <summary>A keyboard key with <paramref name="label"/> on it, its top-left corner at <paramref name="position"/>. Returns its width.</summary>
        public static float DrawKeycap(Vector2 position, float height, string label)
        {
            var rect = new Rect(position.x, position.y, KeycapWidth(label, height), height);
            float radius = height * 0.2f;
            var face = new Rect(rect.x, rect.y, rect.width, rect.height - Mathf.Max(2f, height * 0.13f));
            Fill(rect, KeyEdge, radius);
            Fill(face, KeyFace, radius);
            Label(face, label, KeycapStyle(height), KeyText);
            return rect.width;
        }

        static GUIStyle KeycapStyle(float height) => Text(height * 0.5f, FontStyle.Bold, TextAnchor.MiddleCenter, false);

        /// <summary>
        /// A mouse <paramref name="height"/> tall, its top-left corner at <paramref name="position"/>, with its left button
        /// filled in <paramref name="highlight"/> when <paramref name="leftClick"/> is set. Returns its width.
        /// </summary>
        public static float DrawMouse(Vector2 position, float height, bool leftClick, Color highlight)
        {
            float width = height * 0.68f, radius = width * 0.5f;
            var body = new Rect(position.x, position.y, width, height);
            float buttons = height * 0.42f;
            float line = Mathf.Max(1f, height * 0.06f);
            Fill(body, KeyFace, radius);
            if (leftClick)
                Fill(new Rect(body.x, body.y, width * 0.5f, buttons), highlight, new Vector4(radius, 0f, 0f, 0f));
            Fill(new Rect(body.center.x - line * 0.5f, body.y, line, buttons), KeyEdge);
            Fill(new Rect(body.x, body.y + buttons - line * 0.5f, width, line), KeyEdge);
            return width;
        }
    }
}
