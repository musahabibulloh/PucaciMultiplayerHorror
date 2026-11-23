using UnityEngine;
using Mirror;
using Pucaci.Gameplay;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    public enum SesajenInteractionMode
    {
        None = 0,
        Destroy = 1,
        Repair = 2,
        BlockedMustBeHuman = 3
    }

    /// <summary>
    /// Menangani interaksi sesajen:
    /// 1. Survivor: Mendekati dan menghancurkan sesajen yang masih utuh.
    /// 2. Impostor (Wujud Manusia): 
    ///    - BISA MENGHANCURKAN sesajen utuh (sebagai tipu daya/kamuflase agar teman setim bingung mencari impostor).
    ///    - BISA MEMPERBAIKI sesajen yang hancur.
    /// 3. Impostor (Wujud Kuntilanak): Dilarang memperbaiki sesajen (harus bertransformasi jadi manusia).
    /// </summary>
    public class PlayerSesajenInteractor : NetworkBehaviour
    {
        [Header("Settings")]
        [Tooltip("Jarak maksimal interaksi mata ke sesajen (meter)")]
        [SerializeField] private float interactRange = 2.8f;

        [Tooltip("Layer mask untuk mendeteksi sesajen")]
        [SerializeField] private LayerMask sesajenLayers = ~0;

        private CharacterModelManager modelManager;
        private PlayerRoleManager roleManager;
        private Camera playerCamera;
        private SesajenItem currentAimSesajen;

        private SesajenInteractionMode currentMode = SesajenInteractionMode.None;
        private bool isInteracting = false;
        private float interactionTimer = 0f;

        public SesajenItem CurrentAimSesajen => currentAimSesajen;
        public SesajenInteractionMode CurrentMode => currentMode;
        public bool IsInteracting => isInteracting;

        public float InteractionProgress
        {
            get
            {
                if (currentAimSesajen == null) return 0f;
                float duration = (currentMode == SesajenInteractionMode.Repair)
                    ? currentAimSesajen.RepairTime
                    : currentAimSesajen.DestructionTime;
                return duration > 0f ? Mathf.Clamp01(interactionTimer / duration) : 0f;
            }
        }

        private void Awake()
        {
            modelManager = GetComponent<CharacterModelManager>();
            roleManager = GetComponent<PlayerRoleManager>();
        }

        private void Start()
        {
            FindPlayerCamera();
        }

        private void FindPlayerCamera()
        {
            if (playerCamera != null) return;
            playerCamera = GetComponentInChildren<Camera>(true);
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void Update()
        {
            // Hanya aktif untuk pemain lokal
            bool isLocal = true;
            if (netIdentity != null && (NetworkClient.active || NetworkServer.active))
            {
                try { isLocal = isLocalPlayer; } catch { isLocal = true; }
            }
            if (!isLocal) return;

            if (playerCamera == null)
            {
                FindPlayerCamera();
                if (playerCamera == null) return;
            }

            DetectSesajenInCrosshair();
            HandleInteractionInput();
        }

        private void DetectSesajenInCrosshair()
        {
            SesajenItem detected = null;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, sesajenLayers, QueryTriggerInteraction.Collide))
            {
                var sesajen = hit.collider.GetComponentInParent<SesajenItem>();
                if (sesajen != null)
                {
                    detected = sesajen;
                }
            }

            // Fallback: deteksi lingkaran jika raycast tipis luput
            if (detected == null)
            {
                Collider[] overlaps = Physics.OverlapSphere(transform.position + transform.forward * 1.0f + Vector3.up * 0.4f, 1.4f, sesajenLayers, QueryTriggerInteraction.Collide);
                foreach (var col in overlaps)
                {
                    var sesajen = col.GetComponentInParent<SesajenItem>();
                    if (sesajen != null)
                    {
                        detected = sesajen;
                        break;
                    }
                }
            }

            if (detected == null)
            {
                ResetInteraction();
                currentAimSesajen = null;
                currentMode = SesajenInteractionMode.None;
                return;
            }

            currentAimSesajen = detected;
            EvaluateInteractionMode();
        }

        private void EvaluateInteractionMode()
        {
            if (currentAimSesajen == null)
            {
                currentMode = SesajenInteractionMode.None;
                return;
            }

            if (roleManager == null) roleManager = GetComponent<PlayerRoleManager>();
            bool isImpostor = (roleManager != null && roleManager.IsImpostor);
            bool isHumanForm = (roleManager != null && roleManager.IsHumanForm);

            if (!isImpostor)
            {
                // SURVIVOR: Hanya bisa menghancurkan sesajen yang belum hancur
                if (!currentAimSesajen.IsDestroyed)
                {
                    currentMode = SesajenInteractionMode.Destroy;
                }
                else
                {
                    currentMode = SesajenInteractionMode.None;
                }
            }
            else
            {
                // IMPOSTOR
                if (currentAimSesajen.IsDestroyed)
                {
                    if (isHumanForm)
                    {
                        // Impostor dalam wujud manusia: BISA MEMPERBAIKI SESAJEN
                        currentMode = SesajenInteractionMode.Repair;
                    }
                    else
                    {
                        // Impostor dalam wujud Kuntilanak: DILARANG MEMPERBAIKI
                        currentMode = SesajenInteractionMode.BlockedMustBeHuman;
                    }
                }
                else
                {
                    // Sesajen masih utuh
                    if (isHumanForm)
                    {
                        // Impostor dalam wujud manusia: BISA MENGHANCURKAN SESAJEN
                        // Gunanya sebagai kamuflase/tipuan agar rekan satu tim mengira ia adalah survivor biasa
                        currentMode = SesajenInteractionMode.Destroy;
                    }
                    else
                    {
                        // Impostor dalam wujud Kuntilanak: Tidak menghancurkan sesajen dalam wujud hantu
                        currentMode = SesajenInteractionMode.None;
                    }
                }
            }
        }

        private void HandleInteractionInput()
        {
            if (currentAimSesajen == null || currentMode == SesajenInteractionMode.None || currentMode == SesajenInteractionMode.BlockedMustBeHuman)
            {
                ResetInteraction();
                return;
            }

            bool isHoldingE = CheckInteractionButton();

            if (isHoldingE)
            {
                isInteracting = true;
                interactionTimer += Time.deltaTime;

                float targetDuration = (currentMode == SesajenInteractionMode.Repair)
                    ? currentAimSesajen.RepairTime
                    : currentAimSesajen.DestructionTime;

                if (interactionTimer >= targetDuration)
                {
                    if (currentMode == SesajenInteractionMode.Destroy)
                    {
                        currentAimSesajen.DestroySesajen();
                    }
                    else if (currentMode == SesajenInteractionMode.Repair)
                    {
                        currentAimSesajen.RepairSesajen();
                    }

                    ResetInteraction();
                    currentAimSesajen = null;
                    currentMode = SesajenInteractionMode.None;
                }
            }
            else
            {
                ResetInteraction();
            }
        }

        private void ResetInteraction()
        {
            isInteracting = false;
            interactionTimer = 0f;
        }

        private bool CheckInteractionButton()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.isPressed) return true;
#else
            if (Input.GetKey(KeyCode.E)) return true;
#endif
            return false;
        }
    }
}
