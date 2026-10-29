using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pucaci.UI
{
    public class SettingsPanel : MenuPanel
    {
        [Header("Audio Controls")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider voiceSlider;

        [Header("Microphone Controls")]
        [SerializeField] private TMP_Dropdown micDropdown;
        [SerializeField] private Slider micSensitivitySlider;
        [SerializeField] private Image micLevelBar; // Nice to have: level meter fill

        [Header("Graphics Controls")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown fullscreenDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Controls")]
        [SerializeField] private Slider mouseSensitivitySlider;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button defaultsButton;
        [SerializeField] private Button backButton;

        private List<Resolution> filteredResolutions = new List<Resolution>();
        private AudioClip micTestClip;
        private string activeMicDevice;

        private bool isInitialized;

        private void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            SetupResolutions();
            SetupQualityOptions();
            SetupFullscreenOptions();
            SetupMicrophoneOptions();

            if (applyButton != null) applyButton.onClick.AddListener(OnApplyClicked);
            if (defaultsButton != null) defaultsButton.onClick.AddListener(OnDefaultsClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
        }

        private void Start()
        {
            EnsureInitialized();
            LoadValuesToUI();
        }

        public override void Show()
        {
            EnsureInitialized();
            base.Show();
            LoadValuesToUI();
            StartMicMonitoring();
        }

        public override void Hide()
        {
            StopMicMonitoring();
            base.Hide();
        }

        private void Update()
        {
            UpdateMicLevelMeter();
        }

        private void SetupResolutions()
        {
            if (resolutionDropdown == null) return;

            resolutionDropdown.ClearOptions();
            filteredResolutions.Clear();

            List<string> options = new List<string>();
            Resolution[] allResolutions = Screen.resolutions;
            int currentResolutionIndex = 0;

            for (int i = 0; i < allResolutions.Length; i++)
            {
                Resolution r = allResolutions[i];
                // Filter unik berdasarkan width & height
                if (!filteredResolutions.Exists(res => res.width == r.width && res.height == r.height))
                {
                    filteredResolutions.Add(r);
                    string option = $"{r.width} x {r.height}";
                    options.Add(option);

                    if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height)
                    {
                        currentResolutionIndex = filteredResolutions.Count - 1;
                    }
                }
            }

            resolutionDropdown.AddOptions(options);

            int savedIndex = SettingsManager.Instance != null ? SettingsManager.Instance.ResolutionIndex : -1;
            resolutionDropdown.value = (savedIndex >= 0 && savedIndex < filteredResolutions.Count) ? savedIndex : currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }

        private void SetupQualityOptions()
        {
            if (qualityDropdown == null) return;

            qualityDropdown.ClearOptions();
            List<string> options = new List<string>(QualitySettings.names);
            qualityDropdown.AddOptions(options);
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.RefreshShownValue();
        }

        private void SetupFullscreenOptions()
        {
            if (fullscreenDropdown == null) return;

            fullscreenDropdown.ClearOptions();
            fullscreenDropdown.AddOptions(new List<string> { "Fullscreen Window", "Windowed" });
            fullscreenDropdown.value = Screen.fullScreen ? 0 : 1;
            fullscreenDropdown.RefreshShownValue();
        }

        private void SetupMicrophoneOptions()
        {
            if (micDropdown == null) return;

            micDropdown.ClearOptions();
            string[] devices = Microphone.devices;
            List<string> options = new List<string>();

            if (devices == null || devices.Length == 0)
            {
                options.Add("Tidak ada mikrofon terdeteksi");
                micDropdown.interactable = false;
            }
            else
            {
                options.AddRange(devices);
                micDropdown.interactable = true;
            }

            micDropdown.AddOptions(options);
            micDropdown.RefreshShownValue();
        }

        private void LoadValuesToUI()
        {
            if (SettingsManager.Instance == null) return;

            var sm = SettingsManager.Instance;
            if (masterSlider != null) masterSlider.value = sm.MasterVolume;
            if (musicSlider != null) musicSlider.value = sm.MusicVolume;
            if (sfxSlider != null) sfxSlider.value = sm.SfxVolume;
            if (voiceSlider != null) voiceSlider.value = sm.VoiceVolume;

            if (micSensitivitySlider != null) micSensitivitySlider.value = sm.MicSensitivity;
            if (micDropdown != null && micDropdown.interactable && !string.IsNullOrEmpty(sm.MicDevice))
            {
                int index = micDropdown.options.FindIndex(o => o.text == sm.MicDevice);
                if (index >= 0) micDropdown.value = index;
            }

            if (fullscreenDropdown != null) fullscreenDropdown.value = sm.FullscreenMode;
            if (vSyncToggle != null) vSyncToggle.isOn = sm.VSync;
            if (qualityDropdown != null) qualityDropdown.value = Mathf.Clamp(sm.QualityIndex, 0, QualitySettings.names.Length - 1);
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = sm.MouseSensitivity;
        }

        private void OnApplyClicked()
        {
            if (SettingsManager.Instance == null) return;

            var sm = SettingsManager.Instance;
            if (masterSlider != null) sm.MasterVolume = masterSlider.value;
            if (musicSlider != null) sm.MusicVolume = musicSlider.value;
            if (sfxSlider != null) sm.SfxVolume = sfxSlider.value;
            if (voiceSlider != null) sm.VoiceVolume = voiceSlider.value;

            if (micDropdown != null && micDropdown.interactable)
            {
                sm.MicDevice = micDropdown.options[micDropdown.value].text;
            }
            if (micSensitivitySlider != null) sm.MicSensitivity = micSensitivitySlider.value;

            if (resolutionDropdown != null && resolutionDropdown.value < filteredResolutions.Count)
            {
                sm.ResolutionIndex = resolutionDropdown.value;
            }
            if (fullscreenDropdown != null) sm.FullscreenMode = fullscreenDropdown.value;
            if (vSyncToggle != null) sm.VSync = vSyncToggle.isOn;
            if (qualityDropdown != null) sm.QualityIndex = qualityDropdown.value;

            if (mouseSensitivitySlider != null) sm.MouseSensitivity = mouseSensitivitySlider.value;

            sm.SaveSettings();
        }

        private void OnDefaultsClicked()
        {
            if (SettingsManager.Instance == null) return;

            SettingsManager.Instance.ResetToDefaults();
            LoadValuesToUI();
        }

        private void OnBackClicked()
        {
            MainMenuController.Instance?.ReturnToPreviousPanel();
        }

        #region Mic Monitoring (Nice to have)
        private void StartMicMonitoring()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0) return;

            string device = Microphone.devices[0];
            if (micDropdown != null && micDropdown.interactable && micDropdown.options.Count > 0 && micDropdown.value >= 0 && micDropdown.value < micDropdown.options.Count)
            {
                device = micDropdown.options[micDropdown.value].text;
            }

            activeMicDevice = device;
            try
            {
                micTestClip = Microphone.Start(device, true, 1, 44100);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Pucaci.UI] Gagal memulai pemantauan mikrofon: {ex.Message}");
            }
        }

        private void StopMicMonitoring()
        {
            if (!string.IsNullOrEmpty(activeMicDevice) && Microphone.IsRecording(activeMicDevice))
            {
                Microphone.End(activeMicDevice);
                activeMicDevice = null;
            }
        }

        private void UpdateMicLevelMeter()
        {
            if (micLevelBar == null || micTestClip == null || string.IsNullOrEmpty(activeMicDevice))
            {
                return;
            }

            int micPos = Microphone.GetPosition(activeMicDevice) - 128;
            if (micPos < 0) return;

            float[] waveData = new float[128];
            micTestClip.GetData(waveData, micPos);

            float sum = 0f;
            for (int i = 0; i < 128; i++)
            {
                sum += waveData[i] * waveData[i];
            }

            float rms = Mathf.Sqrt(sum / 128f);
            float level = Mathf.Clamp01(rms * 10f); // Sensitivitas visual meter

            RectTransform rt = micLevelBar.rectTransform;
            float currentX = rt.anchorMax.x;
            float targetX = Mathf.Lerp(currentX, level, Time.unscaledDeltaTime * 15f);
            rt.anchorMax = new Vector2(targetX, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        #endregion
    }
}
