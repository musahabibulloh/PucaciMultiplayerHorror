using UnityEngine;

namespace Pucaci.UI
{
    /// <summary>
    /// Memberikan efek gerak kamera sinematik halus di Main Menu:
    /// 1. Breathing / drifting lambat menyerupai handheld camera horor.
    /// 2. Parallax mouse tracking halus untuk kedalaman 3D saat kursor bergerak.
    /// </summary>
    public class MenuCameraDrift : MonoBehaviour
    {
        [Header("Drift / Breathing Settings")]
        [SerializeField] private float driftSpeed = 0.35f;
        [SerializeField] private float posDriftAmount = 0.08f;
        [SerializeField] private float rotDriftAmount = 0.6f;

        [Header("Mouse Parallax Settings")]
        [SerializeField] private float mouseSmoothSpeed = 3.5f;
        [SerializeField] private float mouseTiltMaxAngle = 2.2f;
        [SerializeField] private float mousePanDistance = 0.15f;

        private Vector3 initialPos;
        private Quaternion initialRot;
        private Vector2 targetMouseNorm;
        private Vector2 currentMouseNorm;
        private float noiseOffsetX;
        private float noiseOffsetY;

        private void Awake()
        {
            initialPos = transform.position;
            initialRot = transform.rotation;
            noiseOffsetX = Random.Range(0f, 100f);
            noiseOffsetY = Random.Range(0f, 100f);
        }

        private void Update()
        {
            // 1. Mouse normalized coordinates [-1, 1]
            Vector3 mousePos = Input.mousePosition;
            float normX = (mousePos.x / Mathf.Max(1f, Screen.width) - 0.5f) * 2f;
            float normY = (mousePos.y / Mathf.Max(1f, Screen.height) - 0.5f) * 2f;
            targetMouseNorm = new Vector2(Mathf.Clamp(normX, -1f, 1f), Mathf.Clamp(normY, -1f, 1f));

            currentMouseNorm = Vector2.Lerp(currentMouseNorm, targetMouseNorm, Time.deltaTime * mouseSmoothSpeed);

            // 2. Procedural Perlin noise breathing
            float time = Time.time * driftSpeed;
            float noiseX = (Mathf.PerlinNoise(time + noiseOffsetX, 0.2f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0.7f, time + noiseOffsetY) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(time * 0.8f, time * 0.8f) - 0.5f) * 2f;

            // 3. Gabungkan Posisi
            Vector3 posOffset = new Vector3(
                noiseX * posDriftAmount + currentMouseNorm.x * mousePanDistance,
                noiseY * posDriftAmount + currentMouseNorm.y * (mousePanDistance * 0.6f),
                noiseZ * (posDriftAmount * 0.5f)
            );
            transform.position = initialPos + posOffset;

            // 4. Gabungkan Rotasi (Pitch, Yaw, Roll)
            float pitch = -currentMouseNorm.y * mouseTiltMaxAngle + noiseY * rotDriftAmount;
            float yaw = currentMouseNorm.x * mouseTiltMaxAngle + noiseX * rotDriftAmount;
            float roll = -currentMouseNorm.x * 0.5f + noiseZ * (rotDriftAmount * 0.35f);

            transform.rotation = initialRot * Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
