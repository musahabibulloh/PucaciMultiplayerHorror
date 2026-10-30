using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pucaci.Player
{
    [DisallowMultipleComponent]
    public class FlashlightController : MonoBehaviour
    {
        [Header("Flashlight Components")]
        [Tooltip("Komponen Spot Light untuk sorot senter")]
        [SerializeField] private Light spotLight;

        [Tooltip("Renderer 3D senter untuk mengubah efek emisi/glow")]
        [SerializeField] private MeshRenderer flashlightRenderer;

        [Tooltip("Root objek senter (di tangan karakter)")]
        [SerializeField] private Transform flashlightTransform;

        [Header("Flashlight Settings")]
        [Tooltip("Apakah senter aktif saat mulai bermain")]
        [SerializeField] private bool startsOn = true;

        [Tooltip("Intensitas cahaya sorot utama senter")]
        [SerializeField] private float lightIntensity = 22.0f;

        [Tooltip("Jarak sorot senter (meter)")]
        [SerializeField] private float lightRange = 65.0f;

        [Tooltip("Sudut sorot senter (lebar cone)")]
        [SerializeField] private float spotAngle = 75.0f;

        [Tooltip("Sudut sorot dalam senter (hotspot terang)")]
        [SerializeField] private float innerSpotAngle = 38.0f;

        [Header("Crosshair Aiming")]
        [Tooltip("Jarak default sorot jika raycast tidak mengenai objek")]
        [SerializeField] private float defaultAimDistance = 40f;

        [Tooltip("Layer mask untuk raycast titik crosshair")]
        [SerializeField] private LayerMask aimLayerMask = ~0;

        [Header("First-Person Arm Positioning")]
        [Tooltip("Offset posisi tangan kanan relatif terhadap kamera saat First-Person (X=kanan, Y=bawah, Z=depan)")]
        [SerializeField] private Vector3 fpHandOffset = new Vector3(0.20f, -0.16f, 0.25f);

        [Tooltip("Kemiringan pergelangan tangan First-Person (derajat roll)")]
        [SerializeField] private float fpWristRollAngle = 20f;

        [Tooltip("Offset posisi lokal senter di dalam genggaman tangan")]
        [SerializeField] private Vector3 fpFlashlightLocalPos = new Vector3(-0.04f, 0.12f, 0.00f);

        [Tooltip("Offset rotasi lokal senter di dalam genggaman tangan")]
        [SerializeField] private Vector3 fpFlashlightLocalRot = new Vector3(315f, 130f, 15f);

        [Header("Finger Curl Angles (Grip Senter)")]
        [Tooltip("Sudut tekuk ruas jari pangkal (mixamorig:RightHandIndex1)")]
        [SerializeField] private Vector3 finger1Curl = new Vector3(60f, 0f, 0f);

        [Tooltip("Sudut tekuk ruas jari tengah (mixamorig:RightHandIndex2)")]
        [SerializeField] private Vector3 finger2Curl = new Vector3(75f, 0f, 0f);

        [Tooltip("Sudut tekuk ruas jari ujung (mixamorig:RightHandIndex3)")]
        [SerializeField] private Vector3 finger3Curl = new Vector3(55f, 0f, 0f);

        [Header("Audio")]
        [Tooltip("AudioSource untuk suara klik senter")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Audio clip klik senter (jika kosong akan dibuat otomatis secara prosedural)")]
        [SerializeField] private AudioClip toggleSound;

        [Header("Bones References")]
        [Tooltip("Aktifkan animasi prosedural agar tangan kanan mengarahkan senter ke crosshair")]
        [SerializeField] private bool enableProceduralAim = true;

        [Tooltip("Transform tulang pundak kanan (mixamorig:RightShoulder)")]
        [SerializeField] private Transform rightShoulderBone;

        [Tooltip("Transform tulang lengan atas kanan (mixamorig:RightArm)")]
        [SerializeField] private Transform rightArmBone;

        [Tooltip("Transform tulang lengan bawah kanan (mixamorig:RightForeArm)")]
        [SerializeField] private Transform rightForeArmBone;

        [Tooltip("Transform tulang pergelangan tangan kanan (mixamorig:RightHand)")]
        [SerializeField] private Transform rightHandBone;

        [Tooltip("Transform tulang jari tangan ruas 1 (mixamorig:RightHandIndex1)")]
        [SerializeField] private Transform rightFingersBone;

        [Tooltip("Transform tulang jari tangan ruas 2 (mixamorig:RightHandIndex2)")]
        [SerializeField] private Transform rightFinger2Bone;

        [Tooltip("Transform tulang jari tangan ruas 3 (mixamorig:RightHandIndex3)")]
        [SerializeField] private Transform rightFinger3Bone;

        [Tooltip("Transform referensi arah pandangan (CameraRoot atau FirstPersonCamera)")]
        [SerializeField] private Transform aimTargetReference;

        [Tooltip("Bobot pengaruh pose memegang senter (0 = animasi bawaan, 1 = mengarah penuh)")]
        [Range(0f, 1f)]
        [SerializeField] private float aimWeight = 0.95f;

        [Tooltip("Kecepatan transisi posisi tangan saat senter dinyalakan/dimatikan")]
        [SerializeField] private float transitionSpeed = 10f;

        [Header("Sway & Bobbing")]
        [Tooltip("Aktifkan animasi goyangan halus (idle sway)")]
        [SerializeField] private bool enableSway = false;

        [Tooltip("Aktifkan goyangan tangan saat First-Person (jika false, posisi tangan akan diam stabil 100%)")]
        [SerializeField] private bool enableFirstPersonSway = false;

        [SerializeField] private float swayAmount = 0.025f;
        [SerializeField] private float swaySpeed = 5.5f;

        // Status internal
        private bool isFlashlightOn;
        private float currentWeight;
        private float swayTimer;
        private Material dynamicMaterial;
        private PlayerCharacterController pcc;
        private Vector3 smoothedAimPoint;
        private bool aimPointInitialized;

        public bool IsOn => isFlashlightOn;
        public Light SpotLightComponent => spotLight;

        private void Awake()
        {
            pcc = GetComponent<PlayerCharacterController>();

            // Auto-resolve tulang jika belum di-assign di Inspector
            if (rightHandBone == null || rightArmBone == null || rightForeArmBone == null)
            {
                var model = transform.Find("Model");
                if (model != null)
                {
                    if (rightShoulderBone == null)
                        rightShoulderBone = model.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder");
                    if (rightArmBone == null && rightShoulderBone != null)
                        rightArmBone = rightShoulderBone.Find("mixamorig:RightArm");
                    if (rightForeArmBone == null && rightArmBone != null)
                        rightForeArmBone = rightArmBone.Find("mixamorig:RightForeArm");
                    if (rightHandBone == null && rightForeArmBone != null)
                        rightHandBone = rightForeArmBone.Find("mixamorig:RightHand");
                }
            }

            if (rightFingersBone == null && rightHandBone != null)
            {
                rightFingersBone = rightHandBone.Find("mixamorig:RightHandIndex1");
            }

            if (rightFinger2Bone == null && rightFingersBone != null)
            {
                rightFinger2Bone = rightFingersBone.Find("mixamorig:RightHandIndex2");
            }

            if (rightFinger3Bone == null && rightFinger2Bone != null)
            {
                rightFinger3Bone = rightFinger2Bone.Find("mixamorig:RightHandIndex3");
            }

            if (flashlightTransform == null && rightHandBone != null)
            {
                flashlightTransform = rightHandBone.Find("Flashlight_Item");
            }

            // Bersihkan objek spill lama jika masih tertinggal agar karakter tidak glowing
            if (flashlightTransform != null)
            {
                var oldSpill = flashlightTransform.Find("Flashlight_Spill");
                if (oldSpill != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(oldSpill.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(oldSpill.gameObject);
                    }
                }
            }

            if (spotLight == null && flashlightTransform != null)
            {
                spotLight = flashlightTransform.GetComponentInChildren<Light>();
            }

            if (flashlightRenderer == null && flashlightTransform != null)
            {
                flashlightRenderer = flashlightTransform.GetComponent<MeshRenderer>();
            }

            if (aimTargetReference == null)
            {
                var camRoot = transform.Find("CameraRoot");
                if (camRoot != null)
                {
                    aimTargetReference = camRoot.Find("FirstPersonCamera");
                }
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0.5f;
                }
            }

            if (toggleSound == null)
            {
                toggleSound = CreateProceduralClickClip();
            }

            if (flashlightRenderer != null)
            {
                dynamicMaterial = flashlightRenderer.material;
            }

            if (spotLight != null)
            {
                spotLight.type = LightType.Spot;
                spotLight.range = lightRange;
                spotLight.spotAngle = spotAngle;
                spotLight.innerSpotAngle = innerSpotAngle;
                spotLight.intensity = lightIntensity;
                spotLight.color = new Color(1.0f, 0.98f, 0.94f, 1.0f);
                spotLight.shadows = LightShadows.None; // Hindari shadow atlas thrashing penyebab kedap-kedip
            }

            SetFlashlightState(startsOn, playSound: false);
        }

        private void Update()
        {
            // Input toggle F key jika berjalan tanpa PlayerCharacterController
            if (pcc == null)
            {
                bool fPressed = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                {
                    fPressed = true;
                }
#else
                if (Input.GetKeyDown(KeyCode.F))
                {
                    fPressed = true;
                }
#endif
                if (fPressed)
                {
                    ToggleFlashlight();
                }
            }

            // Update transisi bobot aiming
            float targetWeight = isFlashlightOn ? aimWeight : 0f;
            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * transitionSpeed);
        }

        private void LateUpdate()
        {
            if (enableProceduralAim && currentWeight > 0.001f && rightHandBone != null)
            {
                ApplyArmAiming();
            }
        }

        /// <summary>
        /// Mengarahkan lengan kanan dan senter ke titik crosshair di layar
        /// </summary>
        private void ApplyArmAiming()
        {
            Transform camTr = aimTargetReference != null ? aimTargetReference : (Camera.main != null ? Camera.main.transform : null);
            if (camTr == null) return;

            bool isFirstPerson = (pcc != null && pcc.CurrentPerspective == PlayerCharacterController.CameraPerspective.FirstPerson);

            // 1. Tentukan titik target 3D crosshair yang stabil tanpa jitter
            Vector3 desiredAimPoint;
            if (isFirstPerson)
            {
                // Pada First-Person, senter stabil 100% lurus mengarah ke depan crosshair
                desiredAimPoint = camTr.position + camTr.forward * defaultAimDistance;
            }
            else
            {
                // Pada Third-Person, cari titik crosshair dengan menyaring collider pemain dan jarak minimal
                Ray ray = new Ray(camTr.position, camTr.forward);
                desiredAimPoint = ray.GetPoint(defaultAimDistance);

                RaycastHit[] hits = Physics.RaycastAll(ray, 100f, aimLayerMask, QueryTriggerInteraction.Ignore);
                if (hits != null && hits.Length > 0)
                {
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var col = hits[i].collider;
                        if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                        {
                            continue;
                        }

                        // Hindari snap ke rumput/tanah dekat kaki yang memicu kedap-kedip visual
                        if (hits[i].distance >= 4.0f)
                        {
                            desiredAimPoint = hits[i].point;
                            break;
                        }
                    }
                }
            }

            if (!aimPointInitialized)
            {
                smoothedAimPoint = desiredAimPoint;
                aimPointInitialized = true;
            }
            else
            {
                smoothedAimPoint = Vector3.Lerp(smoothedAimPoint, desiredAimPoint, Time.deltaTime * 25f);
            }

            // 2. Hitung goyangan halus (sway) - dinonaktifkan di FPP agar tangan diam stabil 100%
            float swayX = 0f;
            float swayY = 0f;
            if (enableSway && (!isFirstPerson || enableFirstPersonSway))
            {
                swayTimer += Time.deltaTime * swaySpeed;
                swayX = Mathf.Sin(swayTimer) * swayAmount;
                swayY = Mathf.Cos(swayTimer * 2f) * swayAmount * 0.6f;
            }

            if (isFirstPerson)
            {
                // FIRST-PERSON: Kunci orientasi pundak agar stabil dan tidak terombang-ambing oleh animasi lari/jalan
                if (rightShoulderBone != null)
                {
                    rightShoulderBone.localRotation = Quaternion.Slerp(
                        rightShoulderBone.localRotation, 
                        Quaternion.Euler(12f, 20f, -8f), 
                        currentWeight
                    );
                }

                // Posisi target pergelangan tangan kanan terkunci stabil di sudut pandang kamera (tanpa goyangan)
                Vector3 targetHandPos = camTr.position 
                    + camTr.right * (fpHandOffset.x + swayX) 
                    + camTr.up * (fpHandOffset.y + swayY) 
                    + camTr.forward * fpHandOffset.z;

                // Siku menekuk ke bawah secara stabil relatif terhadap kamera
                Vector3 elbowHint = camTr.position + camTr.right * (fpHandOffset.x + 0.10f) - camTr.up * 0.45f - camTr.forward * 0.05f;

                if (rightArmBone != null && rightForeArmBone != null && rightHandBone != null)
                {
                    SolveTwoBoneIK(rightArmBone, rightForeArmBone, rightHandBone, targetHandPos, elbowHint, currentWeight);
                }

                // Posisikan senter terkunci rapat di dalam genggaman tangan
                if (flashlightTransform != null)
                {
                    flashlightTransform.localPosition = fpFlashlightLocalPos;
                    flashlightTransform.localRotation = Quaternion.Euler(fpFlashlightLocalRot);
                }

                // Arahkan genggaman pergelangan tangan kanan secara presisi ke arah pandangan kamera (stabil 100%)
                Vector3 localBeam = (flashlightTransform != null ? flashlightTransform.localRotation : Quaternion.identity) * Vector3.up;
                Quaternion alignRot = Quaternion.FromToRotation(localBeam, Vector3.forward);
                Quaternion rollOffset = Quaternion.Euler(0f, 0f, fpWristRollAngle);
                Quaternion targetHandRot = Quaternion.LookRotation(camTr.forward, camTr.up) * rollOffset * alignRot;
                rightHandBone.rotation = Quaternion.Slerp(rightHandBone.rotation, targetHandRot, currentWeight);

                // Tekuk seluruh ruas jari tangan secara realistis melingkari gagang senter
                if (rightFingersBone != null)
                {
                    rightFingersBone.localRotation = Quaternion.Slerp(
                        rightFingersBone.localRotation, 
                        Quaternion.Euler(finger1Curl), 
                        currentWeight
                    );
                }
                if (rightFinger2Bone != null)
                {
                    rightFinger2Bone.localRotation = Quaternion.Slerp(
                        rightFinger2Bone.localRotation, 
                        Quaternion.Euler(finger2Curl), 
                        currentWeight
                    );
                }
                if (rightFinger3Bone != null)
                {
                    rightFinger3Bone.localRotation = Quaternion.Slerp(
                        rightFinger3Bone.localRotation, 
                        Quaternion.Euler(finger3Curl), 
                        currentWeight
                    );
                }
            }
            else
            {
                // THIRD-PERSON: Pose aditif siku terangkat ke depan
                if (rightArmBone != null)
                {
                    rightArmBone.localRotation *= Quaternion.Euler(
                        (-28f + swayY * 20f) * currentWeight, 
                        (14f + swayX * 20f) * currentWeight, 
                        -5f * currentWeight
                    );
                }

                if (rightForeArmBone != null)
                {
                    rightForeArmBone.localRotation *= Quaternion.Euler(
                        5f * currentWeight, 
                        0f, 
                        (62f + swayX * 10f) * currentWeight
                    );
                }

                if (rightHandBone != null)
                {
                    rightHandBone.localRotation *= Quaternion.Euler(
                        (-12f + swayY * 10f) * currentWeight, 
                        -15f * currentWeight, 
                        18f * currentWeight
                    );
                }

                // Tekuk jari tangan memegang senter di Third-Person juga
                if (rightFingersBone != null)
                {
                    rightFingersBone.localRotation = Quaternion.Slerp(
                        rightFingersBone.localRotation, 
                        Quaternion.Euler(finger1Curl), 
                        currentWeight
                    );
                }
                if (rightFinger2Bone != null)
                {
                    rightFinger2Bone.localRotation = Quaternion.Slerp(
                        rightFinger2Bone.localRotation, 
                        Quaternion.Euler(finger2Curl), 
                        currentWeight
                    );
                }
                if (rightFinger3Bone != null)
                {
                    rightFinger3Bone.localRotation = Quaternion.Slerp(
                        rightFinger3Bone.localRotation, 
                        Quaternion.Euler(finger3Curl), 
                        currentWeight
                    );
                }
            }

            // 3. Pastikan sorot Spot Light SELALU mengarah mulus dan stabil ke titik target
            if (spotLight != null)
            {
                Vector3 beamDir = smoothedAimPoint - spotLight.transform.position;
                if (Vector3.Dot(camTr.forward, beamDir) < 0.1f)
                {
                    beamDir = camTr.forward;
                }
                spotLight.transform.rotation = Quaternion.LookRotation(beamDir.normalized);
            }
        }

        /// <summary>
        /// Solusi Two-Bone IK analitik untuk memposisikan lengan dan siku secara akurat dan mulus
        /// </summary>
        private static void SolveTwoBoneIK(Transform root, Transform mid, Transform tip, Vector3 targetPos, Vector3 hintPos, float weight)
        {
            if (weight <= 0.001f || root == null || mid == null || tip == null) return;

            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = tip.position;

            float l1 = Vector3.Distance(a, b);
            float l2 = Vector3.Distance(b, c);
            float d = Vector3.Distance(a, targetPos);

            d = Mathf.Clamp(d, 0.01f, (l1 + l2) * 0.999f);

            float cosAlpha = (l1 * l1 + d * d - l2 * l2) / (2f * l1 * d);
            float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f));

            Vector3 u = (targetPos - a).normalized;
            Vector3 h = (hintPos - a).normalized;
            Vector3 n = Vector3.Cross(u, h);
            if (n.sqrMagnitude < 0.0001f)
            {
                n = root.right;
            }
            n.Normalize();

            Vector3 elbowDir = Quaternion.AngleAxis(alpha * Mathf.Rad2Deg, n) * u;
            Vector3 newElbowPos = a + elbowDir * l1;

            // 1. Putar lengan atas (root)
            Quaternion rootTargetRot = Quaternion.FromToRotation(b - a, newElbowPos - a) * root.rotation;
            root.rotation = Quaternion.Slerp(root.rotation, rootTargetRot, weight);

            // 2. Putar lengan bawah (mid) menggunakan posisi terkini setelah root berotasi
            Vector3 curForearm = tip.position - mid.position;
            Vector3 desForearm = targetPos - mid.position;
            Quaternion midTargetRot = Quaternion.FromToRotation(curForearm, desForearm) * mid.rotation;
            mid.rotation = Quaternion.Slerp(mid.rotation, midTargetRot, weight);
        }

        public void ToggleFlashlight()
        {
            SetFlashlightState(!isFlashlightOn, playSound: true);
        }

        public void SetFlashlightState(bool on, bool playSound = true)
        {
            isFlashlightOn = on;

            if (spotLight != null)
            {
                spotLight.enabled = isFlashlightOn;
            }

            if (dynamicMaterial != null)
            {
                if (isFlashlightOn)
                {
                    dynamicMaterial.EnableKeyword("_EMISSION");
                    dynamicMaterial.SetColor("_EmissionColor", new Color(1.8f, 1.8f, 1.8f, 1f));
                }
                else
                {
                    dynamicMaterial.DisableKeyword("_EMISSION");
                    dynamicMaterial.SetColor("_EmissionColor", Color.black);
                }
            }

            if (playSound && audioSource != null && toggleSound != null)
            {
                audioSource.pitch = isFlashlightOn ? 1.05f : 0.95f;
                audioSource.PlayOneShot(toggleSound, 0.8f);
            }
        }

        private AudioClip CreateProceduralClickClip()
        {
            int sampleRate = 44100;
            float duration = 0.045f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float envelope = Mathf.Exp(-t * 28f);
                float noise = (Random.value * 2f - 1f) * 0.4f;
                float tone1 = Mathf.Sin(2f * Mathf.PI * 2200f * (float)i / sampleRate);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 4400f * (float)i / sampleRate) * 0.5f;
                samples[i] = (tone1 + tone2 + noise) * envelope * 0.7f;
            }

            AudioClip clip = AudioClip.Create("FlashlightSwitchClick", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void AssignReferences(Light light, MeshRenderer renderer, Transform root, Transform rArm, Transform rForeArm, Transform rHand, Transform aimRef, Transform rShoulder = null, Transform rFingers = null)
        {
            spotLight = light;
            flashlightRenderer = renderer;
            flashlightTransform = root;
            rightShoulderBone = rShoulder;
            rightArmBone = rArm;
            rightForeArmBone = rForeArm;
            rightHandBone = rHand;
            rightFingersBone = rFingers;
            aimTargetReference = aimRef;
        }
    }
}
