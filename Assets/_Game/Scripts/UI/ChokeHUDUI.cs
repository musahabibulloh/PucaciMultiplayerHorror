using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Pucaci.Player;

namespace Pucaci.UI
{
    /// <summary>
    /// UI Indikator untuk mekanik Menyekik (Choke) Kuntilanak.
    /// Menampilkan:
    /// 1. Bar durasi dan progres cekikan (0.0s hingga 5.0s / 100%).
    /// 2. Cincin radial progres melingkar tepat di sekeliling crosshair (Crosshair Radial Ring).
    /// 3. Prompt aksi saat dekat mangsa ("[E] / [Klik Kiri] Cekik Korban").
    /// 4. Peringatan panik dan hitung mundur kritis bagi korban yang sedang dicekik.
    /// </summary>
    public class ChokeHUDUI : MonoBehaviour
    {
        public static ChokeHUDUI Instance { get; private set; }

        [Header("Target")]
        [SerializeField] private KuntilanakChokeMechanic localChokeMechanic;
        [SerializeField] private CharacterModelManager modelManager;

        [Header("UI Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image crosshairRadialRing;
        [SerializeField] private Image crosshairRadialRingBG;

        [Header("Colors")]
        [SerializeField] private Color chokerColor = new Color(1.0f, 0.55f, 0.15f, 1.0f); // Oranye pemburu
        [SerializeField] private Color lethalColor = new Color(1.0f, 0.15f, 0.15f, 1.0f); // Merah letal
        [SerializeField] private Color victimPanicColor = new Color(1.0f, 0.12f, 0.12f, 1.0f); // Merah bahaya korban
        [SerializeField] private Color cooldownColor = new Color(0.35f, 0.65f, 0.95f, 0.95f); // Cyan/biru cooldown

        [Header("Settings")]
        [SerializeField] private float fadeSpeed = 8f;

        private RectTransform panelRect;
        private Vector3 initialScale = Vector3.one;

        // Posisi saat aktif mengangkat/mencekik (tengah layar di bawah crosshair)
        private readonly Vector2 centerAnchor = new Vector2(0.5f, 0.5f);
        private readonly Vector2 centerPos = new Vector2(0f, -85f);
        private readonly Vector2 centerSize = new Vector2(380f, 34f);

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

            EnsureCrosshairRadialRing();
        }

        private void Start()
        {
            if (localChokeMechanic == null)
            {
                TryFindLocalPlayer();
            }
            EnsureCrosshairRadialRing();
        }

        private void EnsureCrosshairRadialRing()
        {
            if (crosshairRadialRing != null && crosshairRadialRingBG != null) return;

            var crosshair = GameObject.Find("CrosshairDot");
            if (crosshair == null) return;

            Sprite ringSprite = CreateProceduralRingSprite(64, 4f);

            // 1. Cincin latar belakang redup (Background Ring)
            var existingBg = crosshair.transform.Find("ChokeRadialRing_BG");
            if (existingBg != null)
            {
                crosshairRadialRingBG = existingBg.GetComponent<Image>();
            }
            else
            {
                var bgObj = new GameObject("ChokeRadialRing_BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgObj.transform.SetParent(crosshair.transform, false);
                bgObj.transform.SetAsFirstSibling();

                var rtBg = bgObj.GetComponent<RectTransform>();
                rtBg.anchorMin = new Vector2(0.5f, 0.5f);
                rtBg.anchorMax = new Vector2(0.5f, 0.5f);
                rtBg.anchoredPosition = Vector2.zero;
                rtBg.sizeDelta = new Vector2(46f, 46f);

                crosshairRadialRingBG = bgObj.GetComponent<Image>();
                crosshairRadialRingBG.sprite = ringSprite;
                crosshairRadialRingBG.color = new Color(0.1f, 0.1f, 0.1f, 0.35f);
                crosshairRadialRingBG.raycastTarget = false;
                crosshairRadialRingBG.enabled = false;
            }

            // 2. Cincin pengisi progress radial (Foreground Fill Ring)
            var existing = crosshair.transform.Find("ChokeRadialRing");
            if (existing != null)
            {
                crosshairRadialRing = existing.GetComponent<Image>();
            }
            else
            {
                var ringObj = new GameObject("ChokeRadialRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ringObj.transform.SetParent(crosshair.transform, false);

                var rt = ringObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(46f, 46f);

                crosshairRadialRing = ringObj.GetComponent<Image>();
                crosshairRadialRing.sprite = ringSprite;
                crosshairRadialRing.type = Image.Type.Filled;
                crosshairRadialRing.fillMethod = Image.FillMethod.Radial360;
                crosshairRadialRing.fillOrigin = (int)Image.Origin360.Top;
                crosshairRadialRing.fillClockwise = true;
                crosshairRadialRing.color = chokerColor;
                crosshairRadialRing.raycastTarget = false;
                crosshairRadialRing.enabled = false;
            }
        }

        private static Sprite CreateProceduralRingSprite(int size, float ringWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float rOuter = size * 0.46f;
            float rInner = rOuter - ringWidth;

            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = 0f;
                    if (d <= rOuter && d >= rInner)
                    {
                        float edge1 = Mathf.Clamp01((rOuter - d) * 2.0f);
                        float edge2 = Mathf.Clamp01((d - rInner) * 2.0f);
                        alpha = Mathf.Min(edge1, edge2);
                    }
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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
                    localChokeMechanic = p.GetComponent<KuntilanakChokeMechanic>();
                    modelManager = p.GetComponent<CharacterModelManager>();
                    break;
                }
            }
        }

        private void Update()
        {
            if (crosshairRadialRing == null || crosshairRadialRingBG == null)
            {
                EnsureCrosshairRadialRing();
            }

            // Panel informasi teks/box disembunyikan total sesuai permintaan ("informasi nya gausah")
            if (canvasGroup != null && canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha = 0f;
            }

            if (localChokeMechanic == null)
            {
                TryFindLocalPlayer();
                if (localChokeMechanic == null)
                {
                    if (crosshairRadialRing != null) crosshairRadialRing.enabled = false;
                    if (crosshairRadialRingBG != null) crosshairRadialRingBG.enabled = false;
                    return;
                }
            }

            bool isChoking = localChokeMechanic.IsChoking;
            bool isBeingChoked = localChokeMechanic.IsBeingChoked;

            // HANYA UI BULAT (Radial Ring pada Crosshair) yang aktif saat sedang diangkat & dicekik
            if (isChoking || isBeingChoked)
            {
                float progress = localChokeMechanic.ChokeProgress;

                if (crosshairRadialRingBG != null)
                {
                    crosshairRadialRingBG.enabled = true;
                }

                if (crosshairRadialRing != null)
                {
                    crosshairRadialRing.enabled = true;
                    crosshairRadialRing.fillAmount = progress;
                    crosshairRadialRing.color = isBeingChoked
                        ? Color.Lerp(victimPanicColor, Color.white, Mathf.PingPong(Time.time * 8f, 1f) * 0.4f)
                        : Color.Lerp(chokerColor, lethalColor, progress);
                }
            }
            else
            {
                // Setelah selesai: langsung hilang total agar layar bersih dan tidak mengganggu
                if (crosshairRadialRing != null) crosshairRadialRing.enabled = false;
                if (crosshairRadialRingBG != null) crosshairRadialRingBG.enabled = false;
            }
        }

        private void EnsureCenterLayout()
        {
            if (panelRect == null)
            {
                panelRect = transform as RectTransform;
            }
            if (panelRect == null) return;

            if (panelRect.anchorMin != centerAnchor)
            {
                panelRect.anchorMin = centerAnchor;
                panelRect.anchorMax = centerAnchor;
            }

            if (panelRect.anchoredPosition != centerPos)
            {
                panelRect.anchoredPosition = centerPos;
            }

            if (panelRect.sizeDelta != centerSize)
            {
                panelRect.sizeDelta = centerSize;
            }

            if (statusText != null && !Mathf.Approximately(statusText.fontSize, 14f))
            {
                statusText.fontSize = 14f;
            }

            if (timerText != null && !Mathf.Approximately(timerText.fontSize, 14f))
            {
                timerText.fontSize = 14f;
            }
        }
    }
}
