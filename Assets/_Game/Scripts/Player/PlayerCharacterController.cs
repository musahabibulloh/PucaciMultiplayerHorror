using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [Tooltip("Kecepatan jalan normal (m/s)")]
        [SerializeField] private float walkSpeed = 3.5f;

        [Tooltip("Kecepatan lari / sprint (m/s)")]
        [SerializeField] private float sprintSpeed = 6.5f;

        [Tooltip("Akselerasi kecepatan gerakan saat mulai bergerak")]
        [SerializeField] private float accelerationRate = 12f;

        [Tooltip("Deselerasi kecepatan gerakan saat berhenti")]
        [SerializeField] private float decelerationRate = 8f;

        [Header("Stamina and Exhaustion")]
        [Tooltip("Kapasitas stamina maksimal")]
        [SerializeField] private float maxStamina = 100f;

        [Tooltip("Konsumsi stamina per detik saat sprint")]
        [SerializeField] private float sprintStaminaDrain = 18f;

        [Tooltip("Konsumsi stamina saat melompat")]
        [SerializeField] private float jumpStaminaCost = 10f;

        [Tooltip("Konsumsi stamina saat meluncur / slide")]
        [SerializeField] private float slideStaminaCost = 15f;

        [Tooltip("Kecepatan pemulihan stamina per detik")]
        [SerializeField] private float staminaRegenRate = 16f;

        [Tooltip("Jeda waktu sebelum stamina mulai pulih setelah berhenti sprint")]
        [SerializeField] private float staminaRegenDelay = 1.0f;

        [Tooltip("Ambang batas pemulihan stamina dari kondisi lelah (0.25 = 25 persen)")]
        [SerializeField] private float exhaustionRecoveryThreshold = 0.25f;

        [Tooltip("Kecepatan jalan saat lelah / capek (m/s)")]
        [SerializeField] private float exhaustedWalkSpeed = 2.4f;

        [Header("Jump & Gravity")]
        [Tooltip("Tinggi lompatan (meter)")]
        [SerializeField] private float jumpHeight = 1.2f;

        [Tooltip("Gaya gravitasi")]
        [SerializeField] private float gravity = -18f;

        [Tooltip("Radius cek permukaan tanah")]
        [SerializeField] private float groundCheckRadius = 0.35f;

        [Tooltip("Offset cek permukaan tanah dari dasar controller")]
        [SerializeField] private float groundCheckOffset = -0.1f;

        [Tooltip("Layer tanah/ground")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Slide (Meluncur)")]
        [Tooltip("Kecepatan dorongan slide")]
        [SerializeField] private float slideSpeed = 8.5f;

        [Tooltip("Durasi slide dalam detik")]
        [SerializeField] private float slideDuration = 1.2f;

        [Tooltip("Tinggi CharacterController saat meluncur")]
        [SerializeField] private float slideControllerHeight = 1.0f;

        [Header("Flight (Kuntilanak Mode)")]
        [Tooltip("Kecepatan melayang horizontal untuk karakter terbang (m/s)")]
        [SerializeField] private float flightSpeed = 4.5f;

        [Tooltip("Kecepatan terbang naik/turun (m/s)")]
        [SerializeField] private float flightVerticalSpeed = 3.5f;

        [Tooltip("Tingkat akselerasi penerbangan vertikal")]
        [SerializeField] private float flightAcceleration = 12f;

        [Tooltip("Kecepatan melayang saat wujud hantu Casper aktif (m/s)")]
        [SerializeField] private float ghostFlightSpeed = 6.0f;

        private bool isFlyingCharacter = false;
        public bool IsFlyingCharacter => isFlyingCharacter;
        private bool isGhostSpeedBoosted = false;
        private bool isGhostPhasing = false;
        public bool IsGhostPhasing => isGhostPhasing;
        private readonly System.Collections.Generic.HashSet<Collider> ignoredColliders = new System.Collections.Generic.HashSet<Collider>();
        private float ghostPhasingScanTimer = 0f;

        public void SetGhostSpeedBoost(bool boost)
        {
            isGhostSpeedBoosted = boost;
        }

        public void SetGhostPhasing(bool phasing)
        {
            if (isGhostPhasing == phasing) return;
            isGhostPhasing = phasing;

            if (phasing)
            {
                EnableGhostPhasing();
            }
            else
            {
                DisableGhostPhasing();
            }
        }

        private void EnableGhostPhasing()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (controller == null) return;

            RefreshGhostPhasing();
        }

        private void RefreshGhostPhasing()
        {
            if (controller == null) return;

            var allColliders = FindObjectsOfType<Collider>(false);
            for (int i = 0; i < allColliders.Length; i++)
            {
                var col = allColliders[i];
                if (col == null || col == controller || col.transform.IsChildOf(transform)) continue;

                // Karakter hantu BISA menembus segala objek (pohon, pagar, batu, rintangan luar, player lain)
                // KECUALI:
                // 1. Tanah (Ground) - agar tidak jatuh ke bawah dunia
                // 2. Rumah (House) - hantu tidak bisa menembus / masuk ke dalam rumah!
                if (!IsGroundCollider(col) && !IsHouseCollider(col))
                {
                    if (ignoredColliders.Add(col))
                    {
                        Physics.IgnoreCollision(controller, col, true);
                    }
                }
            }
        }

        private void DisableGhostPhasing()
        {
            if (controller == null) return;

            foreach (var col in ignoredColliders)
            {
                if (col != null)
                {
                    Physics.IgnoreCollision(controller, col, false);
                }
            }
            ignoredColliders.Clear();
        }

        public static bool IsGroundCollider(Collider col)
        {
            if (col == null) return false;

            // 1. Tag pengecekan tanah
            if (col.CompareTag("Ground")) return true;

            // 2. Komponen terrain
            if (col is TerrainCollider) return true;

            // 3. Layer tanah
            string layerName = LayerMask.LayerToName(col.gameObject.layer);
            if (layerName.Equals("Ground", System.StringComparison.OrdinalIgnoreCase) ||
                layerName.Equals("Terrain", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 4. Pengecekan nama objek tanah / lantai / terrain / plane
            string colName = col.gameObject.name.ToLowerInvariant();
            if (colName.Contains("ground") || colName.Contains("tanah") ||
                colName.Contains("terrain") || colName.Contains("plane") ||
                colName.Contains("floor") || colName.Contains("lantai"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mengecek apakah suatu collider merupakan bagian dari bangunan rumah (dinding, atap, pintu, dsb).
        /// Hantu dalam mode menghilang TIDAK BISA menembus objek rumah ini (dilarang masuk ke dalam rumah).
        /// </summary>
        public static bool IsHouseCollider(Collider col)
        {
            if (col == null) return false;

            // 1. Tag pengecekan rumah / bangunan (aman tanpa error jika tag belum terdaftar di TagManager)
            try
            {
                string tag = col.gameObject.tag;
                if (tag.Equals("House", System.StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("Rumah", System.StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("Building", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }

            // 2. Layer rumah / bangunan
            string layerName = LayerMask.LayerToName(col.gameObject.layer);
            if (layerName.Equals("House", System.StringComparison.OrdinalIgnoreCase) ||
                layerName.Equals("Building", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 3. Pengecekan nama GameObject collider
            string n = col.gameObject.name.ToLowerInvariant();
            if (n.Contains("rumah") || n.Contains("house") || n.Contains("gubuk") ||
                n.Contains("home") || n.Contains("building") || n.Contains("pos") ||
                n.Contains("dinding") || n.Contains("wall") || n.Contains("door") || n.Contains("pintu") ||
                n.Contains("roof") || n.Contains("atap") || n.Contains("jendela") || n.Contains("window"))
            {
                return true;
            }

            // 4. Pengecekan nama Parent GameObject
            if (col.transform.parent != null)
            {
                string pn = col.transform.parent.name.ToLowerInvariant();
                if (pn.Contains("rumah") || pn.Contains("house") || pn.Contains("gubuk") ||
                    pn.Contains("building") || pn.Contains("home"))
                {
                    return true;
                }
            }

            // 5. Pengecekan nama Mesh pada MeshFilter
            var mf = col.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string mn = mf.sharedMesh.name.ToLowerInvariant();
                if (mn.Contains("rumah") || mn.Contains("house") || mn.Contains("gubuk") || mn == "pos")
                {
                    return true;
                }
            }

            // 6. Pengecekan nama Material atau Tekstur
            var r = col.GetComponent<Renderer>();
            if (r != null && r.sharedMaterials != null)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    string matName = mat.name.ToLowerInvariant();
                    if (matName.Contains("rumah") || matName.Contains("house") || matName.Contains("gubuk"))
                    {
                        return true;
                    }

                    if (mat.mainTexture != null)
                    {
                        string texName = mat.mainTexture.name.ToLowerInvariant();
                        if (texName.Contains("rumah") || texName.Contains("house") ||
                            texName.Contains("gubuk") || texName == "pos")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (isGhostPhasing && hit.collider != null && !IsGroundCollider(hit.collider) && !IsHouseCollider(hit.collider))
            {
                if (ignoredColliders.Add(hit.collider))
                {
                    Physics.IgnoreCollision(controller, hit.collider, true);
                }
            }
        }

        private void ClampAboveGround()
        {
            Vector3 rayStart = transform.position + Vector3.up * 1.5f;
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 4.0f, ~0, QueryTriggerInteraction.Ignore);

            float highestGroundY = float.MinValue;
            bool foundGround = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider != null && hit.collider != controller && !hit.collider.transform.IsChildOf(transform) && (IsGroundCollider(hit.collider) || IsHouseCollider(hit.collider)))
                {
                    if (hit.point.y > highestGroundY)
                    {
                        highestGroundY = hit.point.y;
                        foundGround = true;
                    }
                }
            }

            if (foundGround && transform.position.y < highestGroundY)
            {
                Vector3 clampedPos = transform.position;
                clampedPos.y = highestGroundY;
                transform.position = clampedPos;
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = 0f;
                }
            }
        }

        [Header("Camera & Mouse Look")]
        [Tooltip("Transform root kamera mata (First-Person)")]
        [SerializeField] private Transform cameraRoot;

        [Tooltip("Sensitivitas mouse horizontal & vertikal")]
        [SerializeField] private float lookSensitivity = 1.5f;

        [Tooltip("Batas sudut pandang ke atas")]
        [SerializeField] private float topClamp = 85.0f;

        [Tooltip("Batas sudut pandang ke bawah")]
        [SerializeField] private float bottomClamp = -85.0f;

        public enum CameraPerspective { ThirdPerson, FirstPerson }

        [Tooltip("Transform tulang kepala (mixamorig:Head) untuk disembunyikan saat First-Person agar tidak mengganggu kamera")]
        [SerializeField] private Transform headBone;

        [Tooltip("Transform tulang leher (mixamorig:Neck) untuk disembunyikan saat First-Person agar kerah baju tidak menutupi kamera")]
        [SerializeField] private Transform neckBone;

        public Transform NeckBone => neckBone;
        public Transform HeadBone => headBone;
        public CharacterController Controller => controller;
        public bool IsChokeLifted => isChokeLifted;

        public CameraPerspective CurrentPerspective => cameraPerspective;
        public bool IsGrounded => isGrounded;
        public bool IsSliding => isSliding;
        public float CurrentHorizontalSpeed => currentHorizontalSpeed;
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float VerticalVelocity => verticalVelocity;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;
        public float StaminaNormalized => maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 1f;
        public bool IsExhausted => isExhausted;
        public bool IsSprinting => isGrounded && currentHorizontalSpeed > (walkSpeed + sprintSpeed) * 0.5f;

        [Header("Camera Perspective (TPP / FPP)")]
        [Tooltip("Mode sudut pandang kamera awal (default Third-Person)")]
        [SerializeField] private CameraPerspective cameraPerspective = CameraPerspective.ThirdPerson;

        [Tooltip("Transform kamera di dalam CameraRoot")]
        [SerializeField] private Transform actualCameraTransform;

        [Tooltip("Renderer karakter untuk mengatur visibilitas kepala/badan")]
        [SerializeField] private SkinnedMeshRenderer characterRenderer;

        [Tooltip("Offset posisi kamera saat Third-Person (relatif ke CameraRoot)")]
        [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.4f, 0.15f, -2.4f);

        [Tooltip("Offset posisi kamera saat First-Person (relatif ke CameraRoot)")]
        [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 0.04f, 0.06f);

        [Tooltip("Offset posisi kamera saat First-Person khusus Pak Mamat (postur tubuh lebih tinggi)")]
        [SerializeField] private Vector3 pakMamatFirstPersonOffset = new Vector3(0f, 0.25f, 0.08f);

        [Header("Flashlight (Senter)")]
        [Tooltip("Lampu senter pemain")]
        [SerializeField] private Light flashlight;

        [Header("Model Offset")]
        [Tooltip("Transform model karakter (biasanya GameObject Model)")]
        [SerializeField] private Transform modelTransform;

        [Tooltip("Offset posisi model karakter relatif terhadap root (Vector3.zero agar menempel di tanah)")]
        [SerializeField] private Vector3 modelOffset = Vector3.zero;

        [Header("Animation")]
        [Tooltip("Animator karakter")]
        [SerializeField] private Animator animator;

        // Komponen
        private CharacterController controller;

        // Status gerakan
        private float currentHorizontalSpeed;
        private float verticalVelocity;
        private bool isGrounded;
        private bool isSliding;
        private float slideTimer;
        private Vector3 slideDirection;
        private Vector3 lastMoveDirection;
        private bool isDead;
        private bool isImmobilized;
        private bool isChokeLifted;
        private float chokeLiftAmount;
        private float targetChokeLift = 0.55f;
        private float chokeHitStruggleTimer;

        public bool IsDead => isDead;
        public bool IsImmobilized => isImmobilized;

        // Status Stamina
        private float currentStamina = 100f;
        private float staminaRegenCooldown;
        private bool isExhausted;

        // Kontrol kamera
        private float cameraPitch;
        private bool isCursorLocked = true;

        // Nilai awal CharacterController
        private float originalControllerHeight;
        private Vector3 originalControllerCenter;

        // Animator Hash IDs
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        private static readonly int MoveXSpaceHash = Animator.StringToHash("Move X");
        private static readonly int MoveZSpaceHash = Animator.StringToHash("Move Z");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        private static readonly int LookBackHash = Animator.StringToHash("LookBack");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int SlideHash = Animator.StringToHash("Slide");
        private static readonly int StumbleHash = Animator.StringToHash("Stumble");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int GetUpHash = Animator.StringToHash("GetUp");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        private System.Collections.Generic.HashSet<int> animatorParamHashes;

        // Parameter penghalusan gerakan animasi
        private float currentMoveX;
        private float currentMoveZ;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            originalControllerHeight = controller.height;
            originalControllerCenter = controller.center;
            currentStamina = maxStamina;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (modelTransform == null)
            {
                if (animator != null && animator.transform != transform)
                {
                    modelTransform = animator.transform;
                }
                else
                {
                    modelTransform = transform.Find("Model");
                }
            }

            if (modelTransform != null)
            {
                modelTransform.localPosition = modelOffset;
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                hipsTransform = animator.transform.Find("mixamorig:Hips");
            }
            if (hipsTransform == null)
            {
                hipsTransform = transform.Find("Model/mixamorig:Hips");
            }
            if (hipsTransform != null)
            {
                initialHipsLocalX = Mathf.Abs(hipsTransform.localPosition.x) > 0.2f ? 0f : hipsTransform.localPosition.x;
                initialHipsLocalZ = Mathf.Abs(hipsTransform.localPosition.z) > 0.2f ? 0f : hipsTransform.localPosition.z;
            }

            if (modelTransform != null)
            {
                if (neckBone == null)
                {
                    neckBone = modelTransform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck");
                }
                if (headBone == null)
                {
                    headBone = neckBone != null
                        ? neckBone.Find("mixamorig:Head")
                        : modelTransform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck/mixamorig:Head");
                }
            }

            RefreshAnimatorParams();
        }

        public void SetupCharacterModel(Transform newModelTransform, Animator newAnimator, SkinnedMeshRenderer newRenderer, Mesh customFppMesh = null, bool isFlying = false, Vector3 customModelOffset = default, Mesh customFullBodyMesh = null)
        {
            modelOffset = customModelOffset;
            isFlyingCharacter = isFlying;
            if (isFlyingCharacter)
            {
                verticalVelocity = 0f;
                isSliding = false;
            }

            modelTransform = newModelTransform;
            animator = newAnimator;
            characterRenderer = newRenderer;
            fppCustomMesh = customFppMesh;

            if (customFullBodyMesh != null)
            {
                fullBodyMesh = customFullBodyMesh;
            }
            else if (characterRenderer != null)
            {
                // Jangan timpa jika sharedMesh saat ini sedang berisi fppCustomMesh (tangan saja)
                if (fppCustomMesh == null || characterRenderer.sharedMesh != fppCustomMesh)
                {
                    fullBodyMesh = characterRenderer.sharedMesh;
                }
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            if (modelTransform != null)
            {
                modelTransform.localPosition = modelOffset;
                hipsTransform = modelTransform.Find("mixamorig:Hips");
                if (hipsTransform == null)
                {
                    hipsTransform = modelTransform.Find("Armature/mixamorig:Hips");
                }
                if (hipsTransform == null && animator != null && animator.isHuman)
                {
                    hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
                }
                if (hipsTransform == null)
                {
                    hipsTransform = modelTransform.Find("Armature.001/Bone");
                }

                neckBone = modelTransform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck");
                if (neckBone == null)
                {
                    neckBone = modelTransform.Find("Armature/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck");
                }
                if (neckBone == null && animator != null && animator.isHuman)
                {
                    neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
                }

                if (neckBone != null)
                {
                    headBone = neckBone.Find("mixamorig:Head");
                }
                else
                {
                    headBone = modelTransform.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck/mixamorig:Head");
                    if (headBone == null)
                    {
                        headBone = modelTransform.Find("Armature/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:Neck/mixamorig:Head");
                    }
                }

                if (headBone == null && animator != null && animator.isHuman)
                {
                    headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                }

                if (neckBone == null)
                {
                    neckBone = modelTransform.Find("Armature.001/Bone/Bone.001/Bone.002/Bone.003/Bone.004");
                    if (neckBone != null)
                    {
                        headBone = neckBone.Find("Bone.005");
                    }
                }
            }

            if (hipsTransform != null)
            {
                bool isMixamo = hipsTransform.name.Contains("mixamorig");
                initialHipsLocalX = (isMixamo && Mathf.Abs(hipsTransform.localPosition.x) > 0.2f) ? 0f : hipsTransform.localPosition.x;
                initialHipsLocalZ = (isMixamo && Mathf.Abs(hipsTransform.localPosition.z) > 0.2f) ? 0f : hipsTransform.localPosition.z;
            }

            RefreshAnimatorParams();
            ApplyCameraPerspective();
        }

        public void RefreshAnimatorParams()
        {
            animatorParamHashes = new System.Collections.Generic.HashSet<int>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var param in animator.parameters)
                {
                    animatorParamHashes.Add(param.nameHash);
                }
            }
        }

        private void SetSafeFloat(int hash, float val)
        {
            if (animator != null && animatorParamHashes != null && animatorParamHashes.Contains(hash))
                animator.SetFloat(hash, val);
        }

        private void SetSafeBool(int hash, bool val)
        {
            if (animator != null && animatorParamHashes != null && animatorParamHashes.Contains(hash))
                animator.SetBool(hash, val);
        }

        private void SetSafeTrigger(int hash)
        {
            if (animator != null && animatorParamHashes != null && animatorParamHashes.Contains(hash))
                animator.SetTrigger(hash);
        }

        private Transform hipsTransform;
        private float initialHipsLocalX;
        private float initialHipsLocalZ;

        private Mesh fullBodyMesh;
        private Mesh fppCustomMesh;

        private void Start()
        {
            SetCursorLock(true);

            if (actualCameraTransform == null && cameraRoot != null)
            {
                actualCameraTransform = cameraRoot.Find("FirstPersonCamera");
            }
            if (characterRenderer == null)
            {
                characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }
            if (characterRenderer != null && (fullBodyMesh == null || (fppCustomMesh != null && fullBodyMesh == fppCustomMesh)))
            {
                if (fppCustomMesh == null || characterRenderer.sharedMesh != fppCustomMesh)
                {
                    fullBodyMesh = characterRenderer.sharedMesh;
                }
            }

            ApplyCameraPerspective();
        }

        private void OnDisable()
        {
            DisableGhostPhasing();
        }

        private void OnDestroy()
        {
            DisableGhostPhasing();
        }

        private void Update()
        {
            HandleCursorToggle();

            if (isDead)
            {
                CheckGrounded();
                ApplyGravityOnly();
                return;
            }

            if (isImmobilized)
            {
                if (isChokeLifted)
                {
                    HandleChokeLiftMovement();
                }
                else
                {
                    ApplyGravityOnly();
                    CheckGrounded();
                }
                HandleLook();
                UpdateAnimator();
                return;
            }

            if (isGhostPhasing)
            {
                ghostPhasingScanTimer -= Time.deltaTime;
                if (ghostPhasingScanTimer <= 0f)
                {
                    ghostPhasingScanTimer = 0.5f;
                    RefreshGhostPhasing();
                }
            }

            CheckGrounded();
            HandleLook();
            HandleSlideInput();
            HandleMovement();
            HandleJumpAndGravity();
            HandleSpecialActions();
            UpdateAnimator();
        }

        private void LateUpdate()
        {
            // Kunci posisi model agar tidak bergeser dari pivot controller
            if (modelTransform != null)
            {
                modelTransform.localPosition = modelOffset;
            }

            // Kunci posisi X dan Z lokal Hips agar animasi Mixamo yang memiliki perpindahan translasi (seperti Running Jump dan Slide)
            // tidak membuat model tubuh bergeser atau melesat jauh keluar dari collider CharacterController
            if (hipsTransform != null)
            {
                Vector3 current = hipsTransform.localPosition;
                hipsTransform.localPosition = new Vector3(initialHipsLocalX, current.y, initialHipsLocalZ);
            }

            // Saat First-Person, sembunyikan tulang leher dan kepala agar kepala/rambut/kerah tidak menghalangi kamera,
            // namun badan, tangan/lengan, dan kaki tetap terlihat di layar
            bool isFirstPerson = (cameraPerspective == CameraPerspective.FirstPerson);
            if (neckBone != null)
            {
                neckBone.localScale = isFirstPerson ? Vector3.zero : Vector3.one;
            }
            if (headBone != null)
            {
                headBone.localScale = isFirstPerson ? Vector3.zero : Vector3.one;
            }

            // Saat tubuh terangkat karena dicekik, tengadahkan leher sedikit ke atas/belakang menandakan reaksi tercekik
            if (isChokeLifted && neckBone != null && !isFirstPerson)
            {
                neckBone.localRotation *= Quaternion.Euler(-18f, 0f, 0f);
            }

            if (isGhostPhasing)
            {
                ClampAboveGround();
            }
        }

        private void HandleCursorToggle()
        {
            // Jangan ubah status kursor saat peta penuh sedang aktif
            if (Pucaci.UI.MinimapController.Instance != null && Pucaci.UI.MinimapController.Instance.IsFullMapOpen)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLock(!isCursorLocked);
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !isCursorLocked)
            {
                // Jangan kunci kursor jika klik mouse berada di atas elemen UI (UGUI / IMGUI)
                if (!IsPointerOverUI())
                {
                    SetCursorLock(true);
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetCursorLock(!isCursorLocked);
            }
            if (Input.GetMouseButtonDown(0) && !isCursorLocked)
            {
                if (!IsPointerOverUI())
                {
                    SetCursorLock(true);
                }
            }
#endif
        }

        private bool IsPointerOverUI()
        {
            // 1. Cek UGUI (Canvas UI seperti Button, Slider, Panel)
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

            // 2. Cek IMGUI OnGUI (seperti NetworkManagerHUD dan GameplayExitMenu)
            Vector2 mousePos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mousePos = Mouse.current.position.ReadValue();
            }
            else
            {
                mousePos = Input.mousePosition;
            }
#else
            mousePos = Input.mousePosition;
#endif

            // Koordinat GUI OnGUI memiliki Y = 0 di atas layar
            float guiY = Screen.height - mousePos.y;
            float guiX = mousePos.x;

            // Area NetworkManagerHUD (kiri atas: x 0..260, y 0..500)
            if (guiX >= 0f && guiX <= 260f && guiY >= 0f && guiY <= 500f)
            {
                return true;
            }

            // Area GameplayExitMenu (kanan atas: x (Screen.width - 340)..Screen.width, y 0..70)
            if (guiX >= (Screen.width - 340f) && guiX <= Screen.width && guiY >= 0f && guiY <= 70f)
            {
                return true;
            }

            return false;
        }

        private void SetCursorLock(bool locked)
        {
            isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private float jumpCooldownTimer;

        private void CheckGrounded()
        {
            if (jumpCooldownTimer > 0f)
            {
                jumpCooldownTimer -= Time.deltaTime;
                isGrounded = false;
                return;
            }

            // Gunakan isGrounded bawaan dari CharacterController
            if (controller.isGrounded)
            {
                isGrounded = true;
                return;
            }

            // Backup raycast dari atas telapak kaki ke bawah tanah (menghindari collider karakter sendiri)
            Vector3 rayStart = transform.position + Vector3.up * 0.2f;
            float rayDistance = 0.35f;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistance, groundLayers, QueryTriggerInteraction.Ignore);
            bool foundGround = false;
            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider != controller && !hit.collider.transform.IsChildOf(transform))
                {
                    if (isGhostPhasing && !IsGroundCollider(hit.collider) && !IsHouseCollider(hit.collider)) continue;
                    foundGround = true;
                    break;
                }
            }

            isGrounded = foundGround;
        }

        private void HandleLook()
        {
            if (!isCursorLocked) return;
            if (Pucaci.UI.MinimapController.Instance != null && Pucaci.UI.MinimapController.Instance.IsFullMapOpen) return;

            Vector2 lookInput = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                lookInput = Mouse.current.delta.ReadValue() * 0.1f;
            }
            if (Gamepad.current != null)
            {
                lookInput += Gamepad.current.rightStick.ReadValue() * (lookSensitivity * 50f * Time.deltaTime);
            }
#else
            lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif

            // Horizontal Yaw (rotasi karakter)
            float yaw = lookInput.x * lookSensitivity;
            transform.Rotate(Vector3.up * yaw);

            // Vertical Pitch (rotasi kamera mata)
            if (cameraRoot != null)
            {
                cameraPitch -= lookInput.y * lookSensitivity;
                cameraPitch = Mathf.Clamp(cameraPitch, bottomClamp, topClamp);
                cameraRoot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            Vector2 moveInput = GetMoveInput();
            if (Pucaci.UI.MinimapController.Instance != null && Pucaci.UI.MinimapController.Instance.IsFullMapOpen)
            {
                moveInput = Vector2.zero;
            }
            bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;
            bool isSprintInput = !isFlyingCharacter && GetSprintInput() && hasMoveInput && isGrounded;

            // Pemrosesan Stamina & Kondisi Capek (Exhausted)
            bool isSprint = false;
            if (isSprintInput && !isExhausted && currentStamina > 0f)
            {
                isSprint = true;
                currentStamina = Mathf.Max(0f, currentStamina - sprintStaminaDrain * Time.deltaTime);
                staminaRegenCooldown = staminaRegenDelay;

                // Jika stamina habis, masuk ke status lelah (cukup jalan kaki biasa, tanpa tersengkal-sengkal)
                if (currentStamina <= 0f)
                {
                    isExhausted = true;
                    isSprint = false;
                }
            }
            else
            {
                // Pemulihan stamina saat tidak sprint
                if (staminaRegenCooldown > 0f)
                {
                    staminaRegenCooldown -= Time.deltaTime;
                }
                else
                {
                    currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
                    // Pulih dari status lelah jika sudah mencapai ambang batas
                    if (isExhausted && currentStamina >= (maxStamina * exhaustionRecoveryThreshold))
                    {
                        isExhausted = false;
                    }
                }
            }

            // Tangani state sliding
            if (isSliding)
            {
                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0f || !isGrounded)
                {
                    StopSlide();
                }
                else
                {
                    // Tetap meluncur ke arah slideDirection dengan pelambatan perlahan
                    float currentSlide = Mathf.Lerp(slideSpeed, walkSpeed, 1f - (slideTimer / slideDuration));
                    controller.Move((slideDirection * currentSlide + Vector3.up * verticalVelocity) * Time.deltaTime);
                    currentHorizontalSpeed = currentSlide;
                    return;
                }
            }

            if (hasMoveInput)
            {
                float currentTargetFlightSpeed = isGhostSpeedBoosted ? ghostFlightSpeed : flightSpeed;
                float targetSpeed = isFlyingCharacter ? currentTargetFlightSpeed : (isSprint ? sprintSpeed : walkSpeed);
                currentHorizontalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * accelerationRate);
                
                Vector3 moveDirection;
                if (isFlyingCharacter)
                {
                    Transform aimRef = cameraRoot != null ? cameraRoot : transform;
                    // Terbang 3D penuh mengikuti arah pandang crosshair kamera:
                    // W = terbang ke arah crosshair (ke atas jika lihat atas, ke bawah jika lihat bawah)
                    // S = terbang mundur menjauhi crosshair
                    // A / D = strafe kiri / kanan relatif terhadap pandangan kamera
                    moveDirection = (aimRef.right * moveInput.x + aimRef.forward * moveInput.y).normalized;
                }
                else
                {
                    moveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
                }

                Vector3 finalMove = moveDirection * (currentHorizontalSpeed * Time.deltaTime);
                if (isFlyingCharacter)
                {
                    // Tambahkan vertical velocity tambahan jika menekan Space atau Ctrl
                    finalMove.y += verticalVelocity * Time.deltaTime;
                }
                else
                {
                    finalMove.y = verticalVelocity * Time.deltaTime;
                }
                controller.Move(finalMove);
            }
            else
            {
                // Berhenti seketika saat tombol dilepas: tidak ada gerakan sisa atau deselerasi berlebih
                currentHorizontalSpeed = 0f;
                Vector3 finalMove = Vector3.up * (verticalVelocity * Time.deltaTime);
                controller.Move(finalMove);
            }
        }

        private void HandleJumpAndGravity()
        {
            if (isFlyingCharacter)
            {
                HandleFlightVerticalMovement();
                return;
            }

            // Jika di tanah dan sedang bergerak ke bawah / diam, kunci kecepatan vertikal kecil ke bawah agar menempel tanah
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                // Gravitasi WAJIB selalu mengurangi kecepatan vertikal setiap frame saat di udara
                verticalVelocity += gravity * Time.deltaTime;
                // Batasi kecepatan jatuh maksimal (terminal velocity)
                if (verticalVelocity < -35f)
                {
                    verticalVelocity = -35f;
                }
            }

            // Eksekusi lompat jika berada di tanah
            if (isGrounded && GetJumpInput() && !isSliding && jumpCooldownTimer <= 0f)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpCooldownTimer = 0.2f; // Cegah deteksi tanah sesaat setelah melompat
                isGrounded = false;
                currentStamina = Mathf.Max(0f, currentStamina - jumpStaminaCost);
                staminaRegenCooldown = staminaRegenDelay;

                SetSafeTrigger(JumpHash);
            }
        }

        private void HandleFlightVerticalMovement()
        {
            float targetVertical = 0f;
            bool flyUp = GetFlyUpInput();
            bool flyDown = GetFlyDownInput();

            if (flyUp && !flyDown)
            {
                targetVertical = flightVerticalSpeed;
            }
            else if (flyDown && !flyUp)
            {
                targetVertical = -flightVerticalSpeed;
            }
            else
            {
                targetVertical = 0f;
            }

            verticalVelocity = Mathf.MoveTowards(verticalVelocity, targetVertical, Time.deltaTime * flightAcceleration);

            // Jika menyentuh tanah dan sedang terbang ke bawah, tahan agar tidak menembus tanah
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = 0f;
            }
        }

        private void HandleSlideInput()
        {
            if (isFlyingCharacter) return;

            if (GetSlideInput() && isGrounded && !isSliding && currentHorizontalSpeed > walkSpeed * 0.8f)
            {
                StartSlide();
            }
        }

        private void StartSlide()
        {
            isSliding = true;
            slideTimer = slideDuration;
            currentStamina = Mathf.Max(0f, currentStamina - slideStaminaCost);
            staminaRegenCooldown = staminaRegenDelay;

            Vector2 moveInput = GetMoveInput();
            if (moveInput.sqrMagnitude > 0.1f)
            {
                slideDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            }
            else if (lastMoveDirection.sqrMagnitude > 0.01f)
            {
                slideDirection = lastMoveDirection;
            }
            else
            {
                slideDirection = transform.forward;
            }

            // Perkecil ukuran controller saat meluncur
            controller.height = slideControllerHeight;
            controller.center = new Vector3(originalControllerCenter.x, slideControllerHeight * 0.5f, originalControllerCenter.z);

            SetSafeTrigger(SlideHash);
        }

        private void StopSlide()
        {
            isSliding = false;
            controller.height = originalControllerHeight;
            controller.center = originalControllerCenter;
        }

        private void HandleSpecialActions()
        {
            // Look Back (tahan Q atau E untuk melihat ke belakang saat berlari)
            bool lookBack = GetLookBackInput();
            SetSafeBool(LookBackHash, lookBack);

            // Toggle Sudut Pandang Kamera (V)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                ToggleCameraPerspective();
            }
