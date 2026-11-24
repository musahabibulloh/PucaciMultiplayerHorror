using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Pucaci.Network
{
    /// <summary>
    /// Sistem Proximity Voice Chat (Obrolan Suara 3D Spasial dengan Attenuasi Jarak).
    /// - Mengizinkan semua pemain berbicara langsung secara real-time via mikrofon.
    /// - Pemain yang berdekatan (< 2m) terdengar jernih dan penuh.
    /// - Semakin jauh posisi antar pemain, suara memudar secara bertahap (linear rolloff).
    /// - Jika pemain berada di luar jangkauan (> 20m), suara hilang sepenuhnya.
    /// - Push-to-Talk didedikasikan pada tombol [V] (atau berbicara langsung via Voice Activity Detection).
    /// - Dilengkapi fitur Loopback / Sidetone (dapat mendengar suara sendiri saat tes mandiri / solo).
    /// - Dilengkapi indikator berbicara di HUD lokal dan billboard neon di atas kepala pemain.
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
        [SerializeField] private float maxVoiceDistance = 20.0f;

        [Tooltip("Mode redaman suara berdasarkan jarak")]
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

        [Header("Pengaturan Mikrofon & Transmisi")]
        [Tooltip("Frekuensi sampling suara standar VoIP")]
        [SerializeField] private int captureSampleRate = 16000;

        [Tooltip("Interval pengiriman paket suara ke server (detik)")]
        [SerializeField] private float packetInterval = 0.06f;

        [Tooltip("Threshold sensitivitas VAD untuk mendeteksi suara bicara")]
        [SerializeField] private float voiceDetectionThreshold = 0.003f;

        [Tooltip("Tombol Push-to-Talk (Default: V)")]
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;

        [Header("Audio Monitoring (Loopback / Sidetone)")]
        [Tooltip("Mendengar suara sendiri untuk verifikasi / tes mandiri bahwa mikrofon & audio 3D berfungsi")]
        [SerializeField] private bool loopbackSelfAudio = true;

        [Header("Komponen Audio 3D")]
        [SerializeField] private AudioSource spatialAudioSource;

        // Status bicara publik
        public bool IsSpeaking { get; private set; }
        public float CurrentMicLevel { get; private set; }

        // Variabel Network Identity yang aman (menghindari NullReference jika netIdentity Mirror belum diset)
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

        // Variabel perekaman lokal
        private AudioClip micClip;
        private string activeMicDevice;
        private int actualSampleRate = 16000;
        private int lastSamplePosition;
        private float nextPacketTime;
        private float[] sampleBuffer;
        private bool isLocalInitialized = false;

        // Buffer pemutaran audio (Ring Buffer thread-safe)
        private const int RingBufferSize = 96000;
        private float[] ringBuffer = new float[RingBufferSize];
        private int writeIndex = 0;
        private int readIndex = 0;
        private float playbackPosition = 0f;
        private readonly object ringLock = new object();
        private int remoteSampleRate = 16000;
        private float lastVoiceReceivedTime = 0f;

        // UI Indikator
        private GameObject overheadVoiceIndicator;
        private TextMesh overheadVoiceText;
        private GameObject hudCanvasObj;
        private GameObject hudVoiceIndicator;
        private TMPro.TextMeshProUGUI hudVoiceTmp;

        private void Awake()
        {
            cachedNetIdentity = GetComponent<NetworkIdentity>();
            SetupSpatialAudioSource();
            CreateOverheadIndicator();
        }

        private void Start()
        {
            // Jika dalam mode offline/singleplayer atau belum terhubung ke Mirror,
            // inisialisasi sebagai local player agar pemain dapat langsung menguji mikrofon
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

            spatialAudioSource.spatialBlend = 1.0f; // 100% 3D Spasial
            spatialAudioSource.minDistance = minVoiceDistance;
            spatialAudioSource.maxDistance = maxVoiceDistance;
            spatialAudioSource.rolloffMode = rolloffMode;
            spatialAudioSource.dopplerLevel = 0f; // Tanpa distorsi Doppler saat berlari
            spatialAudioSource.spread = 0f;
            spatialAudioSource.playOnAwake = false;
            spatialAudioSource.loop = true;
            spatialAudioSource.priority = 10;
            spatialAudioSource.mute = false;

            // Clip looping silent agar thread DSP OnAudioFilterRead selalu dipanggil
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

            // Sesuaikan sample rate dengan kemampuan hardware mic
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(activeMicDevice, out minFreq, out maxFreq);
            actualSampleRate = captureSampleRate;
            if (maxFreq > 0)
            {
                if (minFreq == maxFreq)
                {
                    actualSampleRate = minFreq;
                }
                else
                {
                    actualSampleRate = Mathf.Clamp(actualSampleRate, minFreq, maxFreq);
                }
            }

            Debug.Log($"<color=#00FFAA>[ProximityVoiceChat] Memulai perekaman mikrofon: {activeMicDevice} ({actualSampleRate} Hz)</color>");
            micClip = Microphone.Start(activeMicDevice, true, 10, actualSampleRate);
            lastSamplePosition = 0;
            sampleBuffer = new float[actualSampleRate];
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

                UpdateLocalVoiceTransmission();
                UpdateHUDIndicator();
                UpdateOverheadVisuals(IsSpeaking);
            }
            else
            {
                bool isRemoteTalking = (Time.time - lastVoiceReceivedTime < 0.25f);
                UpdateOverheadVisuals(isRemoteTalking);
            }
        }

        private bool IsPushToTalkPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.vKey.isPressed)
                {
                    return true;
                }
            }
