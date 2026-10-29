using System.Collections;
using UnityEngine;

namespace Pucaci.UI
{
    [RequireComponent(typeof(Light))]
    public class LampFlicker : MonoBehaviour
    {
        [Header("Intensity Settings")]
        [SerializeField] private float baseIntensity = 2.0f;
        [SerializeField] private float intensityVariation = 0.4f;
        [SerializeField] private float flickerSpeed = 1.8f;
        [SerializeField] private bool allowGlitch = true;
        [SerializeField] private float glitchInterval = 7f;

        private Light targetLight;
        private float noiseOffset;

        private void Awake()
        {
            targetLight = GetComponent<Light>();
            noiseOffset = Random.Range(0f, 100f);
        }

        private void Start()
        {
            if (allowGlitch)
            {
                StartCoroutine(GlitchRoutine());
            }
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise((Time.time * flickerSpeed) + noiseOffset, 0.5f);
            targetLight.intensity = baseIntensity + (noise - 0.5f) * 2f * intensityVariation;
        }

        private IEnumerator GlitchRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(glitchInterval * 0.8f, glitchInterval * 1.5f));

                float saved = targetLight.intensity;
                targetLight.intensity = baseIntensity * 0.2f;
                yield return new WaitForSeconds(0.06f);
                targetLight.intensity = baseIntensity * 1.3f;
                yield return new WaitForSeconds(0.04f);
                targetLight.intensity = saved;
            }
        }
    }
}
