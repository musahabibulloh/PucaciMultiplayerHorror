using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Inventory;
using Pucaci.Player;

namespace Pucaci.UI
{
    /// <summary>
    /// HUD UI Inventory interaktif dan elegan.
    /// - Menampilkan kotak slot inventory (2 slot untuk Laras/Vincent/Rocky, 4 slot untuk Pak Mamat).
    /// - Setiap slot interaktif: DAPAT DIKLIK MOUSE atau ditekan tombol angka [1], [2], [3], [4].
    /// - Badge nomor shortcut [1], [2], [3], [4].
    /// - Indikator bingkai emas menyala pada slot yang sedang di-equip (dipegang).
    /// - Menampilkan notifikasi popup / feedback saat inventory penuh atau slot kosong ditekan.
    /// - Banner peringatan saat menerima sinyal peringatan Kentongan dari warga lain.
    /// </summary>
    public class PlayerInventoryUI : MonoBehaviour
    {
        public static PlayerInventoryUI Instance { get; private set; }

        private PlayerInventory targetInventory;
        private Canvas mainCanvas;
        private CanvasGroup canvasGroup;

        // UI Roots
        private RectTransform slotsContainer;
        private List<SlotUIElements> slotUIList = new List<SlotUIElements>();

        // Toast feedback banner
        private GameObject toastBannerObj;
        private TextMeshProUGUI toastText;
        private CanvasGroup toastCanvasGroup;
        private Coroutine toastCoroutine;

        // Kentongan alert banner
        private GameObject alertBannerObj;
        private TextMeshProUGUI alertText;
        private CanvasGroup alertCanvasGroup;
        private Coroutine alertCoroutine;

        // Interaction prompt ([E] Ambil Item)
        private GameObject interactPromptObj;
        private TextMeshProUGUI interactPromptText;
        private CanvasGroup interactPromptCanvasGroup;

        private class SlotUIElements
        {
            public GameObject root;
            public Button button;
            public Image background;
            public Image highlightBorder;
            public Image iconImage;
            public TextMeshProUGUI itemNameText;
            public TextMeshProUGUI hotkeyBadgeText;
            public Image cooldownOverlay;
        }

        private void Awake()
        {
            Instance = this;
            BuildUIHierarchy();
        }

        private void Start()
        {
            FindLocalPlayerInventory();
        }

        private void Update()
        {
            if (targetInventory == null)
            {
                FindLocalPlayerInventory();
                return;
            }

            if (targetInventory.MaxSlots != slotUIList.Count)
            {
                RebuildSlotsUI();
            }

            UpdateLayoutPosition();
            UpdateSlotsDisplay();
        }

        private void UpdateLayoutPosition()
        {
            if (slotsContainer == null) return;

            bool isGhostHudVisible = false;
            if (targetInventory != null)
            {
                var role = targetInventory.GetComponent<PlayerRoleManager>();
                if (role != null && role.IsImpostor)
                {
                    isGhostHudVisible = true;
                }
            }

            if (!isGhostHudVisible && GhostSkillHUDUI.Instance != null && GhostSkillHUDUI.Instance.gameObject.activeInHierarchy)
            {
                var cg = GhostSkillHUDUI.Instance.GetComponent<CanvasGroup>();
                if (cg != null && cg.alpha > 0.05f)
                {
                    isGhostHudVisible = true;
                }
            }

            // Jika HUD skill hantu aktif, letakkan slot inventory di atas Ghost HUD (Y = 158)
            // Jika Survivor normal (tanpa Ghost HUD), letakkan tepat di atas health bar (Y = 36)
            float targetY = isGhostHudVisible ? 158f : 36f;
            if (Mathf.Abs(slotsContainer.anchoredPosition.y - targetY) > 0.5f)
            {
                slotsContainer.anchoredPosition = new Vector2(0f, targetY);

                if (toastBannerObj != null)
                {
                    var toastRect = toastBannerObj.GetComponent<RectTransform>();
                    if (toastRect != null) toastRect.anchoredPosition = new Vector2(0f, targetY + 102f);
                }

                if (interactPromptObj != null)
                {
                    var promptRect = interactPromptObj.GetComponent<RectTransform>();
                    if (promptRect != null) promptRect.anchoredPosition = new Vector2(0f, targetY + 98f);
                }
            }
        }

        private void FindLocalPlayerInventory()
        {
            var allInvs = FindObjectsOfType<PlayerInventory>();
            foreach (var inv in allInvs)
            {
                if (inv.isLocalPlayer)
                {
                    SetTargetInventory(inv);
                    break;
                }
            }
        }

