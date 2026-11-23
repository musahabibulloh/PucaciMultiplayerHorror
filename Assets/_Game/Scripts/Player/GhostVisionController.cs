using UnityEngine;
using Mirror;

namespace Pucaci.Player
{
    /// <summary>
    /// Mengelola penglihatan mode Hantu (Ghost Vision / Penglihatan Malam Jelas Kuntilanak)
    /// vs mode Pemain (Survivor / Wujud Manusia).
    /// - Mode Hantu: Langit TETAP MALAM (Mat_Skybox_Night), namun pandangan lingkungan menjadi jelas dan terbaca (cahaya rembulan dan ambient meningkat wajar, kabut menipis), tanpa ada cahaya yang memancar dari tubuh kuntilanak.
    /// - Mode Pemain: Lingkungan malam pekat gulita dan berkabut tebal, pemain membutuhkan senter untuk menerangi jalan.
    /// </summary>
    [DisallowMultipleComponent]
    public class GhostVisionController : NetworkBehaviour
    {
        [Header("Transition Settings")]
        [Tooltip("Kecepatan transisi penglihatan saat bertransformasi")]
        [SerializeField] private float transitionSpeed = 3.5f;

        [Header("Skybox Material (Tetap Malam)")]
        [Tooltip("Skybox malam yang digunakan untuk kedua mode (tetap malam)")]
        [SerializeField] private Material nightSkyboxMaterial;

        [Header("Ghost Vision Settings (Mode Hantu - Rembulan Terang & Jelas)")]
        [Tooltip("Intensitas cahaya rembulan saat mode hantu (cukup jelas menerangi desa tanpa menjadi siang)")]
        [SerializeField] private float ghostSunIntensity = 0.68f;
        [SerializeField] private Color ghostSunColor = new Color(0.62f, 0.72f, 0.88f);
        [SerializeField] private Color ghostSkyAmbient = new Color(0.18f, 0.24f, 0.35f);
        [SerializeField] private Color ghostEquatorAmbient = new Color(0.12f, 0.16f, 0.24f);
        [SerializeField] private Color ghostGroundAmbient = new Color(0.06f, 0.08f, 0.12f);
        [SerializeField] private float ghostAmbientIntensity = 0.92f;
        [SerializeField] private Color ghostFogColor = new Color(0.02f, 0.028f, 0.055f);
        [SerializeField] private float ghostFogDensity = 0.0028f;

        [Header("Human Night Settings (Mode Pemain - Gelap Gulita & Butuh Senter)")]
        [SerializeField] private float humanSunIntensity = 0.15f;
        [SerializeField] private Color humanSunColor = new Color(0.48f, 0.60f, 0.75f);
        [SerializeField] private Color humanSkyAmbient = new Color(0.04f, 0.06f, 0.10f);
        [SerializeField] private Color humanEquatorAmbient = new Color(0.02f, 0.03f, 0.05f);
        [SerializeField] private Color humanGroundAmbient = new Color(0.008f, 0.01f, 0.015f);
        [SerializeField] private float humanAmbientIntensity = 0.5f;
        [SerializeField] private Color humanFogColor = new Color(0.015f, 0.022f, 0.045f);
        [SerializeField] private float humanFogDensity = 0.012f;

        // Referensi internal
        private Light directionalSun;
        private PlayerRoleManager roleManager;
        private CharacterModelManager modelManager;
        private FlashlightController flashlightController;
        private NetworkIdentity netIdentity;

        private float currentWeight = 0f;
        private bool isGhostActive = false;
        private bool prevGhostActive = false;
        private bool isLocal = true;

        private void Awake()
        {
            roleManager = GetComponent<PlayerRoleManager>();
            modelManager = GetComponent<CharacterModelManager>();
            flashlightController = GetComponent<FlashlightController>();
            netIdentity = GetComponent<NetworkIdentity>();

            CleanupLegacyGhostLights();
            LoadSkyboxMaterials();
        }

        private void Start()
        {
            DetermineLocalPlayer();
            ResolveSunLight();

            if (isLocal)
            {
                isGhostActive = CheckIsGhost();
                prevGhostActive = isGhostActive;
                currentWeight = isGhostActive ? 1.0f : 0.0f;
                ApplyVisionState(currentWeight, true);
            }
        }

