using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// Plays the game's sounds: one-shots from a small pool of 2D audio sources, and one music track at a time.
    /// Clips come from the art library; a missing clip is simply silent. Music sits well below the effects.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Voices = 16;
        /// <summary>Effects at full volume; music sits clearly below them.</summary>
        public const float MusicVolume = 0.32f;

        static Sfx s_Instance;

        AudioSource[] m_Voices;
        AudioSource m_Music;
        int m_Next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Instance = null;

        static Sfx Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var go = new GameObject("Audio");
                    DontDestroyOnLoad(go);
                    s_Instance = go.AddComponent<Sfx>();
                }
                return s_Instance;
            }
        }

        void Awake()
        {
            m_Voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
                m_Voices[i] = NewSource();
            m_Music = NewSource();
            m_Music.loop = true;
            m_Music.volume = MusicVolume;
            m_Music.priority = 0;
        }

        AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Plays <paramref name="clip"/> once, its pitch varied by up to ±<paramref name="pitchJitter"/> so repeats don't drone.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f, float pitchJitter = 0.04f)
        {
            if (clip == null || !Application.isPlaying)
                return;
            var self = Instance;
            var voice = self.m_Voices[self.m_Next];
            self.m_Next = (self.m_Next + 1) % Voices;
            voice.Stop();
            voice.clip = clip;
            voice.volume = volume;
            voice.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            voice.Play();
        }

        /// <summary>One clip picked at random from <paramref name="clips"/>.</summary>
        public static void PlayAny(AudioClip[] clips, float volume = 1f, float pitch = 1f, float pitchJitter = 0.05f)
        {
            if (clips == null || clips.Length == 0)
                return;
            Play(clips[Random.Range(0, clips.Length)], volume, pitch, pitchJitter);
        }

        /// <summary>Loops <paramref name="clip"/> as the music, unless it's already playing.</summary>
        public static void Music(AudioClip clip)
        {
            if (!Application.isPlaying)
                return;
            var music = Instance.m_Music;
            if (clip == null)
            {
                music.Stop();
                return;
            }
            if (music.clip == clip && music.isPlaying)
                return;
            music.clip = clip;
            music.Play();
        }
    }
}
