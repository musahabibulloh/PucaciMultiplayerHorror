using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;
using Pucaci.Player;

namespace Pucaci.Gameplay
{
    /// <summary>
    /// Pengelola Misi Utama Game & Role Impostor vs Survivor di Desa Pucaci:
    /// 1. Survivor Menang: Jika seluruh sesajen dihancurkan (5/5).
    /// 2. Impostor Menang: Jika seluruh survivor berhasil dihabisi/dibunuh.
    /// 3. Impostor dapat memperbaiki sesajen yang dihancurkan saat dalam wujud manusia.
    /// </summary>
    public class SesajenManager : NetworkBehaviour
    {
        public static SesajenManager Instance { get; private set; }

        [Header("Sesajen Objective State")]
        [SyncVar(hook = nameof(OnTotalSesajenChanged))]
        private int totalSesajen = 0;

        [SyncVar(hook = nameof(OnDestroyedCountChanged))]
        private int destroyedCount = 0;

        [Header("Game Over States")]
        [SyncVar(hook = nameof(OnSurvivorWonChanged))]
        private bool isSurvivorWon = false;

        [SyncVar(hook = nameof(OnImpostorWonChanged))]
        private bool isImpostorWon = false;

        public int TotalSesajen => totalSesajen;
        public int DestroyedCount => destroyedCount;
        public int RemainingSesajen => Mathf.Max(0, totalSesajen - destroyedCount);
        public bool IsSurvivorWon => isSurvivorWon;
        public bool IsImpostorWon => isImpostorWon;
        public bool IsGameOver => isSurvivorWon || isImpostorWon;

        public event Action<int, int> OnObjectiveProgressChanged; // (destroyed, total)
        public event Action OnSurvivorVictory;
        public event Action OnImpostorVictory;

        private readonly List<SesajenItem> registeredSesajen = new List<SesajenItem>();
        private readonly List<PlayerRoleManager> registeredPlayers = new List<PlayerRoleManager>();
        private bool rolesAssigned = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            FindAndRegisterAllSesajen();
            Invoke(nameof(EvaluateInitialRoles), 0.5f);
        }

        public void RegisterPlayer(PlayerRoleManager player)
        {
            if (player == null) return;
            if (!registeredPlayers.Contains(player))
            {
                registeredPlayers.Add(player);
            }

            // Jika host / singleplayer dan belum assign role
            if (!rolesAssigned)
            {
                CancelInvoke(nameof(EvaluateInitialRoles));
                Invoke(nameof(EvaluateInitialRoles), 0.3f);
            }
        }

        public void UnregisterPlayer(PlayerRoleManager player)
        {
            if (registeredPlayers.Contains(player))
            {
                registeredPlayers.Remove(player);
            }
            CheckSurvivorElimination();
        }

        private void EvaluateInitialRoles()
        {
            if (rolesAssigned) return;

            // Cari semua PlayerRoleManager di scene
            var players = FindObjectsOfType<PlayerRoleManager>();
            foreach (var p in players)
            {
                if (!registeredPlayers.Contains(p)) registeredPlayers.Add(p);
            }

            if (registeredPlayers.Count == 0) return;

            // Hanya server atau mode offline yang menentukan role
            if (!NetworkClient.active || NetworkServer.active)
            {
                AssignRandomRoles();
            }
        }

        public void AssignRandomRoles()
        {
            if (registeredPlayers.Count == 0) return;

            rolesAssigned = true;

            // 3 Manusia Desa Pucaci: Laras (0), Pak Mamat (1), Pak Vincent (3)
            List<int> humanPool = new List<int> { 0, 1, 3 };

            // Acak urutan 3 manusia
            for (int i = 0; i < humanPool.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, humanPool.Count);
                int temp = humanPool[i];
                humanPool[i] = humanPool[r];
                humanPool[r] = temp;
            }

            // Pilih 1 player secara acak sebagai Hantu (Kuntilanak yang menyamar)
            int impostorIndex = UnityEngine.Random.Range(0, registeredPlayers.Count);

