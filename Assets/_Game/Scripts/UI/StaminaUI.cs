using UnityEngine;
using UnityEngine.UI;
using Pucaci.Player;

namespace Pucaci.UI
{
    /// <summary>
    /// Menampilkan bar stamina di HUD yang berkurang saat berlari.
    /// Warna bar berubah secara bertahap ke merah saat stamina menipis.
    /// Otomatis memudar (fade out) saat stamina penuh.
    /// </summary>
    public class StaminaUI : MonoBehaviour
    {
        public static StaminaUI Instance { get; private set; }

        [Header("Target Player")]
        [SerializeField] private PlayerCharacterController targetController;

        [Header("UI Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text exhaustionWarningText;

        [Header("Dynamic Colors")]
        [Tooltip("Warna bar saat stamina penuh (biru cerah/cyan)")]
        [SerializeField] private Color fullColor = new Color(0.18f, 0.82f, 0.95f, 1f);

        [Tooltip("Warna bar saat stamina sedang/setengah (kuning/oranye)")]
        [SerializeField] private Color midColor = new Color(0.95f, 0.72f, 0.18f, 1f);

        [Tooltip("Warna bar saat stamina menipis/kritis (merah menyala)")]
        [SerializeField] private Color lowColor = new Color(0.95f, 0.22f, 0.22f, 1f);

        [Header("Display Settings")]
        [Tooltip("Fade out bar saat stamina 100% penuh")]
        [SerializeField] private bool autoFadeWhenFull = true;
        [SerializeField] private float fadeSpeed = 4f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (fillImage != null)
            {
                fillImage.color = fullColor;
                if (fillRect == null) fillRect = fillImage.rectTransform;
            }
            if (exhaustionWarningText != null) exhaustionWarningText.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (fillImage != null) fillImage.color = fullColor;
            if (exhaustionWarningText != null) exhaustionWarningText.gameObject.SetActive(false);

            if (targetController == null)
            {
                TryFindLocalPlayer();
            }
        }

        public void SetTarget(PlayerCharacterController controller)
        {
            targetController = controller;
        }

        private void Update()
        {
            if (targetController == null)
            {
                TryFindLocalPlayer();
                if (targetController == null)
                {
                    if (canvasGroup != null) canvasGroup.alpha = 0f;
                    return;
                }
            }

            float normalized = targetController.StaminaNormalized;

            // 1. Hitung warna gradasi: Biru -> Oranye -> Merah saat stamina menipis
            Color currentColor;
            if (normalized > 0.5f)
            {
                float t = (normalized - 0.5f) / 0.5f;
                currentColor = Color.Lerp(midColor, fullColor, t);
            }
            else
            {
                float t = normalized / 0.5f;
                currentColor = Color.Lerp(lowColor, midColor, t);
            }

            // 2. Update fill amount, RectTransform size, dan warna dinamis
            if (fillImage != null)
            {
                fillImage.fillAmount = normalized;
                fillImage.color = currentColor;
            }

            if (fillRect != null)
            {
                fillRect.anchorMax = new Vector2(normalized, 1f);
            }

            // 3. Teks peringatan dinonaktifkan
            if (exhaustionWarningText != null && exhaustionWarningText.gameObject.activeSelf)
            {
                exhaustionWarningText.gameObject.SetActive(false);
            }

            // 4. Auto-fade saat stamina 100% penuh
            if (canvasGroup != null)
            {
                float targetAlpha = (autoFadeWhenFull && normalized >= 0.99f) ? 0f : 1f;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            }
        }

        private void TryFindLocalPlayer()
        {
            var setups = FindObjectsOfType<Pucaci.Network.PlayerNetworkSetup>();
            foreach (var setup in setups)
            {
                if (setup.isLocalPlayer)
                {
                    targetController = setup.GetComponent<PlayerCharacterController>();
                    if (targetController != null) return;
                }
            }

            // Fallback
            var controllers = FindObjectsOfType<PlayerCharacterController>();
            foreach (var c in controllers)
            {
                if (c.enabled)
                {
                    targetController = c;
                    break;
                }
            }
        }
    }
}
