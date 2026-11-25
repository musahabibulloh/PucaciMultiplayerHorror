using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Gameplay;
using Pucaci.Player;
using UnityEngine.SceneManagement;

namespace Pucaci.UI
{
    /// <summary>
    /// Menampilkan HUD objektif misi sesajen, UI bulat radial ring pada crosshair saat menghancurkan/memperbaiki sesajen,
    /// indikator transformasi Impostor [T], banner peran di awal, dan layar kemenangan (Survivor / Impostor).
    /// </summary>
    public class SesajenObjectiveUI : MonoBehaviour
    {
        public static SesajenObjectiveUI Instance { get; private set; }

        [Header("Objective HUD (Top Left)")]
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private TextMeshProUGUI objectiveText;

        [Header("Circular Crosshair Ring HUD")]
        [SerializeField] private Image crosshairRadialRing;
        [SerializeField] private Image crosshairRadialRingBG;
        [SerializeField] private TextMeshProUGUI crosshairPromptText;

        [Header("Impostor Transform HUD (Bottom Center)")]
        [SerializeField] private GameObject transformHUDPanel;
        [SerializeField] private TextMeshProUGUI transformHUDText;

        [Header("Role Announcement Splash Banner")]
        [SerializeField] private GameObject roleSplashPanel;
        [SerializeField] private TextMeshProUGUI roleSplashTitle;
        [SerializeField] private TextMeshProUGUI roleSplashDesc;

        [Header("Victory Screen Modal")]
        [SerializeField] private GameObject victoryModalPanel;
        [SerializeField] private TextMeshProUGUI victoryTitleText;
        [SerializeField] private TextMeshProUGUI victorySubtitleText;
        [SerializeField] private Button victoryMenuButton;

        [Header("Ring Colors")]
        [SerializeField] private Color ringBgColor = new Color(0.1f, 0.1f, 0.1f, 0.45f);
        [SerializeField] private Color ringFillColor = new Color(1.0f, 0.55f, 0.15f, 1.0f); // Oranye bara api (hancurkan)
        [SerializeField] private Color ringCompleteColor = new Color(0.2f, 1.0f, 0.4f, 1.0f); // Hijau saat selesai
        [SerializeField] private Color repairRingColor = new Color(0.65f, 0.25f, 1.0f, 1.0f); // Ungu mistis (perbaiki)
        [SerializeField] private Color repairCompleteColor = new Color(0.1f, 0.95f, 1.0f, 1.0f); // Cyan gaib saat selesai perbaikan

        private PlayerSesajenInteractor localInteractor;
        private PlayerRoleManager localRoleManager;
        private CharacterModelManager localModelManager;
        private float splashTimer = 6.0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            BuildUIHierarchyIfNeeded();
            EnsureCrosshairRadialRing();

            if (objectivePanel != null && objectivePanel.TryGetComponent<RectTransform>(out var panelRt))
            {
                panelRt.sizeDelta = new Vector2(480f, 48f);
            }
        }

        private void Start()
        {
            if (SesajenManager.Instance != null)
            {
                SesajenManager.Instance.OnObjectiveProgressChanged += UpdateObjectiveDisplay;
                SesajenManager.Instance.OnSurvivorVictory += ShowSurvivorVictoryScreen;
                SesajenManager.Instance.OnImpostorVictory += ShowImpostorVictoryScreen;
                UpdateObjectiveDisplay(SesajenManager.Instance.DestroyedCount, SesajenManager.Instance.TotalSesajen);
            }

            if (victoryMenuButton != null)
            {
                victoryMenuButton.onClick.AddListener(OnMenuButtonClicked);
            }

            EnsureCrosshairRadialRing();
        }

        private void OnDestroy()
        {
            if (SesajenManager.Instance != null)
            {
                SesajenManager.Instance.OnObjectiveProgressChanged -= UpdateObjectiveDisplay;
                SesajenManager.Instance.OnSurvivorVictory -= ShowSurvivorVictoryScreen;
                SesajenManager.Instance.OnImpostorVictory -= ShowImpostorVictoryScreen;
            }
        }

