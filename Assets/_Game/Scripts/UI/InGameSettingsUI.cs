using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.UI
{
    /// <summary>
    /// UI Pengaturan & Jeda (In-Game Settings / Pause Menu) yang dapat diakses dengan menekan tombol [ESC] saat bermain.
    /// Fitur mencakup:
    /// - Kontrol Volume Audio (Master, Musik, SFX, Suara Pemain)
    /// - Sensitivitas Mouse
    /// - Kualitas Grafis & Mode Layar (Fullscreen / Windowed) & VSync
    /// - Tombol Lanjutkan (Resume), Reset Default, Kembali ke Menu Utama, dan Keluar Game
    /// Tersinkronisasi otomatis dengan SettingsManager dan PlayerCharacterController.
    /// </summary>
    public class InGameSettingsUI : MonoBehaviour
    {
        public static InGameSettingsUI Instance { get; private set; }

        private Canvas mainCanvas;
        private CanvasGroup canvasGroup;
        private GameObject panelRoot;

        private bool isOpen = false;
        public bool IsOpen => isOpen;

        private int lastToggleFrame = -1;

        // Audio Sliders & Badges
        private Slider masterSlider;
        private TextMeshProUGUI masterValText;
        private Slider musicSlider;
        private TextMeshProUGUI musicValText;
        private Slider sfxSlider;
        private TextMeshProUGUI sfxValText;
        private Slider voiceSlider;
        private TextMeshProUGUI voiceValText;

        // Kontrol Slider & Badge
        private Slider mouseSensSlider;
        private TextMeshProUGUI mouseSensValText;

        // Grafis & Tampilan
        private TextMeshProUGUI qualityLabel;
        private int currentQualityIndex = 3;
        public int CurrentQualityIndex => currentQualityIndex;

        private TextMeshProUGUI fullscreenLabel;
        private int currentFullscreenMode = 0; // 0 = Fullscreen, 1 = Windowed
        public int CurrentFullscreenMode => currentFullscreenMode;

        private Toggle vsyncToggle;

        public static InGameSettingsUI EnsureInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindObjectOfType<InGameSettingsUI>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("InGameSettingsUI");
            Instance = go.AddComponent<InGameSettingsUI>();
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureSettingsManagerExists();
            BuildUI();
            CloseMenuInstant();
        }

        private void EnsureSettingsManagerExists()
        {
            SettingsManager.EnsureInstance();
        }

        private void Update()
        {
            // Jangan buka menu setting in-game jika berada di scene MainMenu
            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                if (isOpen) CloseMenuInstant();
                return;
            }

            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escPressed = true;
            }
