using System;
using System.Collections;
using Mirror;
using UnityEngine;
using Pucaci.UI;

namespace Pucaci.Network
{
    /// <summary>
    /// Sistem Proximity Voice Chat (Obrolan Suara 3D Spasial dengan Attenuasi Jarak).
    /// - Mengizinkan semua pemain berbicara langsung secara real-time via mikrofon.
    /// - Pemain yang berdekatan (< 2m) terdengar jernih dan jelas.
    /// - Semakin jauh posisi antar pemain, suara memudar secara bertahap (linear rolloff).
    /// - Jika pemain berada di luar jangkauan (> 20m), suara hilang sepenuhnya.
    /// - Dilengkapi Voice Activity Detection (VAD) otomatis dan tombol Push-to-Talk opsional [V].
    /// - Dilengkapi indikator berbicara di HUD lokal dan billboard di atas kepala pemain.
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
        [Tooltip("Frekuensi sampling suara (16000 Hz adalah standar VoIP jernih & hemat bandwidth)")]
        [SerializeField] private int captureSampleRate = 16000;

        [Tooltip("Interval pengiriman paket suara ke server (detik)")]
        [SerializeField] private float packetInterval = 0.08f;

        [Tooltip("Threshold sensitivitas VAD untuk mendeteksi suara bicara")]
        [SerializeField] private float voiceDetectionThreshold = 0.012f;

