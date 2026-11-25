using UnityEngine;
using Mirror;
using System;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    public enum PlayerRole
    {
        Survivor = 0,
        Impostor = 1
    }

    public enum ImpostorForm
    {
        Human = 0,
        Kuntilanak = 1
    }

    /// <summary>
    /// Mengelola sistem Role Impostor (Hantu Kuntilanak yang menyamar di antara 3 manusia: Laras, Mamat, Vincent).
    /// Hantu dapat meniru wujud manusia lain secara bebas (Laras [1], Mamat [2], Vincent [3]),
    /// serta berubah ke wujud aslinya Kuntilanak [T/4] untuk membunuh pemain.
    /// Saat meniru wujud manusia: dapat membaur dan MEMPERBAIKI sesajen [E].
    /// Saat dalam wujud Kuntilanak: dapat MENCEKIK/MEMBUNUH dan menggunakan GHOST VANISH.
    /// </summary>
    [RequireComponent(typeof(CharacterModelManager))]
    public class PlayerRoleManager : NetworkBehaviour
    {
        [Header("Role Settings")]
        [SyncVar(hook = nameof(OnRoleChanged))]
        [SerializeField] private PlayerRole currentRole = PlayerRole.Impostor;

        [SyncVar(hook = nameof(OnDisguiseChanged))]
        [SerializeField] private int humanDisguiseIndex = 1; // 0 = Laras, 1 = Pak Mamat, 3 = Pak Vincent, 4 = Rocky

        [SyncVar]
        [SerializeField] private int initialDisguiseIndex = -1; // Karakter utama awal hantu saat match dimulai

        [SyncVar(hook = nameof(OnFormChanged))]
        [SerializeField] private ImpostorForm currentForm = ImpostorForm.Human;

        [Header("Transform Settings")]
        [Tooltip("Jeda (cooldown) antara pergantian wujud/peniruan (detik)")]
        [SerializeField] private float transformCooldown = 2.0f;
        private float transformCooldownTimer = 0f;

        public PlayerRole CurrentRole => currentRole;
        public bool IsImpostor => currentRole == PlayerRole.Impostor;
        public ImpostorForm CurrentForm => currentForm;
        public bool IsHumanForm => currentForm == ImpostorForm.Human;
        public bool IsKuntilanakForm => currentForm == ImpostorForm.Kuntilanak;
        public CharacterType HumanDisguise => (CharacterType)humanDisguiseIndex;
        public int InitialDisguiseIndex => (initialDisguiseIndex >= 0) ? initialDisguiseIndex : humanDisguiseIndex;
        public CharacterType InitialHumanCharacter => (CharacterType)InitialDisguiseIndex;
        public float TransformCooldownRemaining => Mathf.Max(0f, transformCooldownTimer);
        public bool CanTransform => IsImpostor && transformCooldownTimer <= 0.01f;

        public event Action<PlayerRole> OnRoleAssignedEvent;
        public event Action<ImpostorForm> OnFormChangedEvent;
        public event Action<CharacterType> OnDisguiseChangedEvent;

        private CharacterModelManager modelManager;
        private KuntilanakChokeMechanic chokeMechanic;
        private AudioSource audioSource;
        private AudioSource transformAudioSource;

        private void Awake()
        {
            modelManager = GetComponent<CharacterModelManager>();
            chokeMechanic = GetComponent<KuntilanakChokeMechanic>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2.0f;
                audioSource.maxDistance = 20.0f;
            }
        }

        private void Start()
        {
            // Jika mode offline / singleplayer: acak 1 wujud awal manusia dari 4 warga (Laras, Pak Mamat, Pak Vincent, Rocky)
            if (!NetworkClient.active && !NetworkServer.active)
            {
                currentRole = PlayerRole.Impostor;
                int[] villagers = { 0, 1, 3, 4 };
                humanDisguiseIndex = villagers[UnityEngine.Random.Range(0, villagers.Length)];
                initialDisguiseIndex = humanDisguiseIndex;
                currentForm = ImpostorForm.Human;
            }

            // Daftarkan ke SesajenManager jika ada
            if (Pucaci.Gameplay.SesajenManager.Instance != null)
            {
                Pucaci.Gameplay.SesajenManager.Instance.RegisterPlayer(this);
            }

            // Daftarkan ke PucaciNetworkManager jika ada
            if (Pucaci.Network.PucaciNetworkManager.singleton != null)
            {
                Pucaci.Network.PucaciNetworkManager.singleton.RegisterPlayerRole(this);
            }

            ApplyCurrentVisuals(false);
        }

        private void OnDestroy()
        {
            if (Pucaci.Network.PucaciNetworkManager.singleton != null)
            {
                Pucaci.Network.PucaciNetworkManager.singleton.UnregisterPlayerRole(this);
            }
        }

        private void Update()
        {
            if (transformCooldownTimer > 0f)
            {
                transformCooldownTimer -= Time.deltaTime;
                if (transformCooldownTimer < 0f) transformCooldownTimer = 0f;
            }

            // Hanya pemain lokal yang membaca input
            bool isLocal = true;
            if (netIdentity != null && (NetworkClient.active || NetworkServer.active))
            {
                try { isLocal = isLocalPlayer; } catch { isLocal = true; }
            }
            if (!isLocal) return;

            HandleMimicAndTransformInput();
            HandleDebugRoleToggle();
        }

        private void HandleMimicAndTransformInput()
        {
            // Deteksi penekanan tombol: [X] Menu Pemilihan Penyamaran, [T / 5] Toggle Wujud Kunti
            bool pressDisguiseMenu = false; // [X]
            bool pressKunti = false;        // [T] atau [5]

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.xKey.wasPressedThisFrame) pressDisguiseMenu = true;
                if (kb.tKey.wasPressedThisFrame || kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) pressKunti = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.X)) pressDisguiseMenu = true;
            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) pressKunti = true;
