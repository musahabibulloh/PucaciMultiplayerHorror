using UnityEngine;

namespace Pucaci.UI
{
    /// <summary>
    /// Mengontrol animasi melayang halus & mata bercahaya Kuntilanak di latar belakang Main Menu.
    /// </summary>
    public class MenuGhostFloating : MonoBehaviour
    {
        [Header("Floating / Hover Settings")]
        [SerializeField] private float floatSpeed = 0.8f;
        [SerializeField] private float floatHeight = 0.12f;
        [SerializeField] private float tiltAmount = 1.5f;

        [Header("Glowing Eyes")]
        [SerializeField] private Light leftEyeLight;
        [SerializeField] private Light rightEyeLight;
        [SerializeField] private Color eyeColor = new Color(1f, 0.15f, 0.12f, 1f); // Merah membara
        [SerializeField] private float eyeBaseIntensity = 0.4f;

        private Vector3 initialPos;
        private Quaternion initialRot;
        private float seed;

        private void Awake()
        {
            initialPos = transform.position;
            initialRot = transform.rotation;
            seed = Random.Range(0f, 50f);

            SetupEyes();
        }

        private void SetupEyes()
        {
            Transform head = null;
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLower().Contains("head")) { head = t; break; }
            }

            Transform eyeParent = head != null ? head : transform;
            Vector3 leftOffset = head != null ? new Vector3(-0.06f, 0.08f, 0.12f) : new Vector3(-0.07f, 0.45f, 0.22f);
            Vector3 rightOffset = head != null ? new Vector3(0.06f, 0.08f, 0.12f) : new Vector3(0.07f, 0.45f, 0.22f);

            if (leftEyeLight == null)
            {
                leftEyeLight = CreateEye("KuntiEye_Left", eyeParent, leftOffset);
            }

            if (rightEyeLight == null)
            {
                rightEyeLight = CreateEye("KuntiEye_Right", eyeParent, rightOffset);
            }
        }

        private Light CreateEye(string name, Transform parent, Vector3 localPos)
        {
            var eyeObj = new GameObject(name);
            eyeObj.transform.SetParent(parent, false);
            eyeObj.transform.localPosition = localPos;

            // Bola mata bercahaya (emissive) agar terbaca dari kejauhan
            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = name + "_Glow";
            glow.transform.SetParent(eyeObj.transform, false);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = Vector3.one * 0.06f;
            var col = glow.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.color = eyeColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", eyeColor);
            glow.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var light = eyeObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.0f;
            light.color = eyeColor;
            light.intensity = eyeBaseIntensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private void Update()
        {
            float time = Time.time * floatSpeed + seed;

            // Gerakan melayang naik turun
            float yOffset = Mathf.Sin(time) * floatHeight + (Mathf.PerlinNoise(time * 0.5f, 0f) - 0.5f) * (floatHeight * 0.5f);
            transform.position = initialPos + new Vector3(0f, yOffset, 0f);

            // Ayunan miring halus
            float tilt = Mathf.Sin(time * 0.7f) * tiltAmount;
            transform.rotation = initialRot * Quaternion.Euler(0f, 0f, tilt);

            // Kedipan mata samar mistis
            if (leftEyeLight != null && rightEyeLight != null)
            {
                float eyeNoise = Mathf.PerlinNoise(Time.time * 2.5f + seed, 0f);
                float curIntensity = eyeBaseIntensity * (0.75f + eyeNoise * 0.45f);
                leftEyeLight.intensity = curIntensity;
                rightEyeLight.intensity = curIntensity;
            }
        }
    }
}
