using System;
using UnityEngine;

namespace RicochetPoC
{
    /// <summary>The animated pictures on the tutorial boards' cards: the player doing what a card teaches, or the item it introduces.</summary>
    public enum TutorialImage
    {
        Walk,
        Sprint,
        Jump,
        Duck,
        Aim,
        Shoot,
        Pistol,
        Magazine,
        Ricochet,
        TwinGun,
        SwitchGuns,
        TwinShot,
        TopBarrel,
    }

    /// <summary>
    /// The animated pictures on the tutorial boards' cards, in the art library (Assets/Art/UI/Tutorial/tut_*.png, rendered
    /// by Tools/ArtGen/tutorial_gifs.py). A card whose animation is missing shows an empty frame captioned with what
    /// belongs there.
    /// </summary>
    [Serializable]
    public class TutorialArt
    {
        [Header("Level 1: controls and pistol")]
        [Tooltip("The player walking right, then back left.")] public Flipbook walk = new Flipbook();
        [Tooltip("The player walking in, then sprinting off.")] public Flipbook sprint = new Flipbook();
        [Tooltip("The player jumping over a low shot.")] public Flipbook jump = new Flipbook();
        [Tooltip("The player ducking behind a bench under a shot.")] public Flipbook duck = new Flipbook();
        [Tooltip("The player aiming around, the laser sight bouncing off the floor and the wall.")] public Flipbook aim = new Flipbook();
        [Tooltip("The player firing shots that bounce off the floor.")] public Flipbook shoot = new Flipbook();
        [Tooltip("The pistol firing one shot per click.")] public Flipbook pistol = new Flipbook();
        [Tooltip("The HUD magazine emptying, then reloading by itself.")] public Flipbook magazine = new Flipbook();
        [Tooltip("A direct hit doing nothing, then a shot off the floor eliminating the enemy.")] public Flipbook ricochet = new Flipbook();

        [Header("Level 6: twin gun")]
        [Tooltip("The player walking into the twin gun pickup.")] public Flipbook twinGun = new Flipbook();
        [Tooltip("The player switching from the pistol to the twin gun and back.")] public Flipbook switchGuns = new Flipbook();
        [Tooltip("The player firing both of the twin gun's barrels.")] public Flipbook twinShot = new Flipbook();
        [Tooltip("The player firing only the twin gun's top barrel, with one round left.")] public Flipbook topBarrel = new Flipbook();

        /// <summary>True when every card has its animation.</summary>
        public bool IsComplete
        {
            get
            {
                foreach (TutorialImage image in Enum.GetValues(typeof(TutorialImage)))
                    if (!Get(image).IsValid)
                        return false;
                return true;
            }
        }

        public Flipbook Get(TutorialImage image)
        {
            switch (image)
            {
                case TutorialImage.Walk: return walk;
                case TutorialImage.Sprint: return sprint;
                case TutorialImage.Jump: return jump;
                case TutorialImage.Duck: return duck;
                case TutorialImage.Aim: return aim;
                case TutorialImage.Shoot: return shoot;
                case TutorialImage.Pistol: return pistol;
                case TutorialImage.Magazine: return magazine;
                case TutorialImage.Ricochet: return ricochet;
                case TutorialImage.TwinGun: return twinGun;
                case TutorialImage.SwitchGuns: return switchGuns;
                case TutorialImage.TwinShot: return twinShot;
                default: return topBarrel;
            }
        }
    }

    /// <summary>A key, or the mouse, drawn on a tutorial card.</summary>
    public readonly struct TutorialInput
    {
        /// <summary>Label on the keycap; null for the mouse.</summary>
        public readonly string key;
        /// <summary>For the mouse: whether its left button is highlighted.</summary>
        public readonly bool leftClick;

        TutorialInput(string key, bool leftClick)
        {
            this.key = key;
            this.leftClick = leftClick;
        }

        public static TutorialInput Key(string label) => new TutorialInput(label, false);
        public static readonly TutorialInput Mouse = new TutorialInput(null, false);
        public static readonly TutorialInput LeftClick = new TutorialInput(null, true);
    }

    public class TutorialCard
    {
        public TutorialImage image;
        /// <summary>Shown in the picture's frame until a picture is set.</summary>
        public LocalizedText imageCaption;
        public LocalizedText title;
        public LocalizedText text;
        public TutorialInput[] inputs;
        /// <summary>Spans the board's full width on a row of its own, tinted in the board's accent colour: for a key rule.</summary>
        public bool wide;
    }

