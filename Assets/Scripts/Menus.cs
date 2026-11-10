using UnityEngine;

namespace RicochetPoC
{
    /// <summary>The main menu, the death menu, the pause screen and the victory screen, drawn with immediate-mode GUI. Call from OnGUI.</summary>
    public static class Menus
    {
        public enum DeathChoice
        {
            None,
            Retry,
            MainMenu,
        }

        static readonly Color DeathColor = new Color(0.95f, 0.32f, 0.34f);
        static readonly Color VictoryColor = new Color(1f, 0.82f, 0.25f);
        static readonly Color OverlayColor = new Color(0.03f, 0.03f, 0.05f, 0.72f);
        const float ShotSpeed = 520f;  // pixels per second at 1680×840, for the shot ricocheting under the title
        const int RicochetBounces = 5;

        /// <summary>
        /// The game's title, a Play button centred in the lower part of the screen, and the language picker below it.
        /// Picking a language applies it straight away. Returns true when Play is clicked.
        /// </summary>
        public static bool Main(string gameTitle)
        {
            float u = GuiKit.Unit;
            float centerX = Screen.width * 0.5f;

            var titleStyle = GuiKit.Text(112f * u, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            var titleRect = new Rect(0f, Screen.height * 0.17f, Screen.width, 130f * u);
            GuiKit.Label(titleRect, gameTitle, titleStyle, Color.white);
            float titleWidth = titleStyle.CalcSize(new GUIContent(gameTitle)).x;
            Ricochet(new Rect(centerX - titleWidth * 0.5f, titleRect.yMax + 10f * u, titleWidth, 48f * u), u);

            var play = new Rect(centerX - 150f * u, Screen.height * 0.66f - 35f * u, 300f * u, 70f * u);
            bool start = GuiKit.Button(play, Lang.Pick("PLAY", "JOGAR"), GuiKit.ButtonKind.Primary, 30f * u);

            float y = play.yMax + 40f * u;
            GuiKit.Label(new Rect(0f, y, Screen.width, 22f * u), Lang.Pick("Language", "Idioma"),
                GuiKit.Text(16f * u, FontStyle.Normal, TextAnchor.MiddleCenter, false), GuiKit.MutedText);
            y += 30f * u;
            LanguageButton(new Rect(centerX - 176f * u, y, 168f * u, 44f * u), "English", Language.English, u);
            LanguageButton(new Rect(centerX + 8f * u, y, 168f * u, 44f * u), "Português", Language.Portuguese, u);
            return start;
        }

        static void LanguageButton(Rect rect, string name, Language language, float u)
        {
            var kind = Lang.Current == language ? GuiKit.ButtonKind.Primary : GuiKit.ButtonKind.Ghost;
            if (GuiKit.Button(rect, name, kind, 18f * u) && Lang.Current != language)
                Lang.Set(language);
        }

        /// <summary>A shot bouncing between the top and bottom of <paramref name="area"/>, leaving its path drawn faintly behind it.</summary>
        static void Ricochet(Rect area, float u)
        {
            var points = new Vector2[RicochetBounces + 2];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(area.x + area.width * i / (points.Length - 1), i % 2 == 0 ? area.y : area.yMax);

            float length = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                GuiKit.Line(points[i - 1], points[i], Mathf.Max(1f, 2f * u), new Color(GuiKit.Accent.r, GuiKit.Accent.g, GuiKit.Accent.b, 0.35f));
                length += Vector2.Distance(points[i - 1], points[i]);
            }

            // Runs the path, then waits off its end for a moment before the next shot.
            float travelled = Mathf.Repeat(Time.unscaledTime * ShotSpeed * u, length * 1.35f);
            const int trail = 8;
            var previous = GUI.color;
            for (int t = trail; t >= 0; t--)
            {
                float at = travelled - t * 7f * u;
                if (at < 0f || at > length)
                    continue;
                float size = (t == 0 ? 11f : 9f - t * 0.6f) * u;
                GUI.color = new Color(1f, 0.92f, 0.55f, previous.a * (1f - t / (float)(trail + 1)));
                GUI.DrawTexture(new Rect(PointAlong(points, at) - Vector2.one * size * 0.5f, Vector2.one * size), Shapes.Circle.texture);
            }
            GUI.color = previous;
        }

