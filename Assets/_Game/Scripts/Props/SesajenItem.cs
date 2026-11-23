using UnityEngine;
using Mirror;

namespace Pucaci.Gameplay
{
    /// <summary>
    /// Objek Sesajen mistis yang tersebar di Desa Pucaci.
    /// Survivor harus mendekati dan menghancurkan seluruh sesajen untuk mengusir Kuntilanak dan memenangkan game.
    /// Impostor (Kuntilanak dalam wujud manusia) dapat memperbaiki kembali sesajen yang sudah hancur.
    /// </summary>
    public class SesajenItem : NetworkBehaviour
    {
        [Header("State")]
        [SyncVar(hook = nameof(OnDestroyedStateChanged))]
        private bool isDestroyed = false;

        public bool IsDestroyed => isDestroyed;

        [Header("Interaction Settings")]
        [Tooltip("Waktu yang dibutuhkan survivor untuk menghancurkan sesajen (detik)")]
        [SerializeField] private float destructionTime = 2.5f;
        public float DestructionTime => destructionTime;

        [Tooltip("Waktu yang dibutuhkan Impostor (wujud manusia) untuk memperbaiki sesajen (detik)")]
        [SerializeField] private float repairTime = 3.0f;
        public float RepairTime => repairTime;

        [Header("Visual Elements")]
        [SerializeField] private GameObject visualContainer;
        [SerializeField] private Light candleLight;
        [SerializeField] private ParticleSystem incenseSmokeParticles;

        [Header("Ruins / Remains Elements (Bekas Sesajen Hancur)")]
        [Tooltip("Kontainer objek fisik pecahan dan abu sesajen yang hancur")]
        [SerializeField] private GameObject ruinsContainer;

        [Tooltip("Cahaya pendaran bara mistis / sinyal gaib pada bekas sesajen hancur")]
        [SerializeField] private Light ruinsBeaconLight;

        [Tooltip("Partikel asap bara dan abu membara pada bekas sesajen")]
        [SerializeField] private ParticleSystem ruinsSmokeParticles;

        [Header("Audio Elements")]
        [SerializeField] private AudioSource ambientAudio;
        [SerializeField] private AudioSource sfxAudio;

        private Collider rootCollider;
        private float lightOriginalIntensity = 1.6f;
        private float flickerSeed;

        private void Awake()
        {
            flickerSeed = Random.Range(0f, 100f);
            rootCollider = GetComponent<Collider>();
            if (candleLight != null)
            {
                lightOriginalIntensity = candleLight.intensity;
            }

            ResolveReferences();
            SetupAudios();
        }

        private void Start()
        {
            ResolveReferences();
            ApplyInitialStateVisuals();
        }

        public void ResolveReferences()
        {
            if (visualContainer == null)
            {
                var vis = transform.Find("Visual");
                if (vis != null) visualContainer = vis.gameObject;
            }

            if (ruinsContainer == null)
            {
                var r = transform.Find("Ruins");
                if (r != null) ruinsContainer = r.gameObject;
            }

            if (candleLight == null)
            {
                var cl = transform.Find("Candle_Light");
                if (cl != null) candleLight = cl.GetComponent<Light>();
            }

            if (incenseSmokeParticles == null)
            {
                var ism = transform.Find("Incense_Smoke");
                if (ism != null) incenseSmokeParticles = ism.GetComponent<ParticleSystem>();
            }

            if (ruinsBeaconLight == null && ruinsContainer != null)
            {
                var rbl = ruinsContainer.transform.Find("Ruins_Beacon");
                if (rbl != null) ruinsBeaconLight = rbl.GetComponent<Light>();
            }

            if (ruinsSmokeParticles == null && ruinsContainer != null)
            {
                var rsp = ruinsContainer.transform.Find("Ruins_Smoke");
                if (rsp != null) ruinsSmokeParticles = rsp.GetComponent<ParticleSystem>();
            }
        }

        private void ApplyInitialStateVisuals()
        {
            if (visualContainer != null)
            {
                visualContainer.SetActive(!isDestroyed);
            }

            if (candleLight != null)
            {
                candleLight.enabled = !isDestroyed;
            }

            if (ruinsContainer != null)
            {
                ruinsContainer.SetActive(isDestroyed);
            }

            if (ruinsBeaconLight != null)
            {
                ruinsBeaconLight.enabled = isDestroyed;
            }

            if (ruinsSmokeParticles != null)
            {
                if (isDestroyed) ruinsSmokeParticles.Play();
                else ruinsSmokeParticles.Stop();
            }
        }

