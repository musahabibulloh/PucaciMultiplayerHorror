using System.Collections.Generic;
using Mirror;
using Pucaci.Core;
using Pucaci.Network;
using Pucaci.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pucaci.Editor
{
    public static class MultiplayerSetupUtility
    {
        private const string GameplayScenePath = "Assets/_Game/Scenes/Gameplay.unity";
        private const string MainMenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/PlayerCharacter.prefab";

        [MenuItem("Pucaci/Setup Multiplayer Architecture")]
        public static void SetupAll()
        {
            SetupGameplayScene();
            SetupMainMenuScene();
            Debug.Log("<color=#00FFAA><b>[MultiplayerSetupUtility] Mode Multiplayer Host-Client 3 Player berhasil di-setup!</b></color>");
        }

        [MenuItem("Pucaci/Clean & Setup Gameplay Scene")]
        public static void SetupGameplayScene()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (playerPrefab == null)
            {
                Debug.LogError($"[MultiplayerSetupUtility] Player Prefab tidak ditemukan di {PlayerPrefabPath}!");
                return;
            }

            // 1. Bersihkan sisa-sisa test dummy / clone lama di scene Gameplay
            List<GameObject> toDestroy = new List<GameObject>();
            var rootObjects = scene.GetRootGameObjects();

            foreach (var go in rootObjects)
            {
                string name = go.name;
                if (name.StartsWith("PlayerCharacter(Clone)") ||
                    name.StartsWith("Test_Kuntilanak") ||
                    name.StartsWith("Test_Victim") ||
                    name == "P1_Player" ||
                    name == "P2_Player" ||
                    name == "P3_Player" ||
                    name == "TransformSmokeFX")
                {
                    toDestroy.Add(go);
                }
            }

            foreach (var go in toDestroy)
            {
                Object.DestroyImmediate(go);
            }
            Debug.Log($"[MultiplayerSetupUtility] Dihapus {toDestroy.Count} objek test/clone di Gameplay.unity");

            // 2. Setup NetworkManager di Gameplay
            GameObject netManagerObj = GameObject.Find("NetworkManager");
            if (netManagerObj == null)
            {
                netManagerObj = new GameObject("NetworkManager");
            }

            // Ganti NetworkManager standar Mirror dengan PucaciNetworkManager
            var hud = netManagerObj.GetComponent<NetworkManagerHUD>();
            if (hud != null) Object.DestroyImmediate(hud);

            var oldNetMgr = netManagerObj.GetComponent<NetworkManager>();
            if (oldNetMgr != null && !(oldNetMgr is PucaciNetworkManager))
            {
                Object.DestroyImmediate(oldNetMgr);
            }

            var kcp = netManagerObj.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = netManagerObj.AddComponent<kcp2k.KcpTransport>();
            }
            kcp.Port = 7777;

            var pucaciNet = netManagerObj.GetComponent<PucaciNetworkManager>();
            if (pucaciNet == null)
            {
                pucaciNet = netManagerObj.AddComponent<PucaciNetworkManager>();
            }

            pucaciNet.transport = kcp;
            pucaciNet.playerPrefab = playerPrefab;
            pucaciNet.autoCreatePlayer = true;
            pucaciNet.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;
            pucaciNet.maxConnections = 4;
            pucaciNet.dontDestroyOnLoad = true;
            pucaciNet.runInBackground = true;
            pucaciNet.offlineScene = "MainMenu";
            pucaciNet.onlineScene = "Gameplay";

            // 3. Tambahkan MultiplayerLobbyHUD ke GameplayCanvas
            GameObject canvasObj = GameObject.Find("GameplayCanvas");
            if (canvasObj != null)
            {
                var lobbyHUD = canvasObj.GetComponentInChildren<MultiplayerLobbyHUD>(true);
                if (lobbyHUD == null)
                {
                    canvasObj.AddComponent<MultiplayerLobbyHUD>();
                    Debug.Log("[MultiplayerSetupUtility] MultiplayerLobbyHUD ditambahkan ke GameplayCanvas");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MultiplayerSetupUtility] Scene Gameplay berhasil dibersihkan dan disimpan.");
        }

        [MenuItem("Pucaci/Setup MainMenu Scene")]
        public static void SetupMainMenuScene()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            // 1. Setup PucaciMultiplayerLauncher di MainMenuCanvas
            GameObject canvasObj = GameObject.Find("MainMenuCanvas");
            if (canvasObj != null)
            {
                // Hapus StubMultiplayerLauncher lama
                var stub = canvasObj.GetComponent<StubMultiplayerLauncher>();
                if (stub != null)
                {
                    Object.DestroyImmediate(stub);
                }

                var launcher = canvasObj.GetComponent<PucaciMultiplayerLauncher>();
                if (launcher == null)
                {
                    launcher = canvasObj.AddComponent<PucaciMultiplayerLauncher>();
                }

                var controller = canvasObj.GetComponent<MainMenuController>();
                if (controller != null)
                {
                    SerializedObject so = new SerializedObject(controller);
                    so.FindProperty("multiplayerLauncherComponent").objectReferenceValue = launcher;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 2. Setup NetworkManager persisten di MainMenu
            GameObject netManagerObj = GameObject.Find("NetworkManager");
            if (netManagerObj == null)
            {
                netManagerObj = new GameObject("NetworkManager");
            }

            var hud = netManagerObj.GetComponent<NetworkManagerHUD>();
            if (hud != null) Object.DestroyImmediate(hud);

            var oldNetMgr = netManagerObj.GetComponent<NetworkManager>();
            if (oldNetMgr != null && !(oldNetMgr is PucaciNetworkManager))
            {
                Object.DestroyImmediate(oldNetMgr);
            }

            var kcp = netManagerObj.GetComponent<kcp2k.KcpTransport>();
            if (kcp == null)
            {
                kcp = netManagerObj.AddComponent<kcp2k.KcpTransport>();
            }
            kcp.Port = 7777;

            var pucaciNet = netManagerObj.GetComponent<PucaciNetworkManager>();
            if (pucaciNet == null)
            {
                pucaciNet = netManagerObj.AddComponent<PucaciNetworkManager>();
            }

            pucaciNet.transport = kcp;
            pucaciNet.playerPrefab = playerPrefab;
            pucaciNet.autoCreatePlayer = true;
            pucaciNet.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;
            pucaciNet.maxConnections = 4;
            pucaciNet.dontDestroyOnLoad = true;
            pucaciNet.runInBackground = true;
            pucaciNet.offlineScene = "MainMenu";
            pucaciNet.onlineScene = "Gameplay";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MultiplayerSetupUtility] Scene MainMenu berhasil di-setup dan disimpan.");
        }
    }
}