        public void SetTargetInventory(PlayerInventory inv)
        {
            if (targetInventory != null)
            {
                targetInventory.OnInventoryChanged -= HandleInventoryChanged;
                targetInventory.OnEquippedChanged -= HandleEquippedChanged;
                targetInventory.OnFeedbackMessage -= ShowToastFeedback;
                targetInventory.OnKentonganAlertReceived -= ShowKentonganAlert;
            }

            targetInventory = inv;

            if (targetInventory != null)
            {
                targetInventory.OnInventoryChanged += HandleInventoryChanged;
                targetInventory.OnEquippedChanged += HandleEquippedChanged;
                targetInventory.OnFeedbackMessage += ShowToastFeedback;
                targetInventory.OnKentonganAlertReceived += ShowKentonganAlert;

                RebuildSlotsUI();
            }
        }

        private void HandleInventoryChanged()
        {
            RebuildSlotsUI();
        }

        private void HandleEquippedChanged(int slotIdx)
        {
            UpdateSlotsDisplay();
        }

        private void BuildUIHierarchy()
        {
            mainCanvas = GetComponent<Canvas>();
            if (mainCanvas == null)
            {
                mainCanvas = gameObject.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 50;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null) gameObject.AddComponent<GraphicRaycaster>();

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 1. Container Slot di bagian bawah tengah layar
            var containerObj = new GameObject("InventorySlotsContainer", typeof(RectTransform));
            containerObj.transform.SetParent(transform, false);
            slotsContainer = containerObj.GetComponent<RectTransform>();
            slotsContainer.anchorMin = new Vector2(0.5f, 0f);
            slotsContainer.anchorMax = new Vector2(0.5f, 0f);
            slotsContainer.pivot = new Vector2(0.5f, 0f);
            slotsContainer.anchoredPosition = new Vector2(0f, 32f);
            slotsContainer.sizeDelta = new Vector2(400f, 90f);

            var hLayout = containerObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 16f;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;

            // 2. Banner Feedback Toast
            toastBannerObj = new GameObject("InventoryToastBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            toastBannerObj.transform.SetParent(transform, false);
            var toastRect = toastBannerObj.GetComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0.5f, 0f);
            toastRect.anchorMax = new Vector2(0.5f, 0f);
            toastRect.pivot = new Vector2(0.5f, 0f);
            toastRect.anchoredPosition = new Vector2(0f, 135f);
            toastRect.sizeDelta = new Vector2(360f, 44f);

            var toastBg = toastBannerObj.GetComponent<Image>();
            toastBg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            toastCanvasGroup = toastBannerObj.AddComponent<CanvasGroup>();
            toastCanvasGroup.alpha = 0f;

            var tTextObj = new GameObject("ToastText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            tTextObj.transform.SetParent(toastBannerObj.transform, false);
            var tTextRect = tTextObj.GetComponent<RectTransform>();
            tTextRect.anchorMin = Vector2.zero;
            tTextRect.anchorMax = Vector2.one;
            tTextRect.sizeDelta = Vector2.zero;

            toastText = tTextObj.GetComponent<TextMeshProUGUI>();
            toastText.alignment = TextAlignmentOptions.Center;
            toastText.fontSize = 18;
            toastText.fontStyle = FontStyles.Bold;
            toastText.color = new Color(1f, 0.85f, 0.4f, 1f);

            // 3. Banner Kentongan Alert
            alertBannerObj = new GameObject("KentonganAlertBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            alertBannerObj.transform.SetParent(transform, false);
            var alertRect = alertBannerObj.GetComponent<RectTransform>();
            alertRect.anchorMin = new Vector2(0.5f, 0.85f);
            alertRect.anchorMax = new Vector2(0.5f, 0.85f);
            alertRect.pivot = new Vector2(0.5f, 0.5f);
            alertRect.anchoredPosition = Vector2.zero;
            alertRect.sizeDelta = new Vector2(560f, 54f);

            var alertBg = alertBannerObj.GetComponent<Image>();
            alertBg.color = new Color(0.6f, 0.08f, 0.08f, 0.92f);

            alertCanvasGroup = alertBannerObj.AddComponent<CanvasGroup>();
            alertCanvasGroup.alpha = 0f;

            var aTextObj = new GameObject("AlertText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            aTextObj.transform.SetParent(alertBannerObj.transform, false);
            var aTextRect = aTextObj.GetComponent<RectTransform>();
            aTextRect.anchorMin = Vector2.zero;
            aTextRect.anchorMax = Vector2.one;
            aTextRect.sizeDelta = Vector2.zero;

            alertText = aTextObj.GetComponent<TextMeshProUGUI>();
            alertText.alignment = TextAlignmentOptions.Center;
            alertText.fontSize = 20;
            alertText.fontStyle = FontStyles.Bold;
            alertText.color = Color.white;
            alertText.text = "PERINGATAN KENTONGAN: HANTU BERADA DI SEKITAR!";

            // 4. Banner Prompt Interaksi ([E] Ambil Item)
            interactPromptObj = new GameObject("InteractPromptBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            interactPromptObj.transform.SetParent(transform, false);
            var promptRect = interactPromptObj.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 132f);
            promptRect.sizeDelta = new Vector2(280f, 38f);

            var pBg = interactPromptObj.GetComponent<Image>();
            pBg.color = new Color(0.08f, 0.08f, 0.12f, 0.90f);

            interactPromptCanvasGroup = interactPromptObj.AddComponent<CanvasGroup>();
            interactPromptCanvasGroup.alpha = 0f;

            var pTextObj = new GameObject("PromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            pTextObj.transform.SetParent(interactPromptObj.transform, false);
            var pTextRect = pTextObj.GetComponent<RectTransform>();
            pTextRect.anchorMin = Vector2.zero;
            pTextRect.anchorMax = Vector2.one;
            pTextRect.sizeDelta = Vector2.zero;

            interactPromptText = pTextObj.GetComponent<TextMeshProUGUI>();
            interactPromptText.alignment = TextAlignmentOptions.Center;
            interactPromptText.fontSize = 16;
            interactPromptText.fontStyle = FontStyles.Bold;
            interactPromptText.color = new Color(1f, 0.92f, 0.45f, 1f);
            interactPromptText.text = "[E] Ambil Item";

            UpdateLayoutPosition();
        }

        public void ShowInteractPrompt(string text)
        {
            if (interactPromptText != null) interactPromptText.text = text;
            if (interactPromptCanvasGroup != null) interactPromptCanvasGroup.alpha = 1f;
        }

        public void HideInteractPrompt()
        {
            if (interactPromptCanvasGroup != null) interactPromptCanvasGroup.alpha = 0f;
        }

        private void RebuildSlotsUI()
        {
            if (slotsContainer == null) return;

            foreach (var slot in slotUIList)
            {
                if (slot.root != null) Destroy(slot.root);
            }
            slotUIList.Clear();

            int maxSlots = targetInventory != null ? targetInventory.MaxSlots : 2;
            slotsContainer.sizeDelta = new Vector2(maxSlots * 94f + 20f, 90f);

            for (int i = 0; i < maxSlots; i++)
            {
                var elements = CreateSingleSlotUI(i);
                slotUIList.Add(elements);
            }

            UpdateSlotsDisplay();
        }

        private SlotUIElements CreateSingleSlotUI(int slotIndex)
        {
            var elements = new SlotUIElements();

            // Root Slot Box dengan Button component agar BISA DIKLIK MOUSE!
            var slotRoot = new GameObject($"Slot_{slotIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            slotRoot.transform.SetParent(slotsContainer, false);
            var rootRect = slotRoot.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(80f, 80f);

            var bg = slotRoot.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.16f, 0.88f);
            elements.root = slotRoot;
            elements.background = bg;

            var btn = slotRoot.GetComponent<Button>();
            int capturedIdx = slotIndex;
            btn.onClick.AddListener(() =>
            {
                if (targetInventory != null)
                {
                    targetInventory.TrySelectSlot(capturedIdx);
                }
            });
            elements.button = btn;

            // Highlight Border (Bingkai Emas saat di-equip)
            var borderObj = new GameObject("HighlightBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            borderObj.transform.SetParent(slotRoot.transform, false);
            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = new Vector2(6f, 6f);
            borderRect.anchoredPosition = Vector2.zero;

            var borderImg = borderObj.GetComponent<Image>();
            borderImg.color = new Color(1.0f, 0.82f, 0.2f, 0.95f);
            borderObj.SetActive(false);
            elements.highlightBorder = borderImg;

            // Cooldown Overlay (Gelap memutar saat cooldown)
            var cdObj = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cdObj.transform.SetParent(slotRoot.transform, false);
            var cdRect = cdObj.GetComponent<RectTransform>();
            cdRect.anchorMin = Vector2.zero;
            cdRect.anchorMax = Vector2.one;
            cdRect.sizeDelta = Vector2.zero;

            var cdImg = cdObj.GetComponent<Image>();
            cdImg.color = new Color(0f, 0f, 0f, 0.65f);
            cdImg.type = Image.Type.Filled;
            cdImg.fillMethod = Image.FillMethod.Radial360;
            cdImg.fillOrigin = (int)Image.Origin360.Top;
            cdImg.fillClockwise = false;
            cdImg.fillAmount = 0f;
            elements.cooldownOverlay = cdImg;

            // Hotkey Badge [1], [2], [3], [4]
            var badgeObj = new GameObject("HotkeyBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObj.transform.SetParent(slotRoot.transform, false);
            var badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(4f, -4f);
            badgeRect.sizeDelta = new Vector2(24f, 24f);

            var badgeImg = badgeObj.GetComponent<Image>();
            badgeImg.color = new Color(0.22f, 0.24f, 0.30f, 0.95f);

            var badgeTextObj = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            badgeTextObj.transform.SetParent(badgeObj.transform, false);
            var bTextRect = badgeTextObj.GetComponent<RectTransform>();
            bTextRect.anchorMin = Vector2.zero;
            bTextRect.anchorMax = Vector2.one;
            bTextRect.sizeDelta = Vector2.zero;

            var bText = badgeTextObj.GetComponent<TextMeshProUGUI>();
            bText.alignment = TextAlignmentOptions.Center;
            bText.fontSize = 14;
            bText.fontStyle = FontStyles.Bold;
            bText.color = new Color(1f, 0.9f, 0.4f, 1f);
            bText.text = $"{slotIndex + 1}";
            elements.hotkeyBadgeText = bText;

            // Item Name / Label
            var nameObj = new GameObject("ItemNameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(slotRoot.transform, false);
            var nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0.72f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = Vector2.zero;

            var nText = nameObj.GetComponent<TextMeshProUGUI>();
            nText.alignment = TextAlignmentOptions.Center;
            nText.fontSize = 12;
            nText.fontStyle = FontStyles.Bold;
            nText.enableWordWrapping = true;
            nText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            elements.itemNameText = nText;

            return elements;
        }

        private void UpdateSlotsDisplay()
        {
            if (targetInventory == null) return;

            int equippedIndex = targetInventory.EquippedSlotIndex;

            for (int i = 0; i < slotUIList.Count; i++)
            {
                var elements = slotUIList[i];
                ItemType item = targetInventory.GetItemAtSlot(i);

                if (item != ItemType.None)
                {
                    var cfg = ItemConfig.GetDefaultConfig(item);
                    elements.itemNameText.text = cfg.itemName;
                    elements.itemNameText.color = Color.white;

                    float cdNorm = targetInventory.GetCooldownNormalized(item);
                    if (elements.cooldownOverlay != null)
                    {
                        elements.cooldownOverlay.fillAmount = cdNorm;
                    }
                }
                else
                {
                    elements.itemNameText.text = "- Kosong -";
                    elements.itemNameText.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                    if (elements.cooldownOverlay != null)
                    {
                        elements.cooldownOverlay.fillAmount = 0f;
                    }
                }

                bool isEquipped = (i == equippedIndex && item != ItemType.None);
                if (elements.highlightBorder != null)
                {
                    elements.highlightBorder.gameObject.SetActive(isEquipped);
                }

                if (elements.background != null)
                {
                    elements.background.color = isEquipped ? new Color(0.28f, 0.24f, 0.10f, 0.98f) : new Color(0.12f, 0.12f, 0.16f, 0.88f);
                }
            }
        }

        public void ShowToastFeedback(string msg)
        {
            if (toastCoroutine != null) StopCoroutine(toastCoroutine);
            toastCoroutine = StartCoroutine(CoShowToast(msg));
        }

        private IEnumerator CoShowToast(string msg)
        {
            if (toastText != null) toastText.text = msg;
            if (toastCanvasGroup != null)
            {
                toastCanvasGroup.alpha = 1f;
                yield return new WaitForSeconds(2.0f);

                float elapsed = 0f;
                while (elapsed < 0.5f)
                {
                    elapsed += Time.deltaTime;
                    toastCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.5f);
                    yield return null;
                }
                toastCanvasGroup.alpha = 0f;
            }
        }

        public void ShowKentonganAlert(Vector3 soundPos)
        {
            if (alertCoroutine != null) StopCoroutine(alertCoroutine);
            alertCoroutine = StartCoroutine(CoShowAlert());
        }

        private IEnumerator CoShowAlert()
        {
            if (alertCanvasGroup != null)
            {
                alertCanvasGroup.alpha = 1f;
                yield return new WaitForSeconds(4.0f);

                float elapsed = 0f;
                while (elapsed < 0.8f)
                {
                    elapsed += Time.deltaTime;
                    alertCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.8f);
                    yield return null;
                }
                alertCanvasGroup.alpha = 0f;
            }
        }
    }
}
