using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    /// <summary>
    /// Pengontrol mode Spectator (penonton) saat pemain lokal mati.
    /// Kamera akan otomatis beralih menyorot pemain lain yang masih hidup
    /// dan memungkinkan pemain berganti target penonton dengan tombol [A] / [D] / [Spasi] / [Klik Mouse].
    /// </summary>
    public class SpectatorController : MonoBehaviour
    {
        [Header("Spectator Settings")]
        [Tooltip("Jarak kamera di belakang pemain yang ditonton")]
        [SerializeField] private float followDistance = 2.8f;

        [Tooltip("Tinggi kamera di atas kepala pemain")]
        [SerializeField] private float followHeight = 1.7f;

        [Tooltip("Kecepatan kehalusan kamera mengikuti target")]
        [SerializeField] private float smoothSpeed = 10f;

        [Tooltip("Jeda waktu sebelum spectator aktif setelah mati (detik)")]
        [SerializeField] private float activationDelay = 1.8f;

        [Header("Runtime State")]
        [SerializeField] private bool isSpectating = false;
        [SerializeField] private int currentTargetIndex = 0;

        public bool IsSpectating => isSpectating;

        private Transform cameraRootTransform;
        private Camera spectatorCamera;
        private Vector3 originalLocalPos;
        private Quaternion originalLocalRot;
        private Transform originalParent;

        private PlayerCharacterController currentTarget;
        private readonly List<PlayerCharacterController> alivePlayers = new List<PlayerCharacterController>();
        private Coroutine activationCoroutine;

        // Gaya UI OnGUI
        private GUIStyle headerStyle;
        private GUIStyle nameStyle;
        private GUIStyle instructionStyle;
        private Texture2D backgroundTex;

        public void Initialize(Transform camRoot, Camera cam)
        {
            cameraRootTransform = camRoot;
            spectatorCamera = cam;

            if (cameraRootTransform != null)
            {
                originalParent = cameraRootTransform.parent;
                originalLocalPos = cameraRootTransform.localPosition;
                originalLocalRot = cameraRootTransform.localRotation;
            }
        }

        public void StartSpectating()
        {
            if (isSpectating) return;

            if (activationCoroutine != null)
            {
                StopCoroutine(activationCoroutine);
            }
            activationCoroutine = StartCoroutine(ActivateSpectatorRoutine());
        }

        private IEnumerator ActivateSpectatorRoutine()
        {
            // Beri jeda agar pemain melihat animasi kematian dirinya sendiri terlebih dahulu
            yield return new WaitForSeconds(activationDelay);

            isSpectating = true;
            RefreshAlivePlayers();

            if (cameraRootTransform != null)
            {
                // Lepaskan sementara parent kamera dari tubuh mayat pemain agar bebas mengikuti target hidup
                cameraRootTransform.SetParent(null, true);
            }

            if (alivePlayers.Count > 0)
            {
                currentTargetIndex = 0;
                currentTarget = alivePlayers[currentTargetIndex];
            }
            else
            {
                currentTarget = null;
            }
        }

        public void StopSpectating()
        {
            if (activationCoroutine != null)
            {
                StopCoroutine(activationCoroutine);
                activationCoroutine = null;
            }

            isSpectating = false;
            currentTarget = null;
            alivePlayers.Clear();

            // Kembalikan kamera ke tubuh pemain lokal
            if (cameraRootTransform != null)
            {
                cameraRootTransform.SetParent(originalParent != null ? originalParent : transform, false);
                cameraRootTransform.localPosition = originalLocalPos;
                cameraRootTransform.localRotation = originalLocalRot;
            }
        }

        private void RefreshAlivePlayers()
        {
            alivePlayers.Clear();
            var allControllers = FindObjectsOfType<PlayerCharacterController>();

            foreach (var p in allControllers)
            {
                if (p == null || p.gameObject == gameObject) continue;

                var health = p.GetComponent<PlayerHealth>();
                if (health != null && health.IsDead) continue;

                // Hanya pemain yang masih bernyawa
                alivePlayers.Add(p);
            }

            if (alivePlayers.Count > 0)
            {
                if (currentTargetIndex >= alivePlayers.Count)
                {
                    currentTargetIndex = 0;
                }
                currentTarget = alivePlayers[currentTargetIndex];
            }
            else
            {
                currentTarget = null;
            }
        }

        private void Update()
        {
            if (!isSpectating) return;

            // Periksa jika target yang sedang ditonton tiba-tiba mati atau keluar dari game
            if (currentTarget == null || (currentTarget.GetComponent<PlayerHealth>() != null && currentTarget.GetComponent<PlayerHealth>().IsDead))
            {
                RefreshAlivePlayers();
            }

            // Input untuk mengganti pemain yang ditonton
            HandleSpectatorInput();
        }

        private void HandleSpectatorInput()
        {
            if (alivePlayers.Count <= 1) return;

            bool nextPressed = false;
            bool prevPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    nextPressed = true;
                }
                if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
                {
                    prevPressed = true;
                }
            }
            if (Mouse.current != null)
            {
                if (Mouse.current.rightButton.wasPressedThisFrame) nextPressed = true;
                if (Mouse.current.leftButton.wasPressedThisFrame) prevPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1))
            {
                nextPressed = true;
            }
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetMouseButtonDown(0))
            {
                prevPressed = true;
            }
