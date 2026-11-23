using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    /// <summary>
    /// Mekanik menghilang khusus Kuntilanak ala kartun Casper (Translucent Blue Ethereal Ghost Mode).
    /// Model 3D tidak dihilangkan/dimatikan, melainkan berubah menjadi wujud arwah transparan berwarna biru
    /// dengan siluet kontur cyan bercahaya dan efek denyut gaib (spectral rim glow).
    /// </summary>
    public class KuntilanakGhostVanish : NetworkBehaviour
    {
        [Header("Ghost Transparency Visuals (Wujud Gaib Tembus Pandang)")]
        [Tooltip("Warna tint dan transparansi tubuh saat menghilang (Alpha menentukan tembus pandang)")]
        [ColorUsage(true, true)]
        [SerializeField] private Color ghostBodyColor = new Color(1.0f, 1.0f, 1.0f, 0.28f);

        [Tooltip("Warna kontur siluet menyala gaib di tepi tubuh (Ghost Rim Glow)")]
        [ColorUsage(true, true)]
        [SerializeField] private Color ghostRimColor = new Color(0.90f, 0.95f, 1.0f, 0.8f);

        [Tooltip("Eksponen ketajaman rim glow di tepi badan")]
        [SerializeField] private float rimPower = 2.0f;

        [Tooltip("Intensitas pendaran rim glow di tepi badan")]
        [SerializeField] private float rimIntensity = 2.2f;

        [Tooltip("Durasi transisi memudar menjadi arwah tembus pandang (detik)")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Ghost Vanish Duration & Cooldown")]
        [Tooltip("Durasi maksimal mode hilang dalam detik (otomatis kembali padat setelah durasi habis)")]
        [SerializeField] private float maxGhostDuration = 6.0f;

        [Tooltip("Durasi jeda (cooldown) setelah wujud gaib berakhir sebelum bisa digunakan lagi (detik)")]
        [SerializeField] private float ghostCooldown = 8.0f;

        [Header("Casper Flight Boost")]
        [Tooltip("Kecepatan melayang saat dalam wujud arwah tanpa bobot (m/s)")]
        [SerializeField] private float ghostSpeed = 6.0f;

        [Header("Network State")]
        [SyncVar(hook = nameof(OnGhostVanishStateChanged))]
        private bool isGhostForm = false;

        public bool IsGhostForm => isGhostForm;
        public float RemainingGhostDuration => Mathf.Max(0f, ghostDurationTimer);
        public float MaxGhostDuration => maxGhostDuration;
        public float GhostDurationNormalized => maxGhostDuration > 0f ? Mathf.Clamp01(ghostDurationTimer / maxGhostDuration) : 0f;

        public float GhostCooldown => ghostCooldown;
        public float RemainingCooldown => Mathf.Max(0f, ghostCooldownTimer);
        public bool IsOnCooldown => ghostCooldownTimer > 0.01f;
        public float CooldownNormalized => ghostCooldown > 0f ? Mathf.Clamp01(ghostCooldownTimer / ghostCooldown) : 0f;

        private float ghostDurationTimer = 0.0f;
        private float ghostCooldownTimer = 0.0f;

        private PlayerCharacterController pcc;
        private AudioSource audioSource;
        private AudioClip enterGhostClip;
        private AudioClip exitGhostClip;

        private SkinnedMeshRenderer[] kuntiRenderers;
        private Material[] instanceMaterials;
        private float currentGhostBlend = 0.0f;
        private bool isKuntilanakActive = false;

        private static readonly int GhostBlendProp = Shader.PropertyToID("_GhostBlend");
        private static readonly int GhostColorProp = Shader.PropertyToID("_GhostColor");
        private static readonly int RimColorProp = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerProp = Shader.PropertyToID("_RimPower");
        private static readonly int RimIntensityProp = Shader.PropertyToID("_RimIntensity");
        private static readonly int ZWriteProp = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendProp = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendProp = Shader.PropertyToID("_DstBlend");
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            pcc = GetComponent<PlayerCharacterController>();
            SetupAudio();
        }

        private void SetupAudio()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2.0f;
                audioSource.maxDistance = 30.0f;
            }

            enterGhostClip = CreateSpectralSound(isEnter: true);
            exitGhostClip = CreateSpectralSound(isEnter: false);
        }

        public void SetKuntilanakActive(bool active, Transform kuntiModelTransform)
        {
            isKuntilanakActive = active;

            if (active && kuntiModelTransform != null)
            {
                // Kondisi awal selalu full bentuk (padat / solid 100%, bukan transparan / menghilang)
                isGhostForm = false;
                ghostDurationTimer = 0.0f;
                ghostCooldownTimer = 0.0f;
                currentGhostBlend = 0.0f;
                if (pcc != null)
                {
                    pcc.SetGhostSpeedBoost(false);
                    pcc.SetGhostPhasing(false);
                }

                kuntiRenderers = kuntiModelTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (kuntiRenderers != null && kuntiRenderers.Length > 0)
                {
                    instanceMaterials = new Material[kuntiRenderers.Length];
                    for (int i = 0; i < kuntiRenderers.Length; i++)
                    {
                        var origMat = kuntiRenderers[i].sharedMaterial;
                        if (origMat != null)
                        {
                            instanceMaterials[i] = new Material(origMat);
                            instanceMaterials[i].name = origMat.name + "_GhostInstance";
                            kuntiRenderers[i].material = instanceMaterials[i];
                        }
                    }
                }

                // Langsung inisialisasi material ke status padat (full bentuk) di awal
                ApplyMaterialPropertiesImmediate(0.0f, false);
            }
            else
            {
                // Reset wujud jika ganti ke karakter manusia
                isGhostForm = false;
                ghostDurationTimer = 0.0f;
                ghostCooldownTimer = 0.0f;
                currentGhostBlend = 0.0f;
                if (pcc != null)
                {
                    pcc.SetGhostSpeedBoost(false);
                    pcc.SetGhostPhasing(false);
                }
            }
        }

        private void Update()
        {
            // Update interpolasi material setiap frame untuk semua pemain
            UpdateMaterialFade();

            // Hitung mundur durasi mode hilang jika sedang aktif (hanya bertahan selama 6 detik)
            if (isGhostForm)
            {
                ghostDurationTimer -= Time.deltaTime;
                if (ghostDurationTimer <= 0f)
                {
                    ghostDurationTimer = 0f;
                    // Durasi 6 detik habis: otomatis kembali ke wujud padat
                    if (isLocalPlayer || (!NetworkClient.active && !NetworkServer.active) || isServer)
                    {
                        SetGhostForm(false);
                    }
                }
            }
            else if (ghostCooldownTimer > 0f)
            {
                // Hitung mundur jeda / cooldown 8 detik setelah kembali padat
                ghostCooldownTimer -= Time.deltaTime;
                if (ghostCooldownTimer < 0f)
                {
                    ghostCooldownTimer = 0f;
                }
            }

            // Hanya pemain lokal pemilik karakter Kuntilanak yang membaca tombol input
            if (!isLocalPlayer || !isKuntilanakActive) return;

            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                // Tombol [R] (Skill) atau [F] (Ghost mode)
                if (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)
                {
                    togglePressed = true;
                }
            }
            if (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.F))
            {
                togglePressed = true;
            }
