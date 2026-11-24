using Mirror;
using UnityEngine;
using Pucaci.Player;

namespace Pucaci.Network
{
    [DisallowMultipleComponent]
    public class PlayerNetworkSetup : NetworkBehaviour
    {
        [Header("Components to isolate")]
        [SerializeField] private PlayerCharacterController characterController;
        [SerializeField] private CharacterController unityCharacterController;
        [SerializeField] private GameObject cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private SkinnedMeshRenderer characterRenderer;
        [SerializeField] private SpectatorController spectatorController;

        [Header("Name Tag (Billboard Overhead)")]
        [SerializeField] private GameObject nameTagObject;
        [SerializeField] private TextMesh nameTagText;

        private Vector3 lastRemotePosition;
        private bool hasRecordedRemotePos = false;

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<PlayerCharacterController>();
            }

            if (unityCharacterController == null)
            {
                unityCharacterController = GetComponent<CharacterController>();
            }

            // Inisialisasi CapsuleCollider fisik tubuh (untuk blocking collision antar pemain di multiplayer)
            var bodyCol = GetComponent<CapsuleCollider>();
            if (bodyCol == null)
            {
                bodyCol = gameObject.AddComponent<CapsuleCollider>();
            }
            if (bodyCol != null && unityCharacterController != null)
            {
                bodyCol.height = unityCharacterController.height;
                bodyCol.radius = unityCharacterController.radius;
                bodyCol.center = unityCharacterController.center;
                bodyCol.isTrigger = false;
                bodyCol.enabled = false;
            }

            if (cameraRoot == null)
            {
                Transform camRootTransform = transform.Find("CameraRoot");
                if (camRootTransform != null)
                {
                    cameraRoot = camRootTransform.gameObject;
                }
            }

            if (playerCamera == null && cameraRoot != null)
            {
                playerCamera = cameraRoot.GetComponentInChildren<Camera>(true);
            }

            if (audioListener == null && cameraRoot != null)
            {
                audioListener = cameraRoot.GetComponentInChildren<AudioListener>(true);
            }

            if (characterRenderer == null)
            {
                characterRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }

            if (spectatorController == null)
            {
                spectatorController = GetComponent<SpectatorController>();
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // Aktifkan kontrol, CharacterController, kamera, dan audio listener untuk pemain lokal
            if (unityCharacterController != null)
            {
                unityCharacterController.enabled = true;
            }

            // Pada pemain lokal, matikan CapsuleCollider agar tidak ganda dengan CharacterController lokal
            var localBodyCol = GetComponent<CapsuleCollider>();
            if (localBodyCol != null)
            {
                localBodyCol.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            if (cameraRoot != null)
            {
                cameraRoot.SetActive(true);
            }

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                playerCamera.gameObject.SetActive(true);
            }

            if (audioListener != null)
            {
                audioListener.enabled = true;
            }

            // Inisialisasi sistem spectator untuk pemain lokal jika sewaktu-waktu mati
            if (spectatorController == null)
            {
                spectatorController = gameObject.AddComponent<SpectatorController>();
            }
            if (spectatorController != null)
            {
                spectatorController.Initialize(cameraRoot != null ? cameraRoot.transform : null, playerCamera);
            }

            var health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.OnDeathEvent += HandleLocalPlayerDeath;
                health.OnReviveEvent += HandleLocalPlayerRevive;
            }

            // Matikan SceneCamera / LobbyCamera jika ada di scene agar kamera beralih ke karakter lokal
            var sceneCam = GameObject.Find("SceneCamera");
            if (sceneCam != null)
            {
                sceneCam.SetActive(false);
            }

            // Sembunyikan name tag di atas kepala diri sendiri
            if (nameTagObject != null)
            {
                nameTagObject.SetActive(false);
            }

            // Daftarkan ke MinimapController jika ada
            if (Pucaci.UI.MinimapController.Instance != null)
            {
                Pucaci.UI.MinimapController.Instance.SetTarget(transform);
            }

