using System.Collections;
using Mirror;
using Pucaci.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pucaci.UI
{
    /// <summary>
    /// HUD Status Ruangan / Lobby di scene Gameplay:
    /// - Menampilkan jumlah pemain yang terhubung (misal: "Menunggu Pemain: 1/3")
    /// - Menampilkan hitung mundur saat 3 pemain telah berkumpul
    /// - Menampilkan hint tombol [F5] untuk Host agar bisa mulai langsung saat testing
    /// </summary>
    public class MultiplayerLobbyHUD : MonoBehaviour
    {
        public static MultiplayerLobbyHUD Instance { get; private set; }

        [Header("UI Containers")]
        [SerializeField] private GameObject lobbyBannerRoot;
        [SerializeField] private CanvasGroup bannerCanvasGroup;
        [SerializeField] private TMP_Text statusTitleText;
        [SerializeField] private TMP_Text statusSubText;
        [SerializeField] private Image bannerBgImage;

        private PucaciNetworkManager netManager;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            BuildUIIfMissing();
        }

        private void Start()
        {
            netManager = PucaciNetworkManager.singleton;
            if (netManager != null)
            {
                netManager.OnPlayerCountChanged += OnPlayerCountChanged;
                netManager.OnCountdownTick += OnCountdownTick;
                netManager.OnMatchStartedEvent += OnMatchStarted;

                // Update tampilan awal
                UpdateStatusUI(netManager.ConnectedPlayers.Count, netManager.TargetPlayerCount);

                if (netManager.IsMatchStarted)
                {
                    HideBannerImmediate();
                }
            }
            else
            {
                // Jika bermain offline murni
                if (!NetworkServer.active && !NetworkClient.active)
                {
                    HideBannerImmediate();
                }
            }
        }

        private void OnDestroy()
        {
            if (netManager != null)
            {
                netManager.OnPlayerCountChanged -= OnPlayerCountChanged;
                netManager.OnCountdownTick -= OnCountdownTick;
                netManager.OnMatchStartedEvent -= OnMatchStarted;
            }
        }

        private void OnPlayerCountChanged(int current, int target)
        {
            if (netManager != null && netManager.IsMatchStarted) return;
            UpdateStatusUI(current, target);
        }

        private void OnCountdownTick(float remaining)
        {
            if (lobbyBannerRoot != null) lobbyBannerRoot.SetActive(true);
            if (statusTitleText != null)
            {
                statusTitleText.text = "<color=#00FFAA><b>PEMAIN LENGKAP (3/3)!</b></color>";
            }
            if (statusSubText != null)
            {
                statusSubText.text = $"Mengundi peran hantu dalam <color=#FFB347><b>{Mathf.CeilToInt(remaining)}</b></color> detik...";
            }
        }

        private void OnMatchStarted()
        {
            StartCoroutine(FadeOutAndHideRoutine());
        }

        private void UpdateStatusUI(int current, int target)
        {
            if (lobbyBannerRoot == null) return;

            // Jika offline, sembunyikan banner
            if (!NetworkServer.active && !NetworkClient.active)
            {
                lobbyBannerRoot.SetActive(false);
                return;
            }

            lobbyBannerRoot.SetActive(true);

            if (current < target)
            {
                if (statusTitleText != null)
                {
                    statusTitleText.text = $"MENUNGGU PEMAIN (<color=#FFB347>{current}/{target}</color>)";
                }

                if (statusSubText != null)
                {
                    if (NetworkServer.active)
                    {
                        statusSubText.text = "Ajak 2 teman lagi untuk mabar, atau tekan <color=#00FFAA><b>[F5]</b></color> untuk Mulai Langsung.";
                    }
                    else
                    {
                        statusSubText.text = "Menunggu host memulai atau pemain lain bergabung...";
                    }
                }
            }
            else
            {
                if (statusTitleText != null)
                {
                    statusTitleText.text = "<color=#00FFAA><b>PEMAIN LENGKAP (3/3)!</b></color>";
                }
                if (statusSubText != null)
                {
                    statusSubText.text = "Bersiaplah... Hantu Kuntilanak sedang menyamar di antara kalian!";
                }
            }
        }

        private IEnumerator FadeOutAndHideRoutine()
        {
            if (statusTitleText != null)
            {
                statusTitleText.text = "<color=#FF2244><b>PERMAINAN DIMULAI!</b></color>";
            }
            if (statusSubText != null)
            {
                statusSubText.text = "Waspadai gerak-gerik temanmu...";
            }

            yield return new WaitForSeconds(2.0f);

            if (bannerCanvasGroup != null)
            {
                float t = 1.0f;
                while (t > 0f)
                {
                    t -= Time.deltaTime * 2.5f;
                    bannerCanvasGroup.alpha = Mathf.Clamp01(t);
                    yield return null;
                }
            }

            if (lobbyBannerRoot != null)
            {
                lobbyBannerRoot.SetActive(false);
            }
        }

        private void HideBannerImmediate()
        {
            if (lobbyBannerRoot != null) lobbyBannerRoot.SetActive(false);
        }

        private void BuildUIIfMissing()
        {
            if (lobbyBannerRoot != null) return;

            // Cari Canvas di scene
            Canvas targetCanvas = FindObjectOfType<Canvas>();
            if (targetCanvas == null) return;

            // Buat root banner
            lobbyBannerRoot = new GameObject("MultiplayerLobbyBanner", typeof(RectTransform), typeof(CanvasGroup));
            lobbyBannerRoot.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = lobbyBannerRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -20f);
            rootRect.sizeDelta = new Vector2(620f, 75f);

            bannerCanvasGroup = lobbyBannerRoot.GetComponent<CanvasGroup>();

            // Background panel
            GameObject bgObj = new GameObject("BannerBg", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(lobbyBannerRoot.transform, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            bannerBgImage = bgObj.GetComponent<Image>();
            bannerBgImage.color = new Color(0.05f, 0.07f, 0.10f, 0.88f);

            // Layout teks
            GameObject textContainer = new GameObject("TextLayout", typeof(RectTransform), typeof(VerticalLayoutGroup));
            textContainer.transform.SetParent(lobbyBannerRoot.transform, false);
            RectTransform layoutRect = textContainer.GetComponent<RectTransform>();
            layoutRect.anchorMin = Vector2.zero;
            layoutRect.anchorMax = Vector2.one;
            layoutRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = textContainer.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.spacing = 3f;
            vlg.padding = new RectOffset(15, 15, 8, 8);

            // Title text
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(textContainer.transform, false);
            statusTitleText = titleObj.GetComponent<TextMeshProUGUI>();
            statusTitleText.fontSize = 20;
            statusTitleText.alignment = TextAlignmentOptions.Center;
            statusTitleText.color = new Color(0.95f, 0.95f, 0.95f, 1.0f);
            statusTitleText.text = "MENUNGGU PEMAIN (1/3)";

            // Sub text
            GameObject subObj = new GameObject("SubText", typeof(RectTransform), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(textContainer.transform, false);
            statusSubText = subObj.GetComponent<TextMeshProUGUI>();
            statusSubText.fontSize = 13;
            statusSubText.alignment = TextAlignmentOptions.Center;
            statusSubText.color = new Color(0.75f, 0.80f, 0.85f, 1.0f);
            statusSubText.text = "Menunggu pemain bergabung...";
        }
    }
}