#else
            if (Input.GetKeyDown(KeyCode.V))
            {
                ToggleCameraPerspective();
            }
#endif

            // Toggle Senter (F)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                ToggleFlashlight();
            }
#else
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleFlashlight();
            }
#endif

            // Tombol uji coba aksi khusus (Stumble = G, Death = H, GetUp = J)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.gKey.wasPressedThisFrame) TriggerStumble();
                if (Keyboard.current.hKey.wasPressedThisFrame) TriggerDeath();
                if (Keyboard.current.jKey.wasPressedThisFrame) TriggerGetUp();
            }
#else
            if (Input.GetKeyDown(KeyCode.G)) TriggerStumble();
            if (Input.GetKeyDown(KeyCode.H)) TriggerDeath();
            if (Input.GetKeyDown(KeyCode.J)) TriggerGetUp();
#endif
        }

        public void ToggleCameraPerspective()
        {
            cameraPerspective = (cameraPerspective == CameraPerspective.ThirdPerson)
                ? CameraPerspective.FirstPerson
                : CameraPerspective.ThirdPerson;

            ApplyCameraPerspective();
        }

        public void SetCameraPerspective(CameraPerspective perspective)
        {
            cameraPerspective = perspective;
            ApplyCameraPerspective();
        }

        private void ApplyCameraPerspective()
        {
            if (actualCameraTransform == null && cameraRoot != null)
            {
                actualCameraTransform = cameraRoot.Find("FirstPersonCamera");
            }

            if (actualCameraTransform == null) return;

            if (cameraPerspective == CameraPerspective.ThirdPerson)
            {
                actualCameraTransform.localPosition = thirdPersonOffset;
            }
            else
            {
                Vector3 targetFppOffset = firstPersonOffset;
                var modelMgr = GetComponent<CharacterModelManager>();
                if (modelMgr != null && modelMgr.ActiveCharacterType == CharacterType.PakMamat)
                {
                    targetFppOffset = pakMamatFirstPersonOffset;
                }
                actualCameraTransform.localPosition = targetFppOffset;
            }

            // Perbarui mesh sesuai sudut pandang kamera (misal: Pak Mamat hanya menampilkan tangan di FPP)
            if (characterRenderer != null)
            {
                if (cameraPerspective == CameraPerspective.FirstPerson && fppCustomMesh != null)
                {
                    characterRenderer.sharedMesh = fppCustomMesh;
                }
                else
                {
                    // Di mode Third-Person, pastikan selalu menggunakan full-body mesh utuh
                    if (fullBodyMesh == null || (fppCustomMesh != null && fullBodyMesh == fppCustomMesh))
                    {
                        var modelMgr = GetComponent<CharacterModelManager>();
                        if (modelMgr != null)
                        {
                            fullBodyMesh = modelMgr.GetOriginalFullBodyMesh(modelMgr.ActiveCharacterType);
                        }
                    }

                    if (fullBodyMesh != null && (fppCustomMesh == null || fullBodyMesh != fppCustomMesh))
                    {
                        characterRenderer.sharedMesh = fullBodyMesh;
                    }
                }

                var childOutline = characterRenderer.transform.Find("ToonOutline_Backface")?.GetComponent<SkinnedMeshRenderer>();
                if (childOutline != null)
                {
                    childOutline.sharedMesh = characterRenderer.sharedMesh;
                }
            }

            if (modelTransform != null)
            {
                var allRenderers = modelTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var r in allRenderers)
                {
                    if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        public void ToggleFlashlight()
        {
            var fc = GetComponent<FlashlightController>();
            if (fc != null)
            {
                fc.ToggleFlashlight();
                return;
            }

            if (flashlight != null)
            {
                flashlight.enabled = !flashlight.enabled;
            }
        }

        private void ApplyGravityOnly()
        {
            if (!isGrounded)
            {
                verticalVelocity += gravity * Time.deltaTime;
                controller.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
            }
            else
            {
                // Memberikan sedikit tekanan ke bawah agar CharacterController stabil di lantai
                verticalVelocity = -2f;
                controller.Move(Vector3.up * (verticalVelocity * Time.deltaTime));
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            // Langsung deteksi apakah tombol gerakan sedang ditekan
            Vector2 moveInput = GetMoveInput();
            bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;
            bool isMoving = isFlyingCharacter 
                ? (hasMoveInput || Mathf.Abs(verticalVelocity) > 0.15f) 
                : (hasMoveInput && isGrounded);
            bool isSprint = !isFlyingCharacter && GetSprintInput();
            float normalizedSpeed = isMoving ? (isFlyingCharacter ? 1.0f : Mathf.Clamp01(currentHorizontalSpeed / sprintSpeed)) : 0f;

            // Skala target gerakan: Berjalan = 0.5f, Berlari/Sprint = 1.0f
            float speedScale = isSprint ? 1.0f : 0.5f;
            float targetMoveX = isMoving ? moveInput.x * speedScale : 0f;
            float targetMoveZ = isMoving ? moveInput.y * speedScale : 0f;

            // Haluskan transisi gerakan secara mulus
            // Saat bergerak lerp cepat dan responsif (10f), saat berhenti redam halus (8f) agar transisi ke idle tidak kaku
            float smoothRate = isMoving ? 10f : 8f;
            currentMoveX = Mathf.MoveTowards(currentMoveX, targetMoveX, Time.deltaTime * smoothRate);
            currentMoveZ = Mathf.MoveTowards(currentMoveZ, targetMoveZ, Time.deltaTime * smoothRate);

            SetSafeFloat(SpeedHash, normalizedSpeed);
            SetSafeFloat(MoveXHash, currentMoveX);
            SetSafeFloat(MoveZHash, currentMoveZ);
            SetSafeFloat(MoveXSpaceHash, currentMoveX);
            SetSafeFloat(MoveZSpaceHash, currentMoveZ);
            SetSafeBool(IsMovingHash, isMoving);
            SetSafeBool(IsGroundedHash, isFlyingCharacter ? true : isGrounded);
            SetSafeBool(IsSlidingHash, isFlyingCharacter ? false : isSliding);
        }

        /// <summary>
        /// Dipanggil dari PlayerNetworkSetup pada client remote untuk menyinkronkan animasi gerakan
        /// berdasarkan perubahan posisi nyata yang diterima dari NetworkTransform.
        /// </summary>
        public void UpdateRemoteMovement(Vector3 worldDelta, float dt)
        {
            if (animator == null) return;
            if (isDead) return;

            // Kunci posisi model agar tidak bergeser dari pivot controller
            if (modelTransform != null)
            {
                modelTransform.localPosition = modelOffset;
            }

            if (hipsTransform != null)
            {
                Vector3 current = hipsTransform.localPosition;
                hipsTransform.localPosition = new Vector3(initialHipsLocalX, current.y, initialHipsLocalZ);
            }

            float safeDt = (dt > 0.0001f) ? dt : 0.016f;
            Vector3 worldVelocity = worldDelta / safeDt;
            float rawSpeed = worldVelocity.magnitude;
            Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

            bool isMoving = isFlyingCharacter ? (rawSpeed > 0.15f) : (rawSpeed > 0.2f);
            float normalizedSpeed = isMoving ? Mathf.Clamp01(rawSpeed / sprintSpeed) : 0f;

            float targetMoveX = isMoving ? Mathf.Clamp(localVelocity.x / walkSpeed, -1f, 1f) : 0f;
            float targetMoveZ = isMoving ? Mathf.Clamp(localVelocity.z / walkSpeed, -1f, 1f) : 0f;

            float smoothRate = isMoving ? 12f : 8f;
            currentMoveX = Mathf.MoveTowards(currentMoveX, targetMoveX, safeDt * smoothRate);
            currentMoveZ = Mathf.MoveTowards(currentMoveZ, targetMoveZ, safeDt * smoothRate);

            SetSafeFloat(SpeedHash, normalizedSpeed);
            SetSafeFloat(MoveXHash, currentMoveX);
            SetSafeFloat(MoveZHash, currentMoveZ);
            SetSafeFloat(MoveXSpaceHash, currentMoveX);
            SetSafeFloat(MoveZSpaceHash, currentMoveZ);
            SetSafeBool(IsMovingHash, isMoving);
            SetSafeBool(IsGroundedHash, true);
            SetSafeBool(IsSlidingHash, false);
        }

        #region Public Action Triggers
        public void TriggerStumble()
        {
            SetSafeTrigger(StumbleHash);
            currentHorizontalSpeed *= 0.3f;
        }

        public void TriggerDeath()
        {
            isDead = true;
            isImmobilized = false;
            isChokeLifted = false;
            chokeLiftAmount = 0f;
            verticalVelocity = 0f;

            // Turunkan langsung ke permukaan tanah seketika agar tidak tergeletak mengambang di udara
            SnapToGround();

            // Perkecil ukuran kapsul CharacterController saat mati agar badan tergeletak pas di tanah
            // dan tidak menjadi tembok penghalang bagi pemain lain
            if (controller != null)
            {
                controller.height = 0.35f;
                controller.center = new Vector3(originalControllerCenter.x, 0.175f, originalControllerCenter.z);
            }

            SetSafeTrigger(DeathHash);
            SetSafeTrigger(DieHash);
        }

        public void TriggerGetUp()
        {
            if (isDead)
            {
                isDead = false;
                isImmobilized = false;

                // Kembalikan ukuran normal CharacterController saat bangkit kembali
                if (controller != null)
                {
                    controller.height = originalControllerHeight;
                    controller.center = originalControllerCenter;
                }

                SetSafeTrigger(GetUpHash);
            }
        }

        /// <summary>
        /// Menjatuhkan / menempelkan karakter seketika ke permukaan tanah di bawahnya.
        /// Sangat penting saat dilepas dari cekikan atau saat mati di udara agar tidak mengambang.
        /// </summary>
        public void SnapToGround()
        {
            isChokeLifted = false;
            chokeLiftAmount = 0f;
            verticalVelocity = 0f;

            Vector3 rayStart = transform.position + Vector3.up * 0.5f;
            float rayDistance = 15f;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistance, groundLayers, QueryTriggerInteraction.Ignore);
            RaycastHit? bestHit = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider != controller && !hit.collider.transform.IsChildOf(transform))
                {
                    // Abaikan collider milik karakter lain (misalnya CharacterController Kuntilanak)
                    if (hit.collider is CharacterController) continue;

                    if (hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        bestHit = hit;
                    }
                }
            }

            if (bestHit.HasValue)
            {
                Vector3 targetPos = transform.position;
                targetPos.y = bestHit.Value.point.y;
                Vector3 delta = targetPos - transform.position;

                if (controller != null && controller.enabled)
                {
                    controller.Move(delta);
                }
                if (Vector3.Distance(transform.position, targetPos) > 0.05f)
                {
                    transform.position = targetPos;
                }
                isGrounded = true;
            }
        }

        public void SetImmobilized(bool immobilized)
        {
            isImmobilized = immobilized;
            if (immobilized)
            {
                currentHorizontalSpeed = 0f;
                currentMoveX = 0f;
                currentMoveZ = 0f;
                isSliding = false;
            }
        }

        public void SetChokeLifted(bool lifted, float height = 0.55f)
        {
            isChokeLifted = lifted;
            targetChokeLift = height;
            if (!lifted)
            {
                chokeLiftAmount = 0f;
            }
            else
            {
                isImmobilized = true;
                currentHorizontalSpeed = 0f;
                currentMoveX = 0f;
                currentMoveZ = 0f;
                isSliding = false;
                verticalVelocity = 0f;
                chokeHitStruggleTimer = 0.6f;
            }
        }

        private void HandleChokeLiftMovement()
        {
            // Pemicu reaksi kesakitan/meronta pada animator korban saat digantung di udara
            chokeHitStruggleTimer -= Time.deltaTime;
            if (chokeHitStruggleTimer <= 0f)
            {
                chokeHitStruggleTimer = 1.2f;
                SetSafeTrigger(HitHash);
            }

            // Karakter di udara (kaki menggantung / tidak menempel tanah, posisi di-drive oleh Kuntilanak)
            isGrounded = false;
            verticalVelocity = 0f;
        }
        #endregion

        #region Input Helpers
        private Vector2 GetMoveInput()
        {
            Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
            }
            if (Gamepad.current != null)
            {
                input += Gamepad.current.leftStick.ReadValue();
            }