#endif

            if (nextPressed)
            {
                NextTarget();
            }
            else if (prevPressed)
            {
                PreviousTarget();
            }
        }

        public void NextTarget()
        {
            RefreshAlivePlayers();
            if (alivePlayers.Count == 0) return;

            currentTargetIndex = (currentTargetIndex + 1) % alivePlayers.Count;
            currentTarget = alivePlayers[currentTargetIndex];
        }

        public void PreviousTarget()
        {
            RefreshAlivePlayers();
            if (alivePlayers.Count == 0) return;

            currentTargetIndex = (currentTargetIndex - 1 + alivePlayers.Count) % alivePlayers.Count;
            currentTarget = alivePlayers[currentTargetIndex];
        }

        private void LateUpdate()
        {
            if (!isSpectating || cameraRootTransform == null) return;

            if (currentTarget != null)
            {
                Vector3 targetHead = currentTarget.transform.position + Vector3.up * followHeight;
                Vector3 desiredPos = targetHead - currentTarget.transform.forward * followDistance + Vector3.up * 0.3f;

                // Cek tabrakan dinding/rintangan dengan SphereCast agar kamera tidak tembus tembok
                Vector3 dir = (desiredPos - targetHead);
                float dist = dir.magnitude;
                if (dist > 0.01f)
                {
                    dir /= dist;
                    int mask = ~LayerMask.GetMask("Ignore Raycast", "Player");
                    if (Physics.SphereCast(targetHead, 0.22f, dir, out RaycastHit hit, dist, mask))
                    {
                        desiredPos = targetHead + dir * Mathf.Max(0.5f, hit.distance - 0.15f);
                    }
                }

                cameraRootTransform.position = Vector3.Lerp(cameraRootTransform.position, desiredPos, Time.deltaTime * smoothSpeed);

                Vector3 lookTarget = targetHead;
                Vector3 lookDir = (lookTarget - cameraRootTransform.position);
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    cameraRootTransform.rotation = Quaternion.Slerp(cameraRootTransform.rotation, targetRot, Time.deltaTime * (smoothSpeed + 2f));
                }
            }
        }

        private void OnGUI()
        {
            if (!isSpectating) return;

            InitStyles();

            float bannerWidth = 480f;
            float bannerHeight = 85f;
            float bannerX = (Screen.width - bannerWidth) * 0.5f;
            float bannerY = 25f;

            // Kotak background hitam transparan elegan
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerWidth, bannerHeight), backgroundTex);
            GUI.color = Color.white;

            // Judul Banner
            GUI.Label(new Rect(bannerX, bannerY + 6f, bannerWidth, 22f), "— MODE SPECTATOR (PENONTON) —", headerStyle);

            // Nama Target yang ditonton
            string targetName = "Mencari pemain yang masih hidup...";
            if (currentTarget != null)
            {
                var modelMgr = currentTarget.GetComponent<CharacterModelManager>();
                string charName = (modelMgr != null) ? modelMgr.ActiveCharacterType.ToString() : "Pemain";
                var netId = currentTarget.GetComponent<NetworkIdentity>();
                string idStr = (netId != null) ? $" [ID: {netId.netId}]" : "";
                targetName = $"Menonton: {charName}{idStr}";
            }
            else if (alivePlayers.Count == 0)
            {
                targetName = "Semua pemain telah gugur!";
            }

            GUI.Label(new Rect(bannerX, bannerY + 28f, bannerWidth, 28f), targetName, nameStyle);

            // Instruksi ganti target
            string instruction = (alivePlayers.Count > 1)
                ? "[A / D] atau [Klik Mouse] untuk ganti pemain  •  [Spasi] Berikutnya"
                : "Menunggu permainan berakhir...";
            GUI.Label(new Rect(bannerX, bannerY + 58f, bannerWidth, 20f), instruction, instructionStyle);
        }

        private void InitStyles()
        {
            if (backgroundTex == null)
            {
                backgroundTex = new Texture2D(1, 1);
                backgroundTex.SetPixel(0, 0, Color.white);
                backgroundTex.Apply();
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                headerStyle.normal.textColor = new Color(1f, 0.35f, 0.35f, 0.95f); // Merah peringatan
            }

            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
                nameStyle.normal.textColor = Color.white;
            }

            if (instructionStyle == null)
            {
                instructionStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Normal
                };
                instructionStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.85f);
            }
        }
    }
}
