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
        [SerializeField] private float thickness = 6.0f;

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

        private void Update()
        {
            // Toggle perbandingan sebelum/sesudah dengan F6
            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f6Key.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#endif
            if (!togglePressed && Input.GetKeyDown(toggleKey))
            {
                togglePressed = true;
            }

            if (togglePressed)
            {
                ToggleOutline();
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
