using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Gameplay;
using Pucaci.Player;

namespace Pucaci.UI
{
    /// <summary>
    /// HUD Action Bar Terpadu khusus Skill Hantu Kuntilanak (Impostor):
    /// 1. [Q] Sabotase Lampu Desa (Padam Total 15s, Cooldown 40s)
    /// 2. [F] Wujud Gaib Casper (Melayang & Tembus Objek 6s, Cooldown 8s)
    /// 3. [Klik Kiri / E] Cekik Mangsa (Jangkauan 2.4m, Durasi 5s, Cooldown 10s)
    /// 4. [1-3] / [T] Penyamaran Mimic (Berubah wujud manusia Laras/Mamat/Vincent)
    /// 
    /// Menampilkan card status yang rapi, indikator cooldown yang sangat jelas dengan hitung mundur angka besar,
    /// efek visual saat skill aktif, serta deteksi target mangsa saat berada di jangkauan serangan.
    /// </summary>
    public class GhostSkillHUDUI : MonoBehaviour
    {
        public static GhostSkillHUDUI Instance { get; private set; }

        [System.Serializable]
        public class SkillCardUI
        {
            public GameObject rootObject;
            public Image backgroundImage;
            public Image borderImage;
            public Image cooldownOverlay;
            public TextMeshProUGUI keyBadgeText;
            public TextMeshProUGUI titleText;
            public TextMeshProUGUI statusText;
            public TextMeshProUGUI timerText;
            public Image iconImage;
            public RectTransform cardRect;
        }

        [Header("Main Containers")]
        [SerializeField] private CanvasGroup mainCanvasGroup;
        [SerializeField] private TextMeshProUGUI hudHeaderTitle;
        [SerializeField] private TextMeshProUGUI disguiseSubInfo;

        [Header("Skill Cards")]
        [SerializeField] private SkillCardUI sabotageCard;
        [SerializeField] private SkillCardUI vanishCard;
        [SerializeField] private SkillCardUI chokeCard;
        [SerializeField] private SkillCardUI disguiseCard;

        [Header("Color Themes")]
        [SerializeField] private Color sabotageTheme = new Color(0.95f, 0.22f, 0.22f, 1.0f); // Crimson
        [SerializeField] private Color sabotageActiveColor = new Color(0.60f, 0.25f, 0.95f, 1.0f); // Electric Purple
        [SerializeField] private Color vanishTheme = new Color(0.25f, 0.85f, 1.0f, 1.0f); // Cyan Ethereal
        [SerializeField] private Color chokeTheme = new Color(1.0f, 0.40f, 0.15f, 1.0f); // Blood Orange
        [SerializeField] private Color chokeTargetInRangeColor = new Color(1.0f, 0.10f, 0.10f, 1.0f); // Lethal Red
        [SerializeField] private Color disguiseTheme = new Color(0.70f, 0.50f, 1.0f, 1.0f); // Phantom Purple

        [SerializeField] private Color cardBgNormal = new Color(0.06f, 0.08f, 0.12f, 0.88f);
        [SerializeField] private Color cardBgCooldown = new Color(0.04f, 0.05f, 0.07f, 0.92f);
        [SerializeField] private Color borderDim = new Color(0.35f, 0.40f, 0.48f, 0.40f);
        [SerializeField] private Color cooldownTextColor = new Color(1.0f, 0.85f, 0.35f, 1.0f);

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 7.0f;

        // Target referensi lokal
        private PlayerRoleManager roleManager;
        private KuntilanakGhostVanish ghostVanish;
        private KuntilanakChokeMechanic chokeMechanic;
        private KuntilanakLightSabotage lightSabotage;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (mainCanvasGroup == null) mainCanvasGroup = GetComponent<CanvasGroup>();
            if (mainCanvasGroup != null) mainCanvasGroup.alpha = 0f;
        }

        private void Start()
        {
            TryResolveLocalPlayer();
        }

