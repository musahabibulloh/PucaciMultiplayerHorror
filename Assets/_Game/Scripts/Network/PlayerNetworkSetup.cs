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
        [SerializeField] private GameObject cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private SkinnedMeshRenderer characterRenderer;

        [Header("Name Tag (Billboard Overhead)")]
        [SerializeField] private GameObject nameTagObject;
        [SerializeField] private TextMesh nameTagText;

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<PlayerCharacterController>();
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
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // Aktifkan kontrol, kamera, dan audio listener untuk pemain lokal
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
        }

        private void OnDestroy()
        {
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

        private void CreateOverheadNameTag()
        {
            nameTagObject = new GameObject("OverheadNameTag");
            nameTagObject.transform.SetParent(transform, false);
            nameTagObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            nameTagText = nameTagObject.AddComponent<TextMesh>();
            nameTagText.text = $"Pemain {netId}";
            nameTagText.characterSize = 0.08f;
            nameTagText.fontSize = 28;
            nameTagText.anchor = TextAnchor.MiddleCenter;
            nameTagText.alignment = TextAlignment.Center;
            nameTagText.color = new Color(1f, 0.9f, 0.2f, 1f); // Kuning terang agar mudah dibaca di malam hari
        }

        private void LateUpdate()
        {
            // Pastikan label nama pemain lain selalu menghadap kamera aktif
            if (!isLocalPlayer && nameTagObject != null && nameTagObject.activeSelf)
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
    }
}
