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
        [SerializeField] private int humanDisguiseIndex = 1; // 0 = Laras, 1 = Pak Mamat, 3 = Pak Vincent

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
        public float TransformCooldownRemaining => Mathf.Max(0f, transformCooldownTimer);
        public bool CanTransform => IsImpostor && transformCooldownTimer <= 0.01f;

        public event Action<PlayerRole> OnRoleAssignedEvent;
        public event Action<ImpostorForm> OnFormChangedEvent;
        public event Action<CharacterType> OnDisguiseChangedEvent;

        private CharacterModelManager modelManager;
        private KuntilanakChokeMechanic chokeMechanic;
        private AudioSource audioSource;

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
            // Jika mode offline / singleplayer: acak 1 wujud awal manusia dari 3 warga (Laras, Pak Mamat, Pak Vincent)
            if (!NetworkClient.active && !NetworkServer.active)
            {
                currentRole = PlayerRole.Impostor;
                int[] villagers = { 0, 1, 3 };
                humanDisguiseIndex = villagers[UnityEngine.Random.Range(0, villagers.Length)];
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
            // Deteksi penekanan tombol
            bool pressLaras = false;   // [1]
            bool pressMamat = false;   // [2]
            bool pressVincent = false; // [3]
            bool pressKunti = false;   // [T] atau [4]

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) pressLaras = true;
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) pressMamat = true;
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) pressVincent = true;
                if (kb.tKey.wasPressedThisFrame || kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) pressKunti = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) pressLaras = true;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) pressMamat = true;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) pressVincent = true;
            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) pressKunti = true;
#endif

            if (!pressLaras && !pressMamat && !pressVincent && !pressKunti) return;

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

            // Eksekusi aksi peniruan wujud
            if (pressLaras)
            {
                MimicHuman(0); // Laras
            }
            else if (pressMamat)
            {
                MimicHuman(1); // Pak Mamat
            }
            else if (pressVincent)
            {
                MimicHuman(3); // Pak Vincent
            }
            else if (pressKunti)
            {
                ToggleKuntilanakForm();
            }
        }

        /// <summary>
        /// Meniru wujud manusia tertentu (0 = Laras, 1 = Pak Mamat, 3 = Pak Vincent).
        /// </summary>
        public void MimicHuman(int targetHumanIndex)
        {
            if (targetHumanIndex != 0 && targetHumanIndex != 1 && targetHumanIndex != 3)
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
        /// Berganti antara wujud asli Kuntilanak dan wujud manusia yang sedang ditiru.
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
                // Kembali ke wujud manusia yang terakhir ditiru
                MimicHuman(humanDisguiseIndex);
            }
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
        /// </summary>
        private void HandleDebugRoleToggle()
        {
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
            if (isServer || NetworkServer.active)
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
                if (isServer || NetworkServer.active)
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

        private void PlayTransformAudio(bool toKuntilanak)
        {
            if (audioSource != null)
            {
                AudioClip clip = null;
                if (toKuntilanak)
                {
                    clip = Resources.Load<AudioClip>("Audio/Screech_Monster");
                    if (clip == null) clip = CreateKuntiTransformSound();
                }
                else
                {
                    clip = Resources.Load<AudioClip>("Audio/Death_Gasp");
                    if (clip == null) clip = CreateHumanTransformSound();
                }

                if (clip != null)
                {
                    audioSource.PlayOneShot(clip, 0.95f);
                }
            }
        }

        private static AudioClip CreateKuntiTransformSound()
        {
            int sampleRate = 22050;
            float duration = 1.8f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 2.0f);
                float pitch = Mathf.Lerp(450f, 160f, t / duration);
                float hiss = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                float howl = Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.5f;
                samples[i] = (howl + hiss) * env;
            }

            var clip = AudioClip.Create("Transform_Kuntilanak", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateHumanTransformSound()
        {
            int sampleRate = 22050;
            float duration = 1.2f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 3.0f);
                float breath = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.3f;
                float puff = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                samples[i] = (breath + puff) * env;
            }

            var clip = AudioClip.Create("Transform_Human", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
