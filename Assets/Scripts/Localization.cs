using UnityEngine;

namespace RicochetPoC
{
    public enum Language
    {
        English,
        Portuguese,
    }

    /// <summary>
    /// The language all player-facing text is shown in. It's picked on the main menu and remembered between sessions;
    /// until the player picks one, it follows the system language (Portuguese on a Portuguese system, English otherwise).
    /// </summary>
    public static class Lang
    {
        const string PrefsKey = "RicochetPoC.Language";

        public static Language Current { get; private set; }

        /// <summary>Reads the saved choice, falling back to the system language.</summary>
        public static void Load()
        {
            int saved = PlayerPrefs.GetInt(PrefsKey, -1);
            Current = System.Enum.IsDefined(typeof(Language), saved) ? (Language)saved
                : Application.systemLanguage == SystemLanguage.Portuguese ? Language.Portuguese
                : Language.English;
        }

        public static void Set(Language language)
        {
            Current = language;
            PlayerPrefs.SetInt(PrefsKey, (int)language);
            PlayerPrefs.Save();
        }

        /// <summary>Whichever of the two translations matches the current language.</summary>
        public static string Pick(string english, string portuguese) => Current == Language.Portuguese ? portuguese : english;
    }

    /// <summary>Player-facing text in every language the game supports. Converts to the current language's version.</summary>
    public readonly struct LocalizedText
    {
        public readonly string english;
        public readonly string portuguese;

        public LocalizedText(string english, string portuguese)
        {
            this.english = english;
            this.portuguese = portuguese;
        }

        public override string ToString() => Lang.Pick(english, portuguese);

        public static implicit operator string(LocalizedText text) => text.ToString();
    }
}
