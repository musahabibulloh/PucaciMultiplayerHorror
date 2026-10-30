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

        public CameraPerspective CurrentPerspective => cameraPerspective;

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

        [Header("Idle / AFK")]
        [Tooltip("Detik tanpa aktivitas sebelum animasi idle AFK menyala")]
        [SerializeField] private float afkDelay = 3f;

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

        // Timer idle AFK
        private float idleTimer;

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
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        private static readonly int LookBackHash = Animator.StringToHash("LookBack");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int SlideHash = Animator.StringToHash("Slide");
        private static readonly int StumbleHash = Animator.StringToHash("Stumble");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int GetUpHash = Animator.StringToHash("GetUp");
        private static readonly int IsAfkHash = Animator.StringToHash("IsAfk");

        // Parameter penghalusan gerakan animasi
        private float currentMoveX;
        private float currentMoveZ;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            originalControllerHeight = controller.height;
            originalControllerCenter = controller.center;

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
        }

        private Transform hipsTransform;
        private float initialHipsLocalX;
        private float initialHipsLocalZ;

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

            ApplyCameraPerspective();
        }

        private void Update()
        {
            HandleCursorToggle();

            if (isDead)
            {
                ApplyGravityOnly();
                return;
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
        }

        private void HandleCursorToggle()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLock(!isCursorLocked);
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !isCursorLocked)
            {
                SetCursorLock(true);
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetCursorLock(!isCursorLocked);
            }
            if (Input.GetMouseButtonDown(0) && !isCursorLocked)
            {
                SetCursorLock(true);
            }
#endif
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
                    foundGround = true;
                    break;
                }
            }

            isGrounded = foundGround;
        }

        private void HandleLook()
        {
            if (!isCursorLocked) return;

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
            bool isSprint = GetSprintInput();

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

            bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;

            if (hasMoveInput)
            {
                float targetSpeed = isSprint ? sprintSpeed : walkSpeed;
                currentHorizontalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * accelerationRate);
                Vector3 moveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
                Vector3 finalMove = moveDirection * (currentHorizontalSpeed * Time.deltaTime);
                finalMove.y = verticalVelocity * Time.deltaTime;
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

                if (animator != null)
                {
                    animator.SetTrigger(JumpHash);
                }
            }
        }

        private void HandleSlideInput()
        {
            if (GetSlideInput() && isGrounded && !isSliding && currentHorizontalSpeed > walkSpeed * 0.8f)
            {
                StartSlide();
            }
        }

        private void StartSlide()
        {
            isSliding = true;
            slideTimer = slideDuration;

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

            if (animator != null)
            {
                animator.SetTrigger(SlideHash);
            }
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
            if (animator != null)
            {
                animator.SetBool(LookBackHash, lookBack);
            }

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
                actualCameraTransform.localPosition = firstPersonOffset;
            }

            // Selalu aktifkan ShadowCastingMode.On agar lengan, tangan, dan tubuh tetap terlihat di layar
            if (characterRenderer != null)
            {
                characterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
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
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            // Langsung deteksi apakah tombol gerakan sedang ditekan
            Vector2 moveInput = GetMoveInput();
            bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;
            bool isMoving = hasMoveInput && isGrounded;
            bool isSprint = GetSprintInput();
            float normalizedSpeed = isMoving ? Mathf.Clamp01(currentHorizontalSpeed / sprintSpeed) : 0f;

            // Skala target gerakan: Berjalan = 0.5f, Berlari/Sprint = 1.0f
            float speedScale = isSprint ? 1.0f : 0.5f;
            float targetMoveX = isMoving ? moveInput.x * speedScale : 0f;
            float targetMoveZ = isMoving ? moveInput.y * speedScale : 0f;

            // Haluskan transisi gerakan secara mulus
            // Saat bergerak lerp cepat dan responsif (10f), saat berhenti redam halus (8f) agar transisi ke idle tidak kaku
            float smoothRate = isMoving ? 10f : 8f;
            currentMoveX = Mathf.MoveTowards(currentMoveX, targetMoveX, Time.deltaTime * smoothRate);
            currentMoveZ = Mathf.MoveTowards(currentMoveZ, targetMoveZ, Time.deltaTime * smoothRate);

            // Hitung waktu diam: reset jika bergerak, di udara, slide, atau look back
            bool isIdleNow = !hasMoveInput
                             && isGrounded
                             && !isSliding
                             && !GetLookBackInput()
                             && Mathf.Abs(currentMoveX) < 0.01f
                             && Mathf.Abs(currentMoveZ) < 0.01f;

            if (isIdleNow) idleTimer += Time.deltaTime;
            else idleTimer = 0f;

            animator.SetBool(IsAfkHash, idleTimer >= afkDelay);

            animator.SetFloat(SpeedHash, normalizedSpeed);
            animator.SetFloat(MoveXHash, currentMoveX);
            animator.SetFloat(MoveZHash, currentMoveZ);
            animator.SetBool(IsMovingHash, isMoving);
            animator.SetBool(IsGroundedHash, isGrounded);
            animator.SetBool(IsSlidingHash, isSliding);
        }

        #region Public Action Triggers
        public void TriggerStumble()
        {
            idleTimer = 0f;
            if (animator != null)
            {
                animator.SetTrigger(StumbleHash);
            }
            currentHorizontalSpeed *= 0.3f;
        }

        public void TriggerDeath()
        {
            idleTimer = 0f;
            isDead = true;
            if (animator != null)
            {
                animator.SetBool(IsAfkHash, false);
                animator.SetTrigger(DeathHash);
            }
        }

        public void TriggerGetUp()
        {
            if (isDead)
            {
                isDead = false;
                if (animator != null)
                {
                    animator.SetTrigger(GetUpHash);
                }
            }
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
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
#else
            if (Input.GetKeyDown(KeyCode.Space)) return true;
#endif
            return false;
        }

        private bool GetSlideInput()
        {
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
            if (Keyboard.current != null && (Keyboard.current.qKey.isPressed || Keyboard.current.eKey.isPressed)) return true;
            if (Mouse.current != null && Mouse.current.middleButton.isPressed) return true;
#else
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E) || Input.GetMouseButton(2)) return true;
#endif
            return false;
        }
        #endregion
    }
}