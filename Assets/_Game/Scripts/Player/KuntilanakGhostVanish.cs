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

        private Material ghostWhiteMaterial;

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
        private AudioSource vanishAudioSource;
        private AudioClip enterGhostClip;
        private AudioClip exitGhostClip;
        private float lastSpectralAudioTime = -10f;
        private bool lastSpectralAudioState = false;

        private SkinnedMeshRenderer[] kuntiRenderers;
        private Material[] instanceMaterials;
        private Material[][] originalSharedMaterials;
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
            GetVanishAudioSource();
            enterGhostClip = GetSpectralClip(isEnter: true);
            exitGhostClip = GetSpectralClip(isEnter: false);
        }

        private AudioSource GetVanishAudioSource()
        {
            if (vanishAudioSource != null) return vanishAudioSource;

            Transform child = transform.Find("GhostVanishAudioSource");
            if (child != null)
            {
                vanishAudioSource = child.GetComponent<AudioSource>();
            }
            if (vanishAudioSource == null)
            {
                GameObject go = new GameObject("GhostVanishAudioSource");
                go.transform.SetParent(transform, false);
                vanishAudioSource = go.AddComponent<AudioSource>();
                vanishAudioSource.playOnAwake = false;
            }

            return vanishAudioSource;
        }

        public static AudioClip GetSpectralClip(bool isEnter)
        {
            AudioClip clip = null;
            if (isEnter)
            {
                clip = Resources.Load<AudioClip>("Audio/Ghost_Vanish_Enter");
                if (clip == null)
                {
#if UNITY_EDITOR
                    clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/Ghost_Vanish_Enter.wav");
#endif
                }
                if (clip == null) clip = CreateSpectralSound(true);
            }
            else
            {
                clip = Resources.Load<AudioClip>("Audio/Ghost_Vanish_Exit");
                if (clip == null)
                {
#if UNITY_EDITOR
                    clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/Ghost_Vanish_Exit.wav");
#endif
                }
                if (clip == null) clip = CreateSpectralSound(false);
            }
            return clip;
        }

        private void EnsureGhostMaterial()
        {
            if (ghostWhiteMaterial == null)
            {
                ghostWhiteMaterial = Resources.Load<Material>("M_Kuntilanak_GhostWhite");
                if (ghostWhiteMaterial == null)
                {
#if UNITY_EDITOR
                    ghostWhiteMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/M_Kuntilanak_GhostWhite.mat");
#endif
                }
            }
        }

        public void SetKuntilanakActive(bool active, Transform kuntiModelTransform)
        {
            isKuntilanakActive = active;

            if (active && kuntiModelTransform != null)
            {
                EnsureGhostMaterial();

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

                var allKuntiRenderers = kuntiModelTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var validRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>();
                if (allKuntiRenderers != null)
                {
                    foreach (var rend in allKuntiRenderers)
                    {
                        if (rend != null && !rend.name.Contains("ToonOutline"))
                        {
                            validRenderers.Add(rend);
                        }
                    }
                }
                kuntiRenderers = validRenderers.ToArray();
                if (kuntiRenderers != null && kuntiRenderers.Length > 0)
                {
                    originalSharedMaterials = new Material[kuntiRenderers.Length][];
                    for (int i = 0; i < kuntiRenderers.Length; i++)
                    {
                        var mats = kuntiRenderers[i].sharedMaterials;
                        if (mats != null && mats.Length > 0)
                        {
                            originalSharedMaterials[i] = (Material[])mats.Clone();
                        }
                    }
                }

                // Langsung inisialisasi material ke status padat (full bentuk) di awal
                ApplyGhostVisibility(isGhostForm);
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
                ApplyGhostVisibility(false);
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
                // Putar audio di client seketika untuk respon horror instan tanpa jeda latency
                PlaySpectralAudio(state);
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

            ApplyGhostVisibility(newState);
        }

        /// <summary>
        /// Mengatur visibilitas Kuntilanak saat mode menghilang (tekan F atau R):
        /// Tubuh Kuntilanak, outline, dan nametag disembunyikan sepenuhnya (rend.enabled = false)
        /// agar benar-benar menghilang dari pandangan player selama durasi skill berlangsung,
        /// dan dimunculkan kembali saat durasi habis atau kembali ke wujud padat.
        /// </summary>
        public void ApplyGhostVisibility(bool isGhost)
        {
            if (kuntiRenderers != null && kuntiRenderers.Length > 0)
            {
                for (int i = 0; i < kuntiRenderers.Length; i++)
                {
                    var rend = kuntiRenderers[i];
                    if (rend == null) continue;

                    // Saat menghilang (isGhost == true): Matikan renderer tubuh sepenuhnya
                    // Saat muncul kembali (isGhost == false): Nyalakan kembali
                    rend.enabled = !isGhost;

                    if (!isGhost)
                    {
                        if (originalSharedMaterials != null && i < originalSharedMaterials.Length && originalSharedMaterials[i] != null)
                        {
                            rend.sharedMaterials = originalSharedMaterials[i];
                        }
                    }

                    // Sembunyikan outline hitam saat dalam wujud menghilang
                    var outlineChild = rend.transform.Find("ToonOutline_Backface");
                    if (outlineChild != null)
                    {
                        var outlineRend = outlineChild.GetComponent<Renderer>();
                        if (outlineRend != null)
                        {
                            outlineRend.enabled = !isGhost;
                        }
                    }
                }
            }

            // Sembunyikan nametag melayang di atas kepala saat mode hilang agar tidak membocorkan posisi
            var nameTag = transform.Find("OverheadNameTag");
            if (nameTag != null)
            {
                nameTag.gameObject.SetActive(!isGhost);
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

        public void PlaySpectralAudio(bool isEnteringGhost)
        {
            // Cegah pemutaran ganda jika state yang sama dipanggil beruntun dalam 0.25s (client prediction + SyncVar hook)
            if (Mathf.Abs(Time.time - lastSpectralAudioTime) < 0.25f && lastSpectralAudioState == isEnteringGhost)
            {
                return;
            }
            lastSpectralAudioTime = Time.time;
            lastSpectralAudioState = isEnteringGhost;

            var source = GetVanishAudioSource();
            if (source == null) source = audioSource;
            if (source == null) source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }

            AudioClip clip = isEnteringGhost ? enterGhostClip : exitGhostClip;
            if (clip == null)
            {
                clip = GetSpectralClip(isEnteringGhost);
            }

            if (clip != null && source != null)
            {
                bool isLocal = (netIdentity == null || netIdentity.isLocalPlayer || netIdentity.isOwned);
                source.spatialBlend = isLocal ? 0.15f : 1.0f;
                source.minDistance = 3.0f;
                source.maxDistance = 35.0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.pitch = isEnteringGhost ? 1.0f : 0.95f;
                source.PlayOneShot(clip, 1.0f);
            }
        }

        /// <summary>
        /// Menghasilkan audio desau gaib prosedural kualitas tinggi (haunting ethereal whoosh fallback)
        /// </summary>
        public static AudioClip CreateSpectralSound(bool isEnter)
        {
            int sampleRate = 44100;
            float duration = isEnter ? 1.8f : 1.4f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                if (isEnter)
                {
                    // Haunting sub-bass + rising ethereal whoosh + spectral wind
                    float subEnv = t < 1.0f ? Mathf.Sin(t * Mathf.PI) : 0f;
                    float sub = Mathf.Sin(2f * Mathf.PI * (60f + t * 40f) * t) * 0.4f * subEnv;

                    float freq = Mathf.Lerp(220f, 740f, Mathf.Pow(progress, 0.7f));
                    float whooshEnv = Mathf.Sin(progress * Mathf.PI);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;
                    float toneHarmonic = Mathf.Sin(2f * Mathf.PI * (freq * 1.5f) * t) * 0.2f;

                    float noise = (Random.value * 2f - 1f) * 0.35f;
                    float chimeEnv = t > 0.3f ? Mathf.Exp(-(t - 0.3f) * 2f) : 0f;
                    float chime = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.25f * chimeEnv;

                    samples[i] = Mathf.Clamp((sub + (tone + toneHarmonic + noise) * whooshEnv + chime) * 0.85f, -1f, 1f);
                }
                else
                {
                    // Falling re-materialization whoosh + solidifying snap
                    float freq = Mathf.Lerp(650f, 180f, Mathf.Pow(progress, 1.2f));
                    float whooshEnv = Mathf.Sin(progress * Mathf.PI);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.45f;
                    float noise = (Random.value * 2f - 1f) * 0.3f;

                    float snapEnv = (t > 0.1f && t < 0.45f) ? Mathf.Sin((t - 0.1f) / 0.35f * Mathf.PI) : 0f;
                    float snap = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.5f * snapEnv;

                    samples[i] = Mathf.Clamp(((tone + noise) * whooshEnv + snap) * 0.85f, -1f, 1f);
                }
            }

            AudioClip clip = AudioClip.Create(isEnter ? "Ghost_Vanish_Enter" : "Ghost_Vanish_Exit", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