    /// <summary>
    /// A panel of cards teaching the controls and the pistol (level 1) or the twin gun (level 6). Each card has a picture,
    /// the keys it uses drawn as keycaps, and a short explanation. GameBootstrap shows it in the top-right corner of the
    /// screen, right of the wall between the safe zone and the room, while the player is in the safe zone.
    /// </summary>
    public class TutorialBoard
    {
        const int Columns = 2;

        // Sizes in pixels at 1680×840, scaled with the window.
        const float Pad = 16f;
        const float HeaderHeight = 32f;
        const float HeaderFont = 21f;
        const float Gap = 10f;
        const float CardWidth = 380f;
        const float CardPad = 10f;
        const float ImageWidth = 100f;
        const float ImageHeight = 75f;
        const float ImageGap = 12f;
        const float InputHeight = 26f;
        const float InputGap = 5f;
        const float TitleFont = 17f;
        const float TextFont = 14f;
        const float TextGap = 6f;
        const float CaptionFont = 11f;

        static readonly Color BoardColor = new Color(0.07f, 0.08f, 0.11f, 0.97f);
        static readonly Color CardColor = new Color(1f, 1f, 1f, 0.045f);
        static readonly Color SlotColor = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color SlotLineColor = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color CaptionColor = new Color(1f, 1f, 1f, 0.4f);
        static readonly Color BodyColor = new Color(0.8f, 0.83f, 0.9f);

        public readonly LocalizedText title;
        public readonly Color accent;
        public readonly TutorialCard[] cards;

        Rect[] m_CardRects;

        TutorialBoard(LocalizedText title, Color accent, params TutorialCard[] cards)
        {
            this.title = title;
            this.accent = accent;
            this.cards = cards;
        }

        /// <summary>Level 1: the controls, and the pistol the player starts with.</summary>
        public static TutorialBoard Controls() => new TutorialBoard(new LocalizedText("HOW TO PLAY", "COMO JOGAR"), new Color(0.38f, 0.62f, 1f),
            Card(TutorialImage.Walk, "player walking", "jogador andando",
                "Move", "Andar",
                "Walk left and right.",
                "Ande para a esquerda e para a direita.",
                TutorialInput.Key("A"), TutorialInput.Key("D")),
            Card(TutorialImage.Sprint, "player sprinting", "jogador correndo",
                "Sprint", "Correr",
                "Hold while moving to run faster.",
                "Segure enquanto anda para correr mais rápido.",
                TutorialInput.Key("Shift")),
            Card(TutorialImage.Jump, "player jumping", "jogador pulando",
                "Jump", "Pular",
                "Leap over low walls and incoming shots.",
                "Pule muros baixos e tiros que vêm na sua direção.",
                TutorialInput.Key("W")),
            Card(TutorialImage.Duck, "player ducking", "jogador agachado",
                "Duck", "Agachar",
                "Hold to become a smaller target and hide behind low cover. You move slower while ducking.",
                "Segure para virar um alvo menor e se esconder atrás de coberturas baixas. Agachado, você anda mais devagar.",
                TutorialInput.Key("S")),
            Card(TutorialImage.Aim, "player aiming, laser on", "jogador mirando, laser ligado",
                "Aim", "Mirar",
                "Point with the mouse. The laser shows your shot's path to its first bounce, and a little beyond.",
                "Aponte com o mouse. O laser mostra o caminho do tiro até o primeiro ricochete, e um pouco além.",
                TutorialInput.Mouse),
            Card(TutorialImage.Shoot, "player shooting", "jogador atirando",
                "Shoot", "Atirar",
                "Click to fire where you're aiming.",
                "Clique para atirar na direção da mira.",
                TutorialInput.LeftClick),
            Card(TutorialImage.Pistol, "pistol", "pistola",
                "Pistol", "Pistola",
                "Your starting gun. It fires one shot per click.",
                "Sua arma inicial. Dispara um tiro por clique."),
            Card(TutorialImage.Magazine, "magazine", "pente",
                "Magazine", "Pente",
                "Your rounds show at the bottom left. When they run out, the gun reloads by itself.",
                "Suas balas aparecem no canto inferior esquerdo. Quando acabam, a arma recarrega sozinha."),
            Wide(Card(TutorialImage.Ricochet, "shot bouncing into an enemy", "tiro ricocheteando até um inimigo",
                "Ricochet to kill", "Ricochete para matar",
                "Bullets only kill enemies after ricocheting off a surface at least once: the floor, the ceiling or a wall. A direct hit does nothing.",
                "As balas só matam inimigos depois de ricochetear em alguma superfície pelo menos uma vez: o chão, o teto ou uma parede. Um tiro direto não faz nada.")));

