using System.Collections.Generic;
using Pucaci.Core;
using Pucaci.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pucaci.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }

        [Header("Scene Configuration")]
        [Tooltip("Nama scene game yang dimuat saat tombol Latihan Sendiri atau Mulai ditekan")]
        [SerializeField] private string gameSceneName = "Gameplay";

        [Header("Multiplayer Launcher")]
        [SerializeField] private MonoBehaviour multiplayerLauncherComponent;
        private IMultiplayerLauncher multiplayerLauncher;

        [Header("Panels")]
        [SerializeField] private MenuPanel mainPanel;
        [SerializeField] private MenuPanel characterSelectPanel;
        [SerializeField] private MenuPanel mabarPanel;
        [SerializeField] private MenuPanel caraBermainPanel;
        [SerializeField] private MenuPanel settingsPanel;
        [SerializeField] private MenuPanel creditsPanel;
        [SerializeField] private MenuPanel quitConfirmDialog;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button mabarButton;
        [SerializeField] private Button caraBermainButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Character Selection Panel Elements")]
        [SerializeField] private Button selectLarasCardButton;
        [SerializeField] private Button selectMamatCardButton;
        [SerializeField] private Button selectKuntiCardButton;
        [SerializeField] private Image larasCardBorder;
        [SerializeField] private Image mamatCardBorder;
        [SerializeField] private Image kuntiCardBorder;
        [SerializeField] private TMP_Text larasStatusBadge;
        [SerializeField] private TMP_Text mamatStatusBadge;
        [SerializeField] private TMP_Text kuntiStatusBadge;
        [SerializeField] private Button startPlayWithCharacterButton;
        [SerializeField] private Button characterSelectBackButton;

        [Header("Mabar Panel Elements")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button soloPracticeButton;
        [SerializeField] private Button mabarBackButton;
        [SerializeField] private TMP_Text mabarStatusText;
        [SerializeField] private TMP_Text mabarCharacterText;
        [SerializeField] private Button mabarChangeCharacterButton;

        [Header("Cara Bermain Panel")]
        [SerializeField] private Button caraBermainBackButton;

        [Header("Credits Panel")]
        [SerializeField] private Button creditsBackButton;
        [SerializeField] private TMP_Text creditsText;

        [Header("Quit Confirm Dialog")]
        [SerializeField] private Button confirmQuitButton;
        [SerializeField] private Button cancelQuitButton;

        [Header("Version Display")]
        [SerializeField] private TMP_Text versionText;

        private readonly Stack<MenuPanel> panelHistory = new Stack<MenuPanel>();
        private MenuPanel currentPanel;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Instance == null)
            {
                Instance = this;
            }

            if (multiplayerLauncherComponent != null)
            {
                multiplayerLauncher = multiplayerLauncherComponent as IMultiplayerLauncher;
            }

            if (multiplayerLauncher == null)
            {
                multiplayerLauncher = GetComponent<IMultiplayerLauncher>() ?? gameObject.AddComponent<StubMultiplayerLauncher>();
            }
        }

        private void Start()
        {
            // Tampilkan versi aplikasi
            if (versionText != null)
            {
                versionText.text = $"v{Application.version}";
            }

            // Bind Main Menu Buttons
            if (playButton != null) playButton.onClick.AddListener(OpenCharacterSelectPanel);
            if (mabarButton != null) mabarButton.onClick.AddListener(() => OpenPanel(mabarPanel));
            if (caraBermainButton != null) caraBermainButton.onClick.AddListener(() => OpenPanel(caraBermainPanel));
            if (settingsButton != null) settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
            if (creditsButton != null) creditsButton.onClick.AddListener(() => OpenPanel(creditsPanel));
            if (quitButton != null) quitButton.onClick.AddListener(() => OpenPanel(quitConfirmDialog));

            // Bind Character Select Panel
            if (selectLarasCardButton != null) selectLarasCardButton.onClick.AddListener(() => OnCharacterSelected(CharacterType.Laras));
            if (selectMamatCardButton != null) selectMamatCardButton.onClick.AddListener(() => OnCharacterSelected(CharacterType.PakMamat));
            if (selectKuntiCardButton != null) selectKuntiCardButton.onClick.AddListener(() => OnCharacterSelected(CharacterType.Kuntilanak));
            if (startPlayWithCharacterButton != null) startPlayWithCharacterButton.onClick.AddListener(OnSoloPracticeClicked);
            if (characterSelectBackButton != null) characterSelectBackButton.onClick.AddListener(ReturnToPreviousPanel);

            // Bind Mabar Panel
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (soloPracticeButton != null) soloPracticeButton.onClick.AddListener(OpenCharacterSelectPanel);
            if (mabarBackButton != null) mabarBackButton.onClick.AddListener(ReturnToPreviousPanel);
            if (mabarChangeCharacterButton != null) mabarChangeCharacterButton.onClick.AddListener(OpenCharacterSelectPanel);

            // Bind Other Back Buttons
            if (caraBermainBackButton != null) caraBermainBackButton.onClick.AddListener(ReturnToPreviousPanel);
            if (creditsBackButton != null) creditsBackButton.onClick.AddListener(ReturnToPreviousPanel);

            // Bind Quit Dialog
            if (confirmQuitButton != null) confirmQuitButton.onClick.AddListener(ExecuteQuit);
            if (cancelQuitButton != null) cancelQuitButton.onClick.AddListener(ReturnToPreviousPanel);

            UpdateCharacterSelectUI();

            // Sembunyikan semua panel sekunder dan tampilkan panel utama
            HideAllSecondaryPanels();
            OpenPanel(mainPanel, clearHistory: true);
        }

        private void Update()
        {
            HandleCancelInput();
        }

        private void HandleCancelInput()
        {
            bool cancelPressed = false;

            // Keyboard Escape
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                cancelPressed = true;
            }

            // Gamepad East/B Button
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                cancelPressed = true;
            }

            if (cancelPressed)
            {
                if (currentPanel == mainPanel)
                {
                    OpenPanel(quitConfirmDialog);
                }
                else
                {
                    ReturnToPreviousPanel();
                }
            }
        }

        public void OpenPanel(MenuPanel panelToOpen, bool clearHistory = false)
        {
            if (panelToOpen == null) return;

            if (clearHistory)
            {
                panelHistory.Clear();
            }
            else if (currentPanel != null && currentPanel != panelToOpen)
            {
                panelHistory.Push(currentPanel);
            }

            if (currentPanel != null && currentPanel != panelToOpen)
            {
                currentPanel.Hide();
            }

            currentPanel = panelToOpen;
            currentPanel.Show();
        }

        public void ReturnToPreviousPanel()
        {
            if (panelHistory.Count > 0)
            {
                MenuPanel previous = panelHistory.Pop();
                if (currentPanel != null)
                {
                    currentPanel.Hide();
                }
                currentPanel = previous;
                currentPanel.Show();
            }
            else
            {
                // Jika riwayat kosong, kembali ke menu utama
                if (currentPanel != mainPanel)
                {
                    if (currentPanel != null) currentPanel.Hide();
                    currentPanel = mainPanel;
                    currentPanel.Show();
                }
            }
        }

        private void HideAllSecondaryPanels()
        {
            if (characterSelectPanel != null) characterSelectPanel.Hide();
            if (mabarPanel != null) mabarPanel.Hide();
            if (caraBermainPanel != null) caraBermainPanel.Hide();
            if (settingsPanel != null) settingsPanel.Hide();
            if (creditsPanel != null) creditsPanel.Hide();
            if (quitConfirmDialog != null) quitConfirmDialog.Hide();
        }

        #region Character Selection Actions
        public void OpenCharacterSelectPanel()
        {
            UpdateCharacterSelectUI();
            OpenPanel(characterSelectPanel);
        }

        public void OnCharacterSelected(CharacterType type)
        {
            CharacterSelectionData.CurrentCharacter = type;
            UpdateCharacterSelectUI();
        }

        public void UpdateCharacterSelectUI()
        {
            CharacterType current = CharacterSelectionData.CurrentCharacter;
            bool isLaras = (current == CharacterType.Laras);
            bool isMamat = (current == CharacterType.PakMamat);
            bool isKunti = (current == CharacterType.Kuntilanak);

            Color accentColor = new Color(255f / 255f, 179f / 255f, 71f / 255f, 1f); // #FFB347
            Color normalBorderColor = new Color(0.18f, 0.22f, 0.32f, 1f);
            Color normalTextColor = new Color(242f / 255f, 232f / 255f, 213f / 255f, 1f);

            if (larasCardBorder != null)
            {
                larasCardBorder.color = isLaras ? accentColor : normalBorderColor;
            }
            if (larasStatusBadge != null)
            {
                larasStatusBadge.text = isLaras ? "[V] TERPILIH" : "PILIH LARAS";
                larasStatusBadge.color = isLaras ? accentColor : normalTextColor;
            }

            if (mamatCardBorder != null)
            {
                mamatCardBorder.color = isMamat ? accentColor : normalBorderColor;
            }
            if (mamatStatusBadge != null)
            {
                mamatStatusBadge.text = isMamat ? "[V] TERPILIH" : "PILIH PAK MAMAT";
                mamatStatusBadge.color = isMamat ? accentColor : normalTextColor;
            }

            if (kuntiCardBorder != null)
            {
                kuntiCardBorder.color = isKunti ? accentColor : normalBorderColor;
            }
            if (kuntiStatusBadge != null)
            {
                kuntiStatusBadge.text = isKunti ? "[V] TERPILIH" : "PILIH KUNTILANAK";
                kuntiStatusBadge.color = isKunti ? accentColor : normalTextColor;
            }

            if (mabarCharacterText != null)
            {
                string charName = CharacterSelectionData.GetCharacterName(current);
                string charRole = CharacterSelectionData.GetCharacterRole(current);
                mabarCharacterText.text = $"Karakter Aktif: <color=#FFB347><b>{charName}</b></color> ({charRole})";
            }
        }
        #endregion

        #region Mabar Actions
        private void OnHostClicked()
        {
            multiplayerLauncher.Host();
            if (!multiplayerLauncher.IsAvailable && mabarStatusText != null)
            {
                mabarStatusText.text = "Fitur online belum tersedia.";
            }
        }

        private void OnJoinClicked()
        {
            string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";
            multiplayerLauncher.Join(code);
            if (!multiplayerLauncher.IsAvailable && mabarStatusText != null)
            {
                mabarStatusText.text = "Fitur online belum tersedia.";
            }
        }

        private void OnSoloPracticeClicked()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/_Game/Scenes/{gameSceneName}.unity");
                return;
            }
#endif
            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeAndLoadScene(gameSceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
            }
        }
        #endregion

        #region Quit Actions
        private void ExecuteQuit()
        {
            Debug.Log("[Pucaci.UI] Keluar dari permainan.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        #endregion
    }
}