        private void Update()
        {
            if (crosshairRadialRing == null || crosshairRadialRingBG == null)
            {
                EnsureCrosshairRadialRing();
            }

            if (localInteractor == null)
            {
                FindLocalPlayer();
            }

            UpdateInteractionHUD();
            UpdateTransformHUD();
            UpdateRoleSplash();
        }

        private void FindLocalPlayer()
        {
            var interactors = FindObjectsOfType<PlayerSesajenInteractor>();
            foreach (var p in interactors)
            {
                var netId = p.GetComponent<Mirror.NetworkIdentity>();
                if (netId == null || netId.isLocalPlayer)
                {
                    localInteractor = p;
                    localRoleManager = p.GetComponent<PlayerRoleManager>();
                    localModelManager = p.GetComponent<CharacterModelManager>();
                    if (localRoleManager != null)
                    {
                        localRoleManager.OnRoleAssignedEvent -= OnRoleAssigned;
                        localRoleManager.OnRoleAssignedEvent += OnRoleAssigned;
                        ShowRoleAnnouncement(localRoleManager.CurrentRole);
                    }
                    if (SesajenManager.Instance != null)
                    {
                        UpdateObjectiveDisplay(SesajenManager.Instance.DestroyedCount, SesajenManager.Instance.TotalSesajen);
                    }
                    else
                    {
                        UpdateObjectiveDisplay(0, 5);
                    }
                    break;
                }
            }
        }

        private void OnRoleAssigned(PlayerRole role)
        {
            ShowRoleAnnouncement(role);
            if (SesajenManager.Instance != null)
            {
                UpdateObjectiveDisplay(SesajenManager.Instance.DestroyedCount, SesajenManager.Instance.TotalSesajen);
            }
            else
            {
                UpdateObjectiveDisplay(0, 5);
            }
        }

        private void ShowRoleAnnouncement(PlayerRole role)
        {
            splashTimer = 6.0f;
            if (roleSplashPanel != null)
            {
                roleSplashPanel.SetActive(true);
            }

            if (role == PlayerRole.Impostor)
            {
                string disguise = localRoleManager != null ? CharacterSelectionData.GetCharacterName(localRoleManager.HumanDisguise) : "Pak Mamat";
                if (roleSplashTitle != null)
                {
                    roleSplashTitle.text = "<color=#FF1133><b>PERAN: HANTU (PENIRU WUJUD)</b></color>";
                }
                if (roleSplashDesc != null)
                {
                    roleSplashDesc.text = $"Kamu menyamar sebagai <b>{disguise}</b>!\n<b>[X] Menu Penyamaran | [T] Wujud Kuntilanak</b>\nBunuh semua karakter sebelum seluruh sesajen dihancurkan!";
                }
            }
            else
            {
                string myName = localRoleManager != null ? CharacterSelectionData.GetCharacterName(localRoleManager.HumanDisguise) : "Warga";
                if (roleSplashTitle != null)
                {
                    roleSplashTitle.text = $"<color=#33FF77><b>PERAN: SURVIVOR ({myName.ToUpper()})</b></color>";
                }
                if (roleSplashDesc != null)
                {
                    roleSplashDesc.text = "Salah satu warga desa adalah Hantu Peniru Wujud!\nCari & hancurkan <b>5 sesajen</b> di Desa Pucaci! Waspadai temanmu!";
                }
            }
        }

        private void UpdateRoleSplash()
        {
            if (splashTimer > 0f)
            {
                splashTimer -= Time.deltaTime;
                if (splashTimer <= 0f && roleSplashPanel != null)
                {
                    roleSplashPanel.SetActive(false);
                }
            }
        }

