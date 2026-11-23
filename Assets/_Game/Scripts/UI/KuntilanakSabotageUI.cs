using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Gameplay;
using Pucaci.Player;

namespace Pucaci.UI
{
    /// <summary>
    /// UI Indikator Skill Sabotase Lampu Desa untuk Kuntilanak [Q].
    /// Menampilkan status skill (Siap Digunakan, Padam/Aktif, Cooldown), bar progres, dan hitung mundur timer.
    /// Otomatis muncul saat Kuntilanak aktif, dan memudar keluar saat menjadi manusia / survivor.
    /// </summary>
    public class KuntilanakSabotageUI : MonoBehaviour
    {
        public static KuntilanakSabotageUI Instance { get; private set; }

        [Header("UI Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Colors")]
        [SerializeField] private Color readyColor = new Color(0.95f, 0.20f, 0.20f, 1.0f); // Crimson
        [SerializeField] private Color activeColor = new Color(0.40f, 0.30f, 0.90f, 1.0f); // Purple
        [SerializeField] private Color cooldownColor = new Color(0.90f, 0.60f, 0.15f, 0.9f); // Amber

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 6.0f;

        private PlayerRoleManager localRoleManager;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            TryFindLocalPlayer();
        }

        private void TryFindLocalPlayer()
        {
            var players = FindObjectsOfType<PlayerCharacterController>();
            foreach (var p in players)
            {
                var netSetup = p.GetComponent<Pucaci.Network.PlayerNetworkSetup>();
                bool isLocal = (netSetup == null) || (p.TryGetComponent<Mirror.NetworkIdentity>(out var id) && id.isLocalPlayer);
                if (isLocal)
                {
                    localRoleManager = p.GetComponent<PlayerRoleManager>();
                    break;
                }
            }
        }

        private void Update()
        {
            if (localRoleManager == null)
            {
                TryFindLocalPlayer();
                if (localRoleManager == null)
                {
                    FadeOut();
                    return;
                }
            }

            bool isKunti = localRoleManager.IsImpostor && localRoleManager.IsKuntilanakForm;
            if (!isKunti)
            {
                FadeOut();
                return;
            }

            // Tampilkan UI
            FadeIn();
            UpdateUI();
        }

        private void UpdateUI()
        {
            var vlm = VillageLightManager.Instance;
            if (vlm == null) return;

            if (titleText != null)
            {
                titleText.text = "<b>[Q] SABOTASE LAMPU DESA</b>";
            }

            if (vlm.IsSabotaged)
            {
                // Sedang aktif padam
                float rem = vlm.RemainingBlackout;
                if (statusText != null)
                {
                    statusText.text = $"PADAM TOTAL: {rem:F1}s";
                    statusText.color = activeColor;
                }
                if (fillImage != null)
                {
                    fillImage.fillAmount = vlm.BlackoutNormalized;
                    fillImage.color = activeColor;
                }
                if (borderImage != null) borderImage.color = activeColor;
            }
            else if (vlm.IsOnCooldown)
            {
                // Sedang jeda cooldown
                float rem = vlm.RemainingCooldown;
                if (statusText != null)
                {
                    statusText.text = $"COOLDOWN: {rem:F1}s";
                    statusText.color = cooldownColor;
                }
                if (fillImage != null)
                {
                    fillImage.fillAmount = 1f - vlm.CooldownNormalized;
                    fillImage.color = cooldownColor;
                }
                if (borderImage != null) borderImage.color = cooldownColor;
            }
            else
            {
                // Siap digunakan
                if (statusText != null)
                {
                    statusText.text = "SIAP DIGUNAKAN";
                    statusText.color = readyColor;
                }
                if (fillImage != null)
                {
                    fillImage.fillAmount = 1.0f;
                    fillImage.color = readyColor;
                }
                if (borderImage != null) borderImage.color = readyColor;
            }
        }

        private void FadeIn()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1.0f, Time.deltaTime * fadeSpeed);
        }

        private void FadeOut()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0.0f, Time.deltaTime * fadeSpeed);
        }
    }
}