#endif

            if (!pressDisguiseMenu && !pressKunti) return;

            // Jika sedang bermain sendiri/testing dan belum Impostor, otomatis jadikan Impostor
            if (!IsImpostor)
            {
                var allPlayers = FindObjectsOfType<PlayerRoleManager>();
                if (allPlayers.Length <= 1)
                {
                    SetRole(PlayerRole.Impostor, humanDisguiseIndex);
                }
                else
                {
                    Debug.Log("<color=#FFAA00>[ROLE] Anda adalah Survivor! Tekan F1 untuk beralih role ke Hantu (Impostor).</color>");
                    return;
                }
            }

            if (pressDisguiseMenu)
            {
                // Buka atau tutup menu pemilihan penyamaran karakter [X]
                var ui = Pucaci.UI.DisguiseSelectionUI.EnsureInstance();
                ui.ToggleMenu(this);
                return;
            }

            if (pressKunti)
            {
                if (transformCooldownTimer > 0.05f)
                {
                    Debug.Log($"[ROLE] Peniruan/Transformasi sedang cooldown! ({transformCooldownTimer:F1}s)");
                    return;
                }

                if (chokeMechanic != null && chokeMechanic.IsChoking)
                {
                    Debug.Log("[ROLE] Tidak bisa berganti wujud saat sedang mencekik mangsa!");
                    return;
                }

                ToggleKuntilanakForm();
            }
        }

        /// <summary>
        /// Meniru wujud manusia tertentu (0 = Laras, 1 = Pak Mamat, 3 = Pak Vincent, 4 = Rocky).
        /// </summary>
        public void MimicHuman(int targetHumanIndex)
        {
            if (targetHumanIndex != 0 && targetHumanIndex != 1 && targetHumanIndex != 3 && targetHumanIndex != 4)
            {
                targetHumanIndex = 1; // Default Pak Mamat
            }

            // Jika sudah dalam wujud manusia tersebut, abaikan
            if (currentForm == ImpostorForm.Human && humanDisguiseIndex == targetHumanIndex)
            {
                return;
            }

            transformCooldownTimer = transformCooldown;
            humanDisguiseIndex = targetHumanIndex;
            currentForm = ImpostorForm.Human;

            ApplyCurrentVisuals(true);
            OnDisguiseChangedEvent?.Invoke((CharacterType)humanDisguiseIndex);
            OnFormChangedEvent?.Invoke(currentForm);

            if (NetworkServer.active)
            {
                // SyncVar otomatis sinkron ke clients
            }
            else if (NetworkClient.active)
            {
                CmdRequestMimic(targetHumanIndex);
            }
        }

        /// <summary>
        /// Berganti antara wujud asli Kuntilanak dan wujud manusia.
        /// Saat kembali dari Kuntilanak ke wujud manusia, hantu selalu kembali ke wujud karakter utama dia di awal permainan.
        /// </summary>
        public void ToggleKuntilanakForm()
        {
            if (currentForm == ImpostorForm.Human)
            {
                // Berubah ke Kuntilanak asli
                TransformToKuntilanak();
            }
            else
            {
                // Kembali ke wujud manusia karakter utama dia di awal pertandingan
                MimicHuman(InitialDisguiseIndex);
            }
        }

        /// <summary>
        /// Mengembalikan wujud hantu langsung ke karakter manusia utama dia di awal permainan.
        /// </summary>
        public void RevertToOriginalHuman()
        {
            MimicHuman(InitialDisguiseIndex);
        }

        public void TransformToKuntilanak()
        {
            if (currentForm == ImpostorForm.Kuntilanak) return;

            transformCooldownTimer = transformCooldown;
            currentForm = ImpostorForm.Kuntilanak;

            ApplyCurrentVisuals(true);
            OnFormChangedEvent?.Invoke(currentForm);

            if (NetworkServer.active)
            {
                // SyncVar
            }
            else if (NetworkClient.active)
            {
                CmdRequestKuntilanak();
            }
        }

        [Command]
        private void CmdRequestMimic(int targetHumanIndex)
        {
            humanDisguiseIndex = targetHumanIndex;
            currentForm = ImpostorForm.Human;
            ApplyCurrentVisuals(true);
            OnDisguiseChangedEvent?.Invoke((CharacterType)humanDisguiseIndex);
            OnFormChangedEvent?.Invoke(currentForm);
        }

        [Command]
        private void CmdRequestKuntilanak()
        {
            currentForm = ImpostorForm.Kuntilanak;
            ApplyCurrentVisuals(true);
            OnFormChangedEvent?.Invoke(currentForm);
        }

        /// <summary>
        /// Shortcut F1 untuk beralih role (Survivor <-> Impostor) secara instan saat testing.
        /// Hanya dapat digunakan di pre-match lobby sebelum permainan dimulai.
        /// </summary>
        private void HandleDebugRoleToggle()
        {
            if (Pucaci.Network.PucaciNetworkManager.singleton != null && Pucaci.Network.PucaciNetworkManager.singleton.IsMatchStarted)
            {
                return;
            }

            bool pressedF1 = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) pressedF1 = true;