        private void SetupAudios()
        {
            if (ambientAudio == null)
            {
                ambientAudio = gameObject.AddComponent<AudioSource>();
                ambientAudio.playOnAwake = true;
                ambientAudio.loop = true;
                ambientAudio.spatialBlend = 1.0f;
                ambientAudio.minDistance = 1.5f;
                ambientAudio.maxDistance = 14.0f;
                ambientAudio.volume = 0.45f;
                ambientAudio.clip = CreateIncenseAmbientClip();
                ambientAudio.Play();
            }

            if (sfxAudio == null)
            {
                sfxAudio = gameObject.AddComponent<AudioSource>();
                sfxAudio.playOnAwake = false;
                sfxAudio.spatialBlend = 0.8f;
                sfxAudio.minDistance = 3.0f;
                sfxAudio.maxDistance = 30.0f;
                sfxAudio.volume = 0.9f;
            }
        }

        private void Update()
        {
            if (isDestroyed)
            {
                // Pendaran bara mistis pada bekas sesajen hancur (denyut gaib)
                if (ruinsBeaconLight != null && ruinsBeaconLight.enabled)
                {
                    float pulse = 0.50f + 0.25f * Mathf.Sin(Time.time * 4f);
                    ruinsBeaconLight.intensity = pulse;
                }
                return;
            }

            // Efek kedip lilin temaram mistis
            if (candleLight != null)
            {
                float noise = Mathf.PerlinNoise(Time.time * 8f, flickerSeed);
                candleLight.intensity = lightOriginalIntensity * (0.85f + noise * 0.35f);
            }
        }

        /// <summary>
        /// Dipanggil oleh survivor saat selesai menahan tombol interaksi untuk menghancurkan sesajen.
        /// </summary>
        public void DestroySesajen()
        {
            if (isDestroyed) return;

            if (NetworkServer.active)
            {
                isDestroyed = true;
            }
            else if (NetworkClient.active)
            {
                CmdRequestDestroy();
            }
            else
            {
                // Mode offline / singleplayer / Editor simulation
                isDestroyed = true;
                ExecuteDestructionEffects();
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestDestroy()
        {
            if (isDestroyed) return;
            isDestroyed = true;
        }

        /// <summary>
        /// Dipanggil oleh Impostor (dalam wujud manusia) untuk memperbaiki sesajen yang hancur.
        /// </summary>
        public void RepairSesajen()
        {
            if (!isDestroyed) return;

            if (NetworkServer.active)
            {
                isDestroyed = false;
            }
            else if (NetworkClient.active)
            {
                CmdRequestRepair();
            }
            else
            {
                // Mode offline / singleplayer / Editor simulation
                isDestroyed = false;
                ExecuteRepairEffects();
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestRepair()
        {
            if (!isDestroyed) return;
            isDestroyed = false;
        }

        private void OnDestroyedStateChanged(bool oldVal, bool newVal)
        {
            if (newVal)
            {
                ExecuteDestructionEffects();
            }
            else
            {
                ExecuteRepairEffects();
            }
        }

        private void ExecuteDestructionEffects()
        {
            // Matikan visual utuh & efek lilin/dupa
            if (visualContainer != null)
            {
                visualContainer.SetActive(false);
            }

            if (candleLight != null)
            {
                candleLight.enabled = false;
            }

            if (incenseSmokeParticles != null)
            {
                incenseSmokeParticles.Stop();
            }

            if (ambientAudio != null && ambientAudio.isPlaying)
            {
                ambientAudio.Stop();
            }

            // Tampilkan visual bekas sesajen hancur (abu, pecahan nampan, arang, bara api gaib)
            if (ruinsContainer != null)
            {
                ruinsContainer.SetActive(true);
            }

            if (ruinsBeaconLight != null)
            {
                ruinsBeaconLight.enabled = true;
            }

            if (ruinsSmokeParticles != null)
            {
                ruinsSmokeParticles.Play();
            }

            // Mainkan efek partikel ledakan abu/bara/asap hitam
            SpawnDestructionParticles();

            // Mainkan suara hancur & jeritan gaib
            PlayDestructionAudio();

            // Tetap aktifkan collider sebagai trigger agar pemain impostor bisa mendeteksi & memperbaikinya
            if (rootCollider != null)
            {
                rootCollider.enabled = true;
                rootCollider.isTrigger = true;
            }

            // Notifikasi ke manager objektif
            if (SesajenManager.Instance != null)
            {
                SesajenManager.Instance.NotifySesajenDestroyed(this);
            }
        }

        private void ExecuteRepairEffects()
        {
            // Sembunyikan bekas sesajen hancur
            if (ruinsContainer != null)
            {
                ruinsContainer.SetActive(false);
            }

            if (ruinsBeaconLight != null)
            {
                ruinsBeaconLight.enabled = false;
            }

            if (ruinsSmokeParticles != null)
            {
                ruinsSmokeParticles.Stop();
            }

            // Nyalakan kembali visual & efek lilin/dupa utuh
            if (visualContainer != null)
            {
                visualContainer.SetActive(true);
            }

            if (candleLight != null)
            {
                candleLight.enabled = true;
            }

            if (incenseSmokeParticles != null)
            {
                incenseSmokeParticles.Play();
            }

            if (ambientAudio != null && !ambientAudio.isPlaying)
            {
                ambientAudio.Play();
            }

            // Kembalikan collider solid normal
            if (rootCollider != null)
            {
                rootCollider.enabled = true;
                rootCollider.isTrigger = false;
            }

            // Mainkan efek partikel pemulihan spiritual mistis
            SpawnRepairParticles();

            // Mainkan suara perbaikan gaib (denting lonceng/gong ritual suci)
            PlayRepairAudio();

            // Notifikasi ke manager objektif
            if (SesajenManager.Instance != null)
            {
                SesajenManager.Instance.NotifySesajenRepaired(this);
            }
        }

        private void SpawnDestructionParticles()
        {
            var burstObj = new GameObject("SesajenBurstFX");
            burstObj.transform.position = transform.position + Vector3.up * 0.2f;

            var ps = burstObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1.0f;
            main.loop = false;
            main.startLifetime = 1.5f;
            main.startSpeed = 3.5f;
            main.startSize = 0.45f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.1f, 0.1f, 0.9f), new Color(0.9f, 0.35f, 0.05f, 1f));
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            var renderer = burstObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));

            ps.Play();
        }