        private void TryResolveLocalPlayer()
        {
            var players = FindObjectsOfType<PlayerCharacterController>();
            foreach (var p in players)
            {
                var netSetup = p.GetComponent<Pucaci.Network.PlayerNetworkSetup>();
                bool isLocal = (netSetup == null) || (p.TryGetComponent<Mirror.NetworkIdentity>(out var id) && id.isLocalPlayer);
                if (isLocal)
                {
                    roleManager = p.GetComponent<PlayerRoleManager>();
                    ghostVanish = p.GetComponent<KuntilanakGhostVanish>();
                    chokeMechanic = p.GetComponent<KuntilanakChokeMechanic>();
                    lightSabotage = p.GetComponent<KuntilanakLightSabotage>();
                    break;
                }
            }
        }

        private void Update()
        {
            if (roleManager == null)
            {
                TryResolveLocalPlayer();
                if (roleManager == null)
                {
                    FadeOut();
                    return;
                }
            }

            // HUD hanya tampil untuk pemain Impostor (Hantu Kuntilanak)
            if (!roleManager.IsImpostor)
            {
                FadeOut();
                return;
            }

            FadeIn();
            UpdateHeaderInfo();
            UpdateSabotageCard();
            UpdateVanishCard();
            UpdateChokeCard();
            UpdateDisguiseCard();
        }

        private void UpdateHeaderInfo()
        {
            if (hudHeaderTitle == null) return;

            if (roleManager.IsKuntilanakForm)
            {
                hudHeaderTitle.text = "<b><color=#FF4444>KEMAMPUAN HANTU KUNTILANAK</color></b>";
                if (disguiseSubInfo != null)
                {
                    disguiseSubInfo.text = "<color=#AAAAAA>Wujud Asli</color> | Tekan <b>[1] [2] [3] [4]</b> untuk Menyamar";
                }
            }
            else
            {
                string disguiseName = roleManager.HumanDisguise.ToString().ToUpper();
                hudHeaderTitle.text = $"<b><color=#99BBFF>PENYAMARAN: {disguiseName}</color></b>";
                if (disguiseSubInfo != null)
                {
                    disguiseSubInfo.text = "Tekan <b>[T / 5]</b> wujud Kunti | <b>[Q]</b> Sabotase Lampu";
                }
            }
        }

        // =========================================================================
        // 1. CARD [Q] SABOTASE LAMPU DESA
        // =========================================================================
        private void UpdateSabotageCard()
        {
            if (sabotageCard.rootObject == null) return;
            var vlm = VillageLightManager.Instance;
            if (vlm == null) return;

            // Kuntilanak bisa mensabotase lampu baik saat berwujud Kuntilanak maupun saat masih dalam wujud manusia
            bool canUse = roleManager.IsImpostor;
            bool isSabotaged = vlm.IsSabotaged;
            bool isOnCd = vlm.IsOnCooldown;

            if (isSabotaged)
            {
                // Status: Lampu Padam Aktif
                float rem = vlm.RemainingBlackout;
                float fill = vlm.BlackoutNormalized;
                SetCardState(sabotageCard, canUse, true, sabotageActiveColor, cardBgNormal,
                    fill, "SABOTASE", $"{rem:0.0}s", "<color=#DDAAFF>PADAM TOTAL</color>");

                // Animasi denyut
                ApplyPulse(sabotageCard.cardRect, 1.03f, 8f);
            }
            else if (isOnCd)
            {
                // Status: Cooldown jeda padam
                float rem = vlm.RemainingCooldown;
                float fill = vlm.CooldownNormalized;
                SetCardState(sabotageCard, canUse, false, borderDim, cardBgCooldown,
                    fill, "COOLDOWN", $"{rem:0.0}s", "<color=#FFAA33>JEDA LISTRIK</color>");
                ResetScale(sabotageCard.cardRect);
            }
            else
            {
                // Status: Siap digunakan (bisa diaktifkan baik dalam wujud Kunti maupun manusia)
                Color readyColor = canUse ? sabotageTheme : borderDim;
                string readyStatus = canUse ? "<color=#55FF55>SIAP DIGUNAKAN</color>" : "<color=#888888>NON-AKTIF</color>";
                SetCardState(sabotageCard, canUse, false, readyColor, cardBgNormal,
                    0f, "SABOTASE", "READY", readyStatus);
                ResetScale(sabotageCard.cardRect);
            }
        }