        private void OnDestroy()
        {
            if (isLocal)
            {
                // Kembalikan ke mode malam default
                ApplyVisionState(0.0f, true);
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            isLocal = true;
            CleanupLegacyGhostLights();
            ResolveSunLight();

            isGhostActive = CheckIsGhost();
            prevGhostActive = isGhostActive;
            currentWeight = isGhostActive ? 1.0f : 0.0f;
            ApplyVisionState(currentWeight, true);
        }

        private void DetermineLocalPlayer()
        {
            if (netIdentity != null && (NetworkClient.active || NetworkServer.active))
            {
                try { isLocal = isLocalPlayer; } catch { isLocal = true; }
            }
            else
            {
                isLocal = true;
            }
        }

        private void LoadSkyboxMaterials()
        {
#if UNITY_EDITOR
            if (nightSkyboxMaterial == null)
            {
                nightSkyboxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Mat_Skybox_Night.mat");
            }
#endif
        }

        private void CleanupLegacyGhostLights()
        {
            // Hapus sumber cahaya point light bawaan agar hantu tidak tampak bercahaya sendiri
            var legacyLight = transform.Find("CameraRoot/FirstPersonCamera/GhostVisionLight") ?? transform.Find("GhostVisionLight");
            if (legacyLight != null)
            {
                Destroy(legacyLight.gameObject);
            }

            var legacyVolume = transform.Find("CameraRoot/FirstPersonCamera/GhostVisionVolume") ?? transform.Find("GhostVisionVolume");
            if (legacyVolume != null)
            {
                Destroy(legacyVolume.gameObject);
            }
        }

        private void ResolveSunLight()
        {
            if (directionalSun == null) directionalSun = RenderSettings.sun;
            if (directionalSun == null)
            {
                var sunObj = GameObject.Find("Moonlight") ?? GameObject.Find("Sunlight") ?? GameObject.Find("Directional Light");
                if (sunObj != null) directionalSun = sunObj.GetComponent<Light>();
            }
        }

        private void Update()
        {
            DetermineLocalPlayer();
            if (!isLocal) return;

            // Cek apakah pemain saat ini sedang dalam mode hantu (Kuntilanak)
            isGhostActive = CheckIsGhost();

            // Tangani event pergantian mode (Hantu <-> Manusia)
            if (isGhostActive != prevGhostActive)
            {
                OnGhostModeToggled(isGhostActive);
                prevGhostActive = isGhostActive;
            }

            // Transisi halus bobot penglihatan (0 = Malam, 1 = Siang)
            float targetWeight = isGhostActive ? 1.0f : 0.0f;
            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * transitionSpeed);

            ApplyVisionState(currentWeight, false);
        }

        private void OnGhostModeToggled(bool isGhost)
        {
            if (flashlightController != null)
            {
                flashlightController.SetGhostMode(isGhost);

                if (!isGhost)
                {
                    // Saat kembali ke mode manusia, senter otomatis aktif untuk memandu di kegelapan malam
                    flashlightController.SetFlashlightState(true, playSound: true);
                }
            }
        }

        private void ApplyVisionState(float weight, bool instant)
        {
            ResolveSunLight();

            // 1. Cahaya Rembulan (Directional Light global di langit malam)
            if (directionalSun != null)
            {
                directionalSun.intensity = Mathf.Lerp(humanSunIntensity, ghostSunIntensity, weight);
                directionalSun.color = Color.Lerp(humanSunColor, ghostSunColor, weight);
            }

            // 2. Ambient Lighting Global (Malam temaram vs Penglihatan jelas malam)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(humanSkyAmbient, ghostSkyAmbient, weight);
            RenderSettings.ambientEquatorColor = Color.Lerp(humanEquatorAmbient, ghostEquatorAmbient, weight);
            RenderSettings.ambientGroundColor = Color.Lerp(humanGroundAmbient, ghostGroundAmbient, weight);
            RenderSettings.ambientIntensity = Mathf.Lerp(humanAmbientIntensity, ghostAmbientIntensity, weight);

            // 3. Kabut Malam (Fog - tebal saat manusia, menipis saat hantu)
            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(humanFogColor, ghostFogColor, weight);
            RenderSettings.fogDensity = Mathf.Lerp(humanFogDensity, ghostFogDensity, weight);

            // 4. Skybox: Tetap Malam (Mat_Skybox_Night) untuk kedua mode
            if (nightSkyboxMaterial != null && RenderSettings.skybox != nightSkyboxMaterial)
            {
                RenderSettings.skybox = nightSkyboxMaterial;
                DynamicGI.UpdateEnvironment();
            }
        }

        private bool CheckIsGhost()
        {
            // 1. Cek dari PlayerRoleManager jika ada (Impostor wujud Kuntilanak)
            if (roleManager != null)
            {
                if (roleManager.IsKuntilanakForm) return true;
                if (!roleManager.IsImpostor) return false;
            }

            // 2. Cek dari CharacterModelManager jika ada
            if (modelManager != null)
            {
                var kuntiModel = modelManager.transform.Find("Model_Kuntilanak");
                if (kuntiModel != null && kuntiModel.gameObject.activeSelf) return true;
            }

            return false;
        }

        /// <summary>
        /// Paksa toggle mode hantu (untuk keperluan testing/debug).
        /// </summary>
        public void ForceGhostMode(bool enable)
        {
            isGhostActive = enable;
            OnGhostModeToggled(isGhostActive);
        }
    }
}
