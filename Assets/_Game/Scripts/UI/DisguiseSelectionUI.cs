using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.UI
{
    /// <summary>
    /// UI Menu Pemilihan Penyamaran Karakter untuk Hantu (Impostor):
    /// Dibuka dengan menekan tombol [X].
    /// Pemain dapat memilih salah satu dari 4 karakter warga:
    /// - Laras (Index 0)
    /// - Pak Mamat (Index 1)
    /// - Pak Vincent (Index 3)
    /// - Mas Rocky (Index 4)
    /// Dapat dipilih dengan klik mouse atau menekan angka 1-4 di keyboard saat menu terbuka.
    /// </summary>
    public class DisguiseSelectionUI : MonoBehaviour
    {
        public static DisguiseSelectionUI Instance { get; private set; }

        private Canvas mainCanvas;
        private CanvasGroup canvasGroup;
        private GameObject panelRoot;
        private PlayerRoleManager activeRoleManager;

        private bool isOpen = false;
        public bool IsOpen => isOpen;

        private int openedAtFrame = -1;
        private float openTimestamp = 0f;

        private struct CharacterCardItem
        {
            public int characterIndex;
            public string characterName;
            public string roleTitle;
            public string shortcutKey;
            public Color accentColor;
            public GameObject cardObject;
            public TextMeshProUGUI statusText;
            public Button selectButton;
        }

        private readonly List<CharacterCardItem> cardItems = new List<CharacterCardItem>();

        public static DisguiseSelectionUI EnsureInstance()
        {
            EnsureEventSystem();
            if (Instance != null) return Instance;

            var existing = FindObjectOfType<DisguiseSelectionUI>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("DisguiseSelectionUI");
            Instance = go.AddComponent<DisguiseSelectionUI>();
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return Instance;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                if (Application.isPlaying) DontDestroyOnLoad(esGo);
            }
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

            BuildUI();
            CloseMenuInstant();
        }

        private void Update()
        {
            if (!isOpen) return;

            // Pastikan kursor mouse tetap bebas saat menu pemilihan wujud terbuka
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Jangan izinkan penutupan menu di frame yang sama saat menu baru saja dibuka!
            // Menghindari konflik penekanan tombol [X] yang terdeteksi bersamaan di PlayerRoleManager dan DisguiseSelectionUI
            bool canCloseFromInput = (Time.frameCount > openedAtFrame) && (Time.unscaledTime - openTimestamp > 0.15f);

            // Shortcut navigasi cepat saat menu terbuka
            bool pressClose = false;
            int selectedIndex = -1;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (canCloseFromInput && (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) pressClose = true;
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) selectedIndex = 0; // Laras
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) selectedIndex = 1; // Pak Mamat
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) selectedIndex = 3; // Pak Vincent
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) selectedIndex = 4; // Mas Rocky
            }
#else
            if (canCloseFromInput && (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Escape))) pressClose = true;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) selectedIndex = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) selectedIndex = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) selectedIndex = 3;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) selectedIndex = 4;
