using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

namespace Pucaci.UI
{
    public class GameplayExitMenu : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private bool showExitButton = true;

        private void Awake()
        {
            // Pastikan kualitas grafis maksimal (PC High) aktif agar tidak burik
            int pcHighIndex = QualitySettings.names.Length - 1;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.names[i].ToLower().Contains("high"))
                {
                    pcHighIndex = i;
                    break;
                }
            }
            QualitySettings.SetQualityLevel(pcHighIndex, true);
        }

        private void OnGUI()
        {
            if (!showExitButton) return;

            int btnWidth = 130;
            int btnHeight = 34;
            int margin = 16;

            // Tombol Keluar Game di pojok kanan atas
            Rect quitRect = new Rect(Screen.width - btnWidth - margin, margin, btnWidth, btnHeight);
            GUI.backgroundColor = new Color(0.85f, 0.2f, 0.2f, 0.95f);
            if (GUI.Button(quitRect, "Keluar Game"))
            {
                QuitGame();
            }

            // Tombol Menu Utama di sebelah kiri tombol Keluar Game
            Rect menuRect = new Rect(Screen.width - (btnWidth * 2) - margin - 10, margin, btnWidth, btnHeight);
            GUI.backgroundColor = new Color(0.2f, 0.5f, 0.85f, 0.95f);
            if (GUI.Button(menuRect, "Menu Utama"))
            {
                ReturnToMainMenu();
            }
        }

        public void ReturnToMainMenu()
        {
            DisconnectNetwork();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu");
        }

        public void QuitGame()
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
    }
}

