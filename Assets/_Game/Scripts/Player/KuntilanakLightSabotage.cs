using UnityEngine;
using Mirror;
using Pucaci.Gameplay;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    /// <summary>
    /// Skill Sabotase Lampu Desa khusus Hantu Kuntilanak:
    /// Saat Kuntilanak menekan tombol [Q], seluruh penerangan tiang lampu jalan di desa
    /// akan mengalami korsleting, berkedip panik, lalu padam total dalam kegelapan pekat.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerRoleManager))]
    public class KuntilanakLightSabotage : NetworkBehaviour
    {
        [Header("Keybinding")]
        [Tooltip("Tombol aktivasi skill sabotase lampu desa")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key sabotageKey = Key.Q;
#endif
        [SerializeField] private KeyCode legacySabotageKey = KeyCode.Q;

        [Header("Audio Cue")]
        [SerializeField] private AudioClip kuntiSabotageScreech;

        private PlayerRoleManager roleManager;
        private AudioSource audioSource;

        private void Awake()
        {
            roleManager = GetComponent<PlayerRoleManager>();
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
                audioSource.maxDistance = 35.0f;
            }

#if UNITY_EDITOR
            if (kuntiSabotageScreech == null)
            {
                kuntiSabotageScreech = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Mirror/Examples/_Common/OpenGameArt/Sounds/wolf_monster.mp3");
            }
#endif
        }

        private void Update()
        {
            // Hanya diproses untuk pemain lokal
            if (!isLocalPlayer) return;

            // Hanya bisa digunakan saat berwujud hantu Kuntilanak
            if (roleManager == null || !roleManager.IsImpostor || !roleManager.IsKuntilanakForm)
            {
                return;
            }

            // Cek apakah tombol ditekan
            if (IsSabotageInputPressed())
            {
                TryExecuteSabotage();
            }
        }

        private bool IsSabotageInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current[sabotageKey].wasPressedThisFrame)
            {
                return true;
            }
#endif
            if (Input.GetKeyDown(legacySabotageKey))
            {
                return true;
            }

            return false;
        }

        public bool CanUseSkill()
        {
            if (roleManager == null || !roleManager.IsImpostor || !roleManager.IsKuntilanakForm)
            {
                return false;
            }

            if (VillageLightManager.Instance == null)
            {
                return false;
            }

            return VillageLightManager.Instance.CanSabotage;
        }

        private void TryExecuteSabotage()
        {
            if (!CanUseSkill()) return;

            // Jalankan command ke server
            CmdTriggerSabotage();
        }

        [Command]
        private void CmdTriggerSabotage()
        {
            if (roleManager == null || !roleManager.IsImpostor || !roleManager.IsKuntilanakForm)
            {
                return;
            }

            if (VillageLightManager.Instance != null && VillageLightManager.Instance.CanSabotage)
            {
                bool success = VillageLightManager.Instance.TriggerSabotage();
                if (success)
                {
                    RpcPlaySabotageAudio();
                }
            }
        }

        [ClientRpc]
        private void RpcPlaySabotageAudio()
        {
            if (audioSource != null && kuntiSabotageScreech != null)
            {
                audioSource.PlayOneShot(kuntiSabotageScreech, 0.95f);
            }
        }
    }
}
