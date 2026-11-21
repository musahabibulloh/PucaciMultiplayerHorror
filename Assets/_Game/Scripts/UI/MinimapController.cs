using UnityEngine;
using UnityEngine.UI;

namespace Pucaci.UI
{
    /// <summary>
    /// Mengontrol kamera minimap dan tampilan UI minimap (radar sudut & peta detail penuh).
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public static MinimapController Instance { get; private set; }

        [Header("Camera & Target")]
        [Tooltip("Kamera ortografis yang merender pemandangan atas minimap")]
        [SerializeField] private Camera minimapCamera;
        [Tooltip("Transform pemain lokal yang diikuti")]
        [SerializeField] private Transform targetPlayer;
        [Tooltip("Ketinggian kamera minimap di atas tanah")]
        [SerializeField] private float cameraHeight = 45f;

        [Header("Zoom Settings")]
        [Tooltip("Ukuran ortografis saat mode follow di sudut layar (radius pandang ~20m)")]
        [SerializeField] private float followZoom = 20f;
        [Tooltip("Ukuran ortografis saat mode peta penuh (mencakup seluruh desa ~48m)")]
        [SerializeField] private float fullMapZoom = 48f;
        [Tooltip("Pusat desa untuk peta penuh")]
        [SerializeField] private Vector3 villageCenter = Vector3.zero;

        [Header("Corner Radar UI")]
        [SerializeField] private RectTransform cornerRadarContainer;
        [SerializeField] private RectTransform cornerPlayerMarker;
        [SerializeField] private RectTransform cornerCompassNorth;
        [SerializeField] private GameObject mapPromptText;

        [Header("Full Map Detail UI")]
        [Tooltip("Panel overlay modal peta penuh")]
        [SerializeField] private GameObject fullMapModal;
        [Tooltip("RectTransform gambar feed peta (RawImage) pada modal peta penuh")]
        [SerializeField] private RectTransform fullMapFeed;
        [Tooltip("Marker pemain pada modal peta penuh")]
        [SerializeField] private RectTransform fullMapPlayerMarker;
        [Tooltip("Teks koordinat posisi pemain")]
        [SerializeField] private Text playerCoordinatesText;
        [Tooltip("Tombol tutup peta")]
        [SerializeField] private Button closeMapButton;

        [Header("Map Toggle State")]
        [SerializeField] private bool isFullMap = false;

        public bool IsFullMapOpen => isFullMap;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (minimapCamera == null)
            {
                var camObj = GameObject.Find("MinimapCamera");
                if (camObj != null) minimapCamera = camObj.GetComponent<Camera>();
                if (minimapCamera == null) minimapCamera = GetComponentInChildren<Camera>(true);
            }

            if (closeMapButton != null)
            {
                closeMapButton.onClick.AddListener(CloseFullMap);
            }

            // Awal permainan: pastikan modal peta penuh tertutup tanpa mengunci kursor sebelum pemain spawn
            isFullMap = false;
            if (fullMapModal != null) fullMapModal.SetActive(false);
            if (cornerRadarContainer != null) cornerRadarContainer.gameObject.SetActive(true);