#else
            if (Input.GetKeyDown(KeyCode.F1)) pressedF1 = true;
#endif

            if (pressedF1)
            {
                PlayerRole newRole = (currentRole == PlayerRole.Survivor) ? PlayerRole.Impostor : PlayerRole.Survivor;
                SetRole(newRole, humanDisguiseIndex);
                Debug.Log($"<color=#FFFF00>[DEBUG F1] Role berganti ke: {newRole} (Menyamar/Model: {(CharacterType)humanDisguiseIndex})</color>");
            }
        }

        public void SetRole(PlayerRole role, int disguiseIndex)
        {
            currentRole = role;
            humanDisguiseIndex = disguiseIndex;
            initialDisguiseIndex = disguiseIndex; // Catat karakter utama dia di awal pertandingan
            currentForm = ImpostorForm.Human; // Impostor selalu mulai dalam wujud manusia (menyamar)

            ApplyCurrentVisuals(false);
            OnRoleAssignedEvent?.Invoke(currentRole);
            OnFormChangedEvent?.Invoke(currentForm);
            OnDisguiseChangedEvent?.Invoke((CharacterType)humanDisguiseIndex);
        }

        private void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
        {
            ApplyCurrentVisuals(false);
            OnRoleAssignedEvent?.Invoke(newRole);
        }

        private void OnDisguiseChanged(int oldIdx, int newIdx)
        {
            ApplyCurrentVisuals(true);
            OnDisguiseChangedEvent?.Invoke((CharacterType)newIdx);
        }

        private void OnFormChanged(ImpostorForm oldForm, ImpostorForm newForm)
        {
            ApplyCurrentVisuals(true);
            OnFormChangedEvent?.Invoke(newForm);
        }

        public void ApplyCurrentVisuals(bool playEffects)
        {
            if (modelManager == null) modelManager = GetComponent<CharacterModelManager>();
            if (modelManager == null) return;

            int targetIndex = humanDisguiseIndex;
            bool isKunti = false;

            if (currentRole == PlayerRole.Survivor)
            {
                targetIndex = humanDisguiseIndex;
                isKunti = false;
            }
            else
            {
                if (currentForm == ImpostorForm.Kuntilanak)
                {
                    targetIndex = 2; // Kuntilanak
                    isKunti = true;
                }
                else
                {
                    targetIndex = humanDisguiseIndex;
                    isKunti = false;
                }
            }

            modelManager.ApplyCharacter(targetIndex);
            if (modelManager.ActiveModel != null)
            {
                var applier = Pucaci.Rendering.ToonOutlineApplier.ApplyTo(modelManager.ActiveModel, 3.8f);
                if (applier != null)
                {
                    applier.ApplyOutline();
                    applier.SetOutlineEnabled(true);
                }
            }
            if (netIdentity != null && netIdentity.isServer)
            {
                modelManager.SetServerCharacter(targetIndex);
            }

            if (TryGetComponent<Pucaci.Network.PlayerNetworkSetup>(out var netSetup))
            {
                string displayName = isKunti ? "<color=#FF0033>KUNTILANAK</color>" : CharacterSelectionData.GetCharacterName((CharacterType)targetIndex);
                netSetup.UpdateNameTagText(displayName);
            }

            if (playEffects)
            {
                Vector3 fxPos = transform.position + Vector3.up * 0.9f;
                if (netIdentity != null && netIdentity.isServer)
                {
                    RpcPlayTransformFX(isKunti, fxPos);
                }
                else
                {
                    SpawnTransformFX(isKunti, fxPos);
                    PlayTransformAudio(isKunti);
                }
            }
        }

        [ClientRpc]
        private void RpcPlayTransformFX(bool toKuntilanak, Vector3 fxPosition)
        {
            SpawnTransformFX(toKuntilanak, fxPosition);
            PlayTransformAudio(toKuntilanak);
        }

        private void SpawnTransformFX(bool toKuntilanak, Vector3 fxPosition)
        {
            var fxObj = new GameObject("TransformSmokeFX");
            fxObj.transform.position = fxPosition;

            var ps = fxObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = 1.4f;
            main.startSpeed = 2.4f;
            main.startSize = 0.75f;

            // Warna merah darah pekat kental ala horor saat berubah ke Kuntilanak (tidak akan ungu/magenta)
            Color c1 = toKuntilanak ? new Color(0.92f, 0.03f, 0.05f, 0.98f) : new Color(0.85f, 0.85f, 0.9f, 0.8f);
            Color c2 = toKuntilanak ? new Color(0.38f, 0.01f, 0.02f, 0.95f) : new Color(0.4f, 0.4f, 0.45f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;

            var renderer = fxObj.GetComponent<ParticleSystemRenderer>();

            // Prioritas 1: Load material darah resmi dari Resources (100% selalu disertakan di build standalone)
            Material bloodMat = Resources.Load<Material>("Materials/M_TransformBloodFX");
            if (bloodMat != null)
            {
                var inst = new Material(bloodMat);
                inst.SetColor("_BaseColor", toKuntilanak ? new Color(0.85f, 0.03f, 0.04f, 0.95f) : new Color(0.8f, 0.8f, 0.85f, 0.8f));
                renderer.material = inst;
            }
            else
            {
                // Prioritas 2: Sprites/Default yang selalu terintegrasi dalam engine Unity
                Shader fallbackShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (fallbackShader != null)
                {
                    var fallbackMat = new Material(fallbackShader);
                    fallbackMat.color = toKuntilanak ? new Color(0.9f, 0.04f, 0.05f, 0.95f) : Color.white;
                    renderer.material = fallbackMat;
                }
            }

            ps.Play();
        }

        private AudioSource GetTransformAudioSource()
        {
            if (transformAudioSource != null) return transformAudioSource;

            Transform child = transform.Find("TransformAudioSource");
            if (child != null)
            {
                transformAudioSource = child.GetComponent<AudioSource>();
            }
            if (transformAudioSource == null)
            {
                GameObject go = new GameObject("TransformAudioSource");
                go.transform.SetParent(transform, false);
                transformAudioSource = go.AddComponent<AudioSource>();
                transformAudioSource.playOnAwake = false;
            }

            return transformAudioSource;
        }

        public void PlayTransformAudio(bool toKuntilanak)
        {
            var source = GetTransformAudioSource();
            if (source == null) source = audioSource;
            if (source == null) source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }

            AudioClip clip = GetTransformClip(toKuntilanak);

            if (clip != null && source != null)
            {
                bool isLocal = (netIdentity == null || netIdentity.isLocalPlayer || netIdentity.isOwned);
                source.spatialBlend = isLocal ? 0.2f : 1.0f;
                source.minDistance = 3.0f;
                source.maxDistance = 30.0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.PlayOneShot(clip, 1.0f);
            }
        }

        public static AudioClip GetTransformClip(bool toKuntilanak)
        {
            AudioClip clip = null;
            if (toKuntilanak)
            {
                clip = Resources.Load<AudioClip>("Audio/Transform_Kuntilanak");
                if (clip == null) clip = Resources.Load<AudioClip>("Audio/Screech_Monster");
                if (clip == null) clip = CreateKuntiTransformSound();
            }
            else
            {
                clip = Resources.Load<AudioClip>("Audio/Transform_Human");
                if (clip == null) clip = Resources.Load<AudioClip>("Audio/Death_Gasp");
                if (clip == null) clip = CreateHumanTransformSound();
            }
            return clip;
        }

        public static AudioClip CreateKuntiTransformSound()
        {
            int sampleRate = 44100;
            float duration = 2.2f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float subEnv = t < 1.0f ? Mathf.Exp(-t * 3.5f) : 0f;
                float sub = Mathf.Sin(2f * Mathf.PI * (70f - t * 25f) * t) * 0.45f * subEnv;

                float screechEnv = 0f;
                if (t > 0.03f)
                {
                    float ts = t - 0.03f;
                    screechEnv = Mathf.Sin(Mathf.Min(Mathf.PI, ts * 2.0f)) * Mathf.Exp(-ts * 1.5f);
                }
                float vibrato = Mathf.Sin(2f * Mathf.PI * 9.0f * t) * 35f;
                float freq1 = 880f - 280f * (t / duration) + vibrato;
                float wail1 = Mathf.Sin(2f * Mathf.PI * freq1 * t);
                float wail2 = Mathf.Sin(2f * Mathf.PI * (freq1 * 1.5f) * t) * 0.5f;
                float wail3 = Mathf.Sin(2f * Mathf.PI * (freq1 * 1.06f) * t) * 0.4f;
                float screech = (wail1 + wail2 + wail3) * 0.35f * screechEnv;

                float noise = UnityEngine.Random.Range(-1f, 1f);
                float smokeEnv = (1f - Mathf.Cos(Mathf.Min(Mathf.PI * 2f, t * 4f))) * Mathf.Exp(-t * 2f);
                float smoke = noise * 0.25f * smokeEnv;

                float chimeEnv = t > 0.2f ? Mathf.Exp(-(t - 0.2f) * 2.5f) : 0f;
                float chime = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 554.37f * t)) * 0.25f * chimeEnv;

                samples[i] = Mathf.Clamp(sub + screech + smoke + chime, -1f, 1f);
            }

            var clip = AudioClip.Create("Transform_Kuntilanak", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateHumanTransformSound()
        {
            int sampleRate = 44100;
            float duration = 1.6f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float whooshEnv = Mathf.Sin(Mathf.Min(Mathf.PI, t * (Mathf.PI / duration))) * Mathf.Exp(-t * 1.2f);
                float whooshFreq = 120f + 350f * Mathf.Sin(Mathf.Min(Mathf.PI, t * 2.5f));
                float whooshTonal = Mathf.Sin(2f * Mathf.PI * whooshFreq * t) * 0.35f;

                float noise = UnityEngine.Random.Range(-1f, 1f);
                float whoosh = (whooshTonal + noise * 0.28f) * whooshEnv;

                float settleEnv = t > 0.4f ? Mathf.Exp(-(t - 0.4f) * 3f) : 0f;
                float settle = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.25f * settleEnv;

                samples[i] = Mathf.Clamp(whoosh + settle, -1f, 1f);
            }

            var clip = AudioClip.Create("Transform_Human", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
