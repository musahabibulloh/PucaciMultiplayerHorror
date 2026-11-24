using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Pengontrol runtime untuk efek Toon Outline (Cel-Shading ala Fortnite / Anime) pada Karakter dan Objek 3D.
    /// Tidak menggelapkan dunia sama sekali. Memberi garis siluet hitam tegas dan tajam pada model 3D.
    /// Tombol [F6] untuk membandingkan tampilan dengan/tanpa outline secara langsung.
    /// </summary>
    public class CartoonOutlineManager : MonoBehaviour
    {
        private static CartoonOutlineManager instance;
        public static CartoonOutlineManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<CartoonOutlineManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("[CartoonOutlineManager]");
                        instance = go.AddComponent<CartoonOutlineManager>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return instance;
            }
        }

        [Header("Shortcut & Debug")]
        [Tooltip("Tekan tombol ini di in-game untuk menyalakan/mematikan outline (membandingkan sebelum & sesudah)")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F6;

        [Header("Pengaturan Outline (Fortnite Style)")]
        [SerializeField] private bool outlineEnabled = true;
        [SerializeField] private Color outlineColor = Color.black;
        [Range(1.0f, 15.0f)]
        [Tooltip("Ketebalan garis di layar (5.0 - 8.0 adalah standar tegas ala Fortnite)")]
        [SerializeField] private float thickness = 7.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeRuntime()
        {
            if (instance == null)
            {
                var go = new GameObject("[CartoonOutlineManager]");
                instance = go.AddComponent<CartoonOutlineManager>();
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(go);
                }
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            AutoApplyOutlineToSceneProps();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            AutoApplyOutlineToSceneProps();
        }

        /// <summary>
        /// Secara otomatis memindai dan menerapkan outline kartunis tebal ke seluruh objek dan props desa (rumah, pagar, lampu, sesajen, dll).
        /// </summary>
        public void AutoApplyOutlineToSceneProps()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            var rootObjects = activeScene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                if (root == null) continue;

                // Abaikan UI, kamera, manager sistem, spawn points, dan lighting
                string rootName = root.name;
                if (rootName.Contains("Camera") || rootName.Contains("Canvas") || rootName.Contains("EventSystem") ||
                    rootName.Contains("NetworkManager") || rootName.Contains("SpawnPoints") || rootName.Contains("Moonlight") ||
                    rootName.Contains("GlobalVolume") || rootName.Contains("AudioPlayer") || rootName.StartsWith("["))
                {
                    continue;
                }

                if (root.GetComponent<Camera>() != null || root.GetComponent<Canvas>() != null)
                {
                    continue;
                }

                // Cek apakah memiliki MeshRenderer atau SkinnedMeshRenderer
                if (root.GetComponentInChildren<MeshRenderer>(true) != null || root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                {
                    ToonOutlineApplier.ApplyTo(root, thickness);
                }
            }

            ApplyToAllAppliers();
        }

        private float scanTimer = 0f;
        private const float SCAN_INTERVAL = 1.0f;

        private void Update()
        {
            // Toggle perbandingan sebelum/sesudah dengan F6
            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f6Key.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#else
            if (Input.GetKeyDown(toggleKey))
            {
                togglePressed = true;
            }
#endif

            if (togglePressed)
            {
                ToggleOutline();
            }

            // Periodic scan untuk memastikan pemain jaringan (Mirror) yang baru spawn / berganti wujud selalu mendapatkan outline
            scanTimer += Time.deltaTime;
            if (scanTimer >= SCAN_INTERVAL)
            {
                scanTimer = 0f;
                ScanAndApplyToNewPlayers();
            }
        }

        public void ScanAndApplyToNewPlayers()
        {
            var modelManagers = FindObjectsOfType<Pucaci.Player.CharacterModelManager>();
            foreach (var mgr in modelManagers)
            {
                if (mgr == null) continue;
                var activeModel = mgr.ActiveModel;
                if (activeModel != null && activeModel.activeInHierarchy)
                {
                    var smr = activeModel.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        var outlineChild = smr.transform.Find("ToonOutline_Backface");
                        if (outlineChild == null || !outlineChild.gameObject.activeInHierarchy)
                        {
                            ToonOutlineApplier.ApplyTo(activeModel, thickness);
                        }
                    }
                }
            }
        }

        public void ToggleOutline()
        {
            outlineEnabled = !outlineEnabled;
            ApplyToAllAppliers();
            Debug.Log($"<color=#FFCC00>[CartoonOutline] Outline Karakter & Objek: {(outlineEnabled ? "AKTIF (Garis Hitam On)" : "NONAKTIF (Garis Hitam Off)")}</color>");
        }

        public void SetEnabled(bool isEnabled)
        {
            outlineEnabled = isEnabled;
            ApplyToAllAppliers();
        }

        public void SetThickness(float val)
        {
            thickness = Mathf.Clamp(val, 1.0f, 15.0f);
            ApplyToAllAppliers();
        }

        public void SetColor(Color col)
        {
            outlineColor = col;
            ApplyToAllAppliers();
        }

        private void ApplyToAllAppliers()
        {
            // 1. Terapkan pada semua komponen ToonOutlineApplier terdaftar
            foreach (var applier in ToonOutlineApplier.All)
            {
                if (applier == null) continue;
                applier.SetOutlineWidth(thickness);
                applier.SetOutlineColor(outlineColor);
                applier.SetOutlineEnabled(outlineEnabled);
            }

            // 2. Fallback langsung ke seluruh renderer ToonOutline_Backface di scene agar 100% responsif terhadap F6
            var allRenderers = FindObjectsOfType<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r != null && r.name.Contains("ToonOutline"))
                {
                    r.enabled = outlineEnabled;
                }
            }
        }
    }
}
