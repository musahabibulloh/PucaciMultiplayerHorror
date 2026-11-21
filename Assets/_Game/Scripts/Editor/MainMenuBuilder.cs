using System.Collections.Generic;
using Pucaci.Core;
using Pucaci.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pucaci.Editor
{
    public static class MainMenuBuilder
    {
        private static readonly Color ColorBg = new Color(11f / 255f, 15f / 255f, 26f / 255f, 0.85f); // #0B0F1A 85%
        private static readonly Color ColorText = new Color(242f / 255f, 232f / 255f, 213f / 255f, 1f); // #F2E8D5
        private static readonly Color ColorAccent = new Color(255f / 255f, 179f / 255f, 71f / 255f, 1f); // #FFB347
        private static readonly Color ColorDanger = new Color(192f / 255f, 57f / 255f, 43f / 255f, 1f); // #C0392B
        private static readonly Color ColorDisabled = new Color(85f / 255f, 85f / 255f, 85f / 255f, 1f); // #555555
        private static readonly Color ColorDarkProp = new Color(15f / 255f, 18f / 255f, 25f / 255f, 1f); // #0F1219

        [MenuItem("Tools/Pucaci/Build Main Menu Scene")]
        public static void BuildScene()
        {
            string scenePath = "Assets/_Game/Scenes/MainMenu.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Setup Camera & Ambience
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(11f / 255f, 15f / 255f, 26f / 255f, 1f);
            camObj.AddComponent<AudioListener>();
            camObj.transform.position = new Vector3(0f, 1.6f, -6f);
            camObj.transform.rotation = Quaternion.Euler(5f, 0f, 0f);

            AudioSource ambienceSource = camObj.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;

            // Fog settings
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(11f / 255f, 15f / 255f, 26f / 255f, 1f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.04f;

            // 2. Setup 3D Silhouettes (Pos Ronda, Lampu Jalan, Pagar)
            Build3DBackground();

            // 3. Setup SettingsManager
            GameObject settingsMgrObj = new GameObject("SettingsManager");
            SettingsManager settingsMgr = settingsMgrObj.AddComponent<SettingsManager>();
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Game/Audio/PucaciMixer.mixer");
            if (mixer != null)
            {
                settingsMgr.SetAudioMixer(mixer);
            }

            // 4. Setup EventSystem with InputSystemUIInputModule
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // 5. Setup Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Main Menu Controller on Canvas
            MainMenuController controller = canvasObj.AddComponent<MainMenuController>();
            canvasObj.AddComponent<StubMultiplayerLauncher>();

            // 6. Build Title & Header
            BuildTitleAndHeader(canvasObj.transform);

            // 7. Build Panels
            MenuPanel mainPanel = BuildMainPanel(canvasObj.transform, controller);
            MenuPanel charSelectPanel = BuildCharacterSelectPanel(canvasObj.transform, controller);
            MenuPanel mabarPanel = BuildMabarPanel(canvasObj.transform, controller);
            MenuPanel caraBermainPanel = BuildCaraBermainPanel(canvasObj.transform, controller);
            MenuPanel settingsPanel = BuildSettingsPanel(canvasObj.transform, controller);
            MenuPanel creditsPanel = BuildCreditsPanel(canvasObj.transform, controller);
            MenuPanel quitDialog = BuildQuitDialog(canvasObj.transform, controller);

            // 8. Setup Version Text
            GameObject verObj = CreateTMPText(canvasObj.transform, "VersionText", "v1.0.0", 18, TextAlignmentOptions.BottomLeft);
            RectTransform verRect = verObj.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0f, 0f);
            verRect.anchorMax = new Vector2(0f, 0f);
            verRect.pivot = new Vector2(0f, 0f);
            verRect.anchoredPosition = new Vector2(25f, 20f);
            verRect.sizeDelta = new Vector2(300f, 30f);

            // 9. Setup SceneFader (Full screen overlay)
            BuildSceneFader(canvasObj.transform);

            // Connect Controller Serialized Fields
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            so.FindProperty("characterSelectPanel").objectReferenceValue = charSelectPanel;
            so.FindProperty("mabarPanel").objectReferenceValue = mabarPanel;
            so.FindProperty("caraBermainPanel").objectReferenceValue = caraBermainPanel;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("creditsPanel").objectReferenceValue = creditsPanel;
            so.FindProperty("quitConfirmDialog").objectReferenceValue = quitDialog;
            so.FindProperty("gameSceneName").stringValue = "Gameplay";
            so.FindProperty("versionText").objectReferenceValue = verObj.GetComponent<TMP_Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // 10. Update Build Settings (MainMenu at 0, PrototypeScene at 1)
            UpdateBuildSettings(scenePath);

            // 11. Save Scene
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MainMenuBuilder] Main Menu Scene berhasil dibangun dan disimpan ke {scenePath}!");
        }

        private static void Build3DBackground()
        {
            GameObject bgParent = new GameObject("Environment_Background");

            // Material gelap untuk siluet
            Material darkMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            darkMat.color = ColorDarkProp;

            // Ground
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(bgParent.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = darkMat;

            // Tiang Lampu Jalan
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "TiangLampu";
            pole.transform.SetParent(bgParent.transform);
            pole.transform.position = new Vector3(2.5f, 2.5f, 0f);
            pole.transform.localScale = new Vector3(0.12f, 2.5f, 0.12f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = darkMat;

            // Lampu Jalan Hangat (#FFB347)
            GameObject lightObj = new GameObject("LampuJalan");
            lightObj.transform.SetParent(pole.transform);
            lightObj.transform.position = new Vector3(2.5f, 4.8f, 0f);
            Light streetLight = lightObj.AddComponent<Light>();
            streetLight.type = LightType.Point;
            streetLight.color = ColorAccent;
            streetLight.range = 14f;
            streetLight.intensity = 2.8f;
            lightObj.AddComponent<LampFlicker>();

            // Catatan: Pos Ronda dan Pagar telah dihapus karena akan diimpor dari aset sendiri oleh user.
        }

        private static void BuildTitleAndHeader(Transform canvas)
        {
            GameObject headerObj = new GameObject("TitleHeader", typeof(RectTransform));
            headerObj.transform.SetParent(canvas, false);
            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -60f);
            headerRect.sizeDelta = new Vector2(800f, 180f);

            // Title: PUCACI
            GameObject titleObj = CreateTMPText(headerObj.transform, "TitleText", "PUCACI", 78, TextAlignmentOptions.Center);
            TMP_Text titleTMP = titleObj.GetComponent<TMP_Text>();
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = ColorAccent;
            titleObj.AddComponent<TitleFlicker>();

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.35f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Subtitle
            GameObject subObj = CreateTMPText(headerObj.transform, "SubtitleText", "Ronda malam, jangan berisik.", 24, TextAlignmentOptions.Center);
            TMP_Text subTMP = subObj.GetComponent<TMP_Text>();
            subTMP.color = ColorText;
            RectTransform subRect = subObj.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 0f);
            subRect.anchorMax = new Vector2(1f, 0.35f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
        }

        private static MenuPanel BuildMainPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "MainPanel", new Vector2(420f, 490f), new Vector2(0f, -40f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(20, 20, 25, 25);

            Button btnPlay = CreateButton(panelObj.transform, "BtnMulai", "Mulai Main", ColorAccent, ColorBg);
            Button btnMabar = CreateButton(panelObj.transform, "BtnMabar", "Mabar", ColorAccent, ColorBg);
            Button btnCara = CreateButton(panelObj.transform, "BtnCaraBermain", "Cara Bermain", ColorText, ColorBg);
            Button btnSettings = CreateButton(panelObj.transform, "BtnPengaturan", "Pengaturan", ColorText, ColorBg);
            Button btnCredits = CreateButton(panelObj.transform, "BtnKredit", "Kredit", ColorText, ColorBg);
            Button btnQuit = CreateButton(panelObj.transform, "BtnKeluar", "Keluar", ColorDanger, ColorBg);

            // Connect Serialized properties on controller
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = btnPlay;
            so.FindProperty("mabarButton").objectReferenceValue = btnMabar;
            so.FindProperty("caraBermainButton").objectReferenceValue = btnCara;
            so.FindProperty("settingsButton").objectReferenceValue = btnSettings;
            so.FindProperty("creditsButton").objectReferenceValue = btnCredits;
            so.FindProperty("quitButton").objectReferenceValue = btnQuit;
            so.ApplyModifiedPropertiesWithoutUndo();

            // First selected
            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnPlay;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildCharacterSelectPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "CharacterSelectPanel", new Vector2(1440f, 660f), new Vector2(0f, -30f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(30, 30, 25, 25);

            CreateTMPText(panelObj.transform, "Header", "PILIH KARAKTER", 30, TextAlignmentOptions.Center);
            CreateTMPText(panelObj.transform, "Subtitle", "Pilih siapa yang ingin kamu mainkan dalam ronda malam ini:", 18, TextAlignmentOptions.Center);

            // Cards Row
            GameObject cardsRow = new GameObject("CardsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardsRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup cLayout = cardsRow.GetComponent<HorizontalLayoutGroup>();
            cLayout.spacing = 16f;
            cLayout.childControlWidth = true;
            cLayout.childControlHeight = true;
            cardsRow.GetComponent<RectTransform>().sizeDelta = new Vector2(1380f, 430f);

            // --- Card 1: Laras ---
            GameObject cardLaras = new GameObject("Card_Laras", typeof(RectTransform), typeof(Image));
            cardLaras.transform.SetParent(cardsRow.transform, false);
            Image larasBorder = cardLaras.GetComponent<Image>();
            larasBorder.color = ColorAccent;

            GameObject larasInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            larasInner.transform.SetParent(cardLaras.transform, false);
            RectTransform liRect = larasInner.GetComponent<RectTransform>();
            liRect.anchorMin = Vector2.zero;
            liRect.anchorMax = Vector2.one;
            liRect.offsetMin = new Vector2(4f, 4f);
            liRect.offsetMax = new Vector2(-4f, -4f);
            larasInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup liLayout = larasInner.GetComponent<VerticalLayoutGroup>();
            liLayout.spacing = 10f;
            liLayout.childAlignment = TextAnchor.UpperCenter;
            liLayout.childControlWidth = true;
            liLayout.childControlHeight = false;
            liLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(larasInner.transform, "Name", "LARAS", 30, TextAlignmentOptions.Center);
            var roleLaras = CreateTMPText(larasInner.transform, "Role", "[ MAHASISWI KKN ]", 16, TextAlignmentOptions.Center);
            roleLaras.GetComponent<TMP_Text>().color = new Color(0.5f, 0.8f, 1f, 1f);

            string larasDesc = "Gadis pemberani dan lincah. Memiliki reflek gesit untuk melarikan diri dari kejaran dedemit kampung.";
            CreateTMPText(larasInner.transform, "Desc", larasDesc, 16, TextAlignmentOptions.TopLeft);

            string larasTraits = "• Langkah Lincah & Cepat\n• Reflek Gerak Responsif\n• Pengamatan Senter Tajam";
            var traitLaras = CreateTMPText(larasInner.transform, "Traits", larasTraits, 15, TextAlignmentOptions.TopLeft);
            traitLaras.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectLaras = CreateButton(larasInner.transform, "BtnSelectLaras", "[V] TERPILIH", ColorAccent, ColorBg, 340f);
            TMP_Text larasBadge = btnSelectLaras.GetComponentInChildren<TMP_Text>();

            // --- Card 2: Pak Mamat ---
            GameObject cardMamat = new GameObject("Card_PakMamat", typeof(RectTransform), typeof(Image));
            cardMamat.transform.SetParent(cardsRow.transform, false);
            Image mamatBorder = cardMamat.GetComponent<Image>();
            mamatBorder.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            GameObject mamatInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            mamatInner.transform.SetParent(cardMamat.transform, false);
            RectTransform miRect = mamatInner.GetComponent<RectTransform>();
            miRect.anchorMin = Vector2.zero;
            miRect.anchorMax = Vector2.one;
            miRect.offsetMin = new Vector2(4f, 4f);
            miRect.offsetMax = new Vector2(-4f, -4f);
            mamatInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup miLayout = mamatInner.GetComponent<VerticalLayoutGroup>();
            miLayout.spacing = 10f;
            miLayout.childAlignment = TextAnchor.UpperCenter;
            miLayout.childControlWidth = true;
            miLayout.childControlHeight = false;
            miLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(mamatInner.transform, "Name", "PAK MAMAT", 30, TextAlignmentOptions.Center);
            var roleMamat = CreateTMPText(mamatInner.transform, "Role", "[ HANSIP KAMPUNG ]", 16, TextAlignmentOptions.Center);
            roleMamat.GetComponent<TMP_Text>().color = new Color(1f, 0.7f, 0.4f, 1f);

            string mamatDesc = "Penjaga ronda senior kampung. Kenyang pengalaman mistis dan memiliki langkah mantap penuh waspada.";
            CreateTMPText(mamatInner.transform, "Desc", mamatDesc, 16, TextAlignmentOptions.TopLeft);

            string mamatTraits = "• Langkah Mantap & Siaga\n• Pengalaman Ronda Malam\n• Gaya Gerak Pos Hansip";
            var traitMamat = CreateTMPText(mamatInner.transform, "Traits", mamatTraits, 15, TextAlignmentOptions.TopLeft);
            traitMamat.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectMamat = CreateButton(mamatInner.transform, "BtnSelectMamat", "PILIH PAK MAMAT", ColorText, ColorBg, 340f);
            TMP_Text mamatBadge = btnSelectMamat.GetComponentInChildren<TMP_Text>();

            // --- Card 3: Kuntilanak ---
            GameObject cardKunti = new GameObject("Card_Kuntilanak", typeof(RectTransform), typeof(Image));
            cardKunti.transform.SetParent(cardsRow.transform, false);
            Image kuntiBorder = cardKunti.GetComponent<Image>();
            kuntiBorder.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            GameObject kuntiInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            kuntiInner.transform.SetParent(cardKunti.transform, false);
            RectTransform kiRect = kuntiInner.GetComponent<RectTransform>();
            kiRect.anchorMin = Vector2.zero;
            kiRect.anchorMax = Vector2.one;
            kiRect.offsetMin = new Vector2(4f, 4f);
            kiRect.offsetMax = new Vector2(-4f, -4f);
            kuntiInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup kiLayout = kuntiInner.GetComponent<VerticalLayoutGroup>();
            kiLayout.spacing = 10f;
            kiLayout.childAlignment = TextAnchor.UpperCenter;
            kiLayout.childControlWidth = true;
            kiLayout.childControlHeight = false;
            kiLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(kuntiInner.transform, "Name", "KUNTILANAK", 30, TextAlignmentOptions.Center);
            var roleKunti = CreateTMPText(kuntiInner.transform, "Role", "[ HANTU PENUNGGU ]", 16, TextAlignmentOptions.Center);
            roleKunti.GetComponent<TMP_Text>().color = new Color(0.85f, 0.6f, 1f, 1f);

            string kuntiDesc = "Sosok hantu wanita bergaun putih dengan rambut panjang terurai. Melayang tenang di kegelapan malam desa.";
            CreateTMPText(kuntiInner.transform, "Desc", kuntiDesc, 16, TextAlignmentOptions.TopLeft);

            string kuntiTraits = "• Gerakan Melayang Senyap\n• Langkah Tanpa Suara\n• Aura Dingin Mencekam";
            var traitKunti = CreateTMPText(kuntiInner.transform, "Traits", kuntiTraits, 15, TextAlignmentOptions.TopLeft);
            traitKunti.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectKunti = CreateButton(kuntiInner.transform, "BtnSelectKunti", "PILIH KUNTILANAK", ColorText, ColorBg, 340f);
            TMP_Text kuntiBadge = btnSelectKunti.GetComponentInChildren<TMP_Text>();

            // --- Card 4: Pak Vincent ---
            GameObject cardVincent = new GameObject("Card_PakVincent", typeof(RectTransform), typeof(Image));
            cardVincent.transform.SetParent(cardsRow.transform, false);
            Image vincentBorder = cardVincent.GetComponent<Image>();
            vincentBorder.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            GameObject vincentInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            vincentInner.transform.SetParent(cardVincent.transform, false);
            RectTransform viRect = vincentInner.GetComponent<RectTransform>();
            viRect.anchorMin = Vector2.zero;
            viRect.anchorMax = Vector2.one;
            viRect.offsetMin = new Vector2(4f, 4f);
            viRect.offsetMax = new Vector2(-4f, -4f);
            vincentInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup viLayout = vincentInner.GetComponent<VerticalLayoutGroup>();
            viLayout.spacing = 10f;
            viLayout.childAlignment = TextAnchor.UpperCenter;
            viLayout.childControlWidth = true;
            viLayout.childControlHeight = false;
            viLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(vincentInner.transform, "Name", "PAK VINCENT", 28, TextAlignmentOptions.Center);
            var roleVincent = CreateTMPText(vincentInner.transform, "Role", "[ PENYELIDIK DESA ]", 16, TextAlignmentOptions.Center);
            roleVincent.GetComponent<TMP_Text>().color = new Color(0.4f, 0.9f, 0.6f, 1f);

            string vincentDesc = "Pria tenang dan penuh perhitungan. Memiliki langkah terukur untuk menyelidiki misteri dan bertahan dari teror malam desa.";
            CreateTMPText(vincentInner.transform, "Desc", vincentDesc, 16, TextAlignmentOptions.TopLeft);

            string vincentTraits = "• Gerakan Taktis & Lincah\n• Pengamatan Waspada\n• Senter Taktis Handal";
            var traitVincent = CreateTMPText(vincentInner.transform, "Traits", vincentTraits, 15, TextAlignmentOptions.TopLeft);
            traitVincent.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectVincent = CreateButton(vincentInner.transform, "BtnSelectVincent", "PILIH PAK VINCENT", ColorText, ColorBg, 340f);
            TMP_Text vincentBadge = btnSelectVincent.GetComponentInChildren<TMP_Text>();

            // Bottom Action Row
            GameObject bottomRow = new GameObject("BottomRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bottomRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup bLayout = bottomRow.GetComponent<HorizontalLayoutGroup>();
            bLayout.spacing = 20f;
            bLayout.childControlWidth = true;
            bLayout.childControlHeight = true;
            bottomRow.GetComponent<RectTransform>().sizeDelta = new Vector2(1380f, 50f);

            Button btnStartWithChar = CreateButton(bottomRow.transform, "BtnStartPlay", "MULAI BERMAIN", ColorAccent, ColorBg, 850f);
            Button btnCharBack = CreateButton(bottomRow.transform, "BtnBack", "Kembali", ColorText, ColorBg, 450f);

            // Connect Serialized properties on controller
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("selectLarasCardButton").objectReferenceValue = btnSelectLaras;
            so.FindProperty("selectMamatCardButton").objectReferenceValue = btnSelectMamat;
            so.FindProperty("selectKuntiCardButton").objectReferenceValue = btnSelectKunti;
            so.FindProperty("selectVincentCardButton").objectReferenceValue = btnSelectVincent;
            so.FindProperty("larasCardBorder").objectReferenceValue = larasBorder;
            so.FindProperty("mamatCardBorder").objectReferenceValue = mamatBorder;
            so.FindProperty("kuntiCardBorder").objectReferenceValue = kuntiBorder;
            so.FindProperty("vincentCardBorder").objectReferenceValue = vincentBorder;
            so.FindProperty("larasStatusBadge").objectReferenceValue = larasBadge;
            so.FindProperty("mamatStatusBadge").objectReferenceValue = mamatBadge;
            so.FindProperty("kuntiStatusBadge").objectReferenceValue = kuntiBadge;
            so.FindProperty("vincentStatusBadge").objectReferenceValue = vincentBadge;
            so.FindProperty("startPlayWithCharacterButton").objectReferenceValue = btnStartWithChar;
            so.FindProperty("characterSelectBackButton").objectReferenceValue = btnCharBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            // First selected
            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnStartWithChar;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildMabarPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "MabarPanel", new Vector2(520f, 540f), new Vector2(0f, -40f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(30, 30, 20, 20);

            CreateTMPText(panelObj.transform, "Header", "MABAR (MULTIPLAYER)", 28, TextAlignmentOptions.Center);

            // Character Info Row in Mabar
            GameObject charRow = new GameObject("CharRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            charRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup chLayout = charRow.GetComponent<HorizontalLayoutGroup>();
            chLayout.spacing = 10f;
            chLayout.childControlWidth = true;
            chLayout.childControlHeight = true;
            charRow.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 40f);

            GameObject charTextObj = CreateTMPText(charRow.transform, "MabarCharText", "Karakter Aktif: <b>Laras</b>", 16, TextAlignmentOptions.MidlineLeft, 300f);
            Button btnChangeChar = CreateButton(charRow.transform, "BtnChangeChar", "Ganti", ColorAccent, ColorBg, 140f);

            Button btnHost = CreateButton(panelObj.transform, "BtnHost", "Buat Ruangan (Host)", ColorAccent, ColorBg);

            // Join Section: Input Field + Button
            GameObject joinRow = new GameObject("JoinRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            joinRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup joinLayout = joinRow.GetComponent<HorizontalLayoutGroup>();
            joinLayout.spacing = 10f;
            joinLayout.childControlWidth = true;
            joinLayout.childControlHeight = true;
            joinRow.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 48f);

            TMP_InputField roomInput = CreateInputField(joinRow.transform, "RoomCodeInput", "Kode Ruangan...");
            Button btnJoin = CreateButton(joinRow.transform, "BtnJoin", "Gabung", ColorText, ColorBg, 120f);

            Button btnSolo = CreateButton(panelObj.transform, "BtnSolo", "Latihan Sendiri (Singleplayer)", ColorAccent, ColorBg);
            Button btnBack = CreateButton(panelObj.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            // Status Text
            GameObject statusObj = CreateTMPText(panelObj.transform, "MabarStatusText", "", 18, TextAlignmentOptions.Center);
            TMP_Text statusText = statusObj.GetComponent<TMP_Text>();
            statusText.color = ColorAccent;

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("hostButton").objectReferenceValue = btnHost;
            so.FindProperty("joinButton").objectReferenceValue = btnJoin;
            so.FindProperty("roomCodeInput").objectReferenceValue = roomInput;
            so.FindProperty("soloPracticeButton").objectReferenceValue = btnSolo;
            so.FindProperty("mabarBackButton").objectReferenceValue = btnBack;
            so.FindProperty("mabarStatusText").objectReferenceValue = statusText;
            so.FindProperty("mabarCharacterText").objectReferenceValue = charTextObj.GetComponent<TMP_Text>();
            so.FindProperty("mabarChangeCharacterButton").objectReferenceValue = btnChangeChar;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnHost;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildCaraBermainPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "CaraBermainPanel", new Vector2(650f, 520f), new Vector2(0f, -40f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(35, 35, 25, 25);

            CreateTMPText(panelObj.transform, "Header", "CARA BERMAIN", 28, TextAlignmentOptions.Center);

            string guideText = "1. Tugasmu: keliling kampung dan isi buku absen ronda sampai fajar.\n\n" +
                               "2. Hantu mendengar suaramu. Semakin keras bicara, semakin dekat mereka datang.\n\n" +
                               "3. Pakai kentongan untuk kode senyap ke teman satu tim.\n\n" +
                               "4. Pos ronda adalah tempat aman, tapi jangan terlalu ribut di sana.";

            GameObject descObj = CreateTMPText(panelObj.transform, "DescText", guideText, 19, TextAlignmentOptions.TopLeft);
            descObj.GetComponent<RectTransform>().sizeDelta = new Vector2(580f, 280f);

            Button btnBack = CreateButton(panelObj.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("caraBermainBackButton").objectReferenceValue = btnBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnBack;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildSettingsPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "SettingsPanel", new Vector2(740f, 620f), new Vector2(0f, -30f));
            SettingsPanel panel = panelObj.AddComponent<SettingsPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(35, 35, 20, 20);

            CreateTMPText(panelObj.transform, "Header", "PENGATURAN", 28, TextAlignmentOptions.Center);

            // Audio Section
            Slider sldMaster = CreateSliderRow(panelObj.transform, "MasterVolumeRow", "Volume Utama", 0.8f);
            Slider sldMusic = CreateSliderRow(panelObj.transform, "MusicVolumeRow", "Musik", 0.8f);
            Slider sldSfx = CreateSliderRow(panelObj.transform, "SfxVolumeRow", "Efek Suara (SFX)", 0.8f);
            Slider sldVoice = CreateSliderRow(panelObj.transform, "VoiceVolumeRow", "Suara Pemain", 0.8f);

            // Microphone Section
            TMP_Dropdown micDrop = CreateDropdownRow(panelObj.transform, "MicDeviceRow", "Mikrofon");
            Slider sldMicSens = CreateSliderRow(panelObj.transform, "MicSensRow", "Sensitivitas Suara", 0.5f);

            // Mic Level Bar
            GameObject micMeterRow = new GameObject("MicMeterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            micMeterRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup mLayout = micMeterRow.GetComponent<HorizontalLayoutGroup>();
            mLayout.childControlWidth = false;
            mLayout.childControlHeight = false;
            mLayout.spacing = 20f;
            micMeterRow.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 24f);

            CreateTMPText(micMeterRow.transform, "Label", "Level Mikrofon:", 16, TextAlignmentOptions.MidlineLeft, 160f);
            GameObject meterBg = new GameObject("MeterBg", typeof(RectTransform), typeof(Image));
            meterBg.transform.SetParent(micMeterRow.transform, false);
            meterBg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);
            meterBg.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 16f);

            GameObject meterFill = new GameObject("MeterFill", typeof(RectTransform), typeof(Image));
            meterFill.transform.SetParent(meterBg.transform, false);
            Image fillImg = meterFill.GetComponent<Image>();
            fillImg.color = ColorAccent;
            RectTransform fillRect = meterFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f); // default 0 width
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            // Graphics Section
            TMP_Dropdown resDrop = CreateDropdownRow(panelObj.transform, "ResRow", "Resolusi");
            TMP_Dropdown fsDrop = CreateDropdownRow(panelObj.transform, "FullscreenRow", "Mode Layar");
            Toggle vsyncTog = CreateToggleRow(panelObj.transform, "VsyncRow", "VSync");
            TMP_Dropdown qualDrop = CreateDropdownRow(panelObj.transform, "QualityRow", "Kualitas Grafis");

            // Mouse Sensitivity
            Slider sldMouse = CreateSliderRow(panelObj.transform, "MouseRow", "Sensitivitas Mouse", 0.5f);

            // Bottom Action Buttons
            GameObject btnRow = new GameObject("ActionBtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup bLayout = btnRow.GetComponent<HorizontalLayoutGroup>();
            bLayout.spacing = 15f;
            bLayout.childControlWidth = true;
            bLayout.childControlHeight = false;
            btnRow.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 50f);

            Button btnApply = CreateButton(btnRow.transform, "BtnApply", "Terapkan", ColorAccent, ColorBg);
            Button btnDefaults = CreateButton(btnRow.transform, "BtnDefaults", "Kembalikan Default", ColorText, ColorBg);
            Button btnBack = CreateButton(btnRow.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            // Connect SettingsPanel Serialized fields
            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("masterSlider").objectReferenceValue = sldMaster;
            pSo.FindProperty("musicSlider").objectReferenceValue = sldMusic;
            pSo.FindProperty("sfxSlider").objectReferenceValue = sldSfx;
            pSo.FindProperty("voiceSlider").objectReferenceValue = sldVoice;
            pSo.FindProperty("micDropdown").objectReferenceValue = micDrop;
            pSo.FindProperty("micSensitivitySlider").objectReferenceValue = sldMicSens;
            pSo.FindProperty("micLevelBar").objectReferenceValue = fillImg;
            pSo.FindProperty("resolutionDropdown").objectReferenceValue = resDrop;
            pSo.FindProperty("fullscreenDropdown").objectReferenceValue = fsDrop;
            pSo.FindProperty("vSyncToggle").objectReferenceValue = vsyncTog;
            pSo.FindProperty("qualityDropdown").objectReferenceValue = qualDrop;
            pSo.FindProperty("mouseSensitivitySlider").objectReferenceValue = sldMouse;
            pSo.FindProperty("applyButton").objectReferenceValue = btnApply;
            pSo.FindProperty("defaultsButton").objectReferenceValue = btnDefaults;
            pSo.FindProperty("backButton").objectReferenceValue = btnBack;
            pSo.FindProperty("firstSelected").objectReferenceValue = sldMaster;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("settingsPanel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildCreditsPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "CreditsPanel", new Vector2(550f, 420f), new Vector2(0f, -40f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(35, 35, 30, 30);

            CreateTMPText(panelObj.transform, "Header", "KREDIT", 28, TextAlignmentOptions.Center);

            string creditsContent = "PUCACI\n\nDibuat oleh:\nMusa & Tim Pengembang Pucaci\n\nGenre: Co-op Horor-Komedi Kampung\nDibuat dengan Unity 2022 LTS & Universal Render Pipeline";
            CreateTMPText(panelObj.transform, "Content", creditsContent, 20, TextAlignmentOptions.Center);

            Button btnBack = CreateButton(panelObj.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("creditsBackButton").objectReferenceValue = btnBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnBack;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildQuitDialog(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "QuitConfirmDialog", new Vector2(460f, 260f), Vector2.zero);
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(30, 30, 30, 30);

            CreateTMPText(panelObj.transform, "Prompt", "Yakin mau keluar?", 26, TextAlignmentOptions.Center);

            GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup bLayout = btnRow.GetComponent<HorizontalLayoutGroup>();
            bLayout.spacing = 20f;
            bLayout.childControlWidth = true;
            bLayout.childControlHeight = false;
            btnRow.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 50f);

            Button btnYes = CreateButton(btnRow.transform, "BtnYes", "Ya", ColorDanger, ColorBg);
            Button btnCancel = CreateButton(btnRow.transform, "BtnCancel", "Batal", ColorText, ColorBg);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("confirmQuitButton").objectReferenceValue = btnYes;
            so.FindProperty("cancelQuitButton").objectReferenceValue = btnCancel;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnCancel;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static void BuildSceneFader(Transform canvas)
        {
            GameObject faderObj = new GameObject("SceneFader", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            faderObj.transform.SetParent(canvas, false);
            RectTransform fRect = faderObj.GetComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = Vector2.zero;
            fRect.offsetMax = Vector2.zero;

            Image img = faderObj.GetComponent<Image>();
            img.color = Color.black;

            faderObj.AddComponent<SceneFader>();
        }

        #region UI Helpers
        private static GameObject CreatePanelContainer(Transform parent, string name, Vector2 size, Vector2 anchoredPos)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Image img = panel.GetComponent<Image>();
            img.color = ColorBg;

            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string text, Color textColor, Color bgColor, float width = -1f)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width > 0 ? width : 360f, 48f);

            Image img = btnObj.GetComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

            Button btn = btnObj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.12f, 0.16f, 0.24f, 0.95f);
            cb.highlightedColor = new Color(0.2f, 0.25f, 0.35f, 1f);
            cb.pressedColor = new Color(0.08f, 0.10f, 0.15f, 1f);
            cb.disabledColor = ColorDisabled;
            btn.colors = cb;

            GameObject textObj = CreateTMPText(btnObj.transform, "Text", text, 20, TextAlignmentOptions.Center);
            TMP_Text tmp = textObj.GetComponent<TMP_Text>();
            tmp.color = textColor;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            btnObj.AddComponent<ButtonFeedback>();
            return btn;
        }

        private static GameObject CreateTMPText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions align, float width = -1f)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = ColorText;

            RectTransform rt = obj.GetComponent<RectTransform>();
            if (width > 0)
            {
                rt.sizeDelta = new Vector2(width, 36f);
            }

            return obj;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText)
        {
            GameObject inputObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputObj.transform.SetParent(parent, false);

            RectTransform rt = inputObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 48f);

            Image img = inputObj.GetComponent<Image>();
            img.color = new Color(0.08f, 0.11f, 0.18f, 1f);

            TMP_InputField input = inputObj.GetComponent<TMP_InputField>();

            // Text
            GameObject textObj = CreateTMPText(inputObj.transform, "Text", "", 18, TextAlignmentOptions.MidlineLeft);
            TMP_Text text = textObj.GetComponent<TMP_Text>();
            text.color = ColorText;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            // Placeholder
            GameObject phObj = CreateTMPText(inputObj.transform, "Placeholder", placeholderText, 18, TextAlignmentOptions.MidlineLeft);
            TMP_Text ph = phObj.GetComponent<TMP_Text>();
            ph.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            RectTransform phRt = phObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10f, 0f);
            phRt.offsetMax = new Vector2(-10f, 0f);

            input.textViewport = textRt;
            input.textComponent = text;
            input.placeholder = ph;

            return input;
        }

        private static Slider CreateSliderRow(Transform parent, string rowName, string labelText, float defaultValue)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup rLayout = row.GetComponent<HorizontalLayoutGroup>();
            rLayout.childControlWidth = false;
            rLayout.childControlHeight = false;
            rLayout.spacing = 15f;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 28f);

            CreateTMPText(row.transform, "Label", labelText, 16, TextAlignmentOptions.MidlineLeft, 180f);

            GameObject sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(row.transform, false);
            RectTransform sRect = sliderObj.GetComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(440f, 20f);

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = defaultValue;

            // Background (relatif ke tinggi slider, tinggi 8px di tengah)
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderObj.transform, false);
            bg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgRt.sizeDelta = Vector2.zero;

            // Fill Area (dibatasi agar pas di dalam rel slider)
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = new Vector2(5f, 0f);
            faRt.offsetMax = new Vector2(-15f, 0f);
            faRt.sizeDelta = new Vector2(-20f, 0f);

            // Fill (WAJIB sizeDelta = 0 dan offset = 0 agar tidak meluber menjadi kotak raksasa!)
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = ColorAccent;
            RectTransform fRt = fill.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.zero;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fRt.sizeDelta = new Vector2(10f, 0f);
            slider.fillRect = fRt;

            // Handle Slide Area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10f, 0f);
            haRt.offsetMax = new Vector2(-10f, 0f);
            haRt.sizeDelta = new Vector2(-20f, 0f);

            // Handle (tombol geser rapi, tinggi 20px lebar 16px)
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<Image>().color = ColorText;
            RectTransform hRt = handle.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(0f, 1f);
            hRt.offsetMin = new Vector2(-8f, 0f);
            hRt.offsetMax = new Vector2(8f, 0f);
            hRt.sizeDelta = new Vector2(16f, 0f);
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();

            return slider;
        }

        private static TMP_Dropdown CreateDropdownRow(Transform parent, string rowName, string labelText)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup rLayout = row.GetComponent<HorizontalLayoutGroup>();
            rLayout.childControlWidth = false;
            rLayout.childControlHeight = false;
            rLayout.spacing = 15f;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 32f);

            CreateTMPText(row.transform, "Label", labelText, 16, TextAlignmentOptions.MidlineLeft, 180f);

            GameObject dropObj = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            dropObj.transform.SetParent(row.transform, false);
            RectTransform dRect = dropObj.GetComponent<RectTransform>();
            dRect.sizeDelta = new Vector2(440f, 30f);

            dropObj.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.24f, 1f);

            TMP_Dropdown dropdown = dropObj.GetComponent<TMP_Dropdown>();

            GameObject caption = CreateTMPText(dropObj.transform, "Label", "Pilih...", 16, TextAlignmentOptions.MidlineLeft);
            RectTransform cRect = caption.GetComponent<RectTransform>();
            cRect.anchorMin = Vector2.zero;
            cRect.anchorMax = Vector2.one;
            cRect.offsetMin = new Vector2(10f, 0f);
            cRect.offsetMax = new Vector2(-10f, 0f);
            dropdown.captionText = caption.GetComponent<TMP_Text>();

            return dropdown;
        }

        private static Toggle CreateToggleRow(Transform parent, string rowName, string labelText)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup rLayout = row.GetComponent<HorizontalLayoutGroup>();
            rLayout.childControlWidth = false;
            rLayout.childControlHeight = false;
            rLayout.spacing = 15f;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 28f);

            CreateTMPText(row.transform, "Label", labelText, 16, TextAlignmentOptions.MidlineLeft, 180f);

            GameObject togObj = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            togObj.transform.SetParent(row.transform, false);
            RectTransform tRect = togObj.GetComponent<RectTransform>();
            tRect.sizeDelta = new Vector2(30f, 26f);

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(togObj.transform, false);
            bg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            check.GetComponent<Image>().color = ColorAccent;
            RectTransform chRt = check.GetComponent<RectTransform>();
            chRt.anchorMin = new Vector2(0.2f, 0.2f);
            chRt.anchorMax = new Vector2(0.8f, 0.8f);
            chRt.sizeDelta = Vector2.zero;

            Toggle toggle = togObj.GetComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.isOn = true;

            return toggle;
        }

        private static void UpdateBuildSettings(string mainMenuScenePath)
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();

            // 0: MainMenu
            list.Add(new EditorBuildSettingsScene(mainMenuScenePath, true));

            // 1: PrototypeScene
            string prototypePath = "Assets/Scenes/Prototype/PrototypeScene.unity";
            list.Add(new EditorBuildSettingsScene(prototypePath, true));

            // Tambahkan scene lainnya
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path != mainMenuScenePath && s.path != prototypePath)
                {
                    list.Add(s);
                }
            }

            EditorBuildSettings.scenes = list.ToArray();
        }
        #endregion
    }
}