#endif

            if (togglePressed)
            {
                ToggleGhostForm();
            }
        }

        public void ToggleGhostForm()
        {
            if (!isGhostForm && IsOnCooldown)
            {
                // Sedang cooldown 8 detik: abaikan tombol aktifasi
                return;
            }
            SetGhostForm(!isGhostForm);
        }

        public void SetGhostForm(bool state)
        {
            if (isGhostForm == state) return;

            // Jika hendak mengaktifkan wujud gaib, cek cooldown
            if (state && IsOnCooldown)
            {
                return;
            }

            if (state)
            {
                ghostDurationTimer = maxGhostDuration;
            }
            else
            {
                ghostDurationTimer = 0f;
                ghostCooldownTimer = ghostCooldown;
            }

            if (!NetworkClient.active && !NetworkServer.active)
            {
                // Mode offline / singleplayer
                isGhostForm = state;
                OnGhostVanishStateChanged(!state, state);
            }
            else if (isServer)
            {
                isGhostForm = state;
                OnGhostVanishStateChanged(!state, state);
            }
            else
            {
                CmdSetGhostForm(state);
                if (pcc != null)
                {
                    pcc.SetGhostSpeedBoost(state);
                    pcc.SetGhostPhasing(state);
                }
            }
        }

        [Command]
        private void CmdSetGhostForm(bool state)
        {
            if (state && ghostCooldownTimer > 0.01f)
            {
                return;
            }

            isGhostForm = state;
            if (state)
            {
                ghostDurationTimer = maxGhostDuration;
            }
            else
            {
                ghostDurationTimer = 0f;
                ghostCooldownTimer = ghostCooldown;
            }

            if (isServer && isClient)
            {
                OnGhostVanishStateChanged(!state, state);
            }
        }

        private void OnGhostVanishStateChanged(bool oldState, bool newState)
        {
            PlaySpectralAudio(newState);

            if (newState)
            {
                ghostDurationTimer = maxGhostDuration;
            }
            else
            {
                ghostDurationTimer = 0f;
                if (oldState)
                {
                    ghostCooldownTimer = ghostCooldown;
                }
            }

            if (pcc != null)
            {
                pcc.SetGhostSpeedBoost(newState);
                pcc.SetGhostPhasing(newState);
            }
        }

        private void UpdateMaterialFade()
        {
            if (instanceMaterials == null || instanceMaterials.Length == 0) return;

            float targetBlend = isGhostForm ? 1.0f : 0.0f;
            float speed = (fadeDuration > 0.001f) ? (1f / fadeDuration) : 10f;
            currentGhostBlend = Mathf.MoveTowards(currentGhostBlend, targetBlend, Time.deltaTime * speed);

            bool isGhostActive = currentGhostBlend > 0.01f;

            for (int i = 0; i < instanceMaterials.Length; i++)
            {
                ApplyMaterialState(instanceMaterials[i], currentGhostBlend, isGhostActive);
            }
        }

        private void ApplyMaterialPropertiesImmediate(float blend, bool isGhostActive)
        {
            if (instanceMaterials == null) return;
            for (int i = 0; i < instanceMaterials.Length; i++)
            {
                ApplyMaterialState(instanceMaterials[i], blend, isGhostActive);
            }
        }

        private void ApplyMaterialState(Material mat, float blend, bool isGhostActive)
        {
            if (mat == null) return;

            if (mat.HasProperty(GhostBlendProp))
            {
                // Shader Custom/CasperGhost: Mengatur wujud arwah tembus pandang (transparan)
                mat.SetFloat(GhostBlendProp, blend);
                mat.SetColor(GhostColorProp, ghostBodyColor);
                mat.SetColor(RimColorProp, ghostRimColor);
                mat.SetFloat(RimPowerProp, rimPower);
                mat.SetFloat(RimIntensityProp, rimIntensity);

                mat.SetFloat(ZWriteProp, isGhostActive ? 0.0f : 1.0f);
                mat.SetInt(SrcBlendProp, isGhostActive ? (int)UnityEngine.Rendering.BlendMode.SrcAlpha : (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt(DstBlendProp, isGhostActive ? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.renderQueue = isGhostActive 
                    ? (int)UnityEngine.Rendering.RenderQueue.Transparent 
                    : (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
            else
            {
                // Fallback untuk shader standar URP Lit
                float alpha = Mathf.Lerp(1.0f, ghostBodyColor.a, blend);
                Color c = Color.Lerp(Color.white, ghostBodyColor, blend);
                c.a = alpha;
                mat.SetColor(BaseColorProp, c);

                if (isGhostActive)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor(EmissionColorProp, ghostRimColor * blend * 1.5f);

                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 0);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor(EmissionColorProp, Color.black);

                    mat.SetFloat("_Surface", 0);
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
            }
        }

        private void PlaySpectralAudio(bool isEnteringGhost)
        {
            if (audioSource == null) return;

            AudioClip clip = isEnteringGhost ? enterGhostClip : exitGhostClip;
            if (clip != null)
            {
                audioSource.pitch = isEnteringGhost ? 1.0f : 0.88f;
                audioSource.PlayOneShot(clip, 0.75f);
            }
        }

        /// <summary>
        /// Menghasilkan audio desau gaib prosedural (haunting ethereal whoosh)
        /// </summary>
        private AudioClip CreateSpectralSound(bool isEnter)
        {
            int sampleRate = 44100;
            float duration = 0.55f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Frekuensi desau gaib naik saat memudar, turun saat memadat
                float startFreq = isEnter ? 240f : 480f;
                float endFreq = isEnter ? 560f : 190f;
                float freq = Mathf.Lerp(startFreq, endFreq, progress);

                // Kombinasi sinewave halus + white noise berbisik
                float tone = Mathf.Sin(t * freq * Mathf.PI * 2f) * 0.45f;
                float noise = (Random.value * 2f - 1f) * 0.15f;

                // Envelope kurva halus berbentuk lonceng
                float envelope = Mathf.Sin(progress * Mathf.PI);

                samples[i] = (tone + noise) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create(isEnter ? "Casper_Enter" : "Casper_Exit", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