        static Vector2 PointAlong(Vector2[] points, float distance)
        {
            for (int i = 1; i < points.Length; i++)
            {
                float segment = Vector2.Distance(points[i - 1], points[i]);
                if (distance <= segment)
                    return Vector2.Lerp(points[i - 1], points[i], distance / segment);
                distance -= segment;
            }
            return points[points.Length - 1];
        }

        /// <summary>
        /// Dims the screen and offers Retry with Main menu right below it. <paramref name="shown"/> fades it in from 0 to 1;
        /// the buttons only take clicks once it's fully shown, so a shot fired just as the player died can't press one.
        /// </summary>
        public static DeathChoice Death(float shown, string levelName)
        {
            float u = GuiKit.Unit;
            var previous = Header(shown, Lang.Pick("YOU DIED", "VOCÊ MORREU"), DeathColor, levelName);

            var retry = ButtonRect(u);
            var mainMenu = new Rect(retry.x, retry.yMax + 16f * u, retry.width, retry.height);
            var choice = DeathChoice.None;
            if (GuiKit.Button(retry, Lang.Pick("Retry", "Tentar de novo"), GuiKit.ButtonKind.Primary, 24f * u))
                choice = DeathChoice.Retry;
            if (GuiKit.Button(mainMenu, Lang.Pick("Main menu", "Menu principal"), GuiKit.ButtonKind.Secondary, 24f * u))
                choice = DeathChoice.MainMenu;

            GUI.color = previous;
            return shown >= 1f ? choice : DeathChoice.None;
        }

        /// <summary>Dims the frozen level and offers Main menu, with a reminder that Esc resumes. Returns true when Main menu is clicked.</summary>
        public static bool Pause(string levelName) =>
            OneButton(1f, Lang.Pick("PAUSED", "PAUSADO"), Color.white, levelName, Lang.Pick("Press Esc to resume", "Aperte Esc para continuar"));

        /// <summary>
        /// Shown once the boss has fallen: dims the frozen arena and offers Main menu. <paramref name="shown"/> fades it in
        /// from 0 to 1, and the button only takes clicks once it's fully shown. Returns true when Main menu is clicked.
        /// </summary>
        public static bool Victory(float shown) =>
            OneButton(shown, Lang.Pick("VICTORY!", "VITÓRIA!"), VictoryColor, Lang.Pick("The Warden has fallen.", "O Guardião caiu."), null);

        /// <summary>A screen with a single Main menu button, and an optional <paramref name="caption"/> below it.</summary>
        static bool OneButton(float shown, string title, Color titleColor, string subtitle, string caption)
        {
            float u = GuiKit.Unit;
            var previous = Header(shown, title, titleColor, subtitle);

            var mainMenu = ButtonRect(u);
            bool clicked = GuiKit.Button(mainMenu, Lang.Pick("Main menu", "Menu principal"), GuiKit.ButtonKind.Primary, 24f * u);
            if (caption != null)
                GuiKit.Label(new Rect(0f, mainMenu.yMax + 18f * u, Screen.width, 26f * u), caption,
                    GuiKit.Text(18f * u, FontStyle.Normal, TextAnchor.MiddleCenter, false), GuiKit.MutedText);

            GUI.color = previous;
            return clicked && shown >= 1f;
        }

        /// <summary>
        /// Fades GUI drawing to <paramref name="shown"/>, dims the screen, and draws the title with the subtitle under it.
        /// Returns the GUI colour to put back once the screen is drawn.
        /// </summary>
        static Color Header(float shown, string title, Color titleColor, string subtitle)
        {
            float u = GuiKit.Unit;
            var previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * shown);

            GuiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), OverlayColor);
            var titleRect = new Rect(0f, Screen.height * 0.24f, Screen.width, 110f * u);
            GuiKit.Label(titleRect, title, GuiKit.Text(92f * u, FontStyle.Bold, TextAnchor.MiddleCenter, false), titleColor);
            GuiKit.Label(new Rect(0f, titleRect.yMax, Screen.width, 30f * u), subtitle,
                GuiKit.Text(20f * u, FontStyle.Normal, TextAnchor.MiddleCenter, false), GuiKit.MutedText);
            return previous;
        }

        /// <summary>Where a screen's first button goes: centred, a little below the middle.</summary>
        static Rect ButtonRect(float u) => new Rect(Screen.width * 0.5f - 160f * u, Screen.height * 0.56f, 320f * u, 64f * u);
    }
}
