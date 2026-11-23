using System.Collections;
using UnityEngine;

namespace Pucaci.UI
{
    /// <summary>
    /// Menghasilkan suasana malam desa horor yang mencekam di Main Menu:
    /// - Ambience angin malam dan dengung frekuensi rendah mistis
    /// - Efek suara jangkrik & binatang malam samar-samar
    /// - Random audio stinger / bisikan gaib sesekali di kejauhan
    /// </summary>
    public class MenuAtmosphereAudio : MonoBehaviour
    {
        [Header("Ambience Audio Sources")]
        [SerializeField] private AudioSource windAudioSource;
        [SerializeField] private AudioSource sfxAudioSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip hoverSfx;
        [SerializeField] private AudioClip clickSfx;

        public static MenuAtmosphereAudio Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            SetupAudioSources();
        }

        private void Start()
        {
            StartCoroutine(RandomEerieCuesRoutine());
        }

        private void SetupAudioSources()
        {
            if (windAudioSource == null)
            {
                windAudioSource = gameObject.AddComponent<AudioSource>();
                windAudioSource.loop = true;
                windAudioSource.playOnAwake = false;
                windAudioSource.spatialBlend = 0f; // 2D Stereo
                windAudioSource.volume = 0.45f;
            }

            if (sfxAudioSource == null)
            {
                sfxAudioSource = gameObject.AddComponent<AudioSource>();
                sfxAudioSource.loop = false;
                sfxAudioSource.playOnAwake = false;
                sfxAudioSource.spatialBlend = 0f;
                sfxAudioSource.volume = 0.75f;
            }

            // Generate procedural eerie night wind & drone clip
            if (windAudioSource.clip == null)
            {
                windAudioSource.clip = GenerateEerieNightAmbienceClip();
                windAudioSource.Play();
            }
        }

        public void PlayHoverSound()
        {
            if (sfxAudioSource != null && hoverSfx != null)
            {
                sfxAudioSource.PlayOneShot(hoverSfx, 0.45f);
            }
            else if (sfxAudioSource != null)
            {
                // Fallback synth click/tick
                sfxAudioSource.PlayOneShot(GenerateTickClip(), 0.35f);
            }
        }

        public void PlayClickSound()
        {
            if (sfxAudioSource != null && clickSfx != null)
            {
                sfxAudioSource.PlayOneShot(clickSfx, 0.7f);
            }
            else if (sfxAudioSource != null)
            {
                sfxAudioSource.PlayOneShot(GenerateWoodClackClip(), 0.6f);
            }
        }

        private IEnumerator RandomEerieCuesRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(18f, 32f));

                if (sfxAudioSource != null)
                {
                    // Random subtle distant supernatural cue
                    AudioClip cue = GenerateDistantHorrorCue();
                    sfxAudioSource.PlayOneShot(cue, Random.Range(0.2f, 0.38f));
                }
            }
        }

        /// <summary>
        /// Sintesis procedur ambience malam desa: dengung frekuensi rendah + desau angin malam + suara jangkrik.
        /// </summary>
        private AudioClip GenerateEerieNightAmbienceClip()
        {
            int sampleRate = 44100;
            float duration = 12.0f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // 1. Low ominous sub-bass drone (48 Hz - 55 Hz)
                float bass = Mathf.Sin(2f * Mathf.PI * (50f + Mathf.Sin(t * 0.4f) * 3f) * t) * 0.16f;

                // 2. Wind noise (band-limited noise)
                float rawNoise = (Mathf.PerlinNoise(t * 12f, 0.3f) - 0.5f) * 2f;
                float windSway = 0.5f + 0.5f * Mathf.Sin(t * 0.6f);
                float wind = rawNoise * 0.12f * windSway;

                // 3. Subtle crickets / jangkrik rhythm (high frequency pulses around 4500 Hz)
                float cricketCycle = Mathf.Repeat(t, 0.85f);
                float cricketAmp = 0f;
                if (cricketCycle < 0.35f)
                {
                    float chirps = Mathf.Sin(2f * Mathf.PI * 18f * cricketCycle);
                    if (chirps > 0.2f)
                    {
                        cricketAmp = Mathf.Sin(2f * Mathf.PI * 4600f * t) * 0.025f;
                    }
                }

                // Smooth loop fade at edges (0.5s fade)
                float edgeFade = 1f;
                if (t < 0.5f) edgeFade = t / 0.5f;
                else if (t > duration - 0.5f) edgeFade = (duration - t) / 0.5f;

                samples[i] = (bass + wind + cricketAmp) * edgeFade;
            }

            AudioClip clip = AudioClip.Create("ProceduralNightAmbience", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateTickClip()
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.FloorToInt(sampleRate * 0.04f);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 120f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 1400f * t) * decay * 0.4f;
            }
            AudioClip clip = AudioClip.Create("MenuTick", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateWoodClackClip()
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.FloorToInt(sampleRate * 0.12f);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 40f);
                float tone = Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 680f * t) * 0.3f;
                samples[i] = tone * decay * 0.7f;
            }
            AudioClip clip = AudioClip.Create("MenuWoodClack", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip GenerateDistantHorrorCue()
        {
            int sampleRate = 44100;
            float duration = 3.5f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin((t / duration) * Mathf.PI);
                // Eerie sliding dissonant pitch (tritone 220 Hz & 311 Hz)
                float f1 = 220f + Mathf.Sin(t * 1.5f) * 15f;
                float f2 = 311f + Mathf.Cos(t * 1.2f) * 20f;
                float sig = (Mathf.Sin(2f * Mathf.PI * f1 * t) + Mathf.Sin(2f * Mathf.PI * f2 * t)) * 0.5f;
                samples[i] = sig * env * 0.35f;
            }
            AudioClip clip = AudioClip.Create("DistantHorrorCue", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