#endif

            if (pressClose)
            {
                CloseMenu();
                return;
            }

            if (selectedIndex >= 0)
            {
                SelectCharacter(selectedIndex);
            }
        }

        public void ToggleMenu(PlayerRoleManager roleMgr)
        {
            // Debounce: jika baru saja dibuka dalam < 0.15 detik, abaikan toggle agar tidak langsung menutup
            if (isOpen && (Time.frameCount > openedAtFrame) && (Time.unscaledTime - openTimestamp > 0.15f))
            {
                CloseMenu();
            }
            else if (!isOpen)
            {
                OpenMenu(roleMgr);
            }
        }

        public void OpenMenu(PlayerRoleManager roleMgr)
        {
            activeRoleManager = roleMgr;
            if (activeRoleManager == null) return;

            if (panelRoot == null)
            {
                BuildUI();
            }

            RefreshCardsState();
            isOpen = true;
            openedAtFrame = Time.frameCount;
            openTimestamp = Time.unscaledTime;

            if (panelRoot != null) panelRoot.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Nonaktifkan sementara rotasi kamera controller lokal agar fokus memilih
            if (activeRoleManager.TryGetComponent<PlayerCharacterController>(out var controller))
            {
                controller.SetCursorLock(false);
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

            // Kunci kembali kursor ke gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (activeRoleManager != null && activeRoleManager.TryGetComponent<PlayerCharacterController>(out var controller))
            {
                controller.SetCursorLock(true);
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

        private void SelectCharacter(int charIndex)
        {
            if (activeRoleManager == null) return;

            if (activeRoleManager.TransformCooldownRemaining > 0.05f)
            {
                string cdMsg = $"Cooldown peniruan wujud! Tunggu {activeRoleManager.TransformCooldownRemaining:F1}s";
                Debug.Log($"[ROLE] {cdMsg}");
                var inv = activeRoleManager.GetComponent<Pucaci.Inventory.PlayerInventory>();
                if (inv != null) inv.ShowToast(cdMsg);
                return;
            }

            activeRoleManager.MimicHuman(charIndex);
            CloseMenu();
        }

        private void RefreshCardsState()
        {
            if (activeRoleManager == null) return;

            int currentDisguise = (int)activeRoleManager.HumanDisguise;
            int initialDisguise = activeRoleManager.InitialDisguiseIndex;
            bool isKunti = activeRoleManager.IsKuntilanakForm;

            for (int i = 0; i < cardItems.Count; i++)
            {
                var item = cardItems[i];
                if (item.statusText == null) continue;

                string badge = "";
                if (item.characterIndex == initialDisguise)
                {
                    badge += "<color=#55FF88>[KARAKTER UTAMA]</color>\n";
                }

                if (!isKunti && item.characterIndex == currentDisguise)
                {
                    badge += "<color=#FFCC44><b>● SEDANG DIGUNAKAN</b></color>";
                }
                else
                {
                    badge += "<color=#AAAAAA>Klik / Tekan [" + item.shortcutKey + "]</color>";
                }

                item.statusText.text = badge;
            }
        }

        private void BuildUI()
        {
            // Pastikan Canvas utama ada
            mainCanvas = gameObject.GetComponent<Canvas>();
            if (mainCanvas == null)
            {
                mainCanvas = gameObject.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 999; // Di atas seluruh HUD gameplay
            }

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var raycaster = gameObject.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Hapus panel lama jika ada
            if (panelRoot != null)
            {
                Destroy(panelRoot);
            }
            cardItems.Clear();

            // Panel Root (Overlay Gelap Transparan di seluruh layar)
            panelRoot = new GameObject("PanelRoot", typeof(RectTransform));
            panelRoot.transform.SetParent(transform, false);
            var rootRect = panelRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var bgImage = panelRoot.AddComponent<Image>();
            bgImage.color = new Color(0.02f, 0.03f, 0.05f, 0.85f); // Horror dark tint

            // Modal Box di tengah layar
            var modalBox = new GameObject("ModalBox", typeof(RectTransform));
            modalBox.transform.SetParent(panelRoot.transform, false);
            var modalRect = modalBox.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(980, 520);

            var modalBg = modalBox.AddComponent<Image>();
            modalBg.color = new Color(0.06f, 0.07f, 0.11f, 0.96f);

            // Header Container
            var headerObj = new GameObject("HeaderTitle", typeof(RectTransform));
            headerObj.transform.SetParent(modalBox.transform, false);
            var headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = new Vector2(0, -22);
            headerRect.sizeDelta = new Vector2(-40, 75);

            var headerText = headerObj.AddComponent<TextMeshProUGUI>();
            headerText.text = "<b><color=#DDAAFF>MENU PENYAMARAN HANTU</color></b>\n<size=19><color=#8899AA>Pilih wujud warga desa yang ingin Anda tiru untuk menyusup dan membaur:</size>";
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.fontSize = 32;

            // Grid Container untuk 4 Karakter
            var cardsContainer = new GameObject("CardsContainer", typeof(RectTransform));
            cardsContainer.transform.SetParent(modalBox.transform, false);
            var containerRect = cardsContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.5f);
            containerRect.anchorMax = new Vector2(1, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = new Vector2(0, -10);
            containerRect.sizeDelta = new Vector2(-40, 310);

            var layout = cardsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Setup data 4 Karakter
            SetupCharacterCard(cardsContainer.transform, 0, "LARAS", "Mahasiswi Desa", "1", new Color(0.20f, 0.85f, 0.95f, 1f));
            SetupCharacterCard(cardsContainer.transform, 1, "PAK MAMAT", "Hansip Desa", "2", new Color(0.95f, 0.75f, 0.20f, 1f));
            SetupCharacterCard(cardsContainer.transform, 3, "PAK VINCENT", "Kolektor Misterius", "3", new Color(0.35f, 0.90f, 0.55f, 1f));
            SetupCharacterCard(cardsContainer.transform, 4, "MAS ROCKY", "Pemuda Tangguh", "4", new Color(0.95f, 0.40f, 0.20f, 1f));

            // Footer Petunjuk & Tombol Tutup
            var footerObj = new GameObject("FooterBar", typeof(RectTransform));
            footerObj.transform.SetParent(modalBox.transform, false);
            var footerRect = footerObj.GetComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0, 0);
            footerRect.anchorMax = new Vector2(1, 0);
            footerRect.pivot = new Vector2(0.5f, 0);
            footerRect.anchoredPosition = new Vector2(0, 16);
            footerRect.sizeDelta = new Vector2(-40, 50);

            var closeBtnObj = new GameObject("BtnClose", typeof(RectTransform));
            closeBtnObj.transform.SetParent(footerObj.transform, false);
            var closeBtnRect = closeBtnObj.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
            closeBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
            closeBtnRect.sizeDelta = new Vector2(280, 44);

            var closeBtnImg = closeBtnObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.25f, 0.28f, 0.35f, 0.95f);

            var closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(CloseMenu);

            var closeTextObj = new GameObject("CloseText", typeof(RectTransform));
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            var ctRect = closeTextObj.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = Vector2.zero;
            ctRect.offsetMax = Vector2.zero;

            var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "<b>TUTUP / BATAL [X / ESC]</b>";
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.fontSize = 17;
            closeText.color = Color.white;
        }

        private void SetupCharacterCard(Transform parent, int charIndex, string name, string roleDesc, string shortcut, Color themeColor)
        {
            var card = new GameObject("Card_" + name, typeof(RectTransform));
            card.transform.SetParent(parent, false);

            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.10f, 0.12f, 0.18f, 0.98f);

            int capturedIndex = charIndex;
            var cardBtn = card.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => SelectCharacter(capturedIndex));

            var cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(14, 14, 16, 14);
            cardLayout.spacing = 10;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            // Shortcut Badge
            var badgeObj = new GameObject("Badge", typeof(RectTransform));
            badgeObj.transform.SetParent(card.transform, false);
            var badgeText = badgeObj.AddComponent<TextMeshProUGUI>();
            badgeText.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(themeColor)}><b>[ {shortcut} ]</b></color>";
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.fontSize = 24;

            // Name
            var nameObj = new GameObject("Name", typeof(RectTransform));
            nameObj.transform.SetParent(card.transform, false);
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = $"<b>{name}</b>";
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 22;
            nameText.color = Color.white;

            // Role Subtitle
            var descObj = new GameObject("Desc", typeof(RectTransform));
            descObj.transform.SetParent(card.transform, false);
            var descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = $"<size=15><color=#99AABB>{roleDesc}</color></size>";
            descText.alignment = TextAlignmentOptions.Center;
            descText.fontSize = 15;

            // Status Tag
            var statusObj = new GameObject("StatusTag", typeof(RectTransform));
            statusObj.transform.SetParent(card.transform, false);
            var statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "<color=#AAAAAA>Klik untuk Memilih</color>";
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 14;

            // Select Button
            var btnObj = new GameObject("SelectBtn", typeof(RectTransform));
            btnObj.transform.SetParent(card.transform, false);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(0, 44);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = themeColor;

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectCharacter(capturedIndex));

            var btnTextObj = new GameObject("BtnText", typeof(RectTransform));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btRect = btnTextObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;

            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "<b>PILIH WUJUD</b>";
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.fontSize = 16;
            btnText.color = new Color(0.05f, 0.05f, 0.08f, 1f);

            var item = new CharacterCardItem
            {
                characterIndex = charIndex,
                characterName = name,
                roleTitle = roleDesc,
                shortcutKey = shortcut,
                accentColor = themeColor,
                cardObject = card,
                statusText = statusText,
                selectButton = btn
            };

            cardItems.Add(item);
        }
    }
}
