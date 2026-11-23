using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Player;

namespace Pucaci.UI
{
    /// <summary>
    /// Menampilkan bar nyawa (Health) pemain lokal di HUD.
    /// Berkurang secara dinamis dan berubah warna dari hijau/cyan ke oranye dan merah saat kritis.
    /// </summary>
    public class HealthUI : MonoBehaviour
    {
        public static HealthUI Instance { get; private set; }

        [Header("Target")]
        [SerializeField] private PlayerHealth targetHealth;

        [Header("UI Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Colors")]
        [SerializeField] private Color fullColor = new Color(0.2f, 0.85f, 0.4f, 1.0f);
        [SerializeField] private Color midColor = new Color(0.95f, 0.75f, 0.2f, 1.0f);
        [SerializeField] private Color lowColor = new Color(0.95f, 0.2f, 0.2f, 1.0f);

        [Header("Settings")]
        [SerializeField] private float smoothSpeed = 8f;

        private float currentDisplayNormalized = 1.0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (targetHealth == null)
            {
                TryFindLocalPlayer();
            }
        }

        public void SetTarget(PlayerHealth health)
        {
            targetHealth = health;
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
                    targetHealth = p.GetComponent<PlayerHealth>();
                    break;
                }
            }
        }

        private void Update()
        {
            if (targetHealth == null)
            {
                TryFindLocalPlayer();
                if (targetHealth == null) return;
            }

            float targetNormalized = targetHealth.HealthNormalized;
            currentDisplayNormalized = Mathf.MoveTowards(currentDisplayNormalized, targetNormalized, Time.deltaTime * smoothSpeed);

            if (fillImage != null)
            {
                fillImage.fillAmount = currentDisplayNormalized;

                // Transisi warna: Hijau -> Kuning -> Merah
                Color barColor;
                if (currentDisplayNormalized > 0.5f)
                {
                    barColor = Color.Lerp(midColor, fullColor, (currentDisplayNormalized - 0.5f) * 2f);
                }
                else
                {
                    barColor = Color.Lerp(lowColor, midColor, currentDisplayNormalized * 2f);
                }

                // Berkedip jika kritis (< 25%)
                if (currentDisplayNormalized < 0.25f && currentDisplayNormalized > 0.01f)
                {
                    barColor = Color.Lerp(barColor, Color.white, Mathf.PingPong(Time.time * 6f, 1f) * 0.5f);
                }

                fillImage.color = barColor;
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(targetHealth.CurrentHealth)} / {Mathf.CeilToInt(targetHealth.MaxHealth)}";
            }
        }
    }
}
