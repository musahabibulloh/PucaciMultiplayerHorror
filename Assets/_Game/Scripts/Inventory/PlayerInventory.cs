using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Pucaci.Player;

namespace Pucaci.Inventory
{
    /// <summary>
    /// Sistem Inventory untuk setiap pemain.
    /// - 2 slot inventory untuk karakter biasa (Laras, Pak Vincent, Rocky).
    /// - 4 slot inventory khusus untuk Pak Mamat (dikonfigurasi via CharacterInventoryConfig).
    /// - Memegang (equip) 1 item pada satu waktu menggunakan tombol [1], [2], [3], [4] atau klik UI.
    /// - Menangani item Kentongan, Keris, Totem/Jimat, dan Tulup dengan animasi prosedural.
    /// - Seluruh aksi divalidasi di server dan disinkronkan ke seluruh klien.
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        [Header("Inventory State (Synced)")]
        public readonly SyncList<ItemType> slots = new SyncList<ItemType>();

        [SyncVar(hook = nameof(OnEquippedSlotIndexChanged))]
        private int equippedSlotIndex = -1;

        [Header("Configuration")]
        [SerializeField] private CharacterInventoryConfig characterConfig;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;

        // References
        private PlayerCharacterController pcc;
        private CharacterModelManager modelManager;
        private Camera playerCamera;
        private PlayerItemAnimator itemAnimator;

        // Visual hand item instance
        private GameObject currentHandItemInstance;
        private Transform rightHandBone;

        // Cooldown tracking per item
        private Dictionary<ItemType, float> itemCooldownTimers = new Dictionary<ItemType, float>();

        // Events for UI
        public event Action OnInventoryChanged;
        public event Action<int> OnEquippedChanged;
        public event Action<string> OnFeedbackMessage;
        public event Action<Vector3> OnKentonganAlertReceived;

        public int EquippedSlotIndex => equippedSlotIndex;
        public ItemType EquippedItem => (equippedSlotIndex >= 0 && equippedSlotIndex < slots.Count) ? slots[equippedSlotIndex] : ItemType.None;
        public int SlotCount => slots.Count;
        public GameObject CurrentHandItemInstance => currentHandItemInstance;
        public Transform RightHandBone => rightHandBone;

        public int MaxSlots
        {
            get
            {
                if (modelManager == null)
                {
                    modelManager = GetComponent<CharacterModelManager>();
                }
                CharacterType charType = CharacterType.Laras;
                if (modelManager != null)
                {
                    charType = modelManager.ActiveCharacterType;
                }
                var config = characterConfig != null ? characterConfig : CharacterInventoryConfig.Instance;
                return config.GetMaxSlotsForCharacter(charType);
            }
        }

        public bool IsFull => slots.Count >= MaxSlots;

        public bool IsItemEquipped(ItemType type)
        {
            return (equippedSlotIndex >= 0 && equippedSlotIndex < slots.Count && slots[equippedSlotIndex] == type);
        }

        public ItemType GetItemAtSlot(int index)
        {
            if (index >= 0 && index < slots.Count)
            {
                return slots[index];
            }
            return ItemType.None;
        }

        public float GetCooldownNormalized(ItemType type)
        {
            if (!itemCooldownTimers.ContainsKey(type)) return 0f;
            var cfg = ItemConfig.GetDefaultConfig(type);
            float rem = itemCooldownTimers[type];
            return (cfg.cooldown > 0f) ? Mathf.Clamp01(rem / cfg.cooldown) : 0f;
        }

        private void Awake()
        {
            pcc = GetComponent<PlayerCharacterController>();
            modelManager = GetComponent<CharacterModelManager>();
            itemAnimator = GetComponent<PlayerItemAnimator>();
            if (itemAnimator == null)
            {
                itemAnimator = gameObject.AddComponent<PlayerItemAnimator>();
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.5f;
            }
            if (characterConfig == null)
            {
                characterConfig = CharacterInventoryConfig.Instance;
            }
            if (modelManager != null)
            {
                modelManager.OnCharacterChanged += HandleCharacterChanged;
            }
        }

