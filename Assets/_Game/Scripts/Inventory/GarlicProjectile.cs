using UnityEngine;
using Mirror;
using Pucaci.Player;

namespace Pucaci.Inventory
{
    /// <summary>
    /// Proyektil siung bawang putih yang ditembakkan dari Tulup.
    /// Memiliki deteksi tabrakan terhadap hantu (Kuntilanak).
    /// Jika hantu sedang dalam wujud gaib (IsGhostForm == true), peluru menembus tanpa memberi damage.
    /// Jika hantu dalam wujud padat, peluru memberikan damage ke Kuntilanak.
    /// </summary>
    public class GarlicProjectile : NetworkBehaviour
    {
        [Header("Projectile Properties")]
        [SerializeField] private float speed = 24f;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float maxLifetime = 3.5f;
        [SerializeField] private float radius = 0.25f;

        private Vector3 moveDirection;
        private uint shooterNetId;
        private float aliveTime = 0f;
        private bool hasHit = false;

        public void Initialize(Vector3 direction, float projSpeed, float projDamage, uint shooterId)
        {
            moveDirection = direction.normalized;
            speed = projSpeed;
            damage = projDamage;
            shooterNetId = shooterId;

            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        private void Start()
        {
            SetupVisualAndCollider();
        }

        private void SetupVisualAndCollider()
        {
            var sphereCol = GetComponent<SphereCollider>();
            if (sphereCol == null)
            {
                sphereCol = gameObject.AddComponent<SphereCollider>();
            }
            sphereCol.isTrigger = true;
            sphereCol.radius = radius;

            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            // Visual bawang putih jika belum ada
            if (transform.Find("GarlicVisual") == null)
            {
                var visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visualObj.name = "GarlicVisual";
                visualObj.transform.SetParent(transform, false);
                visualObj.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);

                // Hapus collider pada visual primitive
                var col = visualObj.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rend = visualObj.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    var mat = new Material(litShader);
                    mat.name = "M_Garlic_Clove";
                    mat.color = new Color(0.96f, 0.95f, 0.88f, 1f); // Warna siung bawang putih
                    rend.material = mat;
                }

                // Tambahkan jejak partikel putih / garlic trail
                var trailObj = new GameObject("GarlicTrail");
                trailObj.transform.SetParent(transform, false);
                var trail = trailObj.AddComponent<TrailRenderer>();
                trail.time = 0.35f;
                trail.startWidth = 0.12f;
                trail.endWidth = 0.01f;
                trail.startColor = new Color(1f, 1f, 0.9f, 0.8f);
                trail.endColor = new Color(1f, 1f, 0.9f, 0f);

                Shader spriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Lit");
                trail.material = new Material(spriteShader);
            }
        }

        private void Update()
        {
            // Bergerak maju
            transform.position += moveDirection * (speed * Time.deltaTime);

            aliveTime += Time.deltaTime;
            bool canManage = isServer || (!NetworkClient.active && !NetworkServer.active);
            if (canManage && aliveTime >= maxLifetime && !hasHit)
            {
                if (isServer) NetworkServer.Destroy(gameObject);
                else Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;

            // Hindari tabrakan dengan penembak sendiri
            var otherIdentity = other.GetComponentInParent<NetworkIdentity>();
            if (otherIdentity != null && otherIdentity.netId == shooterNetId && shooterNetId != 0)
            {
                return;
            }

            // Hindari tabrakan dengan proyektil lain
            if (other.GetComponent<GarlicProjectile>() != null) return;

            bool canManage = isServer || (!NetworkClient.active && !NetworkServer.active);

            // Deteksi target karakter
            var targetPcc = other.GetComponentInParent<PlayerCharacterController>();
            if (targetPcc != null)
            {
                var ghostVanish = targetPcc.GetComponent<KuntilanakGhostVanish>();
                var roleManager = targetPcc.GetComponent<PlayerRoleManager>();
                var targetHealth = targetPcc.GetComponent<PlayerHealth>();

                // Periksa apakah hantu sedang dalam wujud menghilang/gaib
                if (ghostVanish != null && ghostVanish.IsGhostForm)
                {
                    // Menembus tanpa melukai hantu gaib
                    Debug.Log("[GarlicProjectile] Menembus Kuntilanak dalam wujud gaib (IsGhostForm = true)! Serangan tidak berefek.");
                    return; // Lanjut terbang tanpa meledak/despawn
                }

                // Jika mengenai hantu (atau wujud padat)
                if (canManage)
                {
                    hasHit = true;
                    if (targetHealth != null && !targetHealth.IsDead)
                    {
                        targetHealth.TakeDamage(damage);
                        Debug.Log($"[GarlicProjectile] Siung bawang putih mengenai target! Damage: {damage}");
                    }
                    if (isServer)
                    {
                        RpcPlayImpactEffect(transform.position);
                        NetworkServer.Destroy(gameObject);
                    }
                    else
                    {
                        PlayImpactLocal(transform.position);
                        Destroy(gameObject);
                    }
                }
                return;
            }

            // Jika menabrak rintangan dunia (tembok, tanah, pohon)
            if (!other.isTrigger)
            {
                if (canManage)
                {
                    hasHit = true;
                    if (isServer)
                    {
                        RpcPlayImpactEffect(transform.position);
                        NetworkServer.Destroy(gameObject);
                    }
                    else
                    {
                        PlayImpactLocal(transform.position);
                        Destroy(gameObject);
                    }
                }
            }
        }

        private static AudioClip CreateGarlicCrunchClip()
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * 0.15f);
            AudioClip clip = AudioClip.Create("GarlicPop", totalSamples, 1, sampleRate, false);
            float[] samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 30f);
                float noise = (Random.value * 2f - 1f) * 0.4f;
                float tone = Mathf.Sin(2f * Mathf.PI * 450f * t) * 0.6f;
                samples[i] = (tone + noise) * env;
            }
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayImpactLocal(Vector3 pos)
        {
            var sfxObj = new GameObject("GarlicImpactSFX");
            sfxObj.transform.position = pos;
            var audio = sfxObj.AddComponent<AudioSource>();
            audio.spatialBlend = 0.8f;
            audio.pitch = Random.Range(0.9f, 1.2f);
            audio.PlayOneShot(CreateGarlicCrunchClip());
            Destroy(sfxObj, 1.5f);
        }

        [ClientRpc]
        private void RpcPlayImpactEffect(Vector3 pos)
        {
            // Buat efek splat audio/visual saat bawang putih pecah
            var sfxObj = new GameObject("GarlicImpactSFX");
            sfxObj.transform.position = pos;
            var audioSource = sfxObj.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 25f;
            audioSource.playOnAwake = false;

            audioSource.clip = CreateGarlicCrunchClip();
            audioSource.Play();
            Destroy(sfxObj, 0.8f);
        }
    }
}