#else
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");
#endif
            return Vector2.ClampMagnitude(input, 1f);
        }

        private bool GetSprintInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) return true;
            if (Gamepad.current != null && (Gamepad.current.leftTrigger.isPressed || Gamepad.current.leftStickButton.isPressed)) return true;
#else
            if (Input.GetKey(KeyCode.LeftShift)) return true;
#endif
            return false;
        }

        private bool GetJumpInput()
        {
            if (isFlyingCharacter) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
#else
            if (Input.GetKeyDown(KeyCode.Space)) return true;
#endif
            return false;
        }

        private bool GetFlyUpInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) return true;
#else
            if (Input.GetKey(KeyCode.Space)) return true;
#endif
            return false;
        }

        private bool GetFlyDownInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed)) return true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.isPressed) return true;
#else
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) return true;
#endif
            return false;
        }

        private bool GetSlideInput()
        {
            if (isFlyingCharacter) return false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame)) return true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) return true;
#else
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C)) return true;
#endif
            return false;
        }

        private bool GetLookBackInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.xKey.isPressed) return true;
            if (Mouse.current != null && Mouse.current.middleButton.isPressed) return true;
#else
            if (Input.GetKey(KeyCode.X) || Input.GetMouseButton(2)) return true;
#endif
            return false;
        }
        #endregion
    }
}
