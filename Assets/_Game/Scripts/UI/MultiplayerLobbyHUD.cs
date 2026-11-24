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

        [Header("Center Big Countdown")]
        [SerializeField] private GameObject centerCountdownRoot;
        [SerializeField] private CanvasGroup centerCanvasGroup;
        [SerializeField] private TMP_Text centerCountdownTitle;
        [SerializeField] private TMP_Text centerCountdownNumber;
        private Coroutine pulseCoroutine;

        private PucaciNetworkManager netManager;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            BuildUIIfMissing();
            BuildCenterCountdownIfMissing();
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
                statusTitleText.text = "<color=#00FFAA><b>MEMULAI PERMAINAN!</b></color>";
            }
            if (statusSubText != null)
            {
                statusSubText.text = $"Permainan dimulai dalam <color=#FFB347><b>{Mathf.CeilToInt(remaining)}</b></color> detik...";
            }

            // --- Tampilan Hitung Mundur Besar di Tengah Layar (5, 4, 3...) ---
            if (centerCountdownRoot == null)
            {
                BuildCenterCountdownIfMissing();
            }

            if (centerCountdownRoot != null)
            {
                centerCountdownRoot.SetActive(true);
                if (centerCanvasGroup != null) centerCanvasGroup.alpha = 1f;

                int seconds = Mathf.CeilToInt(remaining);
                if (centerCountdownNumber != null)
                {
                    string colorHex = (seconds <= 2) ? "#FF2233" : ((seconds <= 3) ? "#FF7711" : "#FFCC00");
                    centerCountdownNumber.text = $"<color={colorHex}>{seconds}</color>";
                }
                if (centerCountdownTitle != null)
                {
                    centerCountdownTitle.text = "<color=#00FFAA>PERMAINAN DIMULAI DALAM</color>";
                }

                if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                pulseCoroutine = StartCoroutine(PulseCountdownRoutine());
            }
        }

        private IEnumerator PulseCountdownRoutine()
        {
            if (centerCountdownNumber == null) yield break;
            Transform numTr = centerCountdownNumber.transform;
            numTr.localScale = Vector3.one * 1.45f;

            float duration = 0.45f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                numTr.localScale = Vector3.Lerp(Vector3.one * 1.45f, Vector3.one, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            numTr.localScale = Vector3.one;
        }

        private void OnMatchStarted()
        {
            StartCoroutine(FadeOutAndHideRoutine());
            if (centerCountdownRoot != null && centerCountdownRoot.activeSelf)
            {
                StartCoroutine(FadeOutCenterCountdownRoutine());
            }
        }

        private IEnumerator FadeOutCenterCountdownRoutine()
        {
            if (centerCountdownNumber != null)
            {
                centerCountdownNumber.text = "<color=#FF2244>MULAI!</color>";
                centerCountdownNumber.transform.localScale = Vector3.one * 1.55f;
            }
            if (centerCountdownTitle != null)
            {
                centerCountdownTitle.text = "<color=#FFFFFF>WASPADA! HANTU MENYAMAR DI ANTARA KALIAN!</color>";
            }

            float elapsed = 0f;
            while (elapsed < 0.35f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.35f;
                if (centerCountdownNumber != null)
                {
                    centerCountdownNumber.transform.localScale = Vector3.Lerp(Vector3.one * 1.55f, Vector3.one, t);
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.9f);

            if (centerCanvasGroup != null)
            {
                float t = 1.0f;
                while (t > 0f)
                {
                    t -= Time.deltaTime * 2.5f;
                    centerCanvasGroup.alpha = Mathf.Clamp01(t);
                    yield return null;
                }
            }

            if (centerCountdownRoot != null)
            {
                centerCountdownRoot.SetActive(false);
            }
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
                        int needed = Mathf.Max(1, target - current);
                        statusSubText.text = $"Ajak {needed} teman lagi untuk mabar ({target} orang), atau tekan <color=#00FFAA><b>[F5]</b></color> untuk Mulai Langsung.";
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
                    statusTitleText.text = $"<color=#00FFAA><b>PEMAIN LENGKAP ({current}/{target})!</b></color>";
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
            if (centerCountdownRoot != null) centerCountdownRoot.SetActive(false);
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

        private void BuildCenterCountdownIfMissing()
        {
            if (centerCountdownRoot != null) return;

            Canvas targetCanvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (targetCanvas == null) return;

            centerCountdownRoot = new GameObject("CenterCountdownOverlay", typeof(RectTransform), typeof(CanvasGroup));
            centerCountdownRoot.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = centerCountdownRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, 40f);
            rootRect.sizeDelta = new Vector2(700f, 320f);

            centerCanvasGroup = centerCountdownRoot.GetComponent<CanvasGroup>();

            // Soft dark backdrop panel
            GameObject backdropObj = new GameObject("CountdownBackdrop", typeof(RectTransform), typeof(Image));
            backdropObj.transform.SetParent(centerCountdownRoot.transform, false);
            RectTransform bdRect = backdropObj.GetComponent<RectTransform>();
            bdRect.anchorMin = new Vector2(0.5f, 0.5f);
            bdRect.anchorMax = new Vector2(0.5f, 0.5f);
            bdRect.sizeDelta = new Vector2(460f, 260f);
            Image bdImg = backdropObj.GetComponent<Image>();
            bdImg.color = new Color(0.03f, 0.05f, 0.08f, 0.78f);

            // Title label
            GameObject titleObj = new GameObject("CountdownTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(centerCountdownRoot.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.68f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.sizeDelta = Vector2.zero;
            titleRect.anchoredPosition = new Vector2(0f, -10f);

            centerCountdownTitle = titleObj.GetComponent<TextMeshProUGUI>();
            centerCountdownTitle.fontSize = 24;
            centerCountdownTitle.fontStyle = FontStyles.Bold;
            centerCountdownTitle.alignment = TextAlignmentOptions.Center;
            centerCountdownTitle.color = new Color(0f, 1f, 0.66f, 1f);
            centerCountdownTitle.text = "PERMAINAN DIMULAI DALAM";

            // Giant Number Text (5, 4, 3...)
            GameObject numObj = new GameObject("CountdownNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
            numObj.transform.SetParent(centerCountdownRoot.transform, false);
            RectTransform numRect = numObj.GetComponent<RectTransform>();
            numRect.anchorMin = new Vector2(0f, 0f);
            numRect.anchorMax = new Vector2(1f, 0.72f);
            numRect.sizeDelta = Vector2.zero;
            numRect.anchoredPosition = new Vector2(0f, -10f);

            centerCountdownNumber = numObj.GetComponent<TextMeshProUGUI>();
            centerCountdownNumber.fontSize = 150;
            centerCountdownNumber.fontStyle = FontStyles.Bold;
            centerCountdownNumber.alignment = TextAlignmentOptions.Center;
            centerCountdownNumber.color = new Color(1f, 0.82f, 0.1f, 1f);
            centerCountdownNumber.text = "5";

            centerCountdownRoot.SetActive(false);
        }
    }
}
