using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Pucaci.Network
{
    /// <summary>
    /// Sistem Proximity Voice Chat (Obrolan Suara 3D Spasial dengan Attenuasi Jarak).
    /// - Pemain yang berdekatan (< 2m) terdengar jernih dan penuh.
    /// - Semakin jauh posisi antar pemain, suara memudar secara bertahap (linear rolloff).
    /// - Di luar jangkauan (25m), suara hilang sepenuhnya.
    /// - Kontrol [V] Cerdas: Bisa DITAHAN (Push-to-Talk) atau DITEKAN/TAP SEKALI (Toggle On/Off).
    /// - Menampilkan Logo Sound murni (Sprite icon suara 🔊) di HUD dan di atas kepala pemain.
    /// - Sinkronisasi status bicara dan logo sound secara real-time via Mirror SyncVar.
    /// - Transmisi audio via Channels.Reliable dengan downsampling 16kHz hemat bandwidth & bebas packet drop.
    /// - Dilengkapi loopback sidetone lokal agar pemain dapat memastikan suara terdengar saat tes mandiri.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public class ProximityVoiceChat : NetworkBehaviour
    {
        public static ProximityVoiceChat LocalInstance { get; private set; }

        [Header("Jarak Suara (Proximity 3D Settings)")]
        [Tooltip("Jarak minimum di mana suara terdengar 100% penuh dan sangat jelas (meter)")]
        [SerializeField] private float minVoiceDistance = 2.0f;

        [Tooltip("Jarak maksimum di mana suara memudar hingga tidak terdengar sama sekali (meter)")]
        [SerializeField] private float maxVoiceDistance = 25.0f;

        [Tooltip("Mode redaman suara berdasarkan jarak")]
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

        [Header("Pengaturan Mikrofon & Transmisi")]
        [Tooltip("Frekuensi sampling VoIP untuk transmisi jaringan (16000 Hz standar broadcast)")]
        [SerializeField] private int voipSampleRate = 16000;

        [Tooltip("Interval pengiriman paket suara ke server (detik, 25ms = 800 bytes per paket, aman di bawah MTU)")]
        [SerializeField] private float packetInterval = 0.025f;

        [Tooltip("Tombol Push-to-Talk / Toggle Voice (Default: V)")]
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;

        [Header("Audio Monitoring (Loopback / Sidetone)")]
        [Tooltip("Mendengar suara sendiri untuk verifikasi / tes mandiri bahwa mikrofon & audio 3D berfungsi")]
        [SerializeField] private bool loopbackSelfAudio = true;

        [Header("Komponen Audio 3D")]
        [SerializeField] private AudioSource spatialAudioSource;

        // Status bicara yang disinkronkan ke seluruh pemain via Network SyncVar
        [SyncVar(hook = nameof(OnSyncSpeakingChanged))]
        private bool syncIsSpeaking = false;

        // Status bicara publik lokal
        public bool IsSpeaking { get; private set; }
        public float CurrentMicLevel { get; private set; }

        // Variabel Network Identity yang aman
        private NetworkIdentity cachedNetIdentity;

        public bool SafeIsLocalPlayer
        {
            get
            {
                if (cachedNetIdentity == null)
                {
                    cachedNetIdentity = GetComponent<NetworkIdentity>();
                }
                if (cachedNetIdentity != null)
                {
                    return cachedNetIdentity.isLocalPlayer || cachedNetIdentity.isOwned;
                }
                return false;
            }
        }

        // Variabel perekaman mikrofon lokal
        private AudioClip micClip;
        private string activeMicDevice;
        private int hardwareSampleRate = 48000;
        private int lastSamplePosition;
        private float nextPacketTime;
        private float[] sampleBuffer;
        private bool isLocalInitialized = false;

        // Kontrol input cerdas (Tap to Toggle / Hold to Talk)
        private bool isToggleActive = false;
        private float keyPressStartTime = 0f;
        private bool wasKeyPressedLastFrame = false;

        // Buffer pemutaran audio (Ring Buffer thread-safe)
        private const int RingBufferSize = 96000;
        private float[] ringBuffer = new float[RingBufferSize];
        private int writeIndex = 0;
        private float playbackPosition = 0f;
        private readonly object ringLock = new object();
        private int remoteSampleRate = 16000;
        private float lastVoiceReceivedTime = 0f;

        // UI Indikator & Sprite Logo Suara
        private static Sprite cachedSpeakerSprite;
        private GameObject overheadVoiceIndicator;
        private SpriteRenderer overheadVoiceRenderer;
        private static GameObject hudCanvasObj;
        private GameObject hudVoiceIndicator;
        private Image hudVoiceIcon;

        public static Sprite GetSpeakerSprite()
        {
            if (cachedSpeakerSprite != null) return cachedSpeakerSprite;
            cachedSpeakerSprite = Resources.Load<Sprite>("UI/VoiceSpeakerIcon");
            if (cachedSpeakerSprite == null)
            {
                cachedSpeakerSprite = CreateProceduralSpeakerSprite();
            }
            return cachedSpeakerSprite;
        }

        private static Sprite CreateProceduralSpeakerSprite()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] cols = new Color[size * size];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.clear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float alpha = 0f;

                    // Kotak dasar speaker
                    if (px >= 11 && px <= 22 && py >= 24 && py <= 40) alpha = 1f;

                    // Corong trapesium
                    if (px >= 22 && px <= 36)
                    {
                        float t = (px - 22f) / 14f;
                        float yMin = Mathf.Lerp(24f, 11f, t);
                        float yMax = Mathf.Lerp(40f, 53f, t);
                        if (py >= yMin && py <= yMax) alpha = 1f;
                    }

                    // Gelombang suara 1
                    float d1 = Vector2.Distance(new Vector2(px, py), new Vector2(30f, 32f));
                    float a1 = Mathf.Atan2(py - 32f, px - 30f) * Mathf.Rad2Deg;
                    if (d1 >= 12f && d1 <= 15f && a1 >= -45f && a1 <= 45f) alpha = 1f;

                    // Gelombang suara 2
                    float d2 = Vector2.Distance(new Vector2(px, py), new Vector2(30f, 32f));
                    float a2 = Mathf.Atan2(py - 32f, px - 30f) * Mathf.Rad2Deg;
                    if (d2 >= 21f && d2 <= 24f && a2 >= -45f && a2 <= 45f) alpha = 1f;

                    // Gelombang suara 3
                    float d3 = Vector2.Distance(new Vector2(px, py), new Vector2(30f, 32f));
                    float a3 = Mathf.Atan2(py - 32f, px - 30f) * Mathf.Rad2Deg;
                    if (d3 >= 30f && d3 <= 33f && a3 >= -45f && a3 <= 45f) alpha = 1f;

                    if (alpha > 0f) cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void Awake()
        {
            cachedNetIdentity = GetComponent<NetworkIdentity>();
            SetupSpatialAudioSource();
            CreateOverheadIndicator();
        }

        private void Start()
        {
            bool isOffline = !NetworkClient.active && !NetworkServer.active;
            if (SafeIsLocalPlayer || isOffline)
            {
                InitializeLocalVoice();
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            InitializeLocalVoice();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!SafeIsLocalPlayer && spatialAudioSource != null)
            {
                spatialAudioSource.mute = false;
                if (!spatialAudioSource.isPlaying)
                {
                    spatialAudioSource.Play();
                }
            }
        }

        private void SetupSpatialAudioSource()
        {
            if (spatialAudioSource == null)
            {
                spatialAudioSource = GetComponent<AudioSource>();
            }

            if (spatialAudioSource == null)
            {
                spatialAudioSource = gameObject.AddComponent<AudioSource>();
            }

            spatialAudioSource.volume = 1.0f;
            spatialAudioSource.spatialBlend = 1.0f; // 100% 3D Spasial
            spatialAudioSource.minDistance = minVoiceDistance;
            spatialAudioSource.maxDistance = maxVoiceDistance;
            spatialAudioSource.rolloffMode = rolloffMode;
            spatialAudioSource.dopplerLevel = 0f;
            spatialAudioSource.spread = 0f;
            spatialAudioSource.playOnAwake = false;
            spatialAudioSource.loop = true;
            spatialAudioSource.priority = 10;
            spatialAudioSource.mute = false;

            if (spatialAudioSource.clip == null)
            {
                spatialAudioSource.clip = AudioClip.Create("VoiceLoopSilent", 48000, 1, 48000, false);
            }

            if (!spatialAudioSource.isPlaying)
            {
                spatialAudioSource.Play();
            }
        }

        private void InitializeLocalVoice()
        {
            if (isLocalInitialized) return;
            isLocalInitialized = true;
            LocalInstance = this;

            if (spatialAudioSource != null)
            {
                spatialAudioSource.mute = false;
                if (!spatialAudioSource.isPlaying)
                {
                    spatialAudioSource.Play();
                }
            }

            StartMicrophoneCapture();
            CreateHUDVoiceIndicator();
        }

        private void StartMicrophoneCapture()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[ProximityVoiceChat] Tidak ada perangkat mikrofon yang terdeteksi.");
                return;
            }

            activeMicDevice = null;
            if (Pucaci.UI.SettingsManager.Instance != null && !string.IsNullOrEmpty(Pucaci.UI.SettingsManager.Instance.MicDevice))
            {
                foreach (var d in Microphone.devices)
                {
                    if (d == Pucaci.UI.SettingsManager.Instance.MicDevice)
                    {
                        activeMicDevice = d;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(activeMicDevice))
            {
                activeMicDevice = Microphone.devices[0];
            }

            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(activeMicDevice, out minFreq, out maxFreq);
            hardwareSampleRate = 48000;
            if (maxFreq > 0)
            {
                if (minFreq == maxFreq)
                {
                    hardwareSampleRate = minFreq;
                }
                else
                {
                    hardwareSampleRate = Mathf.Clamp(hardwareSampleRate, minFreq, maxFreq);
                }
            }

            Debug.Log($"<color=#00FFAA>[ProximityVoiceChat] Memulai mikrofon: {activeMicDevice} ({hardwareSampleRate} Hz)</color>");
            micClip = Microphone.Start(activeMicDevice, true, 10, hardwareSampleRate);
            lastSamplePosition = 0;
            sampleBuffer = new float[hardwareSampleRate];
        }

        private float voiceKeyDownTime = 0f;

        /// <summary>
        /// Dipanggil saat tombol V mulai ditekan (Push-to-Talk & Tap Toggle).
        /// </summary>
        public void OnVoiceKeyDown()
        {
            voiceKeyDownTime = Time.time;
            isToggleActive = !isToggleActive;
            IsSpeaking = isToggleActive;
            Debug.Log($"<color=#00FFAA>[ProximityVoiceChat] Voice Key Down! IsSpeaking={IsSpeaking}</color>");
        }

        /// <summary>
        /// Dipanggil saat tombol V dilepas. Jika ditahan lama (> 0.25s) maka mode PTT selesai (mati).
        /// </summary>
        public void OnVoiceKeyUp()
        {
            float holdDuration = Time.time - voiceKeyDownTime;
            if (holdDuration >= 0.25f)
            {
                isToggleActive = false;
                IsSpeaking = false;
                Debug.Log("<color=#FFAA00>[ProximityVoiceChat] Voice PTT Released -> Mute</color>");
            }
        }

        /// <summary>
        /// Dipanggil langsung untuk toggle on/off mic dengan sekali pencet / klik tombol HUD.
        /// </summary>
        public void ToggleVoice()
        {
            isToggleActive = !isToggleActive;
            IsSpeaking = isToggleActive;
            Debug.Log($"<color=#00FFAA>[ProximityVoiceChat] Voice Toggled: {isToggleActive}</color>");
        }

        private void Update()
        {
            bool isLocal = SafeIsLocalPlayer || (!NetworkClient.active && !NetworkServer.active);

            if (isLocal)
            {
                if (!isLocalInitialized)
                {
                    InitializeLocalVoice();
                }

                // Deteksi input tombol V secara langsung & mandiri
                CheckInputState();

                // Jalankan transmisi audio jika sedang berbicara
                UpdateLocalVoiceTransmission();

                // Perbarui tampilan visual lokal
                UpdateHUDIndicator();
                UpdateOverheadVisuals(IsSpeaking);
            }
            else
            {
                // Pada remote player, tampilkan logo sound jika SyncVar aktif atau paket audio masuk
                bool isRemoteTalking = syncIsSpeaking || (Time.time - lastVoiceReceivedTime < 0.35f);
                UpdateOverheadVisuals(isRemoteTalking);
            }
        }

        private void CheckInputState()
        {
            bool isVDown = false;
            bool isVUp = false;
            bool isVHeld = false;

#if ENABLE_INPUT_SYSTEM
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    isVDown = kb.vKey.wasPressedThisFrame;
                    isVUp = kb.vKey.wasReleasedThisFrame;
                    isVHeld = kb.vKey.isPressed;
                }
            }
            catch { }