        private void OnDestroy()
        {
            if (modelManager != null)
            {
                modelManager.OnCharacterChanged -= HandleCharacterChanged;
            }
        }

        private void HandleCharacterChanged(CharacterType newChar)
        {
            if (equippedSlotIndex >= MaxSlots)
            {
                equippedSlotIndex = -1;
            }
            FindRightHandBone();
            RefreshHandVisual();
            OnInventoryChanged?.Invoke();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            slots.Callback += OnSlotsListChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            slots.Callback += OnSlotsListChanged;
            FindRightHandBone();
            RefreshHandVisual();
        }

        private void Start()
        {
            playerCamera = Camera.main;
            FindRightHandBone();
            RefreshHandVisual();
        }

        private void Update()
        {
            UpdateCooldowns();

            bool isLocal = (isLocalPlayer || (!NetworkClient.active && !NetworkServer.active));
            if (!isLocal) return;

            HandlePickupInput();
            HandleSlotInput();
            HandleUseInput();
        }

        private void HandlePickupInput()
        {
            WorldItemPickup nearest = null;
            float minDist = 2.6f;

            var allPickups = FindObjectsOfType<WorldItemPickup>();
            foreach (var p in allPickups)
            {
                if (p == null) continue;
                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = p;
                }
            }

            if (nearest != null)
            {
                string itemName = ItemConfig.GetItemDisplayName(nearest.ItemType);
                Pucaci.UI.PlayerInventoryUI.Instance?.ShowInteractPrompt($"[E] Ambil {itemName}");

                if (IsKeyJustPressed(KeyCode.E, Key.E))
                {
                    if (IsFull)
                    {
                        ShowToast($"Inventory Penuh! (Maks {MaxSlots} Slot)");
                        PlayFeedbackSound(false);
                    }
                    else
                    {
                        var netId = nearest.GetComponent<NetworkIdentity>();
                        if (NetworkClient.active && netId != null)
                        {
                            CmdPickupWorldItem(netId);
                        }
                        else
                        {
                            LocalPickupWorldItem(nearest);
                        }
                    }
                }
            }
            else
            {
                Pucaci.UI.PlayerInventoryUI.Instance?.HideInteractPrompt();
            }
        }

        private void LocalPickupWorldItem(WorldItemPickup pickup)
        {
            if (pickup == null) return;
            if (slots.Count >= MaxSlots)
            {
                ShowToast($"Inventory Penuh! (Maks {MaxSlots} Slot)");
                PlayFeedbackSound(false);
                return;
            }

            ItemType type = pickup.ItemType;
            slots.Add(type);

            if (equippedSlotIndex == -1)
            {
                equippedSlotIndex = slots.Count - 1;
            }

            PlayFeedbackSound(true);
            ShowToast($"Mengambil {ItemConfig.GetItemDisplayName(type)} (Slot {slots.Count})");
            FindRightHandBone();
            RefreshHandVisual();
            OnInventoryChanged?.Invoke();

            Destroy(pickup.gameObject);
        }

        private void UpdateCooldowns()
        {
            if (itemCooldownTimers.Count == 0) return;

            var keys = new List<ItemType>(itemCooldownTimers.Keys);
            foreach (var key in keys)
            {
                if (itemCooldownTimers[key] > 0f)
                {
                    itemCooldownTimers[key] = Mathf.Max(0f, itemCooldownTimers[key] - Time.deltaTime);
                }
            }
        }

        #region Input Handling

        private void HandleSlotInput()
        {
            // Jangan baca input slot jika menu pemilihan penyamaran [X] sedang terbuka
            if (Pucaci.UI.DisguiseSelectionUI.Instance != null && Pucaci.UI.DisguiseSelectionUI.Instance.IsOpen)
            {
                return;
            }

            // Tombol 1: Slot 0
            if (IsKeyJustPressed(KeyCode.Alpha1, Key.Digit1))
            {
                TrySelectSlot(0);
            }
            // Tombol 2: Slot 1
            else if (IsKeyJustPressed(KeyCode.Alpha2, Key.Digit2))
            {
                TrySelectSlot(1);
            }
            // Tombol 3: Slot 2 (Pak Mamat)
            else if (IsKeyJustPressed(KeyCode.Alpha3, Key.Digit3))
            {
                TrySelectSlot(2);
            }
            // Tombol 4: Slot 3 (Pak Mamat)
            else if (IsKeyJustPressed(KeyCode.Alpha4, Key.Digit4))
            {
                TrySelectSlot(3);
            }
        }

        private bool IsKeyJustPressed(KeyCode legacyKey, Key newKey)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                var control = Keyboard.current[newKey];
                if (control != null && control.wasPressedThisFrame) return true;
            }