            // Jika belum ada pemain lokal (misal masih di menu Host/Join), pastikan kursor bebas
            if (targetPlayer == null)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            TryFindLocalPlayer();
        }

        /// <summary>
        /// Daftarkan target pemain lokal yang akan diikuti oleh minimap.
        /// </summary>
        public void SetTarget(Transform player)
        {
            targetPlayer = player;
            if (!isFullMap)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            // Toggle peta penuh dengan tombol M
            if (IsMKeyPressed())
            {
                ToggleFullMap();
            }
            // Tutup peta penuh jika sedang terbuka dan tombol ESC ditekan
            else if (isFullMap && IsEscapeKeyPressed())
            {
                CloseFullMap();
            }

            // Jika target belum ditemukan (misal baru connect / spawn), cari lagi
            if (targetPlayer == null)
            {
                TryFindLocalPlayer();
            }
        }

        private void LateUpdate()
        {
            UpdateCameraView();
        }

        private void UpdateCameraView()
        {
            if (minimapCamera == null) return;

            if (isFullMap)
            {
                // Mode Full Map: Kamera diam di pusat desa mencakup seluruh area desa
                Vector3 targetCamPos = new Vector3(villageCenter.x, cameraHeight, villageCenter.z);
                minimapCamera.transform.position = targetCamPos;
                minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                minimapCamera.orthographicSize = fullMapZoom;

                // Update marker pemain di peta penuh
                if (targetPlayer != null)
                {
                    if (fullMapPlayerMarker != null)
                    {
                        fullMapPlayerMarker.gameObject.SetActive(true);
                        Vector3 offset = targetPlayer.position - villageCenter;

                        float feedRadius = (fullMapFeed != null) ? (fullMapFeed.rect.width * 0.5f) : 460f;
                        float uiScale = feedRadius / fullMapZoom;

                        fullMapPlayerMarker.anchoredPosition = new Vector2(offset.x * uiScale, offset.z * uiScale);
                        fullMapPlayerMarker.localRotation = Quaternion.Euler(0f, 0f, -targetPlayer.eulerAngles.y);
                    }
                }
                else
                {
                    if (fullMapPlayerMarker != null) fullMapPlayerMarker.gameObject.SetActive(false);
                }
            }
            else
            {
                // Mode Follow: Kamera mengikuti posisi pemain
                if (targetPlayer != null)
                {
                    Vector3 targetCamPos = new Vector3(targetPlayer.position.x, cameraHeight, targetPlayer.position.z);
                    minimapCamera.transform.position = targetCamPos;
                    minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    minimapCamera.orthographicSize = followZoom;

                    // Marker radar sudut selalu di tengah, hanya berotasi sesuai arah hadap pemain
                    if (cornerPlayerMarker != null)
                    {
                        cornerPlayerMarker.gameObject.SetActive(true);
                        cornerPlayerMarker.anchoredPosition = Vector2.zero;
                        cornerPlayerMarker.localRotation = Quaternion.Euler(0f, 0f, -targetPlayer.eulerAngles.y);
                    }
                }
                else
                {
                    // Fallback jika belum spawn: tampilkan pusat desa
                    minimapCamera.transform.position = new Vector3(villageCenter.x, cameraHeight, villageCenter.z);
                    minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    minimapCamera.orthographicSize = fullMapZoom;
                    if (cornerPlayerMarker != null) cornerPlayerMarker.gameObject.SetActive(false);
                }
            }
        }

        public void ToggleFullMap()
        {
            SetFullMapActive(!isFullMap);
        }

        public void OpenFullMap()
        {
            SetFullMapActive(true);
        }

        public void CloseFullMap()
        {
            SetFullMapActive(false);
        }

        private void SetFullMapActive(bool active)
        {
            isFullMap = active;

            // Buka / tutup modal peta penuh
            if (fullMapModal != null)
            {
                fullMapModal.SetActive(isFullMap);
            }

            // Sembunyikan radar sudut saat peta penuh aktif menggunakan CanvasGroup agar script MinimapController tidak ikut mati
            if (cornerRadarContainer != null)
            {
                var cg = cornerRadarContainer.GetComponent<CanvasGroup>();
                if (cg == null && cornerRadarContainer.gameObject == gameObject)
                {
                    cg = cornerRadarContainer.gameObject.AddComponent<CanvasGroup>();
                }

                if (cg != null)
                {
                    cg.alpha = isFullMap ? 0f : 1f;
                    cg.blocksRaycasts = !isFullMap;
                }
                else if (cornerRadarContainer.gameObject != gameObject)
                {
                    cornerRadarContainer.gameObject.SetActive(!isFullMap);
                }
            }

            // Langsung perbarui posisi & zoom kamera agar transisi instan
            UpdateCameraView();

            // Atur status kursor
            if (isFullMap)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Hanya kunci kursor jika pemain lokal sudah aktif di game
                if (targetPlayer != null)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        private bool IsMKeyPressed()
        {
            // Cek New Input System
            try
            {
                if (UnityEngine.InputSystem.Keyboard.current != null &&
                    UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
            catch {}

            // Cek Legacy Input sebagai fallback
            try
            {
                if (Input.GetKeyDown(KeyCode.M))
                {
                    return true;
                }
            }
            catch {}

            return false;
        }

        private bool IsEscapeKeyPressed()
        {
            // Cek New Input System
            try
            {
                if (UnityEngine.InputSystem.Keyboard.current != null &&
                    UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
            catch {}

            // Cek Legacy Input sebagai fallback
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    return true;
                }
            }
            catch {}

            return false;
        }

        private void TryFindLocalPlayer()
        {
            // 1. Coba cari PlayerNetworkSetup dengan isLocalPlayer
            var setups = FindObjectsOfType<Pucaci.Network.PlayerNetworkSetup>();
            foreach (var s in setups)
            {
                if (s.isLocalPlayer)
                {
                    targetPlayer = s.transform;
                    return;
                }
            }

            // 2. Fallback: Cari kamera pemain yang aktif (bukan minimap camera)
            if (Camera.main != null && Camera.main != minimapCamera)
            {
                var parent = Camera.main.transform.root;
                if (parent != null && parent.name.Contains("Player"))
                {
                    targetPlayer = parent;
                }
            }
        }
    }
}
