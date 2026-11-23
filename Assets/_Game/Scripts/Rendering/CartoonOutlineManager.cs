using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Pengontrol runtime untuk efek Toon Outline (Cel-Shading) pada Karakter dan Objek 3D.
    /// Tidak menggelapkan dunia sama sekali. Hanya memberi garis tepi hitam tipis dan halus pada model.
    /// </summary>
    public class CartoonOutlineManager : MonoBehaviour
    {
        public static CartoonOutlineManager Instance { get; private set; }

        [Header("Shortcut & Debug")]
        [Tooltip("Tekan tombol ini di in-game untuk menyalakan/mematikan outline (membandingkan sebelum & sesudah)")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F6;

        [Header("Pengaturan Outline")]
        [SerializeField] private bool outlineEnabled = true;
        [SerializeField] private Color outlineColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        [Range(0.5f, 3.5f)]
        [Tooltip("Ketebalan garis di layar (1.0 - 1.5 sangat tipis dan halus ala anime/kartun)")]
        [SerializeField] private float thickness = 1.2f;

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
            thickness = Mathf.Clamp(val, 0.2f, 4.0f);
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
