using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Player;

namespace Pucaci.UI
{
    /// <summary>
    /// UI Indikator durasi wujud gaib (Ghost Vanish) Kuntilanak.
    /// Menampilkan bar progres, hitung mundur angka (detik), dan peringatan saat durasi hampir habis.
    /// Otomatis muncul saat Kuntilanak masuk mode hilang (F), dan memudar keluar saat kembali padat.
    /// </summary>
    public class KuntilanakGhostVanishUI : MonoBehaviour
    {
        public static KuntilanakGhostVanishUI Instance { get; private set; }

        [Header("Target")]
        [SerializeField] private KuntilanakGhostVanish targetVanish;
        [SerializeField] private CharacterModelManager modelManager;

        [Header("UI Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Colors & Styling")]
        [Tooltip("Warna bar gaib normal (cyan keperakan bercahaya)")]
        [SerializeField] private Color normalColor = new Color(0.65f, 0.90f, 1.0f, 1.0f);

        [Tooltip("Warna bar saat durasi kritis < 2 detik (oranye-merah menyala)")]
        [SerializeField] private Color warningColor = new Color(1.0f, 0.30f, 0.25f, 1.0f);

        [Tooltip("Warna bar saat fase jeda / cooldown 8 detik (oranye amber redup)")]
        [SerializeField] private Color cooldownColor = new Color(0.95f, 0.65f, 0.20f, 0.95f);

        [Tooltip("Warna background panel")]
        [SerializeField] private Color backgroundColor = new Color(0.04f, 0.08f, 0.12f, 0.85f);

        [Header("Animation Settings")]
        [SerializeField] private float fadeSpeed = 6.0f;
        [SerializeField] private float criticalThreshold = 2.0f;

        private RectTransform panelRect;
        private Vector3 initialScale;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            panelRect = transform as RectTransform;
            initialScale = panelRect != null ? panelRect.localScale : Vector3.one;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Start()
        {
            if (targetVanish == null)
            {
                TryFindLocalPlayer();
            }
        }

        public void SetTarget(KuntilanakGhostVanish vanish, CharacterModelManager manager)
        {
            targetVanish = vanish;
            modelManager = manager;
        }

        private void TryFindLocalPlayer()
        {
            var players = FindObjectsOfType<PlayerCharacterController>();
            foreach (var p in players)
            {
                var netSetup = p.GetComponent<Pucaci.Network.PlayerNetworkSetup>();
                // Cek apakah pemain lokal
                bool isLocal = (netSetup == null) || (p.TryGetComponent<Mirror.NetworkIdentity>(out var id) && id.isLocalPlayer);
                if (isLocal)
                {
                    targetVanish = p.GetComponent<KuntilanakGhostVanish>();
                    modelManager = p.GetComponent<CharacterModelManager>();
                    break;
                }
            }
        }

        private void Update()
        {
            if (targetVanish == null)
            {
                TryFindLocalPlayer();
                if (targetVanish == null)
                {
                    FadeOutInstant();
                    return;
                }
            }

            // Pastikan karakter yang sedang aktif adalah Kuntilanak (index 2)
            bool isKunti = (modelManager != null) ? (modelManager.ActiveCharacterType == CharacterType.Kuntilanak) : true;
            bool isGhostActive = isKunti && targetVanish.IsGhostForm && (targetVanish.RemainingGhostDuration > 0.01f);
            bool isOnCooldown = isKunti && !targetVanish.IsGhostForm && targetVanish.IsOnCooldown;
            bool shouldDisplay = isGhostActive || isOnCooldown;

            // 1. Animasi Fade In / Fade Out panel UI
            float targetAlpha = shouldDisplay ? 1.0f : 0.0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            }

            if (!shouldDisplay && (canvasGroup == null || canvasGroup.alpha <= 0.01f))
            {
                return;
            }

            // 2. Update Tampilan Berdasarkan Status (Mode Gaib Aktif VS Mode Cooldown)
            if (isGhostActive)
            {
                // Status: Wujud Gaib Aktif (Maksimal 6 Detik)
                float remaining = targetVanish.RemainingGhostDuration;
                float normalized = targetVanish.GhostDurationNormalized;
                bool isCritical = remaining <= criticalThreshold && remaining > 0f;

                if (fillImage != null)
                {
                    fillImage.fillAmount = normalized;
                    Color currentColor = isCritical ? Color.Lerp(warningColor, normalColor, Mathf.PingPong(Time.time * 6f, 1f)) : normalColor;
                    fillImage.color = currentColor;
                }

                if (timerText != null)
                {
                    timerText.text = $"{remaining:0.0}s";
                    timerText.color = isCritical ? warningColor : Color.white;
                }

                if (labelText != null)
                {
                    labelText.text = isCritical ? "<color=#FF5555>DURASI HAMPIR HABIS!</color>" : "WUJUD GAIB [F]";
                }

                if (panelRect != null)
                {
                    if (isCritical)
                    {
                        float pulse = 1.0f + Mathf.Sin(Time.time * 12f) * 0.04f;
                        panelRect.localScale = initialScale * pulse;
                    }
                    else
                    {
                        panelRect.localScale = initialScale;
                    }
                }
            }
            else if (isOnCooldown)
            {
                // Status: Cooldown 20 Detik (Bar Mengisi Kembali ke 100%)
                float cdRemaining = targetVanish.RemainingCooldown;
                float cdProgress = 1.0f - targetVanish.CooldownNormalized;

                if (fillImage != null)
                {
                    fillImage.fillAmount = cdProgress;
                    fillImage.color = cooldownColor;
                }

                if (timerText != null)
                {
                    timerText.text = $"{cdRemaining:0.0}s";
                    timerText.color = new Color(1.0f, 0.85f, 0.5f, 1.0f);
                }

                if (labelText != null)
                {
                    labelText.text = "<color=#FFAA33>COOLDOWN [F]</color>";
                }

                if (panelRect != null)
                {
                    panelRect.localScale = initialScale;
                }
            }
        }

        private void FadeOutInstant()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (panelRect != null)
            {
                panelRect.localScale = initialScale;
            }
        }
    }
}