        /// <summary>Level 6: the twin gun waiting in the safe zone.</summary>
        public static TutorialBoard TwinGun() => new TutorialBoard(new LocalizedText("NEW WEAPON: TWIN GUN", "NOVA ARMA: ARMA DUPLA"), new Color(1f, 0.82f, 0.25f),
            Card(TutorialImage.TwinGun, "twin gun", "arma dupla",
                "Twin gun", "Arma dupla",
                "Walk into it to pick it up. It stays with you in every level after this one.",
                "Encoste nela para pegá-la. Ela continua com você em todas as fases seguintes."),
            Card(TutorialImage.SwitchGuns, "player switching guns", "jogador trocando de arma",
                "Switch guns", "Trocar de arma",
                "Swap between the pistol and the twin gun. Both share one magazine, and neither fires mid-swap.",
                "Alterne entre a pistola e a arma dupla. As duas usam o mesmo pente, e nenhuma atira durante a troca.",
                TutorialInput.Key("F")),
            Card(TutorialImage.TwinShot, "player firing both barrels", "jogador atirando com os dois canos",
                "Twin shot", "Tiro duplo",
                "Fires two shots that spread slightly apart, one round each. A laser marks each path.",
                "Dispara dois tiros que se abrem um pouco, gastando uma bala cada. Um laser marca cada caminho.",
                TutorialInput.LeftClick),
            Card(TutorialImage.TopBarrel, "player firing the top barrel", "jogador atirando com o cano de cima",
                "Last round", "Última bala",
                "With one round left, only the top barrel fires.",
                "Com só uma bala no pente, apenas o cano de cima dispara."));

        static TutorialCard Card(TutorialImage image, string caption, string captionPortuguese, string title, string titlePortuguese,
            string text, string textPortuguese, params TutorialInput[] inputs) => new TutorialCard
        {
            image = image,
            imageCaption = new LocalizedText(caption, captionPortuguese),
            title = new LocalizedText(title, titlePortuguese),
            text = new LocalizedText(text, textPortuguese),
            inputs = inputs,
        };

        static TutorialCard Wide(TutorialCard card)
        {
            card.wide = true;
            return card;
        }

        /// <summary>
        /// Draws the board in the top-right corner of the screen, <paramref name="margin"/> pixels in from the edges, and
        /// shrinks it if it would reach further left than <paramref name="minX"/>. Pictures come from <paramref name="art"/>.
        /// Returns the board's screen rect.
        /// </summary>
        public Rect Draw(float margin, float minX, TutorialArt art)
        {
            float u = GuiKit.Unit;
            Vector2 size = Layout(u, Vector2.zero, null);
            float room = Screen.width - margin - minX;
            if (size.x > room && room > 0f)
            {
                u *= room / size.x;
                size = Layout(u, Vector2.zero, null);
            }
            var board = new Rect(Screen.width - margin - size.x, margin, size.x, size.y);

            GuiKit.Fill(board, BoardColor, 14f * u);
            GuiKit.Outline(board, new Color(accent.r, accent.g, accent.b, 0.45f), Mathf.Max(1f, 1.5f * u), 14f * u);

            var header = new Rect(board.x + Pad * u, board.y + Pad * u, board.width - Pad * 2f * u, HeaderHeight * u);
            GuiKit.Fill(new Rect(header.x, header.y + header.height * 0.2f, 4f * u, header.height * 0.6f), accent, 2f * u);
            GuiKit.Label(new Rect(header.x + 14f * u, header.y, header.width - 14f * u, header.height), title,
                GuiKit.Text(HeaderFont * u, FontStyle.Bold, TextAnchor.MiddleLeft, false), Color.white);

            if (m_CardRects == null || m_CardRects.Length != cards.Length)
                m_CardRects = new Rect[cards.Length];
            Layout(u, board.position, m_CardRects);
            for (int i = 0; i < cards.Length; i++)
                DrawCard(cards[i], m_CardRects[i], u, art);
            return board;
        }