#endif

            if (!isVDown && !isVUp && !isVHeld)
            {
                try
                {
                    isVDown = Input.GetKeyDown(pushToTalkKey);
                    isVUp = Input.GetKeyUp(pushToTalkKey);
                    isVHeld = Input.GetKey(pushToTalkKey);
                }
                catch { }
            }

            if (isVDown)
            {
                voiceKeyDownTime = Time.time;
                isToggleActive = !isToggleActive;
            }

            if (isVUp)
            {
                float holdDuration = Time.time - voiceKeyDownTime;
                if (holdDuration >= 0.25f)
                {
                    isToggleActive = false;
                }
            }

            // Suara aktif jika tombol V sedang ditahan atau mode toggle aktif
            bool shouldSpeak = isVHeld || isToggleActive;
            if (IsSpeaking != shouldSpeak)
            {
                IsSpeaking = shouldSpeak;
                Debug.Log($"<color=#00FFAA>[ProximityVoiceChat] Status Bicara: {IsSpeaking} (Held={isVHeld}, Toggle={isToggleActive})</color>");
                if (NetworkClient.active)
                {
                    CmdSetSpeaking(IsSpeaking);
                }
            }
        }

        private void UpdateLocalVoiceTransmission()
        {
            if (!IsSpeaking)
            {
                // Saat tidak bicara, sinkronkan posisi rekaman agar saat tombol ditekan audio tidak basi/tertunda
                if (!string.IsNullOrEmpty(activeMicDevice))
                {
                    lastSamplePosition = Microphone.GetPosition(activeMicDevice);
                }
                CurrentMicLevel = 0f;
                return;
            }

            // Fallback simulasi suara jika perangkat mikrofon fisik tidak terdeteksi
            if (micClip == null || string.IsNullOrEmpty(activeMicDevice))
            {
                if (Time.time >= nextPacketTime)
                {
                    nextPacketTime = Time.time + packetInterval;
                    int simSamples = (int)(voipSampleRate * packetInterval);
                    float[] simChunk = new float[simSamples];
                    float phase = Time.time * 440f * 2f * Mathf.PI;
                    for (int i = 0; i < simSamples; i++)
                    {
                        simChunk[i] = Mathf.Sin(phase + (float)i / voipSampleRate * 440f * 2f * Mathf.PI) * 0.15f;
                    }
                    CurrentMicLevel = 0.15f;
                    byte[] simBytes = FloatToPcm16(simChunk);
                    if (NetworkClient.active) CmdSendVoice(simBytes, voipSampleRate);
                    if (loopbackSelfAudio) ReceiveVoicePacket(simBytes, voipSampleRate);
                }
                return;
            }

            int currentMicPos = Microphone.GetPosition(activeMicDevice);
            if (currentMicPos < 0 || currentMicPos == lastSamplePosition) return;

            int samplesToRead = 0;
            if (currentMicPos > lastSamplePosition)
            {
                samplesToRead = currentMicPos - lastSamplePosition;
            }
            else
            {
                samplesToRead = (micClip.samples - lastSamplePosition) + currentMicPos;
            }

            if (Time.time >= nextPacketTime && samplesToRead > 0)
            {
                nextPacketTime = Time.time + packetInterval;

                if (samplesToRead > sampleBuffer.Length)
                {
                    samplesToRead = sampleBuffer.Length;
                }

                float[] chunk = new float[samplesToRead];
                if (currentMicPos > lastSamplePosition)
                {
                    micClip.GetData(chunk, lastSamplePosition);
                }
                else
                {
                    int firstPart = micClip.samples - lastSamplePosition;
                    float[] part1 = new float[firstPart];
                    micClip.GetData(part1, lastSamplePosition);
                    Array.Copy(part1, 0, chunk, 0, firstPart);

                    int secondPart = currentMicPos;
                    if (secondPart > 0)
                    {
                        float[] part2 = new float[secondPart];
                        micClip.GetData(part2, 0);
                        Array.Copy(part2, 0, chunk, firstPart, secondPart);
                    }
                }

                lastSamplePosition = currentMicPos;

                // Hitung volume RMS untuk VU meter
                float sum = 0f;
                for (int i = 0; i < chunk.Length; i++)
                {
                    sum += chunk[i] * chunk[i];
                }
                CurrentMicLevel = Mathf.Sqrt(sum / chunk.Length);

                // Downsample ke 16.000 Hz untuk pengiriman VoIP yang efisien
                float[] voipChunk = Downsample(chunk, hardwareSampleRate, voipSampleRate);
                byte[] pcmBytes = FloatToPcm16(voipChunk);

                // Kirim via Reliable channel (bebas packet drop KCP)
                if (NetworkClient.active)
                {
                    CmdSendVoice(pcmBytes, voipSampleRate);
                }

                // Loopback sidetone lokal
                if (loopbackSelfAudio)
                {
                    ReceiveVoicePacket(pcmBytes, voipSampleRate);
                }
            }
        }

        #region Mirror Network Commands & SyncVar
        private void OnSyncSpeakingChanged(bool oldVal, bool newVal)
        {
            UpdateOverheadVisuals(newVal);
        }

        [Command(channel = Channels.Reliable)]
        private void CmdSetSpeaking(bool speaking)
        {
            syncIsSpeaking = speaking;
        }

        [Command(channel = Channels.Reliable)]
        private void CmdSendVoice(byte[] pcmData, int sampleRate)
        {
            if (pcmData == null || pcmData.Length == 0) return;
            RpcReceiveVoice(pcmData, sampleRate);
        }

        [ClientRpc(channel = Channels.Reliable, includeOwner = false)]
        private void RpcReceiveVoice(byte[] pcmData, int sampleRate)
        {
            if (SafeIsLocalPlayer) return;
            ReceiveVoicePacket(pcmData, sampleRate);
        }
        #endregion

        private void ReceiveVoicePacket(byte[] pcmData, int sampleRate)
        {
            float[] samples = Pcm16ToFloat(pcmData);
            if (samples == null || samples.Length == 0) return;

            remoteSampleRate = sampleRate;

            lock (ringLock)
            {
                bool wasSilent = (Time.time - lastVoiceReceivedTime > 0.35f);
                if (wasSilent)
                {
                    playbackPosition = writeIndex;
                }

                for (int i = 0; i < samples.Length; i++)
                {
                    ringBuffer[writeIndex] = samples[i];
                    writeIndex = (writeIndex + 1) % RingBufferSize;
                }
            }

            lastVoiceReceivedTime = Time.time;

            if (spatialAudioSource != null)
            {
                spatialAudioSource.mute = false;
                if (!spatialAudioSource.isPlaying)
                {
                    spatialAudioSource.Play();
                }
            }
        }

        /// <summary>
        /// Callback native Unity Audio Thread: Mengalirkan suara ke AudioSource secara real-time
        /// yang kemudian diproses oleh sistem 3D Spatial Audio bawaan Unity (fading jarak & panning).
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            bool isLocal = SafeIsLocalPlayer || (!NetworkClient.active && !NetworkServer.active);
            if (isLocal && !loopbackSelfAudio)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            int outputRate = AudioSettings.outputSampleRate;
            if (outputRate <= 0) outputRate = 48000;
            float resampleRatio = (float)remoteSampleRate / outputRate;

            lock (ringLock)
            {
                int currentPos = (int)playbackPosition % RingBufferSize;
                int buffered = (writeIndex >= currentPos)
                    ? (writeIndex - currentPos)
                    : (RingBufferSize - currentPos + writeIndex);

                // Jika audio hening > 0.35 detik atau buffer hampir kosong
                if (buffered < 64 || (Time.time - lastVoiceReceivedTime > 0.35f))
                {
                    for (int i = 0; i < data.Length; i++) data[i] = 0f;
                    return;
                }

                // Kompensasi latensi: jika tertumpuk lebih dari 0.2 detik, percepat playback
                if (buffered > 9600)
                {
                    playbackPosition = (writeIndex - 3200 + RingBufferSize) % RingBufferSize;
                }

                for (int i = 0; i < data.Length; i += channels)
                {
                    int curIdx = (int)playbackPosition % RingBufferSize;
                    int nextIdx = (curIdx + 1) % RingBufferSize;
                    float frac = playbackPosition - (int)playbackPosition;

                    float sample = Mathf.Lerp(ringBuffer[curIdx], ringBuffer[nextIdx], frac);

                    for (int c = 0; c < channels; c++)
                    {
                        data[i + c] = sample;
                    }

                    playbackPosition += resampleRatio;
                    if (playbackPosition >= RingBufferSize)
                    {
                        playbackPosition -= RingBufferSize;
                    }
                }
            }
        }

        #region Audio Resampling & Conversion Helpers
        private static float[] Downsample(float[] input, int srcRate, int dstRate)
        {
            if (srcRate == dstRate || input.Length == 0) return input;
            int dstLength = (int)((long)input.Length * dstRate / srcRate);
            if (dstLength <= 0) return new float[0];

            float[] output = new float[dstLength];
            float ratio = (float)srcRate / dstRate;
            for (int i = 0; i < dstLength; i++)
            {
                float srcIndex = i * ratio;
                int idx = (int)srcIndex;
                int next = Mathf.Min(idx + 1, input.Length - 1);
                float frac = srcIndex - idx;
                output[i] = Mathf.Lerp(input[idx], input[next], frac);
            }
            return output;
        }

        private static byte[] FloatToPcm16(float[] samples)
        {
            byte[] bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
                bytes[i * 2] = (byte)(val & 0xFF);
                bytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            return bytes;
        }

        private static float[] Pcm16ToFloat(byte[] bytes)
        {
            int sampleCount = bytes.Length / 2;
            float[] floats = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short val = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                floats[i] = val / 32768f;
            }
            return floats;
        }
        #endregion

        #region Visual Indicators (HUD & Overhead)
        private void CreateOverheadIndicator()
        {
            if (overheadVoiceIndicator != null) return;
            overheadVoiceIndicator = new GameObject("OverheadVoiceIndicator");
            overheadVoiceIndicator.transform.SetParent(transform, false);
            overheadVoiceIndicator.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            overheadVoiceIndicator.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

            overheadVoiceRenderer = overheadVoiceIndicator.AddComponent<SpriteRenderer>();
            overheadVoiceRenderer.sprite = GetSpeakerSprite();
            // Shader Sprites/Default yang unlit agar logo sound berpendar terang di malam hari
            var unlitMat = new Material(Shader.Find("Sprites/Default"));
            overheadVoiceRenderer.material = unlitMat;
            overheadVoiceRenderer.color = new Color(0f, 1f, 0.65f, 1f); // Neon green
            overheadVoiceRenderer.sortingOrder = 100;

            overheadVoiceIndicator.SetActive(false);
        }

        private void UpdateOverheadVisuals(bool isTalking)
        {
            if (overheadVoiceIndicator != null)
            {
                if (overheadVoiceIndicator.activeSelf != isTalking)
                {
                    overheadVoiceIndicator.SetActive(isTalking);
                }

                if (isTalking)
                {
                    overheadVoiceIndicator.transform.localPosition = new Vector3(0f, 2.45f, 0f);
                    float pulse = 0.55f + Mathf.PingPong(Time.time * 2.5f, 0.08f);
                    overheadVoiceIndicator.transform.localScale = new Vector3(pulse, pulse, pulse);

                    Camera cam = Camera.main;
                    if (cam == null) cam = FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        // Billboard murni: selalu sejajar datar sempurna dengan layar kamera
                        overheadVoiceIndicator.transform.rotation = cam.transform.rotation;
                    }
                }
            }
        }

        private void CreateHUDVoiceIndicator()
        {
            if (hudVoiceIndicator != null) return;

            // Gunakan Canvas Overlay mandiri dengan DontDestroyOnLoad agar selalu tampil konsisten di semua scene
            if (hudCanvasObj == null)
            {
                hudCanvasObj = GameObject.Find("VoiceChatOverlayCanvas");
            }

            if (hudCanvasObj == null)
            {
                hudCanvasObj = new GameObject("VoiceChatOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var targetCanvas = hudCanvasObj.GetComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.sortingOrder = 9999;
                var scaler = hudCanvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                DontDestroyOnLoad(hudCanvasObj);
            }

            Canvas overlayCanvas = hudCanvasObj.GetComponent<Canvas>();
            hudVoiceIndicator = new GameObject("HUDVoiceStatus", typeof(RectTransform), typeof(Image), typeof(Button));
            hudVoiceIndicator.transform.SetParent(overlayCanvas.transform, false);

            var btn = hudVoiceIndicator.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(ToggleVoice);
            }

            RectTransform rt = hudVoiceIndicator.GetComponent<RectTransform>();
            // Posisi di tengah bawah (tepat di atas action skill bar)
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 210f);
            rt.sizeDelta = new Vector2(64f, 64f);

            var bgImg = hudVoiceIndicator.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.08f, 0.12f, 0.85f);

            // Icon Image (Logo Sound murni 🔊 tanpa teks apa pun)
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(hudVoiceIndicator.transform, false);
            RectTransform irt = iconObj.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(48f, 48f);

            hudVoiceIcon = iconObj.GetComponent<Image>();
            hudVoiceIcon.sprite = GetSpeakerSprite();
            hudVoiceIcon.color = new Color(0f, 1f, 0.65f, 1f);

            // Sembunyikan saat awal (Hanya MUNCUL / KELUAR saat tombol V ditekan / aktif!)
            hudVoiceIndicator.SetActive(false);
        }

        private void UpdateHUDIndicator()
        {
            if (hudVoiceIndicator == null || hudVoiceIcon == null) return;

            // KELUAR (TAMPIL) SAAT BICARA, SEMBUNYI SAAT TIDAK BICARA
            if (hudVoiceIndicator.activeSelf != IsSpeaking)
            {
                hudVoiceIndicator.SetActive(IsSpeaking);
            }

            if (IsSpeaking)
            {
                hudVoiceIcon.color = new Color(0f, 1f, 0.65f, 1f); // Neon green
                hudVoiceIcon.transform.localScale = Vector3.one * (1f + Mathf.PingPong(Time.time * 3f, 0.18f));
            }
        }
        #endregion

        private void OnDestroy()
        {
            if (micClip != null && !string.IsNullOrEmpty(activeMicDevice))
            {
                if (Microphone.IsRecording(activeMicDevice))
                {
                    Microphone.End(activeMicDevice);
                }
            }

            if (hudVoiceIndicator != null)
            {
                Destroy(hudVoiceIndicator);
            }
        }
    }
}