        private void SpawnRepairParticles()
        {
            var repairObj = new GameObject("SesajenRepairFX");
            repairObj.transform.position = transform.position + Vector3.up * 0.2f;

            var ps = repairObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1.2f;
            main.loop = false;
            main.startLifetime = 1.8f;
            main.startSpeed = 2.0f;
            main.startSize = 0.35f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.2f, 1.0f, 0.9f), new Color(0.2f, 0.9f, 1.0f, 1.0f));
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.35f;

            var renderer = repairObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));

            ps.Play();
        }

        private void PlayDestructionAudio()
        {
            if (sfxAudio != null)
            {
                sfxAudio.PlayOneShot(CreateDestructionSound());
            }
        }

        private void PlayRepairAudio()
        {
            if (sfxAudio != null)
            {
                sfxAudio.PlayOneShot(CreateRepairSound());
            }
        }

        private static AudioClip CreateIncenseAmbientClip()
        {
            int sampleRate = 22050;
            float duration = 2.0f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.12f;
                float noise = (Random.value * 2f - 1f) * 0.035f;
                samples[i] = hum + noise;
            }

            var clip = AudioClip.Create("Sesajen_Incense_Ambient", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateDestructionSound()
        {
            int sampleRate = 22050;
            float duration = 1.2f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 4.5f);
                float boom = Mathf.Sin(2f * Mathf.PI * (110f - t * 60f) * t) * 0.6f;
                float hiss = (Random.value * 2f - 1f) * 0.3f * Mathf.Exp(-t * 2.5f);
                samples[i] = (boom + hiss) * env;
            }

            var clip = AudioClip.Create("Sesajen_Destroy_SFX", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateRepairSound()
        {
            int sampleRate = 22050;
            float duration = 2.0f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Suara mistis pemulihan sesajen (harmonik spiritual berpadu desis kemenyan hangat)
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 1.5f);
                float tone1 = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.4f;
                float tone2 = Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.25f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.15f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                samples[i] = (tone1 + tone2 + shimmer) * env;
            }

            var clip = AudioClip.Create("Sesajen_Repair_SFX", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
