using System;
using System.Net;
using System.Net.Sockets;
using Mirror;
using Pucaci.Core;
using UnityEngine;

namespace Pucaci.Network
{
    /// <summary>
    /// Launcher Multiplayer Host-Client untuk Pucaci:
    /// - Mengontrol Mirror NetworkManager untuk memulai Host atau Client
    /// - Mendukung input IP dan Room Code (LAN / Radmin VPN / ZeroTier / Playit.gg)
    /// - Menampilkan IP lokal untuk dibagikan ke teman
    /// </summary>
    public class PucaciMultiplayerLauncher : MonoBehaviour, IMultiplayerLauncher
    {
        public static PucaciMultiplayerLauncher Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private ushort defaultPort = 7777;

        public bool IsAvailable => true;
        public string LastStatus { get; private set; } = "";

        public event Action<string> OnStatusChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void Host()
        {
            EnsureNetworkManager();

            if (NetworkManager.singleton == null)
            {
                SetStatus("Gagal: NetworkManager tidak ditemukan.");
                return;
            }

            SetStatus("Membuat Ruangan Server (Host)...");
            Debug.Log($"<color=#00FFAA>[PucaciMultiplayerLauncher] Memulai Host di port {defaultPort}</color>");

            try
            {
                // Set transport port jika menggunakan KcpTransport
                if (Transport.active is kcp2k.KcpTransport kcp)
                {
                    kcp.Port = defaultPort;
                }

                NetworkManager.singleton.StartHost();
            }
            catch (Exception ex)
            {
                SetStatus($"Gagal membuat host: {ex.Message}");
                Debug.LogError($"[PucaciMultiplayerLauncher] Gagal StartHost: {ex}");
            }
        }

        public void Join(string roomCodeOrIp)
        {
            EnsureNetworkManager();

            if (NetworkManager.singleton == null)
            {
                SetStatus("Gagal: NetworkManager tidak ditemukan.");
                return;
            }

            string targetAddress = string.IsNullOrWhiteSpace(roomCodeOrIp) ? "localhost" : roomCodeOrIp.Trim();
            ushort port = defaultPort;

            // Dukungan format "IP:Port" (misal: 192.168.1.15:7777 atau tunnel.playit.gg:12345)
            if (targetAddress.Contains(":"))
            {
                var parts = targetAddress.Split(':');
                targetAddress = parts[0];
                if (ushort.TryParse(parts[1], out ushort parsedPort))
                {
                    port = parsedPort;
                }
            }

            SetStatus($"Menghubungkan ke {targetAddress}:{port}...");
            Debug.Log($"<color=#00DDFF>[PucaciMultiplayerLauncher] Menghubungkan ke {targetAddress}:{port}</color>");

            try
            {
                NetworkManager.singleton.networkAddress = targetAddress;
                if (Transport.active is kcp2k.KcpTransport kcp)
                {
                    kcp.Port = port;
                }

                NetworkManager.singleton.StartClient();
            }
            catch (Exception ex)
            {
                SetStatus($"Gagal terhubung: {ex.Message}");
                Debug.LogError($"[PucaciMultiplayerLauncher] Gagal StartClient: {ex}");
            }
        }

        public string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // Abaikan error jaringan
            }

            return "127.0.0.1";
        }

        private void SetStatus(string msg)
        {
            LastStatus = msg;
            OnStatusChanged?.Invoke(msg);
        }

        private void EnsureNetworkManager()
        {
            if (NetworkManager.singleton != null) return;

            var existing = FindObjectOfType<PucaciNetworkManager>();
            if (existing != null) return;

            // Inisialisasi PucaciNetworkManager di scene jika belum ada
            GameObject netObj = new GameObject("NetworkManager");
            var kcp = netObj.AddComponent<kcp2k.KcpTransport>();
            kcp.Port = defaultPort;

            var mgr = netObj.AddComponent<PucaciNetworkManager>();
            mgr.transport = kcp;

            // Cari player prefab dari Resources atau Assets jika memungkinkan
            var playerPrefab = Resources.Load<GameObject>("Player/PlayerCharacter");
            if (playerPrefab != null)
            {
                mgr.playerPrefab = playerPrefab;
            }
        }
    }
}
