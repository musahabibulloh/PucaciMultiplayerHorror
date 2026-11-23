using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Pucaci.Props;

namespace Pucaci.Gameplay
{
    /// <summary>
    /// Pengelola global jaringan listrik dan penerangan lampu jalan seluruh Desa Pucaci.
    /// Mengelola sinkronisasi status padam/menyala (Sabotase Kuntilanak) via Mirror Networking.
    /// </summary>
    public class VillageLightManager : NetworkBehaviour
    {
        public static VillageLightManager Instance { get; private set; }

        [Header("Sabotage Configuration")]
        [Tooltip("Durasi lampu padam saat disabotase Kuntilanak (detik)")]
        [SerializeField] private float blackoutDuration = 15.0f;

        [Tooltip("Durasi jeda / cooldown setelah lampu kembali menyala sebelum bisa disabotase lagi (detik)")]
        [SerializeField] private float sabotageCooldown = 40.0f;

        [Header("Audio Effects")]
        [SerializeField] private AudioClip sabotageSound;
        [SerializeField] private AudioClip powerRestoredSound;

        [Header("Network State")]
        [SyncVar(hook = nameof(OnSabotageStateChanged))]
        private bool isSabotaged = false;

        [SyncVar]
        private float remainingBlackoutTimer = 0.0f;

        [SyncVar]
        private float cooldownTimer = 0.0f;

        public bool IsSabotaged => isSabotaged;
        public float BlackoutDuration => blackoutDuration;
        public float RemainingBlackout => Mathf.Max(0f, remainingBlackoutTimer);
        public float BlackoutNormalized => blackoutDuration > 0f ? Mathf.Clamp01(remainingBlackoutTimer / blackoutDuration) : 0f;

        public float SabotageCooldown => sabotageCooldown;
        public float RemainingCooldown => Mathf.Max(0f, cooldownTimer);
        public bool IsOnCooldown => cooldownTimer > 0.05f;
        public float CooldownNormalized => sabotageCooldown > 0f ? Mathf.Clamp01(cooldownTimer / sabotageCooldown) : 0f;

        public bool CanSabotage => !isSabotaged && cooldownTimer <= 0.05f;

        private readonly List<StreetLamp> registeredLamps = new List<StreetLamp>();
        private AudioSource audioSource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SetupAudio();
        }

        private void Start()
        {
            CollectAllLamps();
        }

        private void SetupAudio()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D global sound
                audioSource.volume = 0.85f;
            }

#if UNITY_EDITOR
            if (sabotageSound == null)
            {
                sabotageSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Mirror/Examples/_Common/OpenGameArt/Sounds/hjm-tesla_sound_shot.mp3");
            }
            if (powerRestoredSound == null)
            {
                powerRestoredSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Mirror/Examples/_Common/OpenGameArt/Sounds/Light Switch Click On Sfx.mp3");
            }
#endif
        }

        /// <summary>
        /// Mengumpulkan semua tiang lampu di desa untuk dikontrol secara terpusat.
        /// </summary>
        public void CollectAllLamps()
        {
            registeredLamps.Clear();

            // Prioritas: cari di bawah transform StreetLamps
            var streetLampsParent = GameObject.Find("StreetLamps");
            if (streetLampsParent != null)
            {
                var lamps = streetLampsParent.GetComponentsInChildren<StreetLamp>(true);
                foreach (var l in lamps)
                {
                    if (!registeredLamps.Contains(l)) registeredLamps.Add(l);
                }
            }

            // Fallback: cari seluruh StreetLamp di scene
            var allLamps = FindObjectsOfType<StreetLamp>(true);
            foreach (var l in allLamps)
            {
                if (!registeredLamps.Contains(l)) registeredLamps.Add(l);
            }

            // Jika lampu belum memiliki komponen StreetLamp, pasang otomatis
            if (streetLampsParent != null && registeredLamps.Count == 0)
            {
                for (int i = 0; i < streetLampsParent.transform.childCount; i++)
                {
                    var child = streetLampsParent.transform.GetChild(i);
                    var sl = child.GetComponent<StreetLamp>();
                    if (sl == null) sl = child.gameObject.AddComponent<StreetLamp>();
                    if (!registeredLamps.Contains(sl)) registeredLamps.Add(sl);
                }
            }
        }

        private void Update()
        {
            if (!isServer) return;

            if (isSabotaged)
            {
                remainingBlackoutTimer -= Time.deltaTime;
                if (remainingBlackoutTimer <= 0f)
                {
                    EndSabotage();
                }
            }
            else if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer < 0f)
                {
                    cooldownTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Memicu aksi sabotase listrik desa (hanya dapat dipanggil di Server).
        /// </summary>
        [Server]
        public bool TriggerSabotage()
        {
            if (!CanSabotage) return false;

            isSabotaged = true;
            remainingBlackoutTimer = blackoutDuration;
            cooldownTimer = 0f;

            if (audioSource != null && sabotageSound != null)
            {
                audioSource.PlayOneShot(sabotageSound, 0.9f);
            }

            ApplyAllLampsState(false, true);
            RpcOnSabotageStarted();
            return true;
        }

        private void EndSabotage()
        {
            isSabotaged = false;
            remainingBlackoutTimer = 0f;
            cooldownTimer = sabotageCooldown;

            if (audioSource != null && powerRestoredSound != null)
            {
                audioSource.PlayOneShot(powerRestoredSound, 0.85f);
            }

            ApplyAllLampsState(true, true);
            RpcOnSabotageEnded();
        }

        [ClientRpc]
        private void RpcOnSabotageStarted()
        {
            if (!isServer && audioSource != null && sabotageSound != null)
            {
                audioSource.PlayOneShot(sabotageSound, 0.9f);
            }

            if (!isServer)
            {
                ApplyAllLampsState(false, true);
            }
        }

        [ClientRpc]
        private void RpcOnSabotageEnded()
        {
            if (!isServer && audioSource != null && powerRestoredSound != null)
            {
                audioSource.PlayOneShot(powerRestoredSound, 0.85f);
            }

            if (!isServer)
            {
                ApplyAllLampsState(true, true);
            }
        }

        private void OnSabotageStateChanged(bool oldState, bool newState)
        {
            ApplyAllLampsState(!newState, true);
        }

        private void ApplyAllLampsState(bool lightsOn, bool withFlicker)
        {
            if (registeredLamps.Count == 0)
            {
                CollectAllLamps();
            }

            for (int i = 0; i < registeredLamps.Count; i++)
            {
                if (registeredLamps[i] != null)
                {
                    registeredLamps[i].SetLightState(lightsOn, withFlicker);
                }
            }
        }
    }
}