#endif
            return Input.GetKeyDown(legacyKey);
        }

        /// <summary>
        /// Memilih atau melepas slot inventory tertentu (0-3).
        /// Dapat dipanggil dari keyboard shortcut [1-4] maupun klik mouse di UI inventory.
        /// </summary>
        public void TrySelectSlot(int slotIndex)
        {
            int max = MaxSlots;
            if (slotIndex >= max)
            {
                ShowToast($"Karakter ini hanya memiliki {max} Slot!");
                PlayFeedbackSound(false);
                return;
            }

            // Kasus 1: Slot Kosong
            if (slotIndex >= slots.Count || slots[slotIndex] == ItemType.None)
            {
                PlayFeedbackSound(false);
                ShowToast($"Slot [{slotIndex + 1}] Kosong!");
                return;
            }

            // Kasus 2: Slot Berisi Item
            int targetIdx = (equippedSlotIndex == slotIndex) ? -1 : slotIndex;
            if (NetworkClient.active)
            {
                CmdEquipSlot(targetIdx);
            }
            else
            {
                equippedSlotIndex = targetIdx;
                RefreshHandVisual();
                OnEquippedChanged?.Invoke(targetIdx);
            }
            PlayFeedbackSound(true);
        }

        private void HandleUseInput()
        {
            if (equippedSlotIndex < 0 || equippedSlotIndex >= slots.Count) return;

            ItemType current = slots[equippedSlotIndex];
            if (current == ItemType.None) return;

            // Jangan gunakan item jika kursor sedang bebas untuk klik UI atau menu terbuka
            if (Cursor.lockState != CursorLockMode.Locked && !Input.GetMouseButtonDown(0)) return;

            bool isUsePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) isUsePressed = true;
