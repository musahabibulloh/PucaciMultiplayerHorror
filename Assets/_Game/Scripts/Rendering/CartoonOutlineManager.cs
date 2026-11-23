using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Pengontrol runtime untuk efek Cartoon Outline (Cel-Shading).
    /// Memungkinkan pengaturan ketebalan, warna, dan toggle on/off melalui script, UI settings, atau shortcut tombol.
    /// </summary>
    public class CartoonOutlineManager : MonoBehaviour
    {
        public static CartoonOutlineManager Instance { get; private set; }

        [Header("Shortcut & Debug")]
        [Tooltip("Tekan tombol ini di in-game untuk menyalakan/mematikan outline (membandingkan sebelum & sesudah)")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F6;

        [Header("Pengaturan Outline")]
        [SerializeField] private bool outlineEnabled = true;
        [SerializeField] private Color outlineColor = new Color(0.01f, 0.01f, 0.01f, 1f);
        [Range(0.5f, 3.5f)]
        [SerializeField] private float thickness = 1.25f;

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
            ApplySettingsToFeature();
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
            ApplySettingsToFeature();
            Debug.Log($"<color=#FFCC00>[CartoonOutline] Outline Kartunis: {(outlineEnabled ? "AKTIF" : "NONAKTIF")}</color>");
        }

        public void SetEnabled(bool isEnabled)
        {
            outlineEnabled = isEnabled;
            ApplySettingsToFeature();
        }

        public void SetThickness(float val)
        {
            thickness = Mathf.Clamp(val, 0.5f, 4.0f);
            ApplySettingsToFeature();
        }

        public void SetColor(Color col)
        {
            outlineColor = col;
            ApplySettingsToFeature();
        }

        private void ApplySettingsToFeature()
        {
            if (CartoonOutlineFeature.Instance != null && CartoonOutlineFeature.Instance.Settings != null)
            {
                CartoonOutlineFeature.Instance.Settings.isEnabled = outlineEnabled;
                CartoonOutlineFeature.Instance.Settings.outlineColor = outlineColor;
                CartoonOutlineFeature.Instance.Settings.outlineThickness = thickness;
            }
        }
    }
}
