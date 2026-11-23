using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Pengontrol runtime untuk efek Toon Outline (Cel-Shading ala Fortnite / Anime) pada Karakter dan Objek 3D.
    /// Tidak menggelapkan dunia sama sekali. Memberi garis siluet hitam tegas dan tajam pada model 3D.
    /// </summary>
    public class CartoonOutlineManager : MonoBehaviour
    {
        public static CartoonOutlineManager Instance { get; private set; }

        [Header("Shortcut & Debug")]
        [Tooltip("Tekan tombol ini di in-game untuk menyalakan/mematikan outline (membandingkan sebelum & sesudah)")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F6;

        [Header("Pengaturan Outline (Fortnite Style)")]
        [SerializeField] private bool outlineEnabled = true;
        [SerializeField] private Color outlineColor = Color.black;
        [Range(1.0f, 8.0f)]
        [Tooltip("Ketebalan garis di layar (3.0 - 5.0 adalah standar tegas ala Fortnite)")]
        [SerializeField] private float thickness = 3.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeRuntime()
        {
            if (Instance == null)
            {
                var go = new GameObject("[CartoonOutlineManager]");
                Instance = go.AddComponent<CartoonOutlineManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Toggle perbandingan sebelum/sesudah dengan F6
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleOutline();
            }
        }

        public void ToggleOutline()
        {
            outlineEnabled = !outlineEnabled;
            ApplyToAllAppliers();
            Debug.Log($"<color=#FFCC00>[CartoonOutline] Outline Karakter & Objek: {(outlineEnabled ? "AKTIF" : "NONAKTIF")}</color>");
        }

        public void SetEnabled(bool isEnabled)
        {
            outlineEnabled = isEnabled;
            ApplyToAllAppliers();
        }

        public void SetThickness(float val)
        {
            thickness = Mathf.Clamp(val, 0.5f, 10.0f);
            ApplyToAllAppliers();
        }

        public void SetColor(Color col)
        {
            outlineColor = col;
            ApplyToAllAppliers();
        }

        private void ApplyToAllAppliers()
        {
            foreach (var applier in ToonOutlineApplier.All)
            {
                if (applier == null) continue;

                if (outlineEnabled)
                {
                    applier.SetOutlineWidth(thickness);
                    applier.SetOutlineColor(outlineColor);
                    applier.ApplyOutline();
                }
                else
                {
                    applier.RemoveOutline();
                }
            }
        }
    }
}