        [Tooltip("Tombol Push-to-Talk")]
        [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;

        [Tooltip("Gunakan Push-to-Talk (True = harus tahan tombol V, False = Open Mic dengan VAD)")]
        [SerializeField] private bool usePushToTalk = false;

        [Header("Komponen Audio 3D")]
        [SerializeField] private AudioSource spatialAudioSource;

        // Status bicara
        public bool IsSpeaking { get; private set; }
        public float CurrentMicLevel { get; private set; }

        // Variabel perekaman lokal
        private AudioClip micClip;
        private string activeMicDevice;
        private int lastSamplePosition;
        private float nextPacketTime;
        private float[] sampleBuffer;

        // Buffer pemutaran audio jarak jauh (Ring Buffer thread-safe)
        private const int RingBufferSize = 65536;
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
        private GameObject hudVoiceIndicator;
        private TMPro.TextMeshProUGUI hudVoiceTmp;

        private void Awake()
        {
            SetupSpatialAudioSource();
            CreateOverheadIndicator();
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
            spatialAudioSource.dopplerLevel = 0f; // Hindari efek distorsi nada saat lari
            spatialAudioSource.spread = 0f;
            spatialAudioSource.playOnAwake = false;
            spatialAudioSource.loop = true;
            spatialAudioSource.priority = 10; // Prioritas tinggi agar tidak terputus audio SFX lain

            // Buat clip kosong 1 detik yang looping agar OnAudioFilterRead selalu dipanggil oleh Unity
            if (spatialAudioSource.clip == null)
            {
                spatialAudioSource.clip = AudioClip.Create("VoiceLoopSilent", 48000, 1, 48000, false);
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            LocalInstance = this;

            // Di pemain lokal, kita tidak perlu mendengar diri sendiri dari AudioSource lokal
            if (spatialAudioSource != null)
            {
                spatialAudioSource.mute = true;
            }

            StartMicrophoneCapture();
            CreateHUDVoiceIndicator();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Pada remote client, pastikan AudioSource mulai berjalan untuk menerima filter read
            if (!isLocalPlayer && spatialAudioSource != null)
            {
                spatialAudioSource.mute = false;
                if (!spatialAudioSource.isPlaying)
                {
                    spatialAudioSource.Play();
                }
            }
        }

        private void StartMicrophoneCapture()
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[ProximityVoiceChat] Tidak ada perangkat mikrofon yang terdeteksi.");
                return;
            }

            activeMicDevice = null;
            if (SettingsManager.Instance != null && !string.IsNullOrEmpty(SettingsManager.Instance.MicDevice))
            {
                foreach (var d in Microphone.devices)
                {
                    if (d == SettingsManager.Instance.MicDevice)
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

            Debug.Log($"<color=#00FFAA>[ProximityVoiceChat] Memulai perekaman mikrofon: {activeMicDevice} ({captureSampleRate} Hz)</color>");
            micClip = Microphone.Start(activeMicDevice, true, 10, captureSampleRate);
            lastSamplePosition = 0;
            sampleBuffer = new float[captureSampleRate]; // 1 detik buffer penampung
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                UpdateLocalVoiceTransmission();
                UpdateHUDIndicator();
            }
            else
            {
                UpdateRemoteVoiceVisuals();
            }
        }

        private void UpdateLocalVoiceTransmission()
        {
            if (micClip == null || string.IsNullOrEmpty(activeMicDevice)) return;

            int currentMicPos = Microphone.GetPosition(activeMicDevice);
            if (currentMicPos < 0 || currentMicPos == lastSamplePosition) return;

            // Hitung berapa banyak sample baru yang telah terekam
            int samplesToRead = 0;
            if (currentMicPos > lastSamplePosition)
            {
                samplesToRead = currentMicPos - lastSamplePosition;
            }
            else
            {
                // Terjadi wrap-around circular buffer mikrofon
                samplesToRead = (micClip.samples - lastSamplePosition) + currentMicPos;
            }

            if (Time.time >= nextPacketTime && samplesToRead > 0)
            {
                nextPacketTime = Time.time + packetInterval;

                if (samplesToRead > sampleBuffer.Length)
                {
                    samplesToRead = sampleBuffer.Length;
                }

                // Ambil sample audio baru
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

                // Hitung volume RMS
                float sum = 0f;
                for (int i = 0; i < chunk.Length; i++)
                {
                    sum += chunk[i] * chunk[i];
                }
                float rms = Mathf.Sqrt(sum / chunk.Length);
                CurrentMicLevel = rms;

                // Cek sensitivitas dari SettingsManager jika ada
                float threshold = voiceDetectionThreshold;
                if (SettingsManager.Instance != null)
                {
                    // Sensitivitas slider 0..1 (semakin tinggi slider, semakin rendah threshold = semakin sensitif)
                    threshold = Mathf.Lerp(0.04f, 0.003f, SettingsManager.Instance.MicSensitivity);
                }

                bool pttPressed = Input.GetKey(pushToTalkKey);
                bool vadActive = (!usePushToTalk && rms > threshold);
                IsSpeaking = vadActive || pttPressed;

                if (IsSpeaking)
                {
                    // Kompresi float[] ke 16-bit PCM bytes (hemat bandwidth & kompatibel di Mirror)
                    byte[] pcmBytes = FloatToPcm16(chunk);
                    CmdSendVoice(pcmBytes, captureSampleRate);
                }
            }
        }

        #region Mirror Network Commands
        [Command(channel = Channels.Unreliable)]
        private void CmdSendVoice(byte[] pcmData, int sampleRate)
        {
            if (pcmData == null || pcmData.Length == 0) return;

            // Broadcast ke semua client di sekitarnya via ClientRpc
            RpcReceiveVoice(pcmData, sampleRate);
        }

        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
        private void RpcReceiveVoice(byte[] pcmData, int sampleRate)
        {
            if (isLocalPlayer) return;

            ReceiveVoicePacket(pcmData, sampleRate);
        }
        #endregion

        private void ReceiveVoicePacket(byte[] pcmData, int sampleRate)
        {
            float[] samples = Pcm16ToFloat(pcmData);
            if (samples == null || samples.Length == 0) return;

            remoteSampleRate = sampleRate;
            lastVoiceReceivedTime = Time.time;

            lock (ringLock)
            {
                // Tambahkan samples ke circular ring buffer
                for (int i = 0; i < samples.Length; i++)
                {
                    ringBuffer[writeIndex] = samples[i];
                    writeIndex = (writeIndex + 1) % RingBufferSize;
                }
            }

            // Pastikan audio source sedang aktif memutar agar DSP OnAudioFilterRead berjalan
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
            if (isLocalPlayer) return;

            int outputRate = AudioSettings.outputSampleRate;
            if (outputRate <= 0) outputRate = 48000;
            float resampleRatio = (float)remoteSampleRate / outputRate;

            lock (ringLock)
            {
                int availableSamples = (writeIndex >= readIndex)
                    ? (writeIndex - readIndex)
                    : (RingBufferSize - readIndex + writeIndex);

                // Jika buffer hampir kosong atau audio sudah berhenti > 0.3s, output hening
                if (availableSamples < 64 || (Time.time - lastVoiceReceivedTime > 0.4f))
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
                        int curIdx = (int)playbackPosition;
                        int nextIdx = (curIdx + 1) % RingBufferSize;
                        float frac = playbackPosition - curIdx;

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

                        readIndex = (int)playbackPosition;
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
            overheadVoiceText.text = "🔊 [BICARA]";
            overheadVoiceText.characterSize = 0.075f;
            overheadVoiceText.fontSize = 24;
            overheadVoiceText.anchor = TextAnchor.MiddleCenter;
            overheadVoiceText.alignment = TextAlignment.Center;
            overheadVoiceText.color = new Color(0.2f, 1f, 0.4f, 1f); // Hijau neon

            overheadVoiceIndicator.SetActive(false);
        }

        private void UpdateRemoteVoiceVisuals()
        {
            bool isTalking = (Time.time - lastVoiceReceivedTime < 0.25f);
            if (overheadVoiceIndicator != null)
            {
                if (overheadVoiceIndicator.activeSelf != isTalking)
                {
                    overheadVoiceIndicator.SetActive(isTalking);
                }

                if (isTalking)
                {
                    // Hadapkan teks ke kamera pemain lokal
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
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            hudVoiceIndicator = new GameObject("HUDVoiceStatus", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            hudVoiceIndicator.transform.SetParent(canvas.transform, false);

            RectTransform rt = hudVoiceIndicator.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 25f);
            rt.sizeDelta = new Vector2(250f, 38f);

            var bgImg = hudVoiceIndicator.GetComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.04f, 0.07f, 0.11f, 0.88f);

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObj.transform.SetParent(hudVoiceIndicator.transform, false);
            RectTransform trt = textObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;

            hudVoiceTmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();
            hudVoiceTmp.fontSize = 13;
            hudVoiceTmp.fontStyle = TMPro.FontStyles.Bold;
            hudVoiceTmp.alignment = TMPro.TextAlignmentOptions.Center;
            hudVoiceTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            hudVoiceTmp.text = "🎙️ MIC: SIAP (Tekan [V] / Bicara)";
        }

        private void UpdateHUDIndicator()
        {
            if (hudVoiceTmp == null) return;

            if (IsSpeaking)
            {
                hudVoiceTmp.text = "🎙️ <color=#00FFAA><b>MIC: BICARA (AKTIF)</b></color>";
            }
            else
            {
                hudVoiceTmp.text = "🎙️ <color=#AAAAAA>MIC: SIAP [V]</color>";
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