#endif

            if (escPressed)
            {
                // Jika menu penyamaran wujud hantu terbuka, biarkan ia ditutup terlebih dahulu
                if (DisguiseSelectionUI.Instance != null && DisguiseSelectionUI.Instance.IsOpen)
                {
                    return;
                }

                // Jika peta penuh terbuka, biarkan ia ditutup terlebih dahulu
                if (MinimapController.Instance != null && MinimapController.Instance.IsFullMapOpen)
                {
                    return;
                }

                ToggleMenu();
                return;
            }

            if (isOpen)
            {
                // Jaga agar kursor mouse selalu bebas dan terlihat saat menu pengaturan terbuka
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        public void ToggleMenu()
        {
            if (Time.frameCount == lastToggleFrame) return;
            lastToggleFrame = Time.frameCount;

            if (isOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        public void OpenMenu()
        {
            if (panelRoot == null)
            {
                BuildUI();
            }

            EnsureSettingsManagerExists();
            LoadValuesToUI();

            isOpen = true;
            if (panelRoot != null) panelRoot.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Bebaskan kursor di local PlayerCharacterController jika ada
            var localPlayers = FindObjectsOfType<Pucaci.Player.PlayerCharacterController>();
            foreach (var p in localPlayers)
            {
                if (p != null) p.SetCursorLock(false);
            }
        }

        public void CloseMenu()
        {
            isOpen = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (panelRoot != null) panelRoot.SetActive(false);

            // Simpan dan terapkan pengaturan saat menu ditutup
            SaveCurrentSettings();

            // Kunci kembali kursor ke gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var localPlayers = FindObjectsOfType<Pucaci.Player.PlayerCharacterController>();
            foreach (var p in localPlayers)
            {
                if (p != null) p.SetCursorLock(true);
            }
        }

        private void CloseMenuInstant()
        {
            isOpen = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void LoadValuesToUI()
        {
            var sm = SettingsManager.Instance;
            if (sm == null) return;

            if (masterSlider != null)
            {
                masterSlider.SetValueWithoutNotify(sm.MasterVolume);
                UpdateSliderLabel(masterValText, sm.MasterVolume, true);
            }
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(sm.MusicVolume);
                UpdateSliderLabel(musicValText, sm.MusicVolume, true);
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(sm.SfxVolume);
                UpdateSliderLabel(sfxValText, sm.SfxVolume, true);
            }
            if (voiceSlider != null)
            {
                voiceSlider.SetValueWithoutNotify(sm.VoiceVolume);
                UpdateSliderLabel(voiceValText, sm.VoiceVolume, true);
            }

            if (mouseSensSlider != null)
            {
                mouseSensSlider.SetValueWithoutNotify(sm.MouseSensitivity);
                UpdateSliderLabel(mouseSensValText, sm.MouseSensitivity * 2f, false, "x");
            }

            currentQualityIndex = Mathf.Clamp(sm.QualityIndex, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            UpdateQualityLabel();

            currentFullscreenMode = sm.FullscreenMode;
            UpdateFullscreenLabel();

            if (vsyncToggle != null)
            {
                vsyncToggle.SetIsOnWithoutNotify(sm.VSync);
            }
        }

        private void SaveCurrentSettings()
        {
            var sm = SettingsManager.Instance;
            if (sm == null) return;

            if (masterSlider != null) sm.MasterVolume = masterSlider.value;
            if (musicSlider != null) sm.MusicVolume = musicSlider.value;
            if (sfxSlider != null) sm.SfxVolume = sfxSlider.value;
            if (voiceSlider != null) sm.VoiceVolume = voiceSlider.value;
            if (mouseSensSlider != null) sm.MouseSensitivity = mouseSensSlider.value;

            sm.QualityIndex = currentQualityIndex;
            sm.FullscreenMode = currentFullscreenMode;
            if (vsyncToggle != null) sm.VSync = vsyncToggle.isOn;

            sm.SaveSettings();
            sm.ApplySettings();
        }

        private void UpdateSliderLabel(TextMeshProUGUI label, float val, bool isPercent, string suffix = "%")
        {
            if (label == null) return;
            if (isPercent)
            {
                label.text = Mathf.RoundToInt(val * 100f) + suffix;
            }
            else
            {
                label.text = val.ToString("F1") + suffix;
            }
        }

        private void UpdateQualityLabel()
        {
            if (qualityLabel == null) return;
            string[] names = QualitySettings.names;
            if (names != null && names.Length > 0)
            {
                currentQualityIndex = Mathf.Clamp(currentQualityIndex, 0, names.Length - 1);
                qualityLabel.text = names[currentQualityIndex].ToUpper();
            }
            else
            {
                qualityLabel.text = "DEFAULT";
            }
        }

        private void UpdateFullscreenLabel()
        {
            if (fullscreenLabel == null) return;
            fullscreenLabel.text = (currentFullscreenMode == 0) ? "LAYAR PENUH (FULLSCREEN)" : "JENDELA (WINDOWED)";
        }

        public void CycleQuality(int direction)
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0) return;

            currentQualityIndex = (currentQualityIndex + direction + names.Length) % names.Length;
            QualitySettings.SetQualityLevel(currentQualityIndex, true);
            UpdateQualityLabel();

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.QualityIndex = currentQualityIndex;
                SettingsManager.Instance.SaveSettings();
            }
        }

        public void CycleFullscreen(int direction)
        {
            currentFullscreenMode = (currentFullscreenMode + direction + 2) % 2;
            FullScreenMode mode = (currentFullscreenMode == 0) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.fullScreenMode = mode;
            UpdateFullscreenLabel();

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.FullscreenMode = currentFullscreenMode;
                SettingsManager.Instance.SaveSettings();
            }
        }

        private void OnResetDefaults()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.ResetToDefaults();
            }
            LoadValuesToUI();
        }

        private void OnReturnToMainMenu()
        {
            CloseMenuInstant();
            DisconnectNetwork();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }

        private void OnQuitGame()
        {
            DisconnectNetwork();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DisconnectNetwork()
        {
            try
            {
                if (NetworkServer.active && NetworkClient.isConnected)
                {
                    NetworkManager.singleton?.StopHost();
                }
                else if (NetworkClient.isConnected)
                {
                    NetworkManager.singleton?.StopClient();
                }
                else if (NetworkServer.active)
                {
                    NetworkManager.singleton?.StopServer();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InGameSettingsUI] DisconnectNetwork warning: {ex.Message}");
            }
        }

        #region UI Construction
        private void BuildUI()
        {
            mainCanvas = gameObject.GetComponent<Canvas>();
            if (mainCanvas == null)
            {
                mainCanvas = gameObject.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 998;
            }

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (panelRoot != null)
            {
                Destroy(panelRoot);
            }

            // Fullscreen dark backdrop
            panelRoot = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(transform, false);
            var rootRt = panelRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            panelRoot.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 0.88f);

            // Modal Box
            var modalBox = new GameObject("ModalBox", typeof(RectTransform), typeof(Image));
            modalBox.transform.SetParent(panelRoot.transform, false);
            var modalRt = modalBox.GetComponent<RectTransform>();
            modalRt.anchorMin = new Vector2(0.5f, 0.5f);
            modalRt.anchorMax = new Vector2(0.5f, 0.5f);
            modalRt.pivot = new Vector2(0.5f, 0.5f);
            modalRt.sizeDelta = new Vector2(800, 640);
            modalBox.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.98f);

            // Top Border Accent
            var borderTop = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
            borderTop.transform.SetParent(modalBox.transform, false);
            var borderRt = borderTop.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0, 1);
            borderRt.anchorMax = new Vector2(1, 1);
            borderRt.pivot = new Vector2(0.5f, 1);
            borderRt.sizeDelta = new Vector2(0, 5);
            borderTop.GetComponent<Image>().color = new Color(0.95f, 0.35f, 0.25f, 1f);

            // Header Container
            var headerObj = new GameObject("Header", typeof(RectTransform));
            headerObj.transform.SetParent(modalBox.transform, false);
            var hRt = headerObj.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0, 1);
            hRt.anchorMax = new Vector2(1, 1);
            hRt.pivot = new Vector2(0.5f, 1);
            hRt.anchoredPosition = new Vector2(0, -18);
            hRt.sizeDelta = new Vector2(-40, 68);

            var title = headerObj.AddComponent<TextMeshProUGUI>();
            title.text = "<b><color=#FFFFFF>PENGATURAN & JEDA</color></b>\n<size=16><color=#8899AA>Tekan [ESC] atau klik Lanjutkan untuk kembali bermain</size>";
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 26;

            // Content Container
            var contentObj = new GameObject("ContentContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObj.transform.SetParent(modalBox.transform, false);
            var cRt = contentObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = new Vector2(0, -6);
            cRt.sizeDelta = new Vector2(720, 460);

            var vLayout = contentObj.GetComponent<VerticalLayoutGroup>();
            vLayout.spacing = 8f;
            vLayout.childAlignment = TextAnchor.UpperCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;

            // 1. SECTION: AUDIO
            CreateSectionBadge(contentObj.transform, "PENGATURAN SUARA (AUDIO)", new Color(0.95f, 0.75f, 0.25f, 1f));
            masterSlider = CreateSliderRow(contentObj.transform, "Volume Utama (Master)", 0.8f, out masterValText, (val) => {
                UpdateSliderLabel(masterValText, val, true);
                if (SettingsManager.Instance != null) { SettingsManager.Instance.MasterVolume = val; SettingsManager.Instance.ApplySettings(); }
            });
            musicSlider = CreateSliderRow(contentObj.transform, "Musik Latar", 0.8f, out musicValText, (val) => {
                UpdateSliderLabel(musicValText, val, true);
                if (SettingsManager.Instance != null) { SettingsManager.Instance.MusicVolume = val; SettingsManager.Instance.ApplySettings(); }
            });
            sfxSlider = CreateSliderRow(contentObj.transform, "Efek Suara (SFX)", 0.8f, out sfxValText, (val) => {
                UpdateSliderLabel(sfxValText, val, true);
                if (SettingsManager.Instance != null) { SettingsManager.Instance.SfxVolume = val; SettingsManager.Instance.ApplySettings(); }
            });
            voiceSlider = CreateSliderRow(contentObj.transform, "Suara Pemain (Voice Chat)", 0.8f, out voiceValText, (val) => {
                UpdateSliderLabel(voiceValText, val, true);
                if (SettingsManager.Instance != null) { SettingsManager.Instance.VoiceVolume = val; SettingsManager.Instance.ApplySettings(); }
            });

            // 2. SECTION: KONTROL
            CreateSectionBadge(contentObj.transform, "KONTROL & SENSITIVITAS", new Color(0.35f, 0.85f, 0.95f, 1f));
            mouseSensSlider = CreateSliderRow(contentObj.transform, "Sensitivitas Mouse", 0.5f, out mouseSensValText, (val) => {
                UpdateSliderLabel(mouseSensValText, val * 2f, false, "x");
                if (SettingsManager.Instance != null) { SettingsManager.Instance.MouseSensitivity = val; }
            });

            // 3. SECTION: GRAFIS
            CreateSectionBadge(contentObj.transform, "GRAFIS & TAMPILAN", new Color(0.55f, 0.95f, 0.45f, 1f));
            CreateStepperRow(contentObj.transform, "Kualitas Grafis", out qualityLabel, () => CycleQuality(-1), () => CycleQuality(1));
            CreateStepperRow(contentObj.transform, "Mode Tampilan", out fullscreenLabel, () => CycleFullscreen(-1), () => CycleFullscreen(1));
            vsyncToggle = CreateToggleRow(contentObj.transform, "Aktifkan V-Sync", (isOn) => {
                if (SettingsManager.Instance != null)
                {
                    SettingsManager.Instance.VSync = isOn;
                    QualitySettings.vSyncCount = isOn ? 1 : 0;
                    SettingsManager.Instance.SaveSettings();
                }
            });

            // 4. BOTTOM ACTION BUTTONS
            var btnRow = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(modalBox.transform, false);
            var bRt = btnRow.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0, 0);
            bRt.anchorMax = new Vector2(1, 0);
            bRt.pivot = new Vector2(0.5f, 0);
            bRt.anchoredPosition = new Vector2(0, 16);
            bRt.sizeDelta = new Vector2(-40, 48);

            var hLayout = btnRow.GetComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 12f;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = true;

            CreateActionButton(btnRow.transform, "LANJUTKAN [ESC]", new Color(0.18f, 0.65f, 0.45f, 1f), () => CloseMenu());
            CreateActionButton(btnRow.transform, "RESET DEFAULT", new Color(0.24f, 0.28f, 0.35f, 1f), () => OnResetDefaults());
            CreateActionButton(btnRow.transform, "MENU UTAMA", new Color(0.20f, 0.45f, 0.75f, 1f), () => OnReturnToMainMenu());
            CreateActionButton(btnRow.transform, "KELUAR GAME", new Color(0.75f, 0.20f, 0.25f, 1f), () => OnQuitGame());
        }

        private void CreateSectionBadge(Transform parent, string title, Color accent)
        {
            var badgeObj = new GameObject("Badge_" + title, typeof(RectTransform));
            badgeObj.transform.SetParent(parent, false);
            var rt = badgeObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(720, 26);

            var text = badgeObj.AddComponent<TextMeshProUGUI>();
            text.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(accent)}>■ {title}</color></b>";
            text.fontSize = 15;
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private Slider CreateSliderRow(Transform parent, string label, float defaultValue, out TextMeshProUGUI valText, Action<float> onValueChanged)
        {
            var row = new GameObject("Row_" + label, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rRt = row.GetComponent<RectTransform>();
            rRt.sizeDelta = new Vector2(720, 30);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Label Kiri (260px)
            var lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.transform.SetParent(row.transform, false);
            lblObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 26);
            var lText = lblObj.AddComponent<TextMeshProUGUI>();
            lText.text = label;
            lText.fontSize = 15;
            lText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            lText.alignment = TextAlignmentOptions.MidlineLeft;

            // Slider Body (360px)
            var sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(row.transform, false);
            var sRt = sliderObj.GetComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(360, 20);

            var slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = defaultValue;

            // Background
            var bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(sliderObj.transform, false);
            bgObj.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.22f, 1f);
            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill Area & Fill
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = new Vector2(4, 0);
            faRt.offsetMax = new Vector2(-4, 0);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = new Color(0.95f, 0.35f, 0.25f, 1f);
            var fRt = fill.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.zero;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fRt.sizeDelta = new Vector2(10, 0);
            slider.fillRect = fRt;

            // Handle Area & Handle
            var handleArea = new GameObject("Handle Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(8, 0);
            haRt.offsetMax = new Vector2(-8, 0);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0, 0);
            hRt.anchorMax = new Vector2(0, 1);
            hRt.offsetMin = new Vector2(-6, -2);
            hRt.offsetMax = new Vector2(6, 2);
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();

            // Value Badge Kanan (70px)
            var valObj = new GameObject("ValueText", typeof(RectTransform));
            valObj.transform.SetParent(row.transform, false);
            valObj.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 26);
            valText = valObj.AddComponent<TextMeshProUGUI>();
            valText.fontSize = 14;
            valText.color = new Color(0.95f, 0.75f, 0.25f, 1f);
            valText.alignment = TextAlignmentOptions.MidlineRight;
            UpdateSliderLabel(valText, defaultValue, true);

            slider.onValueChanged.AddListener((v) => onValueChanged?.Invoke(v));
            return slider;
        }

        private void CreateStepperRow(Transform parent, string label, out TextMeshProUGUI displayLabel, Action onPrev, Action onNext)
        {
            var row = new GameObject("Stepper_" + label, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rRt = row.GetComponent<RectTransform>();
            rRt.sizeDelta = new Vector2(720, 32);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // Label Kiri (260px)
            var lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.transform.SetParent(row.transform, false);
            lblObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 26);
            var lText = lblObj.AddComponent<TextMeshProUGUI>();
            lText.text = label;
            lText.fontSize = 15;
            lText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            lText.alignment = TextAlignmentOptions.MidlineLeft;

            // Stepper Container (440px)
            var stepObj = new GameObject("StepperBox", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
            stepObj.transform.SetParent(row.transform, false);
            stepObj.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 1f);
            var sRt = stepObj.GetComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(440, 30);

            var sLayout = stepObj.GetComponent<HorizontalLayoutGroup>();
            sLayout.spacing = 5f;
            sLayout.childAlignment = TextAnchor.MiddleCenter;
            sLayout.childControlWidth = false;
            sLayout.childControlHeight = false;

            // Tombol Left <
            var btnLeft = CreateSmallStepButton(stepObj.transform, "◀", onPrev);

            // Teks Tengah (340px)
            var textObj = new GameObject("DisplayText", typeof(RectTransform));
            textObj.transform.SetParent(stepObj.transform, false);
            textObj.GetComponent<RectTransform>().sizeDelta = new Vector2(340, 26);
            displayLabel = textObj.AddComponent<TextMeshProUGUI>();
            displayLabel.fontSize = 14;
            displayLabel.color = Color.white;
            displayLabel.alignment = TextAlignmentOptions.Center;

            // Tombol Right >
            var btnRight = CreateSmallStepButton(stepObj.transform, "▶", onNext);
        }

        private Button CreateSmallStepButton(Transform parent, string icon, Action onClick)
        {
            var btnObj = new GameObject("Btn_" + icon, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            btnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(36, 26);
            btnObj.GetComponent<Image>().color = new Color(0.20f, 0.24f, 0.34f, 1f);

            var textObj = new GameObject("Icon", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            var tRt = textObj.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            var t = textObj.AddComponent<TextMeshProUGUI>();
            t.text = icon;
            t.fontSize = 14;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;

            var btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        private Toggle CreateToggleRow(Transform parent, string label, Action<bool> onToggleChanged)
        {
            var row = new GameObject("Toggle_" + label, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rRt = row.GetComponent<RectTransform>();
            rRt.sizeDelta = new Vector2(720, 30);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.transform.SetParent(row.transform, false);
            lblObj.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 26);
            var lText = lblObj.AddComponent<TextMeshProUGUI>();
            lText.text = label;
            lText.fontSize = 15;
            lText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            lText.alignment = TextAlignmentOptions.MidlineLeft;

            var togObj = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            togObj.transform.SetParent(row.transform, false);
            var tRt = togObj.GetComponent<RectTransform>();
            tRt.sizeDelta = new Vector2(440, 26);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(togObj.transform, false);
            bg.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.22f, 1f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.pivot = new Vector2(0, 0.5f);
            bgRt.sizeDelta = new Vector2(26, 26);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            check.GetComponent<Image>().color = new Color(0.35f, 0.85f, 0.55f, 1f);
            var cRt = check.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.2f, 0.2f);
            cRt.anchorMax = new Vector2(0.8f, 0.8f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            var toggle = togObj.GetComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.isOn = true;
            toggle.onValueChanged.AddListener((v) => onToggleChanged?.Invoke(v));

            return toggle;
        }

        private Button CreateActionButton(Transform parent, string title, Color bgColor, Action onClick)
        {
            var btnObj = new GameObject("Btn_" + title, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            btnObj.GetComponent<Image>().color = bgColor;

            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            var tRt = textObj.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            var t = textObj.AddComponent<TextMeshProUGUI>();
            t.text = "<b>" + title + "</b>";
            t.fontSize = 15;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }
        #endregion
    }
}
