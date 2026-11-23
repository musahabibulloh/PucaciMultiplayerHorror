using System;
using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    /// <summary>
    /// Mekanik menyekik (Choke / Strangle) khusus Kuntilanak dengan sinkronisasi multiplayer penuh (Mirror).
    /// Kuntilanak dapat mendekati pemain lain (manusia) dan menyekiknya dengan menahan [E] atau [Klik Kiri].
    /// Jika dicekik selama 5 detik penuh tanpa terputus, korban akan langsung mati.
    /// Seluruh pemain (Kuntilanak, korban, maupun penonton/spectator) dapat melihat animasi mencekik secara real-time.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacterController))]
    public class KuntilanakChokeMechanic : NetworkBehaviour
    {
        [Header("Choke Settings")]
        [Tooltip("Durasi menyekik hingga korban mati (5 detik)")]
        [SerializeField] private float maxChokeDuration = 5.0f;

        [Tooltip("Durasi jeda (cooldown) setelah selesai mencekik sebelum bisa menyekik lagi (25 detik)")]
        [SerializeField] private float chokeCooldown = 25.0f;

        [Tooltip("Jarak maksimal untuk memulai cekikan (meter)")]
        [SerializeField] private float chokeRange = 2.4f;

        [Tooltip("Jarak maksimal sebelum cekikan terlepas/putus (meter)")]
        [SerializeField] private float breakDistance = 3.5f;

        [Header("Choke Lift Settings")]
        [Tooltip("Ketinggian angkat tubuh korban ke atas saat dicekik (meter)")]
        [SerializeField] private float victimLiftHeight = 0.38f;

        [Tooltip("Durasi waktu untuk mengangkat korban ke ketinggian penuh (detik)")]
        [SerializeField] private float liftDuration = 0.5f;

        [Header("Runtime State")]
        [SerializeField] private bool isChoking = false;
        [SerializeField] private bool isBeingChoked = false;

        [Header("Multiplayer Sync")]
        [SyncVar(hook = nameof(OnSyncChokeChanged))]
        private bool syncIsChoking = false;

        [SyncVar(hook = nameof(OnSyncVictimChanged))]
        private uint syncVictimNetId = 0;

        // Properti publik
        public bool IsChoking => isChoking;
        public bool IsBeingChoked => isBeingChoked;
        public float MaxChokeDuration => maxChokeDuration;
        public float ChokeCooldown => chokeCooldown;
        public float RemainingCooldown => Mathf.Max(0f, chokeCooldownTimer);
        public bool IsOnCooldown => chokeCooldownTimer > 0.01f;
        public float CooldownNormalized => chokeCooldown > 0f ? Mathf.Clamp01(chokeCooldownTimer / chokeCooldown) : 0f;

        public float ChokeTimer => (isBeingChoked && activeAttacker != null)
            ? (activeAttacker.GetComponent<KuntilanakChokeMechanic>()?.ChokeTimer ?? chokeTimer)
            : chokeTimer;
        public float ChokeProgress => maxChokeDuration > 0f ? Mathf.Clamp01(ChokeTimer / maxChokeDuration) : 0f;
        public float RemainingChokeTime => Mathf.Max(0f, maxChokeDuration - ChokeTimer);

        public PlayerCharacterController CurrentAimTarget => aimTarget;

        private float chokeTimer = 0.0f;
        private float chokeCooldownTimer = 0.0f;
        private PlayerCharacterController pcc;
        private PlayerCharacterController PCC => pcc != null ? pcc : (pcc = GetComponent<PlayerCharacterController>());
        private CharacterModelManager modelManager;
        private CharacterModelManager ModelMgr => modelManager != null ? modelManager : (modelManager = GetComponent<CharacterModelManager>());
        private KuntilanakGhostVanish ghostVanish;
        private KuntilanakGhostVanish GhostVanish => ghostVanish != null ? ghostVanish : (ghostVanish = GetComponent<KuntilanakGhostVanish>());
        private AudioSource audioSource;
        private AudioClip chokeLoopClip;
        private AudioClip chokeSnapClip;

        private PlayerCharacterController aimTarget;
        private PlayerCharacterController activeVictim;
        private PlayerCharacterController activeAttacker;

        // Tulang lengan Kuntilanak untuk animasi prosedural menyekik leher
        private Transform b6L, b7L, b8L; // Lengan kiri: shoulder, elbow, hand
        private Transform b6R, b7R, b8R; // Lengan kanan: shoulder, elbow, hand
        private float armChokeBlend = 0f;
        private bool bonesCached = false;

        private void Awake()
        {
            pcc = GetComponent<PlayerCharacterController>();
            modelManager = GetComponent<CharacterModelManager>();
            ghostVanish = GetComponent<KuntilanakGhostVanish>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 1.5f;
                audioSource.maxDistance = 25f;
            }

            chokeLoopClip = CreateChokeLoopSound();
            chokeSnapClip = CreateNeckSnapSound();

            CacheArmBones();
        }

        private void OnDisable()
        {
            if (isChoking)
            {
                StopChokingInternal("disabled");
            }
        }

        private void CacheArmBones()
        {
            Transform kuntiModel = transform.Find("Model_Kuntilanak");
            if (kuntiModel == null)
            {
                kuntiModel = transform;
            }

            foreach (var t in kuntiModel.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Bone.006.L": b6L = t; break;
                    case "Bone.007.L": b7L = t; break;
                    case "Bone.008.L": b8L = t; break;
                    case "Bone.006.R": b6R = t; break;
                    case "Bone.007.R": b7R = t; break;
                    case "Bone.008.R": b8R = t; break;
                }
            }

            bonesCached = (b6L != null && b7L != null && b8L != null && b6R != null && b7R != null && b8R != null);
        }

        private void Update()
        {
            // 1. Jika sedang dicekik oleh Kuntilanak: kunci pergerakan
            if (isBeingChoked)
            {
                if (PCC != null && !PCC.IsImmobilized)
                {
                    PCC.SetImmobilized(true);
                }

                // Cek apakah attacker masih ada & valid
                if (activeAttacker != null)
                {
                    var attackerChoke = activeAttacker.GetComponent<KuntilanakChokeMechanic>();
                    if (attackerChoke != null)
                    {
                        chokeTimer = attackerChoke.ChokeTimer;
                    }

                    // Menghadap ke arah Kuntilanak yang mencekik
                    Vector3 lookDir = (activeAttacker.transform.position - transform.position);
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
                    }
                }
                return;
            }

            // 2. Logika hanya berjalan jika karakter aktif adalah Kuntilanak
            bool isKunti = (ModelMgr != null) && (ModelMgr.ActiveCharacterType == CharacterType.Kuntilanak);
            if (!isKunti)
            {
                if (isChoking) StopChokingInternal("not Kuntilanak anymore");
                return;
            }

            // Kuntilanak yang sedang dalam wujud gaib (ghost form) tidak bisa mencekik fisik
            bool isGhost = (GhostVanish != null) && GhostVanish.IsGhostForm;
            if (isGhost)
            {
                if (isChoking) StopChokingInternal("Kuntilanak entered ghost form");
                return;
            }

            // Hitung mundur cooldown jika tidak sedang aktif mencekik
            if (!isChoking && chokeCooldownTimer > 0f)
            {
                chokeCooldownTimer -= Time.deltaTime;
                if (chokeCooldownTimer < 0f)
                {
                    chokeCooldownTimer = 0f;
                }
            }

            // Hanya pemain lokal pemilik karakter Kuntilanak yang membaca input dan mengendalikan target
            bool isLocal = false;
            try { isLocal = isLocalPlayer; } catch { isLocal = true; }
            if (!isLocal) return;

            // Jika sedang aktif mencekik mangsa:
            if (isChoking)
            {
                UpdateChokingProcess();
            }
            else
            {
                // Scan mangsa terdekat di depan Kuntilanak
                ScanPotentialVictim();

                // Deteksi input tombol Cekik ([E] atau [Klik Kiri])
                bool chokePressed = IsChokeInputDown();
                if (chokePressed && aimTarget != null)
                {
                    if (!IsOnCooldown)
                    {
                        StartChokingTarget(aimTarget);
                    }
                }
            }
        }

        private void ScanPotentialVictim()
        {
            aimTarget = null;
            Vector3 origin = transform.position + Vector3.up * 1.2f;

            Collider[] hits = Physics.OverlapSphere(origin, chokeRange);
            float closestDist = float.MaxValue;

            foreach (var col in hits)
            {
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                var targetPcc = col.GetComponentInParent<PlayerCharacterController>();
                if (targetPcc == null || targetPcc == pcc) continue;

                var targetHealth = targetPcc.GetComponent<PlayerHealth>();
                if (targetHealth != null && targetHealth.IsDead) continue;

                var targetModel = targetPcc.GetComponent<CharacterModelManager>();
                if (targetModel != null && targetModel.ActiveCharacterType == CharacterType.Kuntilanak) continue;

                var targetGhost = targetPcc.GetComponent<KuntilanakGhostVanish>();
                if (targetGhost != null && targetGhost.IsGhostForm) continue;

                float dist = Vector3.Distance(transform.position, targetPcc.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    aimTarget = targetPcc;
                }
            }
        }

        private bool IsChokeInputDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