#endif
            try
            {
                return Input.GetKey(pushToTalkKey);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateLocalVoiceTransmission()
        {
            if (micClip == null || string.IsNullOrEmpty(activeMicDevice)) return;

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

                // Push-to-Talk eksklusif: Voice hanya aktif saat tombol [V] ditekan / ditahan
                bool pttPressed = IsPushToTalkPressed();
                IsSpeaking = pttPressed;

                if (IsSpeaking)
                {
                    // Hitung level volume RMS saat berbicara
                    float sum = 0f;
                    for (int i = 0; i < chunk.Length; i++)
                    {
                        sum += chunk[i] * chunk[i];
                    }
                    float rms = Mathf.Sqrt(sum / chunk.Length);
                    CurrentMicLevel = rms;

                    byte[] pcmBytes = FloatToPcm16(chunk);

                    // Kirim ke server jika terhubung di multiplayer Mirror
                    if (NetworkClient.active)
                    {
                        CmdSendVoice(pcmBytes, actualSampleRate);
                    }

                    // Loopback sidetone lokal agar pemain mendengar suara sendiri saat tes
                    if (loopbackSelfAudio)
                    {
                        ReceiveVoicePacket(pcmBytes, actualSampleRate);
                    }
                }
                else
                {
                    CurrentMicLevel = 0f;
                }
            }
        }

        #region Mirror Network Commands
        [Command(channel = Channels.Unreliable)]
        private void CmdSendVoice(byte[] pcmData, int sampleRate)
        {
            if (pcmData == null || pcmData.Length == 0) return;
            RpcReceiveVoice(pcmData, sampleRate);
        }

        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
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
                    // Reset buffer agar tidak ada latensi penumpukan
                    readIndex = writeIndex;
                    playbackPosition = writeIndex;
                }

                for (int i = 0; i < samples.Length; i++)
                {
                    ringBuffer[writeIndex] = samples[i];
                    writeIndex = (writeIndex + 1) % RingBufferSize;
                }
            }

            lastVoiceReceivedTime = Time.time;

            if (spatialAudioSource != null && !spatialAudioSource.isPlaying)
            {
                spatialAudioSource.Play();
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
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = 0f;
                }
                return;
            }

            int outputRate = AudioSettings.outputSampleRate;
            if (outputRate <= 0) outputRate = 48000;
            float resampleRatio = (float)remoteSampleRate / outputRate;

            lock (ringLock)
            {
                int availableSamples = (writeIndex >= readIndex)
                    ? (writeIndex - readIndex)
                    : (RingBufferSize - readIndex + writeIndex);

                if (availableSamples < 64 || (Time.time - lastVoiceReceivedTime > 0.35f))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = 0f;
                    }
                    return;
                }

                for (int i = 0; i < data.Length; i += channels)
                {
                    if (availableSamples > 0)
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

                        readIndex = (int)playbackPosition % RingBufferSize;
                        availableSamples--;
                    }
                    else
                    {
                        for (int c = 0; c < channels; c++)
                        {
                            data[i + c] = 0f;
                        }
                    }
                }
            }
        }

        #region PCM Conversion Helpers
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
            overheadVoiceIndicator = new GameObject("OverheadVoiceIndicator");
            overheadVoiceIndicator.transform.SetParent(transform, false);
            overheadVoiceIndicator.transform.localPosition = new Vector3(0f, 2.45f, 0f);

            overheadVoiceText = overheadVoiceIndicator.AddComponent<TextMesh>();
            overheadVoiceText.text = "🔊";
            overheadVoiceText.characterSize = 0.12f;
            overheadVoiceText.fontSize = 32;
            overheadVoiceText.fontStyle = FontStyle.Bold;
            overheadVoiceText.anchor = TextAnchor.MiddleCenter;
            overheadVoiceText.alignment = TextAlignment.Center;
            overheadVoiceText.color = new Color(0.2f, 1f, 0.5f, 1f); // Neon green / cyan

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
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        overheadVoiceIndicator.transform.rotation = cam.transform.rotation;
                    }
                }
            }
        }

        private void CreateHUDVoiceIndicator()
        {
            if (hudVoiceIndicator != null) return;

            // Cari Canvas aktif atau buat Canvas Overlay khusus berprioritas tinggi
            Canvas targetCanvas = null;
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (var c in allCanvases)
            {
                if (c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    targetCanvas = c;
                    break;
                }
            }

            if (targetCanvas == null)
            {
                hudCanvasObj = new GameObject("VoiceChatOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                targetCanvas = hudCanvasObj.GetComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.sortingOrder = 999;
                var scaler = hudCanvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            hudVoiceIndicator = new GameObject("HUDVoiceStatus", typeof(RectTransform), typeof(Image));
            hudVoiceIndicator.transform.SetParent(targetCanvas.transform, false);

            RectTransform rt = hudVoiceIndicator.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 25f);
            rt.sizeDelta = new Vector2(160f, 38f);

            var bgImg = hudVoiceIndicator.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.07f, 0.12f, 0.88f);

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObj.transform.SetParent(hudVoiceIndicator.transform, false);
            RectTransform trt = textObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;

            hudVoiceTmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();
            hudVoiceTmp.fontSize = 14;
            hudVoiceTmp.fontStyle = TMPro.FontStyles.Bold;
            hudVoiceTmp.alignment = TMPro.TextAlignmentOptions.Center;
            hudVoiceTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            hudVoiceTmp.text = "🔇 <color=#888888>[V]</color>";
        }

        private void UpdateHUDIndicator()
        {
            if (hudVoiceTmp == null) return;

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                hudVoiceTmp.text = "🔇 <color=#FF4444>[NO MIC]</color>";
                return;
            }

            if (IsSpeaking)
            {
                int barCount = Mathf.Clamp((int)(CurrentMicLevel * 100f), 1, 6);
                string meter = new string('|', barCount);
                hudVoiceTmp.text = $"🔊 <color=#00FFAA><b>[V]</b></color> <color=#00FF88>{meter}</color>";
            }
            else
            {
                hudVoiceTmp.text = "🔇 <color=#888888>[V]</color>";
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

            if (hudCanvasObj != null)
            {
                Destroy(hudCanvasObj);
            }
        }
    }
}
