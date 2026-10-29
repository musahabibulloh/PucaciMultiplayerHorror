using System.Collections;
using TMPro;
using UnityEngine;

namespace Pucaci.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class TitleFlicker : MonoBehaviour
    {
        [Header("Flicker Settings")]
        [SerializeField] private float minAlpha = 0.65f;
        [SerializeField] private float maxAlpha = 1.0f;
        [SerializeField] private float flickerSpeed = 2.5f;
        [SerializeField] private bool occasionalGlitch = true;
        [SerializeField] private float glitchInterval = 6f;

        private TMP_Text textComponent;
        private Color baseColor;
        private float noiseOffset;

        private void Awake()
        {
            textComponent = GetComponent<TMP_Text>();
            baseColor = textComponent.color;
            noiseOffset = Random.Range(0f, 100f);
        }

        private void Start()
        {
            if (occasionalGlitch)
            {
                StartCoroutine(OccasionalGlitchRoutine());
            }
        }

        private void Update()
        {
            // Halus menggunakan Perlin noise agar terasa seperti lampu neon kampung yang hangat
            float noise = Mathf.PerlinNoise((Time.time * flickerSpeed) + noiseOffset, 0f);
            float targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, noise);

            Color c = baseColor;
            c.a = targetAlpha;
            textComponent.color = c;
        }

        private IEnumerator OccasionalGlitchRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(glitchInterval * 0.7f, glitchInterval * 1.5f));

                // Kedipan cepat 2-3 kali seperti lampu pos ronda
                for (int i = 0; i < 2; i++)
                {
                    Color c = baseColor;
                    c.a = minAlpha * 0.4f;
                    textComponent.color = c;
                    yield return new WaitForSeconds(0.05f);

                    c.a = maxAlpha;
                    textComponent.color = c;
                    yield return new WaitForSeconds(0.04f);
                }
            }
        }
    }
}