            // Daftarkan ke StaminaUI jika ada
            if (Pucaci.UI.StaminaUI.Instance != null && characterController != null)
            {
                Pucaci.UI.StaminaUI.Instance.SetTarget(characterController);
            }
        }

        private void HandleLocalPlayerDeath()
        {
            if (isLocalPlayer && spectatorController != null)
            {
                spectatorController.StartSpectating();
            }
        }

        private void HandleLocalPlayerRevive()
        {
            if (isLocalPlayer && spectatorController != null)
            {
                spectatorController.StopSpectating();
            }
        }

        private void OnDestroy()
        {
            var health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.OnDeathEvent -= HandleLocalPlayerDeath;
                health.OnReviveEvent -= HandleLocalPlayerRevive;
            }

            if (isLocalPlayer)
            {
                var sceneCam = GameObject.Find("SceneCamera");
                if (sceneCam != null)
                {
                    sceneCam.SetActive(true);
                }
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isLocalPlayer)
            {
                DisableNonLocalComponents();
            }
        }

        private void Start()
        {
            if (!isLocalPlayer)
            {
                DisableNonLocalComponents();
            }
        }

        private void DisableNonLocalComponents()
        {
            // Matikan pergerakan lokal dan pembacaan input
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // Matikan CharacterController bawaan Unity agar NetworkTransform dapat memindahkan objek secara mulus tanpa tahanan fisika
            if (unityCharacterController == null)
            {
                unityCharacterController = GetComponent<CharacterController>();
            }

            // Pada remote player, aktifkan CapsuleCollider solid agar pemain lokal tidak bisa menembus tubuh pemain lain
            var remoteBodyCol = GetComponent<CapsuleCollider>();
            if (remoteBodyCol == null)
            {
                remoteBodyCol = gameObject.AddComponent<CapsuleCollider>();
            }
            if (remoteBodyCol != null && unityCharacterController != null)
            {
                remoteBodyCol.height = unityCharacterController.height;
                remoteBodyCol.radius = unityCharacterController.radius;
                remoteBodyCol.center = unityCharacterController.center;
                remoteBodyCol.isTrigger = false;
                remoteBodyCol.enabled = true;
            }
            if (unityCharacterController != null)
            {
                unityCharacterController.enabled = false;
            }

            // Matikan kamera dan audio listener untuk mencegah bentrok dan duplikat
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                playerCamera.gameObject.SetActive(false);
            }

            if (audioListener != null)
            {
                audioListener.enabled = false;
            }

            if (cameraRoot != null)
            {
                cameraRoot.SetActive(false);
            }

            // Pastikan mesh karakter terlihat oleh pemain lain (ShadowCastingMode.On)
            if (characterRenderer != null)
            {
                characterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            // Inisialisasi name tag jika belum ada
            if (nameTagObject == null)
            {
                CreateOverheadNameTag();
            }
            else
            {
                nameTagObject.SetActive(true);
                if (nameTagText != null)
                {
                    nameTagText.text = $"Pemain {netId}";
                }
            }
        }

        private void Update()
        {
            // Untuk karakter pemain lain di layar kita:
            // Hitung kecepatan dan translasi gerak nyata dari NetworkTransform
            // lalu update animasi (jalan/lari/idle/mundur/strafe) secara mulus
            if (!isLocalPlayer)
            {
                if (!hasRecordedRemotePos)
                {
                    lastRemotePosition = transform.position;
                    hasRecordedRemotePos = true;
                    return;
                }

                Vector3 delta = transform.position - lastRemotePosition;
                lastRemotePosition = transform.position;

                if (characterController != null)
                {
                    characterController.UpdateRemoteMovement(delta, Time.deltaTime);
                }
            }
        }

        private void CreateOverheadNameTag()
        {
            nameTagObject = new GameObject("OverheadNameTag");
            nameTagObject.transform.SetParent(transform, false);
            nameTagObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            nameTagText = nameTagObject.AddComponent<TextMesh>();
            string idStr = (netIdentity != null) ? $" {netId}" : "";
            nameTagText.text = $"Pemain{idStr}";
            nameTagText.characterSize = 0.08f;
            nameTagText.fontSize = 28;
            nameTagText.anchor = TextAnchor.MiddleCenter;
            nameTagText.alignment = TextAlignment.Center;
            nameTagText.color = new Color(1f, 0.9f, 0.2f, 1f); // Kuning terang agar mudah dibaca di malam hari
        }

        private void LateUpdate()
        {
            // Pastikan label nama pemain lain selalu menghadap kamera aktif
            bool isLocal = false;
            try { isLocal = isLocalPlayer; } catch { isLocal = true; }

            if (!isLocal && nameTagObject != null && nameTagObject.activeSelf)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 lookDir = nameTagObject.transform.position - cam.transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        nameTagObject.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
            }
        }

        public void UpdateNameTagText(string newText)
        {
            if (nameTagObject == null)
            {
                CreateOverheadNameTag();
            }

            if (nameTagText != null)
            {
                nameTagText.text = newText;
            }
        }

        public void UpdateNameTag(string newText) => UpdateNameTagText(newText);

        public void SetupRenderer(SkinnedMeshRenderer newRenderer)
        {
            characterRenderer = newRenderer;
            if (characterRenderer != null)
            {
                bool isLocal = false;
                try
                {
                    isLocal = isLocalPlayer;
                }
                catch
                {
                    isLocal = true;
                }

                if (!isLocal)
                {
                    var allRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var r in allRenderers)
                    {
                        if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }
        }
    }
}