        // =========================================================================
        // 2. CARD [F] WUJUD GAIB (CASPER GHOST MODE)
        // =========================================================================
        private void UpdateVanishCard()
        {
            if (vanishCard.rootObject == null) return;
            if (ghostVanish == null) return;

            bool isKunti = roleManager.IsKuntilanakForm;
            bool isGhostActive = isKunti && ghostVanish.IsGhostForm && (ghostVanish.RemainingGhostDuration > 0.01f);
            bool isOnCd = isKunti && !ghostVanish.IsGhostForm && ghostVanish.IsOnCooldown;

            if (isGhostActive)
            {
                // Status: Melayang / Casper Mode aktif
                float rem = ghostVanish.RemainingGhostDuration;
                float fill = ghostVanish.GhostDurationNormalized;
                bool critical = rem <= 2.0f;
                Color activeCol = critical ? Color.Lerp(Color.red, vanishTheme, Mathf.PingPong(Time.time * 6f, 1f)) : vanishTheme;
                string statusLabel = critical ? "<color=#FF5555>SEGERA HABIS!</color>" : "<color=#88FFFF>MELAYANG</color>";

                SetCardState(vanishCard, isKunti, true, activeCol, cardBgNormal,
                    fill, "WUJUD GAIB", $"{rem:0.0}s", statusLabel);
                ApplyPulse(vanishCard.cardRect, critical ? 1.06f : 1.03f, critical ? 14f : 6f);
            }
            else if (isOnCd)
            {
                // Status: Cooldown wujud gaib
                float rem = ghostVanish.RemainingCooldown;
                float fill = ghostVanish.CooldownNormalized;
                SetCardState(vanishCard, isKunti, false, borderDim, cardBgCooldown,
                    fill, "COOLDOWN", $"{rem:0.0}s", "<color=#FFAA33>PULIHKAN ENERGI</color>");
                ResetScale(vanishCard.cardRect);
            }
            else
            {
                // Status: Siap digunakan
                Color readyColor = isKunti ? vanishTheme : borderDim;
                string readyStatus = isKunti ? "<color=#55FF55>SIAP DIGUNAKAN</color>" : "<color=#888888>WUJUD KUNTI</color>";
                SetCardState(vanishCard, isKunti, false, readyColor, cardBgNormal,
                    0f, "WUJUD GAIB", "READY", readyStatus);
                ResetScale(vanishCard.cardRect);
            }
        }

        // =========================================================================
        // 3. CARD [Klik Kiri / E] CEKIK MANGSA (LETHAL ATTACK)
        // =========================================================================
        private void UpdateChokeCard()
        {
            if (chokeCard.rootObject == null) return;
            if (chokeMechanic == null) return;

            bool isKunti = roleManager.IsKuntilanakForm;
            bool isChoking = chokeMechanic.IsChoking;
            bool isOnCd = chokeMechanic.IsOnCooldown;
            bool hasTarget = isKunti && !isChoking && !isOnCd && (chokeMechanic.CurrentAimTarget != null);

            if (isChoking)
            {
                // Status: Sedang mencekik mangsa
                float timer = chokeMechanic.ChokeTimer;
                float maxD = chokeMechanic.MaxChokeDuration;
                float fill = chokeMechanic.ChokeProgress;
                SetCardState(chokeCard, isKunti, true, chokeTargetInRangeColor, cardBgNormal,
                    fill, "MENYEKIK", $"{timer:0.0}s/{maxD:0}s", "<color=#FF3333>LETHAL ATTACK</color>");
                ApplyPulse(chokeCard.cardRect, 1.05f, 10f);
            }
            else if (isOnCd)
            {
                // Status: Cooldown setelah membunuh/menyekik
                float rem = chokeMechanic.RemainingCooldown;
                float fill = chokeMechanic.CooldownNormalized;
                SetCardState(chokeCard, isKunti, false, borderDim, cardBgCooldown,
                    fill, "COOLDOWN", $"{rem:0.0}s", "<color=#FFAA33>RECOVERY</color>");
                ResetScale(chokeCard.cardRect);
            }
            else if (hasTarget)
            {
                // Status: Mangsa dalam jangkauan 2.4m!
                SetCardState(chokeCard, isKunti, false, chokeTargetInRangeColor, cardBgNormal,
                    0f, "CEKIK MANGSA", "SERANG!", "<color=#FF2222>MANGSA TERKUNCI!</color>");
                ApplyPulse(chokeCard.cardRect, 1.06f, 12f);
            }
            else
            {
                // Status: Siap mencari mangsa
                Color readyColor = isKunti ? chokeTheme : borderDim;
                string readyStatus = isKunti ? "<color=#55FF55>JANGKAUAN 2.4M</color>" : "<color=#888888>WUJUD KUNTI</color>";
                SetCardState(chokeCard, isKunti, false, readyColor, cardBgNormal,
                    0f, "CEKIK MANGSA", "READY", readyStatus);
                ResetScale(chokeCard.cardRect);
            }
        }

