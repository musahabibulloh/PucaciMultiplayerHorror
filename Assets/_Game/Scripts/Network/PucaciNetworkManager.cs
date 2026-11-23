using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using Pucaci.Gameplay;
using Pucaci.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pucaci.Network
{
    public struct MatchCountdownMessage : NetworkMessage
    {
        public float remaining;
    }

    public struct MatchStartedMessage : NetworkMessage
    {
    }

    /// <summary>
    /// Custom NetworkManager untuk Pucaci Horror Multiplayer:
    /// - Arsitektur Host-Client 100% gratis (Mirror + KCP UDP / LAN / VPN / Playit)
    /// - Menangani perpindahan scene MainMenu <-> Gameplay
    /// - Menangani spawn 3 player di titik awal (SpawnPoints side-by-side)
    /// - Mengacak peran: 1 Hantu Kuntilanak (Impostor) + 2 Survivor (Warga)
    /// - Menyamarkan Impostor sebagai salah satu manusia (Laras, Pak Mamat, Pak Vincent)
    /// </summary>
    public class PucaciNetworkManager : NetworkManager
    {
        public static new PucaciNetworkManager singleton => NetworkManager.singleton as PucaciNetworkManager;

        [Header("Pucaci Match Configuration")]
        [Tooltip("Jumlah pemain target untuk mode multiplayer (Default: 3 pemain)")]
        [SerializeField] private int targetPlayerCount = 3;

        [Tooltip("Durasi hitung mundur sebelum peran dibagikan setelah pemain lengkap (detik)")]
        [SerializeField] private float countdownDuration = 3.0f;

        [Header("Runtime State")]
        [SerializeField] private bool matchStarted = false;
        private Coroutine startCountdownCoroutine;

        public int TargetPlayerCount => targetPlayerCount;
        public bool IsMatchStarted => matchStarted;

        public event Action<int, int> OnPlayerCountChanged; // (current, target)
        public event Action<float> OnCountdownTick;         // remaining seconds
        public event Action OnMatchStartedEvent;
        public event Action<string> OnStatusMessageChanged;

        private readonly List<PlayerRoleManager> connectedPlayers = new List<PlayerRoleManager>();
        public IReadOnlyList<PlayerRoleManager> ConnectedPlayers => connectedPlayers;

        public override void Awake()
        {
            base.Awake();
            dontDestroyOnLoad = true;
            runInBackground = true;
            maxConnections = 4;
            playerSpawnMethod = PlayerSpawnMethod.RoundRobin;
            autoCreatePlayer = true;
            offlineScene = "MainMenu";
            onlineScene = "Gameplay";
        }

        public override void Update()
        {
            base.Update();

            // Host Debug/Testing Shortcut: Tekan F5 untuk paksa mulai pertandingan sekarang (bisa tes dengan 1 atau 2 player)
            if (NetworkServer.active && !matchStarted)
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f5Key.wasPressedThisFrame)
                {
                    Debug.Log("<color=#FFCC00>[HOST F5] Memulai permainan & mengacak peran secara manual!</color>");
                    ForceStartMatch();
                }
#else
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    Debug.Log("<color=#FFCC00>[HOST F5] Memulai permainan & mengacak peran secara manual!</color>");
                    ForceStartMatch();
                }
#endif
            }
        }

        #region Server Callbacks
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Ambil posisi spawn awal dari NetworkStartPosition (SpawnPoints 1..4)
            Transform startPos = GetStartPosition();
            GameObject player = startPos != null
                ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                : Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);

            player.name = $"{playerPrefab.name} [ConnId={conn.connectionId}]";
            NetworkServer.AddPlayerForConnection(conn, player);

            Debug.Log($"<color=#00FFAA>[PucaciNetworkManager] Player baru bergabung: connId={conn.connectionId}, posisi={player.transform.position}</color>");

            // Cari komponen PlayerRoleManager pada player yang baru dibuat
            if (player.TryGetComponent<PlayerRoleManager>(out var roleManager))
            {
                RegisterPlayerRole(roleManager);
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.identity != null && conn.identity.TryGetComponent<PlayerRoleManager>(out var roleManager))
            {
                UnregisterPlayerRole(roleManager);
            }

            base.OnServerDisconnect(conn);

            Debug.Log($"<color=#FFAA00>[PucaciNetworkManager] Player terputus: connId={conn.connectionId}. Sisa pemain: {connectedPlayers.Count}</color>");
            OnStatusMessageChanged?.Invoke($"Seorang pemain terputus ({connectedPlayers.Count}/{targetPlayerCount})");
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            if (sceneName == "Gameplay")
            {
                matchStarted = false;
                connectedPlayers.Clear();
                StartCoroutine(DelayedPlayerDiscovery());
            }
            else if (sceneName == "MainMenu")
            {
                matchStarted = false;
                connectedPlayers.Clear();
            }
        }
        #endregion

        #region Client Callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<MatchCountdownMessage>(msg => OnCountdownTick?.Invoke(msg.remaining));
            NetworkClient.RegisterHandler<MatchStartedMessage>(msg =>
            {
                matchStarted = true;
                OnMatchStartedEvent?.Invoke();
            });
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("<color=#00FF99>[PucaciNetworkManager] Terhubung ke server!</color>");
            OnStatusMessageChanged?.Invoke("Terhubung ke server! Menyiapkan permainan...");
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            Debug.LogWarning("[PucaciNetworkManager] Terputus dari server.");
            OnStatusMessageChanged?.Invoke("Koneksi ke host terputus.");
            matchStarted = false;
            connectedPlayers.Clear();
        }

        public override void OnClientError(TransportError error, string reason)
        {
            base.OnClientError(error, reason);
            Debug.LogError($"[PucaciNetworkManager] Client Transport Error: {error} - {reason}");
            OnStatusMessageChanged?.Invoke($"Error koneksi ({error}): {reason}");
        }
        #endregion

        #region Player & Match Management
        public void RegisterPlayerRole(PlayerRoleManager roleManager)
        {
            if (roleManager == null) return;
            if (!connectedPlayers.Contains(roleManager))
            {
                connectedPlayers.Add(roleManager);
            }

            OnPlayerCountChanged?.Invoke(connectedPlayers.Count, targetPlayerCount);

            // Cek jika jumlah pemain sudah mencapai target (3 pemain)
            if (NetworkServer.active && !matchStarted)
            {
                if (connectedPlayers.Count >= targetPlayerCount)
                {
                    if (startCountdownCoroutine != null) StopCoroutine(startCountdownCoroutine);
                    startCountdownCoroutine = StartCoroutine(CountdownAndStartMatch());
                }
                else
                {
                    OnStatusMessageChanged?.Invoke($"Menunggu pemain bergabung ({connectedPlayers.Count}/{targetPlayerCount})...");
                }
            }
        }

        public void UnregisterPlayerRole(PlayerRoleManager roleManager)
        {
            if (connectedPlayers.Contains(roleManager))
            {
                connectedPlayers.Remove(roleManager);
            }

            OnPlayerCountChanged?.Invoke(connectedPlayers.Count, targetPlayerCount);

            if (NetworkServer.active && !matchStarted && connectedPlayers.Count < targetPlayerCount)
            {
                if (startCountdownCoroutine != null)
                {
                    StopCoroutine(startCountdownCoroutine);
                    startCountdownCoroutine = null;
                    OnStatusMessageChanged?.Invoke($"Menunggu pemain bergabung ({connectedPlayers.Count}/{targetPlayerCount})...");
                }
            }
        }

        private IEnumerator DelayedPlayerDiscovery()
        {
            yield return new WaitForSeconds(0.6f);
            var found = FindObjectsOfType<PlayerRoleManager>();
            foreach (var p in found)
            {
                if (!connectedPlayers.Contains(p))
                {
                    connectedPlayers.Add(p);
                }
            }
            OnPlayerCountChanged?.Invoke(connectedPlayers.Count, targetPlayerCount);

            if (NetworkServer.active && !matchStarted && connectedPlayers.Count >= targetPlayerCount)
            {
                if (startCountdownCoroutine != null) StopCoroutine(startCountdownCoroutine);
                startCountdownCoroutine = StartCoroutine(CountdownAndStartMatch());
            }
        }

        private IEnumerator CountdownAndStartMatch()
        {
            float timer = countdownDuration;
            while (timer > 0f)
            {
                OnCountdownTick?.Invoke(timer);
                if (NetworkServer.active)
                {
                    NetworkServer.SendToAll(new MatchCountdownMessage { remaining = timer });
                }
                yield return new WaitForSeconds(1.0f);
                timer -= 1.0f;
            }

            StartMatchWithRoles();
        }

        public void ForceStartMatch()
        {
            if (!NetworkServer.active || matchStarted) return;
            if (startCountdownCoroutine != null)
            {
                StopCoroutine(startCountdownCoroutine);
                startCountdownCoroutine = null;
            }
            StartMatchWithRoles();
        }

        private void StartMatchWithRoles()
        {
            if (matchStarted) return;
            matchStarted = true;

            // Refresh list pemain aktif
            connectedPlayers.Clear();
            var players = FindObjectsOfType<PlayerRoleManager>();
            foreach (var p in players)
            {
                connectedPlayers.Add(p);
            }

            if (connectedPlayers.Count == 0)
            {
                Debug.LogWarning("[PucaciNetworkManager] Tidak ada pemain yang ditemukan untuk pembagian peran!");
                matchStarted = false;
                return;
            }

            Debug.Log($"<color=#00FFAA><b>[PucaciNetworkManager] MEMULAI PERMAINAN! Jumlah Pemain: {connectedPlayers.Count}</b></color>");

            // Pool 3 model manusia: 0 = Laras, 1 = Pak Mamat, 3 = Pak Vincent
            List<int> disguisePool = new List<int> { 0, 1, 3 };

            // Acak urutan model manusia
            for (int i = 0; i < disguisePool.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, disguisePool.Count);
                int tmp = disguisePool[i];
                disguisePool[i] = disguisePool[r];
                disguisePool[r] = tmp;
            }

            // Pilih 1 pemain secara acak sebagai Hantu (Impostor)
            int impostorIndex = UnityEngine.Random.Range(0, connectedPlayers.Count);

            for (int i = 0; i < connectedPlayers.Count; i++)
            {
                var p = connectedPlayers[i];
                if (p == null) continue;

                int assignedDisguise = disguisePool[i % disguisePool.Count];

                if (i == impostorIndex)
                {
                    // HANTU / KUNTILANAK (IMPOSTOR)
                    p.SetRole(PlayerRole.Impostor, assignedDisguise);
                    Debug.Log($"<color=#FF0044><b>[ROLE] {p.name} (netId {p.netId}) TERPILIH SEBAGAI HANTU KUNTILANAK! Menyamar sebagai: {(CharacterType)assignedDisguise}</b></color>");
                }
                else
                {
                    // SURVIVOR (WARGA)
                    p.SetRole(PlayerRole.Survivor, assignedDisguise);
                    Debug.Log($"<color=#00DDFF>[ROLE] {p.name} (netId {p.netId}) adalah SURVIVOR (Wujud: {(CharacterType)assignedDisguise})</color>");
                }
            }

            if (SesajenManager.Instance != null)
            {
                SesajenManager.Instance.NotifyRolesAssigned();
            }

            OnMatchStartedEvent?.Invoke();
            if (NetworkServer.active)
            {
                NetworkServer.SendToAll(new MatchStartedMessage());
            }
        }
        #endregion
    }
}