#endif
            if (Input.GetMouseButtonDown(0)) isUsePressed = true;

            if (isUsePressed)
            {
                // Cek cooldown
                if (itemCooldownTimers.ContainsKey(current) && itemCooldownTimers[current] > 0f)
                {
                    return;
                }

                Vector3 origin = transform.position + Vector3.up * 1.4f;
                Vector3 dir = transform.forward;
                if (playerCamera != null)
                {
                    origin = playerCamera.transform.position;
                    dir = playerCamera.transform.forward;
                }

                var config = ItemConfig.GetDefaultConfig(current);
                itemCooldownTimers[current] = config.cooldown;

                // Mainkan animasi item di tangan lokal seketika
                TriggerItemAnimation(current);

                if (NetworkClient.active)
                {
                    CmdUseItem(current, origin, dir);
                }
                else
                {
                    ExecuteItemLocal(current, config, origin, dir);
                }
            }
        }

        public void TriggerItemAnimation(ItemType type)
        {
            if (itemAnimator != null)
            {
                itemAnimator.PlayUseAnimation(type, currentHandItemInstance, rightHandBone);
            }
        }

        private void ExecuteItemLocal(ItemType type, ItemConfig config, Vector3 origin, Vector3 dir)
        {
            switch (type)
            {
                case ItemType.Kentongan:
                    PlayProceduralKentonganSFX(transform.position, 1.0f);
                    OnKentonganAlertReceived?.Invoke(transform.position);
                    break;

                case ItemType.Keris:
                    PlayProceduralSlashSFX(false);
                    if (Physics.SphereCast(origin, 0.45f, dir, out RaycastHit hit, config.range))
                    {
                        var targetPcc = hit.collider.GetComponentInParent<PlayerCharacterController>();
                        if (targetPcc != null && targetPcc != pcc)
                        {
                            var ghostVanish = targetPcc.GetComponent<KuntilanakGhostVanish>();
                            if (ghostVanish != null && ghostVanish.IsGhostForm)
                            {
                                if (audioSource != null)
                                {
                                    audioSource.pitch = 1.3f;
                                    audioSource.PlayOneShot(CreateEtherealWhooshClip());
                                }
                            }
                            else
                            {
                                PlayProceduralSlashSFX(true);
                                var hp = targetPcc.GetComponent<PlayerHealth>();
                                if (hp != null) hp.TakeDamage(config.damage);
                            }
                        }
                    }
                    break;

                case ItemType.Totem:
                    if (audioSource != null)
                    {
                        audioSource.pitch = 0.9f;
                        audioSource.PlayOneShot(CreateHolyWardClip());
                    }
                    break;

                case ItemType.Tulup:
                    if (audioSource != null)
                    {
                        audioSource.pitch = 1.1f;
                        audioSource.PlayOneShot(CreateBreatheShootClip());
                    }
                    GameObject projPrefab = Resources.Load<GameObject>("Items/GarlicProjectile");
#if UNITY_EDITOR
                    if (projPrefab == null)
                    {
                        projPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Items/GarlicProjectile.prefab");
                    }
#endif
                    if (projPrefab != null)
                    {
                        var proj = Instantiate(projPrefab, origin + dir * 0.4f, Quaternion.LookRotation(dir));
                        var gp = proj.GetComponent<GarlicProjectile>();
                        if (gp != null) gp.Initialize(dir, config.projectileSpeed, config.damage, 0);
                    }
                    break;
            }
        }

        #endregion

        #region Server Commands

        [Command]
        public void CmdEquipSlot(int slotIndex)
        {
            if (slotIndex < -1 || slotIndex >= slots.Count) return;

            if (slotIndex >= 0 && slots[slotIndex] == ItemType.None)
            {
                return;
            }

            equippedSlotIndex = slotIndex;
        }

        [Command]
        public void CmdAddItem(ItemType type)
        {
            if (type == ItemType.None) return;

            if (slots.Count >= MaxSlots)
            {
                RpcShowToast("Inventory Penuh!");
                return;
            }

            slots.Add(type);

            if (equippedSlotIndex == -1)
            {
                equippedSlotIndex = slots.Count - 1;
            }

            RpcPlayPickupSFX();
        }

        [Command]
        public void CmdPickupWorldItem(NetworkIdentity pickupIdentity)
        {
            if (pickupIdentity == null) return;
            var pickup = pickupIdentity.GetComponent<WorldItemPickup>();
            if (pickup == null) return;

            if (Vector3.Distance(transform.position, pickupIdentity.transform.position) > 5.0f) return;

            if (slots.Count >= MaxSlots)
            {
                RpcShowToast($"Inventory Penuh! (Maks {MaxSlots} Slot)");
                return;
            }

            ItemType type = pickup.ItemType;
            slots.Add(type);

            if (equippedSlotIndex == -1)
            {
                equippedSlotIndex = slots.Count - 1;
            }

            RpcPlayPickupSFX();
            RpcShowToast($"Mengambil {ItemConfig.GetItemDisplayName(type)} (Slot {slots.Count})");

            NetworkServer.Destroy(pickupIdentity.gameObject);
        }

        [Command]
        public void CmdUseItem(ItemType type, Vector3 aimOrigin, Vector3 aimDir)
        {
            if (!IsItemEquipped(type)) return;

            var config = ItemConfig.GetDefaultConfig(type);

            // Broadcast animasi ke seluruh client
            RpcBroadcastItemAnimation(type);

            switch (type)
            {
                case ItemType.Kentongan:
                    ExecuteKentongan(config);
                    break;

                case ItemType.Keris:
                    ExecuteKeris(config, aimOrigin, aimDir);
                    break;

                case ItemType.Totem:
                    ExecuteTotem(config);
                    break;

                case ItemType.Tulup:
                    ExecuteTulup(config, aimOrigin, aimDir);
                    break;
            }
        }

        private void ExecuteKentongan(ItemConfig config)
        {
            Vector3 soundPos = transform.position;
            float radius = config.alertRadius;

            var allPlayers = FindObjectsOfType<PlayerCharacterController>();
            foreach (var other in allPlayers)
            {
                if (other.gameObject == gameObject) continue;

                float dist = Vector3.Distance(soundPos, other.transform.position);
                if (dist <= radius)
                {
                    var otherInv = other.GetComponent<PlayerInventory>();
                    if (otherInv != null)
                    {
                        otherInv.RpcReceiveKentonganAlert(soundPos);
                    }
                }
            }

            RpcPlayKentonganSound(soundPos);
        }

        private void ExecuteKeris(ItemConfig config, Vector3 aimOrigin, Vector3 aimDir)
        {
            float range = config.range;
            float damage = config.damage;

            Ray ray = new Ray(aimOrigin, aimDir);
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.45f, range);

            bool hitGhost = false;

            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;

                var targetPcc = hit.collider.GetComponentInParent<PlayerCharacterController>();
                if (targetPcc != null)
                {
                    var ghostVanish = targetPcc.GetComponent<KuntilanakGhostVanish>();
                    var roleManager = targetPcc.GetComponent<PlayerRoleManager>();
                    var health = targetPcc.GetComponent<PlayerHealth>();

                    if (ghostVanish != null && ghostVanish.IsGhostForm)
                    {
                        hitGhost = true;
                        RpcPlayKerisMissEthereal(hit.point);
                        Debug.Log("[Keris] Serangan menembus! Kuntilanak sedang berwujud arwah tembus pandang.");
                        break;
                    }

                    bool isKunti = (roleManager != null && roleManager.IsKuntilanakForm) || (ghostVanish != null);
                    if (isKunti && health != null && !health.IsDead)
                    {
                        health.TakeDamage(damage);
                        hitGhost = true;
                        RpcPlayKerisHit(hit.point);
                        Debug.Log($"[Keris] Berhasil melukai Kuntilanak! Damage: {damage}");
                        break;
                    }
                }
            }

            if (!hitGhost)
            {
                RpcPlayKerisSwing();
            }
        }

        private void ExecuteTotem(ItemConfig config)
        {
            RpcPlayTotemWardWave(transform.position);
        }

        private void ExecuteTulup(ItemConfig config, Vector3 aimOrigin, Vector3 aimDir)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.35f + transform.forward * 0.4f;

            var projObj = new GameObject("GarlicProjectile_Spawned");
            projObj.transform.position = spawnPos;

            var proj = projObj.AddComponent<GarlicProjectile>();
            proj.Initialize(aimDir, config.projectileSpeed, config.damage, netId);

            NetworkServer.Spawn(projObj);

            RpcPlayTulupShoot(spawnPos);
        }

        #endregion

        #region Client RPCs & Feedback

        [ClientRpc]
        public void RpcBroadcastItemAnimation(ItemType type)
        {
            TriggerItemAnimation(type);
        }

        [ClientRpc]
        public void RpcReceiveKentonganAlert(Vector3 soundPos)
        {
            OnKentonganAlertReceived?.Invoke(soundPos);
            PlayProceduralKentonganSFX(soundPos, 1.0f);
        }

        [ClientRpc]
        private void RpcPlayKentonganSound(Vector3 soundPos)
        {
            PlayProceduralKentonganSFX(soundPos, 1.0f);
        }

        [ClientRpc]
        private void RpcPlayKerisSwing()
        {
            PlayProceduralSlashSFX(false);
        }

        [ClientRpc]
        private void RpcPlayKerisHit(Vector3 pos)
        {
            PlayProceduralSlashSFX(true);
        }

        [ClientRpc]
        private void RpcPlayKerisMissEthereal(Vector3 pos)
        {
            if (audioSource != null)
            {
                audioSource.pitch = 1.3f;
                audioSource.PlayOneShot(CreateEtherealWhooshClip());
            }
        }

        [ClientRpc]
        private void RpcPlayTotemWardWave(Vector3 pos)
        {
            if (audioSource != null)
            {
                audioSource.pitch = 0.9f;
                audioSource.PlayOneShot(CreateHolyWardClip());
            }
        }

        [ClientRpc]
        private void RpcPlayTulupShoot(Vector3 pos)
        {
            if (audioSource != null)
            {
                audioSource.pitch = 1.1f;
                audioSource.PlayOneShot(CreateBreatheShootClip());
            }
        }

        [ClientRpc]
        private void RpcPlayPickupSFX()
        {
            PlayFeedbackSound(true);
        }

        public void ShowToast(string msg)
        {
            bool isLocal = (isLocalPlayer || (!NetworkClient.active && !NetworkServer.active));
            if (isLocal)
            {
                OnFeedbackMessage?.Invoke(msg);
            }
        }

        [ClientRpc]
        private void RpcShowToast(string msg)
        {
            ShowToast(msg);
        }

        private void PlayFeedbackSound(bool success)
        {
            if (audioSource == null) return;
            audioSource.pitch = success ? 1.15f : 0.85f;
            audioSource.PlayOneShot(CreateBeepClip(success ? 750f : 300f, 0.08f));
        }

        #endregion

        #region Visual Model in Hand

        private void OnEquippedSlotIndexChanged(int oldVal, int newVal)
        {
            equippedSlotIndex = newVal;
            RefreshHandVisual();
            OnEquippedChanged?.Invoke(newVal);
        }

        private void OnSlotsListChanged(SyncList<ItemType>.Operation op, int itemIndex, ItemType oldItem, ItemType newItem)
        {
            RefreshHandVisual();
            OnInventoryChanged?.Invoke();
        }

        public void FindRightHandBone()
        {
            if (modelManager != null && modelManager.ActiveModel != null)
            {
                var anim = modelManager.ActiveModel.GetComponent<Animator>();
                if (anim != null && anim.isHuman)
                {
                    rightHandBone = anim.GetBoneTransform(HumanBodyBones.RightHand);
                }

                if (rightHandBone == null)
                {
                    var bones = modelManager.ActiveModel.GetComponentsInChildren<Transform>(true);
                    foreach (var b in bones)
                    {
                        if (b.name.Contains("RightHand") && !b.name.Contains("Thumb") && !b.name.Contains("Index"))
                        {
                            rightHandBone = b;
                            break;
                        }
                    }
                }
            }

            if (rightHandBone == null)
            {
                var bones = GetComponentsInChildren<Transform>(true);
                foreach (var b in bones)
                {
                    if (b.name.Contains("RightHand") && !b.name.Contains("Thumb") && !b.name.Contains("Index"))
                    {
                        rightHandBone = b;
                        break;
                    }
                }
            }
        }

        public void RefreshHandVisual()
        {
            if (currentHandItemInstance != null)
            {
                Destroy(currentHandItemInstance);
                currentHandItemInstance = null;
            }

            if (rightHandBone == null)
            {
                FindRightHandBone();
            }

            if (rightHandBone == null) return;

            ItemType equipped = EquippedItem;
            if (equipped == ItemType.None) return;

            // Buat instance model 3D item di tangan kanan
            currentHandItemInstance = CreateHandItemVisual(equipped, rightHandBone);
        }

        private GameObject CreateHandItemVisual(ItemType type, Transform parentBone)
        {
            GameObject itemObj = new GameObject($"Equipped_{type}");
            itemObj.transform.SetParent(parentBone, false);

            string modelPath = ItemVisualUtility.GetModelAssetPath(type);
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab != null)
            {
                var visual = Instantiate(prefab, itemObj.transform);
                visual.name = "Model";
                ItemVisualUtility.ApplyHandTransform(visual, type);

                var cols = visual.GetComponentsInChildren<Collider>();
                foreach (var c in cols) Destroy(c);

                return itemObj;
            }
