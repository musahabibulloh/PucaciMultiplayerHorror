using System.Collections;
using UnityEngine;
using Pucaci.Rendering;

namespace Pucaci.Props
{
    /// <summary>
    /// Mengontrol pencahayaan satu tiang lampu jalan desa (visual mesh emission + Spotlight + PointLight).
    /// Mendukung transisi padam seketika, kedip-kedip horor (flicker glitch), dan pemulihan daya listrik.
    /// </summary>
    [DisallowMultipleComponent]
    public class StreetLamp : MonoBehaviour
    {
        [Header("Light References")]
        [SerializeField] private Light pointLight;
        [SerializeField] private Light spotLight;
        [SerializeField] private Renderer lampRenderer;

        [Header("Default Light Settings")]
        [SerializeField] private float basePointIntensity = 25.0f;
        [SerializeField] private float baseSpotIntensity = 42.0f;
        [SerializeField] private Color emissionOnColor = new Color(1.0f, 0.90f, 0.70f);

        private Material lampMaterial;
        private Coroutine activeFlickerRoutine;
        private bool isCurrentlyOn = true;
        private bool isInitialized = false;

        public bool IsOn => isCurrentlyOn;

        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (pointLight == null)
            {
                var pl = transform.Find("LampLight/PointLight");
                if (pl != null) pointLight = pl.GetComponent<Light>();
            }
            if (spotLight == null)
            {
                var sl = transform.Find("LampLight/SpotLight");
                if (sl != null) spotLight = sl.GetComponent<Light>();
            }
            if (lampRenderer == null)
            {
                lampRenderer = GetComponentInChildren<Renderer>();
            }

            if (lampRenderer != null && lampMaterial == null)
            {
                // Gunakan instance material runtime agar tidak mengotori asset disk
                lampMaterial = lampRenderer.material;
            }

            // Outline pada tiang lampu
            ToonOutlineApplier.ApplyTo(gameObject, 2.5f);

            if (!isInitialized)
            {
                if (pointLight != null && pointLight.intensity > 0.1f) basePointIntensity = pointLight.intensity;
                if (spotLight != null && spotLight.intensity > 0.1f) baseSpotIntensity = spotLight.intensity;
                if (basePointIntensity <= 0.1f) basePointIntensity = 25.0f;
                if (baseSpotIntensity <= 0.1f) baseSpotIntensity = 42.0f;
                isInitialized = true;
            }
        }

        /// <summary>
        /// Mengatur kondisi lampu menyala atau mati, dengan opsi kedipan horor.
        /// </summary>
        public void SetLightState(bool on, bool withFlicker = true)
        {
            ResolveReferences();

            if (activeFlickerRoutine != null)
            {
                StopCoroutine(activeFlickerRoutine);
                activeFlickerRoutine = null;
            }

            if (withFlicker && gameObject.activeInHierarchy)
            {
                activeFlickerRoutine = StartCoroutine(on ? FlickerOnRoutine() : FlickerOffRoutine());
            }
            else
            {
                ApplyStateImmediate(on);
            }
        }

        private void ApplyStateImmediate(bool on)
        {
            isCurrentlyOn = on;

            if (pointLight != null)
            {
                pointLight.enabled = on;
                pointLight.intensity = on ? basePointIntensity : 0f;
            }

            if (spotLight != null)
            {
                spotLight.enabled = on;
                spotLight.intensity = on ? baseSpotIntensity : 0f;
            }

            if (lampMaterial != null)
            {
                if (on)
                {
                    lampMaterial.SetColor(EmissionColorProp, emissionOnColor);
                    lampMaterial.EnableKeyword("_EMISSION");
                }
                else
                {
                    lampMaterial.SetColor(EmissionColorProp, Color.black);
                    lampMaterial.DisableKeyword("_EMISSION");
                }
            }
        }

        private void SetFlickerIntensityScale(float scale)
        {
            bool visible = scale > 0.05f;

            if (pointLight != null)
            {
                pointLight.enabled = visible;
                pointLight.intensity = basePointIntensity * scale;
            }

            if (spotLight != null)
            {
                spotLight.enabled = visible;
                spotLight.intensity = baseSpotIntensity * scale;
            }

            if (lampMaterial != null)
            {
                if (visible)
                {
                    lampMaterial.SetColor(EmissionColorProp, emissionOnColor * scale);
                    lampMaterial.EnableKeyword("_EMISSION");
                }
                else
                {
                    lampMaterial.SetColor(EmissionColorProp, Color.black);
                    lampMaterial.DisableKeyword("_EMISSION");
                }
            }
        }

        private IEnumerator FlickerOffRoutine()
        {
            // Variasi delay acak sedikit agar tiap lampu tidak berkedip serentak kaku
            yield return new WaitForSeconds(Random.Range(0.01f, 0.12f));

            // Kedipan 1: padam sekejap
            SetFlickerIntensityScale(0.1f);
            yield return new WaitForSeconds(0.06f);

            // Kedipan 2: lonjakan terang (percikan korsleting)
            SetFlickerIntensityScale(1.4f);
            yield return new WaitForSeconds(0.05f);

            // Kedipan 3: redup
            SetFlickerIntensityScale(0.2f);
            yield return new WaitForSeconds(0.07f);

            // Kedipan 4: kilatan terakhir
            SetFlickerIntensityScale(0.8f);
            yield return new WaitForSeconds(0.04f);

            // Padam total
            ApplyStateImmediate(false);
            activeFlickerRoutine = null;
        }

        private IEnumerator FlickerOnRoutine()
        {
            // Langsung menyala di frame pertama bertepatan dengan suara klik saklar
            ApplyStateImmediate(true);

            // Lonjakan daya singkat (power surge) saat arus listrik pertama kali masuk
            SetFlickerIntensityScale(1.3f);
            yield return new WaitForSeconds(0.08f);

            // Kedipan mikro penyesuaian tegangan
            SetFlickerIntensityScale(0.85f);
            yield return new WaitForSeconds(0.04f);

            // Kembali stabil menyala normal 100%
            ApplyStateImmediate(true);
            activeFlickerRoutine = null;
        }

        private void OnDestroy()
        {
            if (activeFlickerRoutine != null)
            {
                StopCoroutine(activeFlickerRoutine);
                activeFlickerRoutine = null;
            }
        }
    }
}