        private void UpdateTransformHUD()
        {
            if (transformHUDPanel == null) return;

            if (localRoleManager == null || !localRoleManager.IsImpostor)
            {
                transformHUDPanel.SetActive(false);
                return;
            }

            transformHUDPanel.SetActive(true);

            if (transformHUDText != null)
            {
                bool isKunti = localRoleManager.IsKuntilanakForm;
                float cd = localRoleManager.TransformCooldownRemaining;
                CharacterType disguise = localRoleManager.HumanDisguise;

                string formLabel;
                if (isKunti)
                {
                    formLabel = "<color=#FF2244><b>KUNTILANAK (Wujud Asli)</b></color>";
                }
                else
                {
                    string charName = CharacterSelectionData.GetCharacterName(disguise);
                    formLabel = $"<color=#00FFAA><b>{charName.ToUpper()} (Meniru)</b></color>";
                }

                string cdLabel = cd > 0.05f
                    ? $"<color=#FF6666>Cooldown: {cd:F1}s</color>"
                    : $"<color=#FFFF00>[1] Laras  [2] Mamat  [3] Vincent  [T] Kunti</color>";

                transformHUDText.text = $"Wujud: {formLabel} | {cdLabel}";
            }
        }

        private void EnsureCrosshairRadialRing()
        {
            if (crosshairRadialRing != null && crosshairRadialRingBG != null && crosshairPromptText != null) return;

            var crosshair = GameObject.Find("CrosshairDot");
            if (crosshair == null) return;

            Sprite ringSprite = CreateProceduralRingSprite(64, 4.5f);

            // 1. Cincin latar belakang redup (Background Ring)
            var existingBg = crosshair.transform.Find("SesajenRadialRing_BG");
            if (existingBg != null)
            {
                crosshairRadialRingBG = existingBg.GetComponent<Image>();
            }
            else
            {
                var bgObj = new GameObject("SesajenRadialRing_BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgObj.transform.SetParent(crosshair.transform, false);

                var rtBg = bgObj.GetComponent<RectTransform>();
                rtBg.anchorMin = new Vector2(0.5f, 0.5f);
                rtBg.anchorMax = new Vector2(0.5f, 0.5f);
                rtBg.anchoredPosition = Vector2.zero;
                rtBg.sizeDelta = new Vector2(48f, 48f);

                crosshairRadialRingBG = bgObj.GetComponent<Image>();
                crosshairRadialRingBG.sprite = ringSprite;
                crosshairRadialRingBG.color = ringBgColor;
                crosshairRadialRingBG.raycastTarget = false;
                crosshairRadialRingBG.enabled = false;
            }

            // 2. Cincin pengisi progress radial bulat (Foreground Fill Ring)
            var existingFill = crosshair.transform.Find("SesajenRadialRing");
            if (existingFill != null)
            {
                crosshairRadialRing = existingFill.GetComponent<Image>();
            }
            else
            {
                var ringObj = new GameObject("SesajenRadialRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ringObj.transform.SetParent(crosshair.transform, false);

                var rt = ringObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(48f, 48f);

                crosshairRadialRing = ringObj.GetComponent<Image>();
                crosshairRadialRing.sprite = ringSprite;
                crosshairRadialRing.type = Image.Type.Filled;
                crosshairRadialRing.fillMethod = Image.FillMethod.Radial360;
                crosshairRadialRing.fillOrigin = (int)Image.Origin360.Top;
                crosshairRadialRing.fillClockwise = true;
                crosshairRadialRing.color = ringFillColor;
                crosshairRadialRing.raycastTarget = false;
                crosshairRadialRing.enabled = false;
            }

            // 3. Prompt tombol minimalis tepat di bawah cincin crosshair
            var existingPrompt = crosshair.transform.Find("SesajenPromptText");
            if (existingPrompt != null)
            {
                crosshairPromptText = existingPrompt.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var promptObj = new GameObject("SesajenPromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                promptObj.transform.SetParent(crosshair.transform, false);

                var rtPrompt = promptObj.GetComponent<RectTransform>();
                rtPrompt.anchorMin = new Vector2(0.5f, 0.5f);
                rtPrompt.anchorMax = new Vector2(0.5f, 0.5f);
                rtPrompt.anchoredPosition = new Vector2(0f, -32f);
                rtPrompt.sizeDelta = new Vector2(300f, 26f);

                crosshairPromptText = promptObj.GetComponent<TextMeshProUGUI>();
                crosshairPromptText.fontSize = 12f;
                crosshairPromptText.alignment = TextAlignmentOptions.Center;
                crosshairPromptText.color = new Color(1.0f, 0.95f, 0.85f, 0.9f);
                crosshairPromptText.raycastTarget = false;
                crosshairPromptText.text = "<b>[TAHAN E]</b>";
                crosshairPromptText.enabled = false;
            }
        }

        private static Sprite CreateProceduralRingSprite(int size, float ringWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float rOuter = size * 0.46f;
            float rInner = rOuter - ringWidth;

            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = 0f;
                    if (d <= rOuter && d >= rInner)
                    {
                        float edge1 = Mathf.Clamp01((rOuter - d) * 2.0f);
                        float edge2 = Mathf.Clamp01((d - rInner) * 2.0f);
                        alpha = Mathf.Min(edge1, edge2);
                    }
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public void UpdateObjectiveDisplay(int destroyed, int total)
        {
            if (objectiveText == null) return;

            bool isImpostor = (localRoleManager != null && localRoleManager.IsImpostor);

            if (isImpostor)
            {
                // Misi khusus Impostor
                objectiveText.text = "<b><color=#FF3344>MISI:</color></b> Bunuh semua karakter, berubah wujud pencet T";
            }
            else
            {
                // Misi Survivor / Player
                int targetTotal = total > 0 ? total : 5;
                if (destroyed >= targetTotal && targetTotal > 0)
                {
                    objectiveText.text = "<color=#00FF66><b>MISI SELESAI:</b> SEMUA SESAJEN HANCUR!</color>";
                }
                else
                {
                    objectiveText.text = $"<b>MISI:</b> Hancurkan Sesajen <b>({destroyed}/{targetTotal})</b>";
                }
            }
        }

        private void UpdateInteractionHUD()
        {
            if (localInteractor == null)
            {
                HideCircularHUD();
                return;
            }

            var mode = localInteractor.CurrentMode;

            if (mode == SesajenInteractionMode.None)
            {
                HideCircularHUD();
                return;
            }

            if (mode == SesajenInteractionMode.BlockedGameNotStarted)
            {
                // Game belum dimulai (masih di lobby/menunggu pemain)
                if (crosshairRadialRingBG != null) crosshairRadialRingBG.enabled = false;
                if (crosshairRadialRing != null) crosshairRadialRing.enabled = false;
                if (crosshairPromptText != null)
                {
                    crosshairPromptText.enabled = true;
                    crosshairPromptText.text = "<color=#FFFF77><b>Tunggu Permainan Dimulai Untuk Menghancurkan Sesajen</b></color>";
                }
                return;
            }

            if (mode == SesajenInteractionMode.BlockedMustBeHuman)
            {
                // Impostor dalam wujud Kuntilanak melihat sesajen hancur
                if (crosshairRadialRingBG != null) crosshairRadialRingBG.enabled = false;
                if (crosshairRadialRing != null) crosshairRadialRing.enabled = false;
                if (crosshairPromptText != null)
                {
                    crosshairPromptText.enabled = true;
                    crosshairPromptText.text = "<color=#FF6666><b>[1 / 2 / 3 / T] Berubah Ke Manusia Untuk Memperbaiki</b></color>";
                }
                return;
            }

            // Mode Hancurkan (Survivor) atau Mode Perbaiki (Impostor Human)
            if (crosshairRadialRingBG != null) crosshairRadialRingBG.enabled = true;
            if (crosshairRadialRing != null) crosshairRadialRing.enabled = true;
            if (crosshairPromptText != null) crosshairPromptText.enabled = true;

            bool isRepair = (mode == SesajenInteractionMode.Repair);
            Color baseColor = isRepair ? repairRingColor : ringFillColor;
            Color compColor = isRepair ? repairCompleteColor : ringCompleteColor;

            if (localInteractor.IsInteracting)
            {
                float prog = localInteractor.InteractionProgress;
                if (crosshairRadialRing != null)
                {
                    crosshairRadialRing.fillAmount = prog;
                    crosshairRadialRing.color = Color.Lerp(baseColor, compColor, prog * prog);
                }

                if (crosshairPromptText != null)
                {
                    int percent = Mathf.RoundToInt(prog * 100f);
                    string actionLabel = isRepair ? "MEMPERBAIKI" : "MENGHANCURKAN";
                    crosshairPromptText.text = $"<b>{actionLabel} {percent}%</b>";
                    crosshairPromptText.color = isRepair ? new Color(0.3f, 0.9f, 1f, 1f) : new Color(1f, 0.75f, 0.2f, 1f);
                }
            }
            else
            {
                if (crosshairRadialRing != null)
                {
                    crosshairRadialRing.fillAmount = 0f;
                    crosshairRadialRing.color = baseColor;
                }

                if (crosshairPromptText != null)
                {
                    crosshairPromptText.text = isRepair ? "<b>[TAHAN E] PERBAIKI SESAJEN</b>" : "<b>[TAHAN E] HANCURKAN SESAJEN</b>";
                    crosshairPromptText.color = isRepair ? new Color(0.8f, 0.6f, 1.0f, 0.95f) : new Color(1.0f, 0.95f, 0.85f, 0.9f);
                }
            }
        }

        private void HideCircularHUD()
        {
            if (crosshairRadialRingBG != null) crosshairRadialRingBG.enabled = false;
            if (crosshairRadialRing != null)
            {
                crosshairRadialRing.enabled = false;
                crosshairRadialRing.fillAmount = 0f;
            }
            if (crosshairPromptText != null) crosshairPromptText.enabled = false;
        }

        public void ShowSurvivorVictoryScreen()
        {
            HideCircularHUD();

            if (victoryModalPanel != null)
            {
                victoryModalPanel.SetActive(true);
            }

            if (victoryTitleText != null)
            {
                victoryTitleText.text = "<color=#33FF77><b>SURVIVOR MENANG!</b></color>";
            }

            if (victorySubtitleText != null)
            {
                victorySubtitleText.text = "Seluruh sesajen di Desa Pucaci telah dihancurkan!\nKuntilanak kehilangan kekuatannya dan berhasil diusir selamanya.";
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ShowImpostorVictoryScreen()
        {
            HideCircularHUD();

            if (victoryModalPanel != null)
            {
                victoryModalPanel.SetActive(true);
            }

            if (victoryTitleText != null)
            {
                victoryTitleText.text = "<color=#FF1133><b>KUNTILANAK (IMPOSTOR) MENANG!</b></color>";
            }

            if (victorySubtitleText != null)
            {
                victorySubtitleText.text = "Seluruh survivor telah dihabisi tanpa ampun!\nDesa Pucaci kini sepenuhnya dikuasai Kuntilanak selamanya.";
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnMenuButtonClicked()
        {
            if (Mirror.NetworkManager.singleton != null)
            {
                Mirror.NetworkManager.singleton.StopHost();
            }
            SceneManager.LoadScene(0); // Load Main Menu
        }

        private void BuildUIHierarchyIfNeeded()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            Transform root = transform;

            // 1. Objective HUD Panel (Pojok Kiri Atas)
            if (objectivePanel == null)
            {
                var existingObj = root.Find("SesajenObjectivePanel");
                if (existingObj != null)
                {
                    objectivePanel = existingObj.gameObject;
                    objectiveText = objectivePanel.GetComponentInChildren<TextMeshProUGUI>();
                }
                else
                {
                    var panelObj = new GameObject("SesajenObjectivePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    panelObj.transform.SetParent(root, false);

                    var rt = panelObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(25f, -25f);
                    rt.sizeDelta = new Vector2(480f, 48f);

                    var img = panelObj.GetComponent<Image>();
                    img.color = new Color(0.08f, 0.08f, 0.10f, 0.80f);

                    var textObj = new GameObject("ObjectiveText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    textObj.transform.SetParent(panelObj.transform, false);

                    var textRt = textObj.GetComponent<RectTransform>();
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.offsetMin = new Vector2(12f, 0f);
                    textRt.offsetMax = new Vector2(-12f, 0f);

                    objectiveText = textObj.GetComponent<TextMeshProUGUI>();
                    objectiveText.fontSize = 15f;
                    objectiveText.alignment = TextAlignmentOptions.MidlineLeft;
                    objectiveText.color = new Color(1.0f, 0.85f, 0.35f, 1.0f);
                    objectiveText.text = "<b>MISI:</b> Hancurkan Semua Sesajen";

                    objectivePanel = panelObj;
                }
            }

            // 2. Impostor Transform HUD Panel (Bawah Tengah)
            if (transformHUDPanel == null)
            {
                var existingT = root.Find("ImpostorTransformHUD");
                if (existingT != null)
                {
                    transformHUDPanel = existingT.gameObject;
                    transformHUDText = transformHUDPanel.GetComponentInChildren<TextMeshProUGUI>();
                }
                else
                {
                    var tObj = new GameObject("ImpostorTransformHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    tObj.transform.SetParent(root, false);

                    var rt = tObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.08f);
                    rt.anchorMax = new Vector2(0.5f, 0.08f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(520f, 36f);

                    var img = tObj.GetComponent<Image>();
                    img.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

                    var textObj = new GameObject("TransformText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    textObj.transform.SetParent(tObj.transform, false);

                    var textRt = textObj.GetComponent<RectTransform>();
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;

                    transformHUDText = textObj.GetComponent<TextMeshProUGUI>();
                    transformHUDText.fontSize = 13f;
                    transformHUDText.alignment = TextAlignmentOptions.Center;
                    transformHUDText.color = Color.white;
                    transformHUDText.text = "Wujud: MANUSIA | [1] Laras  [2] Mamat  [3] Vincent  [T] Kunti";

                    transformHUDPanel = tObj;
                    transformHUDPanel.SetActive(false);
                }
            }

            // 3. Role Announcement Splash Panel (Tengah Layar Atas)
            if (roleSplashPanel == null)
            {
                var existingSplash = root.Find("RoleSplashBanner");
                if (existingSplash != null)
                {
                    roleSplashPanel = existingSplash.gameObject;
                    roleSplashTitle = roleSplashPanel.transform.Find("SplashTitle")?.GetComponent<TextMeshProUGUI>();
                    roleSplashDesc = roleSplashPanel.transform.Find("SplashDesc")?.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    var sObj = new GameObject("RoleSplashBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    sObj.transform.SetParent(root, false);

                    var rt = sObj.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.80f);
                    rt.anchorMax = new Vector2(0.5f, 0.80f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(520f, 75f);

                    var img = sObj.GetComponent<Image>();
                    img.color = new Color(0.04f, 0.04f, 0.06f, 0.92f);

                    // Title
                    var titleObj = new GameObject("SplashTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    titleObj.transform.SetParent(sObj.transform, false);
                    var tRt = titleObj.GetComponent<RectTransform>();
                    tRt.anchorMin = new Vector2(0f, 0.5f);
                    tRt.anchorMax = new Vector2(1f, 1f);
                    tRt.offsetMin = new Vector2(10f, 0f);
                    tRt.offsetMax = new Vector2(-10f, 0f);

                    roleSplashTitle = titleObj.GetComponent<TextMeshProUGUI>();
                    roleSplashTitle.fontSize = 17f;
                    roleSplashTitle.alignment = TextAlignmentOptions.Center;
                    roleSplashTitle.color = Color.white;

                    // Desc
                    var descObj = new GameObject("SplashDesc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    descObj.transform.SetParent(sObj.transform, false);
                    var dRt = descObj.GetComponent<RectTransform>();
                    dRt.anchorMin = new Vector2(0f, 0f);
                    dRt.anchorMax = new Vector2(1f, 0.55f);
                    dRt.offsetMin = new Vector2(10f, 0f);
                    dRt.offsetMax = new Vector2(-10f, 0f);

                    roleSplashDesc = descObj.GetComponent<TextMeshProUGUI>();
                    roleSplashDesc.fontSize = 12f;
                    roleSplashDesc.alignment = TextAlignmentOptions.Center;
                    roleSplashDesc.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

                    roleSplashPanel = sObj;
                    roleSplashPanel.SetActive(false);
                }
            }

            // 4. Victory Modal Screen
            if (victoryModalPanel == null)
            {
                var existingVic = root.Find("VictoryModalPanel");
                if (existingVic != null)
                {
                    victoryModalPanel = existingVic.gameObject;
                }
                else
                {
                    var vicObj = new GameObject("VictoryModalPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    vicObj.transform.SetParent(root, false);

                    var rt = vicObj.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    var img = vicObj.GetComponent<Image>();
                    img.color = new Color(0.04f, 0.05f, 0.07f, 0.94f);

                    // Title
                    var titleObj = new GameObject("VictoryTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    titleObj.transform.SetParent(vicObj.transform, false);
                    var titleRt = titleObj.GetComponent<RectTransform>();
                    titleRt.anchorMin = new Vector2(0.5f, 0.65f);
                    titleRt.anchorMax = new Vector2(0.5f, 0.65f);
                    titleRt.sizeDelta = new Vector2(700f, 90f);

                    victoryTitleText = titleObj.GetComponent<TextMeshProUGUI>();
                    victoryTitleText.fontSize = 46f;
                    victoryTitleText.alignment = TextAlignmentOptions.Center;
                    victoryTitleText.color = new Color(1.0f, 0.82f, 0.20f, 1.0f);
                    victoryTitleText.text = "<b>SURVIVOR MENANG!</b>";

                    // Subtitle
                    var subObj = new GameObject("VictorySubtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    subObj.transform.SetParent(vicObj.transform, false);
                    var subRt = subObj.GetComponent<RectTransform>();
                    subRt.anchorMin = new Vector2(0.5f, 0.50f);
                    subRt.anchorMax = new Vector2(0.5f, 0.50f);
                    subRt.sizeDelta = new Vector2(650f, 80f);

                    victorySubtitleText = subObj.GetComponent<TextMeshProUGUI>();
                    victorySubtitleText.fontSize = 20f;
                    victorySubtitleText.alignment = TextAlignmentOptions.Center;
                    victorySubtitleText.color = new Color(0.9f, 0.92f, 0.95f, 0.9f);
                    victorySubtitleText.text = "Seluruh sesajen di Desa Pucaci telah dihancurkan!\nKuntilanak kehilangan kekuatannya dan berhasil diusir selamanya.";

                    // Button
                    var btnObj = new GameObject("ReturnMenuButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    btnObj.transform.SetParent(vicObj.transform, false);
                    var btnRt = btnObj.GetComponent<RectTransform>();
                    btnRt.anchorMin = new Vector2(0.5f, 0.35f);
                    btnRt.anchorMax = new Vector2(0.5f, 0.35f);
                    btnRt.sizeDelta = new Vector2(240f, 48f);

                    var btnImg = btnObj.GetComponent<Image>();
                    btnImg.color = new Color(0.85f, 0.35f, 0.1f, 1.0f);
                    victoryMenuButton = btnObj.GetComponent<Button>();

                    var btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    btnTextObj.transform.SetParent(btnObj.transform, false);
                    var btnTextRt = btnTextObj.GetComponent<RectTransform>();
                    btnTextRt.anchorMin = Vector2.zero;
                    btnTextRt.anchorMax = Vector2.one;

                    var btnText = btnTextObj.GetComponent<TextMeshProUGUI>();
                    btnText.fontSize = 16f;
                    btnText.alignment = TextAlignmentOptions.Center;
                    btnText.color = Color.white;
                    btnText.text = "<b>KEMBALI KE MENU</b>";

                    victoryModalPanel = vicObj;
                    victoryModalPanel.SetActive(false);
                }
            }
        }
    }
}