#endif
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fallback.transform.SetParent(itemObj.transform, false);
            fallback.transform.localScale = Vector3.one * 0.08f;
            var fCol = fallback.GetComponent<Collider>();
            if (fCol != null) Destroy(fCol);

            return itemObj;
        }

        #endregion

        #region Procedural Audio Generators

        private void PlayProceduralKentonganSFX(Vector3 pos, float volume)
        {
            var tempObj = new GameObject("KentonganSound_Temp");
            tempObj.transform.position = pos;
            var src = tempObj.AddComponent<AudioSource>();
            src.spatialBlend = 0.85f;
            src.minDistance = 3f;
            src.maxDistance = 40f;
            src.volume = volume;

            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * 0.45f);
            AudioClip clip = AudioClip.Create("KentonganKnock", totalSamples, 1, sampleRate, false);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 18f);
                float knock = Mathf.Sin(2f * Mathf.PI * 340f * t) * 0.7f
                            + Mathf.Sin(2f * Mathf.PI * 680f * t) * 0.35f
                            + Mathf.Sin(2f * Mathf.PI * 1050f * t) * 0.15f;
                float woodClick = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 70f) * 0.5f;
                samples[i] = Mathf.Clamp((knock + woodClick) * env, -1f, 1f);
            }
            clip.SetData(samples, 0);

            src.clip = clip;
            src.Play();
            Destroy(tempObj, 0.7f);
        }

        private void PlayProceduralSlashSFX(bool isHit)
        {
            if (audioSource == null) return;
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * (isHit ? 0.22f : 0.16f));
            AudioClip clip = AudioClip.Create("KerisSlash", totalSamples, 1, sampleRate, false);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * (isHit ? 24f : 35f));
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.7f;
                float metallicRing = isHit ? Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.4f : 0f;
                samples[i] = (noise + metallicRing) * env;
            }
            clip.SetData(samples, 0);

            audioSource.pitch = isHit ? 1.0f : 1.25f;
            audioSource.PlayOneShot(clip);
        }

        private AudioClip CreateEtherealWhooshClip()
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * 0.35f);
            AudioClip clip = AudioClip.Create("EtherealMiss", totalSamples, 1, sampleRate, false);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / 0.35f));
                float tone = Mathf.Sin(2f * Mathf.PI * (220f + t * 400f) * t) * 0.5f;
                samples[i] = tone * env;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateHolyWardClip()
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * 0.5f);
            AudioClip clip = AudioClip.Create("HolyWard", totalSamples, 1, sampleRate, false);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 6f);
                float bell = Mathf.Sin(2f * Mathf.PI * 528f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1056f * t) * 0.3f;
                samples[i] = bell * env;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateBreatheShootClip()
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * 0.2f);
            AudioClip clip = AudioClip.Create("TulupBlow", totalSamples, 1, sampleRate, false);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);
                float breath = (UnityEngine.Random.value * 2f - 1f) * 0.8f;
                samples[i] = breath * env;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateBeepClip(float freq, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            AudioClip clip = AudioClip.Create("Beep", totalSamples, 1, sampleRate, false);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 20f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
