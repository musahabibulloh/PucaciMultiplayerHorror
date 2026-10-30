using UnityEngine;
using UnityEngine.Audio;

namespace Pucaci.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterController))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [Header("Audio Sources")]
        [Tooltip("AudioSource untuk memutar suara langkah kaki dan pendaratan")]
        [SerializeField] private AudioSource footstepSource;

        [Tooltip("AudioSource untuk memutar efek suara sliding / meluncur")]
        [SerializeField] private AudioSource slideSource;

        [Tooltip("AudioMixerGroup output untuk SFX")]
        [SerializeField] private AudioMixerGroup sfxMixerGroup;

        [Header("Foot Bones (100% Sinkronisasi Animasi)")]
        [Tooltip("Transform tulang kaki kiri untuk membaca kontak tanah frame-by-frame")]
        [SerializeField] private Transform leftFootBone;

        [Tooltip("Transform tulang kaki kanan untuk membaca kontak tanah frame-by-frame")]
        [SerializeField] private Transform rightFootBone;

        [Tooltip("Batas ketinggian lokal kaki saat terangkat di udara (meter)")]
        [SerializeField] private float footLiftThreshold = 0.26f;

        [Tooltip("Batas ketinggian lokal saat telapak kaki menapak di tanah (meter)")]
        [SerializeField] private float footStrikeThreshold = 0.16f;

        [Header("Footstep Audio Clips")]
        [Tooltip("Daftar audio clip langkah kaki jalan biasa (rekaman foley tanah alami)")]
        [SerializeField] private AudioClip[] walkFootsteps;

        [Tooltip("Daftar audio clip langkah kaki lari / sprint (hentakan lebih bertenaga)")]
        [SerializeField] private AudioClip[] runFootsteps;

        [Tooltip("Audio clip pendaratan saat mendarat di tanah")]
        [SerializeField] private AudioClip landSound;

        [Tooltip("Audio clip gesekan saat meluncur / sliding di tanah")]
        [SerializeField] private AudioClip slideSound;

        [Header("Volume & Pitch")]
        [Range(0f, 1f)]
        [SerializeField] private float walkVolume = 0.65f;

        [Range(0f, 1f)]
        [SerializeField] private float runVolume = 0.88f;

        [Range(0f, 1f)]
        [SerializeField] private float landVolume = 0.95f;

        [Range(0f, 1f)]
        [SerializeField] private float slideVolume = 0.75f;

        [SerializeField] private float minPitch = 0.95f;
        [SerializeField] private float maxPitch = 1.05f;

        // Komponen dan status internal
        private PlayerCharacterController pcc;
        private Vector3 lastPosition;
        private bool wasGrounded;
        private int lastClipIndex = -1;

        // Status pergerakan kaki frame-by-frame
        private bool leftFootLifted;
        private bool rightFootLifted;
        private float leftFootTimer;
        private float rightFootTimer;

        // Fallback akumulasi jarak (jika tulang tidak ditemukan)
        private float accumulatedDistance;
        private float stepTimer;

        public int TotalStepsPlayed { get; private set; }
        public string LastPlayedClipName { get; private set; }

        private void Awake()
        {
            pcc = GetComponent<PlayerCharacterController>();

            // Auto-resolve tulang kaki jika belum di-assign di Inspector
            if (leftFootBone == null)
            {
                leftFootBone = transform.Find("Model/mixamorig:Hips/mixamorig:LeftUpLeg/mixamorig:LeftLeg/mixamorig:LeftFoot");
            }
            if (rightFootBone == null)
            {
                rightFootBone = transform.Find("Model/mixamorig:Hips/mixamorig:RightUpLeg/mixamorig:RightLeg/mixamorig:RightFoot");
            }

            // Setup AudioSource untuk langkah kaki jika belum ada
            if (footstepSource == null)
            {
                var existingSources = GetComponents<AudioSource>();
                foreach (var src in existingSources)
                {
                    if (src.gameObject.name.Contains("Footstep") || src.clip == null)
                    {
                        footstepSource = src;
                        break;
                    }
                }

                if (footstepSource == null)
                {
                    footstepSource = gameObject.AddComponent<AudioSource>();
                }
            }

            ConfigureAudioSource(footstepSource, "FootstepSource");

            // Setup AudioSource kedua untuk sliding (looping whoosh/scrape)
            if (slideSource == null)
            {
                slideSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource(slideSource, "SlideSource");
            slideSource.loop = true;

            // Auto-resolve sfx mixer group jika belum diatur
            if (sfxMixerGroup == null)
            {
                ResolveMixerGroup();
            }

            if (sfxMixerGroup != null)
            {
                footstepSource.outputAudioMixerGroup = sfxMixerGroup;
                slideSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            lastPosition = transform.position;
            wasGrounded = pcc != null ? pcc.IsGrounded : true;
        }

        private void ConfigureAudioSource(AudioSource src, string debugName)
        {
            if (src == null) return;
            src.playOnAwake = false;
            src.spatialBlend = 1.0f; // 3D Positional Audio untuk multiplayer
            src.dopplerLevel = 0.2f;
            src.minDistance = 1.5f;
            src.maxDistance = 25f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        private void ResolveMixerGroup()
        {
#if UNITY_EDITOR
            var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Game/Audio/PucaciMixer.mixer");
            if (mixer != null)
            {
                var groups = mixer.FindMatchingGroups("Sfx");
                if (groups != null && groups.Length > 0)
                {
                    sfxMixerGroup = groups[0];
                }
            }
#endif
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - lastPosition;
            delta.y = 0f;
            float horizontalDist = delta.magnitude;
            lastPosition = currentPos;

            leftFootTimer += Time.deltaTime;
            rightFootTimer += Time.deltaTime;

            bool isLocalActive = (pcc != null && pcc.enabled);
            bool isGrounded = isLocalActive ? pcc.IsGrounded : true;
            bool isSliding = isLocalActive && pcc.IsSliding;
            float calcSpeed = Time.deltaTime > 0.0001f ? (horizontalDist / Time.deltaTime) : 0f;
            float speed = isLocalActive ? Mathf.Max(pcc.CurrentHorizontalSpeed, calcSpeed) : calcSpeed;
            bool isSprinting = isLocalActive ? pcc.IsSprinting : (speed > 4.5f);

            // 1. Deteksi mendarat setelah melompat/jatuh (Land Impact Sound)
            if (!wasGrounded && isGrounded)
            {
                if (pcc == null || pcc.VerticalVelocity < -3.0f)
                {
                    PlayLandSound();
                }
            }
            wasGrounded = isGrounded;

            // 2. Deteksi sliding / meluncur
            if (isSliding && isGrounded)
            {
                if (!slideSource.isPlaying && slideSound != null)
                {
                    slideSource.clip = slideSound;
                    slideSource.volume = slideVolume;
                    slideSource.pitch = 1.0f;
                    slideSource.Play();
                }
                leftFootLifted = false;
                rightFootLifted = false;
                accumulatedDistance = 0f;
                stepTimer = 0f;
                return;
            }
            else
            {
                if (slideSource.isPlaying)
                {
                    slideSource.Stop();
                }
            }

            // 3. Deteksi langkah kaki (Walking & Running Footsteps)
            if (!isGrounded || speed < 0.35f)
            {
                // Karakter diam atau di udara: reset status tumpuan kaki
                leftFootLifted = false;
                rightFootLifted = false;
                accumulatedDistance = 0f;
                stepTimer = 0f;
                return;
            }

            // METODE UTAMA: Sinkronisasi Fisik 100% Mengikuti Tulang Kaki Animasi
            if (leftFootBone != null && rightFootBone != null)
            {
                // Hitung posisi lokal Y kaki terhadap root pemain (akurat di tanjakan/turunan)
                float leftLocalY = transform.InverseTransformPoint(leftFootBone.position).y;
                float rightLocalY = transform.InverseTransformPoint(rightFootBone.position).y;

                // --- KAKI KIRI ---
                if (leftLocalY > footLiftThreshold)
                {
                    leftFootLifted = true;
                }
                else if (leftFootLifted && leftLocalY <= footStrikeThreshold && leftFootTimer >= 0.14f)
                {
                    PlayFootstep(isSprinting);
                    leftFootLifted = false;
                    leftFootTimer = 0f;
                }

                // --- KAKI KANAN ---
                if (rightLocalY > footLiftThreshold)
                {
                    rightFootLifted = true;
                }
                else if (rightFootLifted && rightLocalY <= footStrikeThreshold && rightFootTimer >= 0.14f)
                {
                    PlayFootstep(isSprinting);
                    rightFootLifted = false;
                    rightFootTimer = 0f;
                }
            }
            else
            {
                // METODE FALLBACK: Akumulasi jarak tempuh jika tulang tidak terpasang
                accumulatedDistance += horizontalDist;
                stepTimer += Time.deltaTime;

                float targetDistance = isSprinting ? 1.45f : 1.15f;
                float maxStepTime = isSprinting ? 0.30f : 0.52f;

                if (accumulatedDistance >= targetDistance || (speed > 1.2f && stepTimer >= maxStepTime))
                {
                    PlayFootstep(isSprinting);
                    accumulatedDistance = 0f;
                    stepTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Memutar audio langkah kaki dengan variasi klip dan pitch alami
        /// </summary>
        public void PlayFootstep(bool isSprint)
        {
            AudioClip[] clips = isSprint ? runFootsteps : walkFootsteps;
            if (clips == null || clips.Length == 0)
            {
                clips = isSprint ? walkFootsteps : runFootsteps;
            }

            if (clips == null || clips.Length == 0 || footstepSource == null) return;

            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastClipIndex)
            {
                index = (index + 1) % clips.Length;
            }
            lastClipIndex = index;

            AudioClip selectedClip = clips[index];
            if (selectedClip == null) return;

            TotalStepsPlayed++;
            LastPlayedClipName = selectedClip.name;

            float volume = (isSprint ? runVolume : walkVolume) * Random.Range(0.94f, 1.06f);
            footstepSource.pitch = Random.Range(minPitch, maxPitch);
            footstepSource.PlayOneShot(selectedClip, volume);
        }

        /// <summary>
        /// Memutar audio pendaratan setelah jatuh / melompat
        /// </summary>
        public void PlayLandSound()
        {
            if (landSound != null && footstepSource != null)
            {
                footstepSource.pitch = Random.Range(0.96f, 1.04f);
                footstepSource.PlayOneShot(landSound, landVolume);
            }
        }

        public void AssignReferences(Transform lFoot, Transform rFoot, AudioClip[] walks, AudioClip[] runs, AudioClip land, AudioClip slide, AudioMixerGroup sfxGroup)
        {
            leftFootBone = lFoot;
            rightFootBone = rFoot;
            walkFootsteps = walks;
            runFootsteps = runs;
            landSound = land;
            slideSound = slide;
            sfxMixerGroup = sfxGroup;
            if (footstepSource != null && sfxMixerGroup != null)
            {
                footstepSource.outputAudioMixerGroup = sfxMixerGroup;
            }
            if (slideSource != null && sfxMixerGroup != null)
            {
                slideSource.outputAudioMixerGroup = sfxMixerGroup;
            }
        }
    }
}
