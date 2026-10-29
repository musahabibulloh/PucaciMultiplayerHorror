using UnityEngine;

namespace Pucaci.Core
{
    public class StubMultiplayerLauncher : MonoBehaviour, IMultiplayerLauncher
    {
        public bool IsAvailable => false;

        public void Host()
        {
            Debug.Log("[Pucaci.Core] StubMultiplayerLauncher.Host() dipanggil - Fitur online belum tersedia.");
        }

        public void Join(string roomCode)
        {
            Debug.Log($"[Pucaci.Core] StubMultiplayerLauncher.Join(\"{roomCode}\") dipanggil - Fitur online belum tersedia.");
        }
    }
}
