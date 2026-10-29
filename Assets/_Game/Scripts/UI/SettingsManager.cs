using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Pucaci.UI
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("Audio Mixer Reference")]
        [SerializeField] private AudioMixer audioMixer;

        // PlayerPrefs Keys
        public const string KeyMasterVol = "Settings_MasterVol";
        public const string KeyMusicVol = "Settings_MusicVol";
        public const string KeySfxVol = "Settings_SfxVol";
        public const string KeyVoiceVol = "Settings_VoiceVol";

        public const string KeyMicDevice = "Settings_MicDevice";
        public const string KeyMicSensitivity = "Settings_MicSensitivity";

        public const string KeyResolutionIndex = "Settings_ResolutionIndex";
        public const string KeyFullscreenMode = "Settings_FullscreenMode";
        public const string KeyVSync = "Settings_VSync";
        public const string KeyQualityIndex = "Settings_QualityIndex";

        public const string KeyMouseSensitivity = "Settings_MouseSensitivity";

        // Current In-Memory Settings
        public float MasterVolume { get; set; } = 0.8f;
        public float MusicVolume { get; set; } = 0.8f;
        public float SfxVolume { get; set; } = 0.8f;
        public float VoiceVolume { get; set; } = 0.8f;

        public string MicDevice { get; set; } = "";
        public float MicSensitivity { get; set; } = 0.5f;

        public int ResolutionIndex { get; set; } = -1;
        public int FullscreenMode { get; set; } = 0; // 0 = FullScreenWindow, 1 = Windowed
        public bool VSync { get; set; } = true;
        public int QualityIndex { get; set; } = 3;

        public float MouseSensitivity { get; set; } = 0.5f;

        public event Action OnSettingsChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
                ApplySettings();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyMasterVol, 0.8f);
            MusicVolume = PlayerPrefs.GetFloat(KeyMusicVol, 0.8f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfxVol, 0.8f);
            VoiceVolume = PlayerPrefs.GetFloat(KeyVoiceVol, 0.8f);

            MicDevice = PlayerPrefs.GetString(KeyMicDevice, "");
            MicSensitivity = PlayerPrefs.GetFloat(KeyMicSensitivity, 0.5f);

            ResolutionIndex = PlayerPrefs.GetInt(KeyResolutionIndex, -1);
            FullscreenMode = PlayerPrefs.GetInt(KeyFullscreenMode, 0);
            VSync = PlayerPrefs.GetInt(KeyVSync, 1) == 1;
            QualityIndex = PlayerPrefs.GetInt(KeyQualityIndex, 3);
            if (QualityIndex < 3 && QualitySettings.names.Length > 3) QualityIndex = 3;


            MouseSensitivity = PlayerPrefs.GetFloat(KeyMouseSensitivity, 0.5f);
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(KeyMasterVol, MasterVolume);
            PlayerPrefs.SetFloat(KeyMusicVol, MusicVolume);
            PlayerPrefs.SetFloat(KeySfxVol, SfxVolume);
            PlayerPrefs.SetFloat(KeyVoiceVol, VoiceVolume);

            PlayerPrefs.SetString(KeyMicDevice, MicDevice);
            PlayerPrefs.SetFloat(KeyMicSensitivity, MicSensitivity);

            PlayerPrefs.SetInt(KeyResolutionIndex, ResolutionIndex);
            PlayerPrefs.SetInt(KeyFullscreenMode, FullscreenMode);
            PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
            PlayerPrefs.SetInt(KeyQualityIndex, QualityIndex);

            PlayerPrefs.SetFloat(KeyMouseSensitivity, MouseSensitivity);

            PlayerPrefs.Save();
            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        public void ApplySettings()
        {
            // Apply Audio to Mixer
            ApplyVolumeToMixer("MasterVolume", MasterVolume);
            ApplyVolumeToMixer("MusicVolume", MusicVolume);
            ApplyVolumeToMixer("SfxVolume", SfxVolume);
            ApplyVolumeToMixer("VoiceVolume", VoiceVolume);

            // Apply Graphics
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            if (QualityIndex >= 0 && QualityIndex < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(QualityIndex, true);
            }

            Resolution[] resolutions = Screen.resolutions;
            if (ResolutionIndex >= 0 && ResolutionIndex < resolutions.Length)
            {
                Resolution res = resolutions[ResolutionIndex];
                FullScreenMode mode = FullscreenMode == 0 ? UnityEngine.FullScreenMode.FullScreenWindow : UnityEngine.FullScreenMode.Windowed;
                Screen.SetResolution(res.width, res.height, mode, res.refreshRateRatio);
            }
        }

        public void ResetToDefaults()
        {
            MasterVolume = 0.8f;
            MusicVolume = 0.8f;
            SfxVolume = 0.8f;
            VoiceVolume = 0.8f;

            MicDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : "";
            MicSensitivity = 0.5f;

            ResolutionIndex = Screen.resolutions.Length - 1;
            FullscreenMode = 0;
            VSync = true;
            QualityIndex = Mathf.Clamp(3, 0, QualitySettings.names.Length - 1);

            MouseSensitivity = 0.5f;

            SaveSettings();
        }

        private void ApplyVolumeToMixer(string parameterName, float linearValue)
        {
            if (audioMixer == null) return;

            float dB = Mathf.Log10(Mathf.Max(linearValue, 0.0001f)) * 20f;
            audioMixer.SetFloat(parameterName, dB);
        }

        public void SetAudioMixer(AudioMixer mixer)
        {
            audioMixer = mixer;
            ApplySettings();
        }
    }
}