            for (int i = 0; i < registeredPlayers.Count; i++)
            {
                var player = registeredPlayers[i];
                int assignedDisguise = humanPool[i % humanPool.Count];

                if (i == impostorIndex)
                {
                    // HANTU / IMPOSTOR
                    player.SetRole(PlayerRole.Impostor, assignedDisguise);
                    Debug.Log($"<color=#FF0044><b>[ROLE] Player {player.name} adalah HANTU (KUNTILANAK)! Menyamar awal sebagai: {(CharacterType)assignedDisguise}</b></color>");
                }
                else
                {
                    // SURVIVOR
                    player.SetRole(PlayerRole.Survivor, assignedDisguise);
                    Debug.Log($"[ROLE] Player {player.name} adalah SURVIVOR ({(CharacterType)assignedDisguise})");
                }
            }
        }

        public void FindAndRegisterAllSesajen()
        {
            registeredSesajen.Clear();
            var items = FindObjectsOfType<SesajenItem>(true);
            foreach (var s in items)
            {
                if (!registeredSesajen.Contains(s))
                {
                    registeredSesajen.Add(s);
                }
            }

            totalSesajen = registeredSesajen.Count;
            int countDestroyed = 0;
            foreach (var s in registeredSesajen)
            {
                if (s.IsDestroyed) countDestroyed++;
            }
            destroyedCount = countDestroyed;

            OnObjectiveProgressChanged?.Invoke(destroyedCount, totalSesajen);
        }

        public void NotifySesajenDestroyed(SesajenItem item)
        {
            if (IsGameOver) return;

            destroyedCount = Mathf.Min(totalSesajen, destroyedCount + 1);
            OnObjectiveProgressChanged?.Invoke(destroyedCount, totalSesajen);

            Debug.Log($"[SESAJEN] Sesajen dihancurkan! ({destroyedCount}/{totalSesajen})");

            // Cek apakah seluruh sesajen telah dihancurkan
            if (destroyedCount >= totalSesajen && totalSesajen > 0)
            {
                TriggerSurvivorVictory();
            }
        }

        public void NotifySesajenRepaired(SesajenItem item)
        {
            if (IsGameOver) return;

            destroyedCount = Mathf.Max(0, destroyedCount - 1);
            OnObjectiveProgressChanged?.Invoke(destroyedCount, totalSesajen);

            Debug.Log($"<color=#00DDFF><b>[SESAJEN] Sesajen diperbaiki oleh Impostor! Progres berkurang ({destroyedCount}/{totalSesajen})</b></color>");
        }

        public void CheckSurvivorElimination()
        {
            if (IsGameOver) return;

            int totalSurvivors = 0;
            int aliveSurvivors = 0;

            var allRoles = FindObjectsOfType<PlayerRoleManager>();
            foreach (var r in allRoles)
            {
                if (!r.IsImpostor)
                {
                    totalSurvivors++;
                    var health = r.GetComponent<PlayerHealth>();
                    if (health != null && !health.IsDead)
                    {
                        aliveSurvivors++;
                    }
                }
            }

            // Jika ada survivor dan semuanya telah tewas, Impostor Menang!
            if (totalSurvivors > 0 && aliveSurvivors <= 0)
            {
                TriggerImpostorVictory();
            }
        }

        public void TriggerSurvivorVictory()
        {
            if (IsGameOver) return;
            isSurvivorWon = true;

            Debug.Log("<color=#00FF66><b>[VICTORY] SELURUH SESAJEN TELAH DIHANCURKAN! SURVIVOR MENANG!</b></color>");

            OnSurvivorVictory?.Invoke();
            PlayVictoryFanfare();
        }

        public void TriggerImpostorVictory()
        {
            if (IsGameOver) return;
            isImpostorWon = true;

            Debug.Log("<color=#FF0033><b>[VICTORY] SELURUH SURVIVOR TELAH TEWAS! IMPOSTOR (KUNTILANAK) MENANG!</b></color>");

            OnImpostorVictory?.Invoke();
            PlayImpostorVictorySound();
        }

        private void OnTotalSesajenChanged(int oldVal, int newVal)
        {
            OnObjectiveProgressChanged?.Invoke(destroyedCount, newVal);
        }

        private void OnDestroyedCountChanged(int oldVal, int newVal)
        {
            OnObjectiveProgressChanged?.Invoke(newVal, totalSesajen);
        }

        private void OnSurvivorWonChanged(bool oldVal, bool newVal)
        {
            if (newVal)
            {
                OnSurvivorVictory?.Invoke();
                PlayVictoryFanfare();
            }
        }

        private void OnImpostorWonChanged(bool oldVal, bool newVal)
        {
            if (newVal)
            {
                OnImpostorVictory?.Invoke();
                PlayImpostorVictorySound();
            }
        }

        private void PlayVictoryFanfare()
        {
            var audioObj = new GameObject("VictoryAudioPlayer");
            var aSrc = audioObj.AddComponent<AudioSource>();
            aSrc.spatialBlend = 0f; // 2D Stereo
            aSrc.volume = 1.0f;
            aSrc.clip = CreateVictoryClip();
            aSrc.Play();
            if (Application.isPlaying) Destroy(audioObj, 6.0f);
            else DestroyImmediate(audioObj);
        }

        private void PlayImpostorVictorySound()
        {
            var audioObj = new GameObject("ImpostorVictoryAudioPlayer");
            var aSrc = audioObj.AddComponent<AudioSource>();
            aSrc.spatialBlend = 0f;
            aSrc.volume = 1.0f;
            aSrc.clip = CreateImpostorVictoryClip();
            aSrc.Play();
            if (Application.isPlaying) Destroy(audioObj, 6.0f);
            else DestroyImmediate(audioObj);
        }

        private static AudioClip CreateVictoryClip()
        {
            int sampleRate = 22050;
            float duration = 4.0f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float[] freqs = new float[] { 261.63f, 329.63f, 392.00f, 523.25f, 659.25f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 0.8f);
                float val = 0f;

                for (int f = 0; f < freqs.Length; f++)
                {
                    val += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) * (1f / (f + 1));
                }

                samples[i] = val * 0.25f * env;
            }

            var clip = AudioClip.Create("Survivor_Victory_Theme", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateImpostorVictoryClip()
        {
            int sampleRate = 22050;
            float duration = 4.5f;
            int totalSamples = Mathf.FloorToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Jeritan tawa mengerikan Kuntilanak dan dengung frekuensi disonan
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 0.5f);
                float screech = Mathf.Sin(2f * Mathf.PI * (520f + 180f * Mathf.Sin(14f * t)) * t) * 0.35f;
                float bassDrone = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.45f;
                float darkHarmonic = Mathf.Sin(2f * Mathf.PI * 67f * t) * 0.2f;
                samples[i] = (screech + bassDrone + darkHarmonic) * env;
            }

            var clip = AudioClip.Create("Impostor_Victory_Theme", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