        // =========================================================================
        // 4. CARD [1-3] / [T] MENYAMAR (MIMIC TRANSFORM)
        // =========================================================================
        private void UpdateDisguiseCard()
        {
            if (disguiseCard.rootObject == null) return;
            if (roleManager == null) return;

            float cdRem = roleManager.TransformCooldownRemaining;
            bool onCd = cdRem > 0.05f;
            bool isKunti = roleManager.IsKuntilanakForm;

            if (onCd)
            {
                float fill = Mathf.Clamp01(cdRem / 2.0f);
                SetCardState(disguiseCard, true, false, borderDim, cardBgCooldown,
                    fill, "TRANSFORM", $"{cdRem:0.0}s", "<color=#FFAA33>COOLDOWN</color>");
                ResetScale(disguiseCard.cardRect);
            }
            else if (isKunti)
            {
                // Kuntilanak siap menyamar menjadi manusia [1] [2] [3]
                SetCardState(disguiseCard, true, false, disguiseTheme, cardBgNormal,
                    0f, "MENYAMAR", "SIAP", "<color=#DDAAFF>[1] [2] [3] PILIH</color>");
                ResetScale(disguiseCard.cardRect);
            }
            else
            {
                // Sedang dalam wujud manusia: siap kembali ke Kuntilanak [T / 4]
                SetCardState(disguiseCard, true, true, sabotageTheme, cardBgNormal,
                    0f, "KUNTILANAK", "[T / 4]", "<color=#FF6666>WUJUD HANTU</color>");
                ApplyPulse(disguiseCard.cardRect, 1.03f, 4f);
            }
        }

        // =========================================================================
        // HELPER TAMPILAN CARD
        // =========================================================================
        private void SetCardState(
            SkillCardUI card,
            bool isUsable,
            bool isActiveNow,
            Color borderColor,
            Color bgColor,
            float cooldownFill,
            string title,
            string timer,
            string statusDesc)
        {
            if (card.borderImage != null) card.borderImage.color = borderColor;
            if (card.backgroundImage != null) card.backgroundImage.color = bgColor;

            if (card.cooldownOverlay != null)
            {
                bool showOverlay = cooldownFill > 0.01f;
                card.cooldownOverlay.enabled = showOverlay;
                if (showOverlay)
                {
                    card.cooldownOverlay.fillAmount = cooldownFill;
                }
            }

            if (card.titleText != null) card.titleText.text = title;

            if (card.timerText != null)
            {
                card.timerText.text = timer;
                if (cooldownFill > 0.01f)
                {
                    card.timerText.color = cooldownTextColor;
                }
                else if (isActiveNow)
                {
                    card.timerText.color = Color.white;
                }
                else
                {
                    card.timerText.color = isUsable ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f);
                }
            }

            if (card.statusText != null)
            {
                card.statusText.text = statusDesc;
            }
        }

        private void ApplyPulse(RectTransform rect, float maxScale, float speed)
        {
            if (rect == null) return;
            float s = 1.0f + (maxScale - 1.0f) * Mathf.PingPong(Time.time * speed, 1.0f);
            rect.localScale = new Vector3(s, s, 1.0f);
        }

        private void ResetScale(RectTransform rect)
        {
            if (rect == null) return;
            if (rect.localScale != Vector3.one)
            {
                rect.localScale = Vector3.one;
            }
        }

        private void FadeIn()
        {
            if (mainCanvasGroup == null) return;
            mainCanvasGroup.alpha = Mathf.MoveTowards(mainCanvasGroup.alpha, 1.0f, Time.deltaTime * fadeSpeed);
        }

        private void FadeOut()
        {
            if (mainCanvasGroup == null) return;
            mainCanvasGroup.alpha = Mathf.MoveTowards(mainCanvasGroup.alpha, 0.0f, Time.deltaTime * fadeSpeed);
        }
    }
}