        void DrawCard(TutorialCard card, Rect rect, float u, TutorialArt art)
        {
            if (card.wide)
            {
                GuiKit.Fill(rect, new Color(accent.r, accent.g, accent.b, 0.12f), 8f * u);
                GuiKit.Outline(rect, new Color(accent.r, accent.g, accent.b, 0.5f), Mathf.Max(1f, u), 8f * u);
            }
            else
            {
                GuiKit.Fill(rect, CardColor, 8f * u);
            }

            var slot = new Rect(rect.x + CardPad * u, rect.center.y - ImageHeight * 0.5f * u, ImageWidth * u, ImageHeight * u);
            var book = art?.Get(card.image);
            if (book != null && book.IsValid)
            {
                // Loops on unscaled time, so it keeps playing whatever the time scale.
                int frame = Mathf.FloorToInt(Time.unscaledTime * book.fps) % book.frames.Length;
                var sprite = book.frames[frame] != null ? book.frames[frame] : book.frames[0];
                GuiKit.DrawSprite(slot, sprite);
                GuiKit.Outline(slot, SlotLineColor, Mathf.Max(1f, u));
            }
            else
            {
                GuiKit.Fill(slot, SlotColor, 6f * u);
                GuiKit.Outline(slot, SlotLineColor, Mathf.Max(1f, u), 6f * u);
                var inset = new Rect(slot.x + 6f * u, slot.y, slot.width - 12f * u, slot.height);
                GuiKit.Label(inset, card.imageCaption, GuiKit.Text(CaptionFont * u, FontStyle.Italic, TextAnchor.MiddleCenter), CaptionColor);
            }

            float x = slot.xMax + ImageGap * u, y = rect.y + CardPad * u;
            float right = rect.xMax - CardPad * u;
            foreach (var input in card.inputs)
            {
                x += input.key != null
                    ? GuiKit.DrawKeycap(new Vector2(x, y), InputHeight * u, input.key)
                    : GuiKit.DrawMouse(new Vector2(x, y), InputHeight * u, input.leftClick, accent);
                x += InputGap * u;
            }
            if (card.inputs.Length > 0)
                x += InputGap * u;
            GuiKit.Label(new Rect(x, y, right - x, InputHeight * u), card.title, GuiKit.Text(TitleFont * u, FontStyle.Bold, TextAnchor.MiddleLeft, false), Color.white);

            float textX = slot.xMax + ImageGap * u, textY = y + (InputHeight + TextGap) * u;
            GuiKit.Label(new Rect(textX, textY, right - textX, rect.yMax - textY), card.text, GuiKit.Text(TextFont * u), BodyColor);
        }

        /// <summary>
        /// Lays the cards out at scale <paramref name="u"/> on a board whose top-left corner is at <paramref name="origin"/>,
        /// filling <paramref name="rects"/> if given, and returns the board's size. Cards fill the grid left to right, and a
        /// wide card takes a row to itself. The other cards all share the height of the one with the most text.
        /// </summary>
        Vector2 Layout(float u, Vector2 origin, Rect[] rects)
        {
            float pad = Pad * u, gap = Gap * u, cardWidth = CardWidth * u;
            float gridWidth = Columns * cardWidth + (Columns - 1) * gap;
            float cardHeight = 0f;
            foreach (var card in cards)
                if (!card.wide)
                    cardHeight = Mathf.Max(cardHeight, CardHeight(card, cardWidth, u));

            float x = origin.x + pad, y = origin.y + pad + (HeaderHeight + Gap) * u;
            int column = 0;
            for (int i = 0; i < cards.Length; i++)
            {
                bool wide = cards[i].wide;
                if (wide && column > 0)
                {
                    y += cardHeight + gap;
                    column = 0;
                }
                float height = wide ? CardHeight(cards[i], gridWidth, u) : cardHeight;
                if (rects != null)
                    rects[i] = new Rect(x + column * (cardWidth + gap), y, wide ? gridWidth : cardWidth, height);
                if (wide || ++column == Columns)
                {
                    y += height + gap;
                    column = 0;
                }
            }
            if (column > 0)
                y += cardHeight + gap;
            return new Vector2(gridWidth + pad * 2f, y - gap + pad - origin.y);
        }

        /// <summary>Height a card <paramref name="width"/> pixels wide needs to fit its picture and its text.</summary>
        static float CardHeight(TutorialCard card, float width, float u)
        {
            float textWidth = width - (CardPad * 2f + ImageWidth + ImageGap) * u;
            float text = (InputHeight + TextGap) * u + GuiKit.Text(TextFont * u).CalcHeight(new GUIContent(card.text), textWidth);
            return Mathf.Max(ImageHeight * u, text) + CardPad * 2f * u;
        }
    }
}