#else
            if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)) return true;
#endif
            return false;
        }

        private bool IsCancelInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#else
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        private void StartChokingTarget(PlayerCharacterController victim)
        {
            if (victim == null || IsOnCooldown) return;

            activeVictim = victim;
            chokeTimer = 0.0f;
            isChoking = true;

            if (!bonesCached)
            {
                CacheArmBones();
            }

            if (PCC != null)
            {
                PCC.SetImmobilized(true);
            }

            PlayChokeAudio(true);

            uint victimNetId = 0;
            if (victim.TryGetComponent<NetworkIdentity>(out var vNet))
            {
                victimNetId = vNet.netId;
            }

            if (NetworkClient.active || NetworkServer.active)
            {
                CmdStartChoke(victimNetId);
            }
            else
            {
                // Mode offline
                var victimChoke = victim.GetComponent<KuntilanakChokeMechanic>();
                if (victimChoke != null)
                {
                    victimChoke.SetBeingChoked(true, PCC);
                }
                else
                {
                    victim.SetChokeLifted(true, 0.45f);
                }
            }
        }

        [Command]
        private void CmdStartChoke(uint victimId)
        {
            syncIsChoking = true;
            syncVictimNetId = victimId;
            isChoking = true;
            chokeTimer = 0f;

            if (PCC != null)
            {
                PCC.SetImmobilized(true);
            }

            if (NetworkServer.spawned.TryGetValue(victimId, out var victimIdentity))
            {
                var victimPcc = victimIdentity.GetComponent<PlayerCharacterController>();
                activeVictim = victimPcc;

                var victimChoke = victimIdentity.GetComponent<KuntilanakChokeMechanic>();
                if (victimChoke != null)
                {
                    victimChoke.RpcSetBeingChoked(true, netId);
                }
            }

            RpcNotifyChokeStart(victimId);
        }

        [ClientRpc]
        private void RpcNotifyChokeStart(uint victimId)
        {
            isChoking = true;
            if (!bonesCached) CacheArmBones();
            PlayChokeAudio(true);

            if (NetworkClient.spawned.TryGetValue(victimId, out var vIdentity))
            {
                activeVictim = vIdentity.GetComponent<PlayerCharacterController>();
            }
        }

        [ClientRpc]
        public void RpcSetBeingChoked(bool state, uint chokerId)
        {
            PlayerCharacterController chokerPcc = null;
            if (chokerId != 0 && NetworkClient.spawned.TryGetValue(chokerId, out var chokerIdentity))
            {
                chokerPcc = chokerIdentity.GetComponent<PlayerCharacterController>();
            }
            SetBeingChoked(state, chokerPcc);
        }

        private void OnSyncChokeChanged(bool oldVal, bool newVal)
        {
            isChoking = newVal;
            if (newVal)
            {
                if (!bonesCached) CacheArmBones();
                PlayChokeAudio(true);
                ResolveVictimFromNetId(syncVictimNetId);
            }
            else
            {
                PlayChokeAudio(false);
                activeVictim = null;
            }
        }

        private void OnSyncVictimChanged(uint oldVal, uint newVal)
        {
            ResolveVictimFromNetId(newVal);
        }

        private void ResolveVictimFromNetId(uint victimId)
        {
            if (victimId == 0) return;
            if (NetworkClient.spawned.TryGetValue(victimId, out var victimObj))
            {
                activeVictim = victimObj.GetComponent<PlayerCharacterController>();
            }
        }

        private void UpdateChokingProcess()
        {
            if (activeVictim == null)
            {
                StopChokingInternal("activeVictim is null");
                return;
            }

            // Jika korban sudah mati:
            var victimHealth = activeVictim.GetComponent<PlayerHealth>();
            if (victimHealth != null && victimHealth.IsDead)
            {
                StopChokingInternal("victim is dead");
                return;
            }

            // Batal jika pemain menekan Esc, atau menekan tombol cekik lagi setelah 0.4 detik
            if (IsCancelInputPressed())
            {
                StopChokingInternal("cancel input pressed");
                return;
            }
            if (chokeTimer > 0.4f && IsChokeInputDown())
            {
                StopChokingInternal("choke input pressed again to release");
                return;
            }

            // 1. Kuntilanak menghadap korban
            Vector3 toVictim = (activeVictim.transform.position - transform.position);
            toVictim.y = 0f;
            if (toVictim.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toVictim), Time.deltaTime * 15f);
            }

            // 2. Tarik korban ke depan Kuntilanak dan angkat sedikit ke atas (kaki menggantung di udara)
            float chokeHoldDist = 0.58f;
            Vector3 targetVictimPos = transform.position + transform.forward * chokeHoldDist;

            // Angkat halus tubuh korban ke atas
            float liftProgress = Mathf.Clamp01(chokeTimer / Mathf.Max(0.05f, liftDuration));
            float currentLift = Mathf.SmoothStep(0f, victimLiftHeight, liftProgress);
            targetVictimPos.y = transform.position.y + currentLift;

            Vector3 posDelta = targetVictimPos - activeVictim.transform.position;
            if (activeVictim.Controller != null && activeVictim.Controller.enabled)
            {
                activeVictim.Controller.Move(posDelta);
            }
            if (Vector3.Distance(activeVictim.transform.position, targetVictimPos) > 0.05f)
            {
                activeVictim.transform.position = targetVictimPos;
            }

            // 3. Korban menghadap ke arah Kuntilanak yang mencekiknya
            Vector3 toKunti = (transform.position - activeVictim.transform.position);
            toKunti.y = 0f;
            if (toKunti.sqrMagnitude > 0.001f)
            {
                activeVictim.transform.rotation = Quaternion.Slerp(activeVictim.transform.rotation, Quaternion.LookRotation(toKunti), Time.deltaTime * 20f);
            }

            // 4. Periksa apakah korban terpisah rintangan/tembok tebal melebihi breakDistance
            float distance = Vector3.Distance(transform.position, activeVictim.transform.position);
            if (distance > breakDistance)
            {
                StopChokingInternal("distance > breakDistance (" + distance + " > " + breakDistance + ")");
                return;
            }

            // Hitung timer cekikan (menuju 5.0 detik)
            chokeTimer += Time.deltaTime;

            // JIKA MENCAPAI 5 DETIK: KORBAN MATI!
            if (chokeTimer >= maxChokeDuration)
            {
                ExecuteLethalChoke();
            }
        }

        private void ExecuteLethalChoke()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                CmdExecuteLethalChoke();
            }
            else
            {
                PlaySnapAudio();
                if (activeVictim != null)
                {
                    activeVictim.SetChokeLifted(false);
                    activeVictim.SnapToGround();
                    var victimHealth = activeVictim.GetComponent<PlayerHealth>();
                    if (victimHealth != null)
                    {
                        victimHealth.Kill();
                    }
                    activeVictim.TriggerDeath();
                }
                StopChokingInternal("lethal choke executed");
            }
        }

        [Command]
        private void CmdExecuteLethalChoke()
        {
            RpcPlaySnapAudio();

            if (activeVictim != null)
            {
                activeVictim.SetChokeLifted(false);
                activeVictim.SnapToGround();
                var victimHealth = activeVictim.GetComponent<PlayerHealth>();
                if (victimHealth != null)
                {
                    victimHealth.Kill();
                }
                activeVictim.TriggerDeath();
            }

            CmdStopChoke("lethal choke executed");
        }

        [ClientRpc]
        private void RpcPlaySnapAudio()
        {
            PlaySnapAudio();
        }

        private void StopChokingInternal(string reason = "")
        {
            if (!isChoking) return;
            Debug.Log("[STOP_REASON] " + reason);

            isChoking = false;
            chokeTimer = 0.0f;
            chokeCooldownTimer = chokeCooldown;

            if (PCC != null)
            {
                PCC.SetImmobilized(false);
            }

            PlayChokeAudio(false);

            if (NetworkClient.active || NetworkServer.active)
            {
                CmdStopChoke(reason);
            }
            else
            {
                if (activeVictim != null)
                {
                    activeVictim.SetChokeLifted(false);
                    activeVictim.SnapToGround();
                    var victimChoke = activeVictim.GetComponent<KuntilanakChokeMechanic>();
                    if (victimChoke != null)
                    {
                        victimChoke.SetBeingChoked(false, null);
                    }
                    activeVictim = null;
                }
            }
        }

        [Command]
        private void CmdStopChoke(string reason)
        {
            syncIsChoking = false;
            syncVictimNetId = 0;
            isChoking = false;

            if (activeVictim != null)
            {
                var victimChoke = activeVictim.GetComponent<KuntilanakChokeMechanic>();
                if (victimChoke != null)
                {
                    victimChoke.RpcSetBeingChoked(false, 0);
                }
                activeVictim = null;
            }

            RpcNotifyChokeStop();
        }

        [ClientRpc]
        private void RpcNotifyChokeStop()
        {
            isChoking = false;
            chokeTimer = 0f;
            PlayChokeAudio(false);
            activeVictim = null;

            if (PCC != null)
            {
                PCC.SetImmobilized(false);
            }
        }

        public void ResetCooldown()
        {
            chokeCooldownTimer = 0f;
        }

        public void SetBeingChoked(bool state, PlayerCharacterController choker)
        {
            isBeingChoked = state;
            activeAttacker = state ? choker : null;

            if (PCC != null)
            {
                PCC.SetChokeLifted(state, 0.55f);
                PCC.SetImmobilized(state);
            }

            PlayChokeAudio(state);
        }

        private void LateUpdate()
        {
            // Transisi halus animasi tangan menyekik (0 -> 1 saat menyekik, 1 -> 0 saat selesai/batal)
            // Berjalan di semua layar (Kuntilanak, korban, maupun penonton lain)
            float targetBlend = isChoking ? 1.0f : 0.0f;
            armChokeBlend = Mathf.MoveTowards(armChokeBlend, targetBlend, Time.deltaTime * 6f);

            if (armChokeBlend <= 0.001f)
            {
                if (b6L != null) b6L.localScale = Vector3.one;
                if (b7L != null) b7L.localScale = Vector3.one;
                if (b8L != null) b8L.localScale = Vector3.one;
                if (b6R != null) b6R.localScale = Vector3.one;
                if (b7R != null) b7R.localScale = Vector3.one;
                if (b8R != null) b8R.localScale = Vector3.one;
                return;
            }

            if (!bonesCached)
            {
                CacheArmBones();
                if (!bonesCached) return;
            }

            // Tentukan posisi leher korban yang menjadi target cengkeraman
            Vector3 neckTarget;
            if (activeVictim != null && activeVictim.NeckBone != null)
            {
                neckTarget = activeVictim.NeckBone.position;
            }
            else if (activeVictim != null)
            {
                neckTarget = activeVictim.transform.position + Vector3.up * 1.55f;
            }
            else
            {
                neckTarget = transform.position + transform.forward * 0.85f + Vector3.up * 1.55f;
            }

            // Getaran tremor halus saat mencekik kuat
            float tremor = Mathf.Sin(Time.time * 28f) * 0.012f;
            neckTarget += new Vector3(tremor, tremor * 0.5f, tremor * 0.7f);

            Vector3 toNeck = neckTarget - transform.position;
            toNeck.y = 0f;
            Vector3 fwd = toNeck.sqrMagnitude > 0.001f ? toNeck.normalized : transform.forward;
            Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;

            // Target genggaman tangan kiri dan kanan di samping leher korban
            Vector3 targetL = neckTarget - rgt * 0.06f;
            Vector3 targetR = neckTarget + rgt * 0.06f;

            // LENGAN KIRI: Bahu -> Siku -> Telapak Tangan
            if (b6L != null)
            {
                Vector3 dir6L = (targetL - b6L.position).normalized;
                Quaternion rot6L = Quaternion.FromToRotation(b6L.up, dir6L) * b6L.rotation;
                b6L.rotation = Quaternion.Slerp(b6L.rotation, rot6L, armChokeBlend);
                b6L.localScale = Vector3.one;

                if (b7L != null)
                {
                    Vector3 dir7L = (targetL - b7L.position).normalized;
                    Quaternion rot7L = Quaternion.FromToRotation(b7L.up, dir7L) * b7L.rotation;
                    b7L.rotation = Quaternion.Slerp(b7L.rotation, rot7L, armChokeBlend);
                    b7L.localScale = Vector3.one;
                }

                if (b8L != null)
                {
                    Vector3 dir8L = (neckTarget - b8L.position).normalized;
                    Quaternion rot8L = Quaternion.FromToRotation(b8L.up, dir8L) * b8L.rotation;
                    b8L.rotation = Quaternion.Slerp(b8L.rotation, rot8L, armChokeBlend);
                    b8L.localScale = Vector3.one;
                }
            }

            // LENGAN KANAN: Bahu -> Siku -> Telapak Tangan
            if (b6R != null)
            {
                Vector3 dir6R = (targetR - b6R.position).normalized;
                Quaternion rot6R = Quaternion.FromToRotation(b6R.up, dir6R) * b6R.rotation;
                b6R.rotation = Quaternion.Slerp(b6R.rotation, rot6R, armChokeBlend);
                b6R.localScale = Vector3.one;

                if (b7R != null)
                {
                    Vector3 dir7R = (targetR - b7R.position).normalized;
                    Quaternion rot7R = Quaternion.FromToRotation(b7R.up, dir7R) * b7R.rotation;
                    b7R.rotation = Quaternion.Slerp(b7R.rotation, rot7R, armChokeBlend);
                    b7R.localScale = Vector3.one;
                }

                if (b8R != null)
                {
                    Vector3 dir8R = (neckTarget - b8R.position).normalized;
                    Quaternion rot8R = Quaternion.FromToRotation(b8R.up, dir8R) * b8R.rotation;
                    b8R.rotation = Quaternion.Slerp(b8R.rotation, rot8R, armChokeBlend);
                    b8R.localScale = Vector3.one;
                }
            }
        }

        #region Audio Synthesis

        private void PlayChokeAudio(bool play)
        {
            if (audioSource == null) return;
            if (play)
            {
                if (chokeLoopClip != null)
                {
                    audioSource.clip = chokeLoopClip;
                    audioSource.loop = true;
                    audioSource.volume = 0.85f;
                    audioSource.Play();
                }
            }
            else
            {
                if (audioSource.isPlaying && audioSource.clip == chokeLoopClip)
                {
                    audioSource.Stop();
                }
            }
        }

        private void PlaySnapAudio()
        {
            if (audioSource != null && chokeSnapClip != null)
            {
                audioSource.PlayOneShot(chokeSnapClip, 1.0f);
            }
        }

        private AudioClip CreateChokeLoopSound()
        {
            int sampleRate = 44100;
            float duration = 1.2f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float breath = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.4f;
                float rasp = Mathf.Sin(2f * Mathf.PI * 130f * t + Mathf.Sin(2f * Mathf.PI * 14f * t) * 4f) * 0.35f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.15f;
                data[i] = (breath + rasp + noise) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("ChokeGaspLoop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateNeckSnapSound()
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);
                float crack = Mathf.Sin(2f * Mathf.PI * 180f * t) + Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.8f;
                float click = (UnityEngine.Random.value * 2f - 1f) * 0.6f;
                data[i] = (crack + click) * env * 0.9f;
            }

            AudioClip clip = AudioClip.Create("NeckSnapLethal", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}
