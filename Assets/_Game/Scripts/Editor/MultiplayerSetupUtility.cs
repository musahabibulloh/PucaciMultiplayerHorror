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

            var soNet = new SerializedObject(pucaciNet);
            var propTarget = soNet.FindProperty("targetPlayerCount");
            if (propTarget != null) propTarget.intValue = 4;
            soNet.ApplyModifiedPropertiesWithoutUndo();

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

            var soNetMain = new SerializedObject(pucaciNet);
            var propTargetMain = soNetMain.FindProperty("targetPlayerCount");
            if (propTargetMain != null) propTargetMain.intValue = 4;
            soNetMain.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MultiplayerSetupUtility] Scene MainMenu berhasil di-setup dan disimpan.");
        }

        [MenuItem("Pucaci/Verify Human Sabotage Feature")]
        public static void VerifyHumanSabotageFeature()
        {
            var go = new GameObject("Test_Player_Sabotage");
            GameObject vlmObj = null;
            try
            {
                var roleMgr = go.AddComponent<Pucaci.Player.PlayerRoleManager>();
                var sabotage = go.AddComponent<Pucaci.Player.KuntilanakLightSabotage>();

                // Setup VillageLightManager dummy jika belum ada
                if (Pucaci.Gameplay.VillageLightManager.Instance == null)
                {
                    vlmObj = new GameObject("Test_VLM");
                    var vlm = vlmObj.AddComponent<Pucaci.Gameplay.VillageLightManager>();
                    typeof(Pucaci.Gameplay.VillageLightManager)
                        .GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                        ?.SetValue(null, vlm);
                }

                var roleField = typeof(Pucaci.Player.PlayerRoleManager).GetField("currentRole", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var formField = typeof(Pucaci.Player.PlayerRoleManager).GetField("currentForm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Test 1: Impostor dalam wujud Human
                roleField?.SetValue(roleMgr, Pucaci.Player.PlayerRole.Impostor);
                formField?.SetValue(roleMgr, Pucaci.Player.ImpostorForm.Human);

                bool canSabotageHuman = sabotage.CanUseSkill();
                Debug.Log($"[TEST] Sabotase Lampu saat Wujud Human: {canSabotageHuman} (Expected: True)");

                // Test 2: Impostor dalam wujud Kuntilanak
                formField?.SetValue(roleMgr, Pucaci.Player.ImpostorForm.Kuntilanak);

                bool canSabotageKunti = sabotage.CanUseSkill();
                Debug.Log($"[TEST] Sabotase Lampu saat Wujud Kuntilanak: {canSabotageKunti} (Expected: True)");

                // Test 3: Survivor (bukan Impostor)
                roleField?.SetValue(roleMgr, Pucaci.Player.PlayerRole.Survivor);

                bool canSabotageSurvivor = sabotage.CanUseSkill();
                Debug.Log($"[TEST] Sabotase Lampu untuk Survivor: {canSabotageSurvivor} (Expected: False)");

                string report = $"[TEST] Sabotase Lampu saat Wujud Human: {canSabotageHuman} (Expected: True)\n" +
                                $"[TEST] Sabotase Lampu saat Wujud Kuntilanak: {canSabotageKunti} (Expected: True)\n" +
                                $"[TEST] Sabotase Lampu untuk Survivor: {canSabotageSurvivor} (Expected: False)\n";

                if (canSabotageHuman && canSabotageKunti && !canSabotageSurvivor)
                {
                    report += "RESULT: SUCCESS - Kuntilanak berhasil mensabotase lampu saat wujud manusia dan wujud kunti! Survivor tidak bisa sabotase.";
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] Kuntilanak berhasil mensabotase lampu saat wujud manusia dan wujud kunti! Survivor tidak bisa sabotase.</b></color>");
                }
                else
                {
                    report += "RESULT: FAILED - Kondisi sabotase tidak sesuai ekspektasi.";
                    Debug.LogError("[VERIFICATION FAILED] Kondisi sabotase tidak sesuai ekspektasi.");
                }

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/sabotage_test_report.txt", report);
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/sabotage_test_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                if (vlmObj != null) Object.DestroyImmediate(vlmObj);
                Object.DestroyImmediate(go);
            }
        }

        [MenuItem("Pucaci/Verify Hantu Revert Initial Character")]
        public static void VerifyHantuRevertInitialCharacter()
        {
            var go = new GameObject("Test_Player_Revert");
            try
            {
                var modelMgr = go.AddComponent<Pucaci.Player.CharacterModelManager>();
                var roleMgr = go.AddComponent<Pucaci.Player.PlayerRoleManager>();

                // Inisialisasi awal: Role Impostor dengan karakter Rocky (4)
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Impostor, 4);

                bool initialCheck = (roleMgr.InitialDisguiseIndex == 4) && (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.Rocky);
                Debug.Log($"[TEST 1] Initial Disguise is Rocky: {initialCheck} (disguise={roleMgr.HumanDisguise})");

                // Bertransformasi ke Kuntilanak
                roleMgr.TransformToKuntilanak();
                bool isKunti = roleMgr.IsKuntilanakForm;
                Debug.Log($"[TEST 2] Transformed to Kuntilanak: {isKunti}");

                // Kembali ke wujud manusia via ToggleKuntilanakForm
                roleMgr.ToggleKuntilanakForm();
                bool backToInitialHuman = roleMgr.IsHumanForm && (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.Rocky);
                Debug.Log($"[TEST 3] Reverted back to Rocky: {backToInitialHuman} (disguise={roleMgr.HumanDisguise})");

                // Test skenario jika sempat meniru orang lain (misal Pak Vincent = 3), lalu berubah kunti, lalu kembali
                roleMgr.MimicHuman(3);
                bool mimickedVincent = (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.PakVincent);
                Debug.Log($"[TEST 4] Mimicked Vincent: {mimickedVincent}");

                roleMgr.TransformToKuntilanak();
                Debug.Log($"[TEST 5] Transformed to Kuntilanak again: {roleMgr.IsKuntilanakForm}");

                roleMgr.ToggleKuntilanakForm();
                bool revertedToInitialAfterMimic = roleMgr.IsHumanForm && (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.Rocky);
                Debug.Log($"[TEST 6] Reverted to Initial (Rocky) after previous mimic: {revertedToInitialAfterMimic} (disguise={roleMgr.HumanDisguise})");

                // Uji juga untuk karakter lain: misal Laras (0)
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Impostor, 0);
                roleMgr.TransformToKuntilanak();
                roleMgr.ToggleKuntilanakForm();
                bool larasCheck = roleMgr.IsHumanForm && (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.Laras);
                Debug.Log($"[TEST 7] Initial Laras reverted correctly: {larasCheck} (disguise={roleMgr.HumanDisguise})");

                // Uji juga untuk karakter Pak Mamat (1)
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Impostor, 1);
                roleMgr.TransformToKuntilanak();
                roleMgr.ToggleKuntilanakForm();
                bool mamatCheck = roleMgr.IsHumanForm && (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.PakMamat);
                Debug.Log($"[TEST 8] Initial Pak Mamat reverted correctly: {mamatCheck} (disguise={roleMgr.HumanDisguise})");

                bool allSuccess = initialCheck && isKunti && backToInitialHuman && mimickedVincent && revertedToInitialAfterMimic && larasCheck && mamatCheck;

                string report = $"[TEST 1] Initial Disguise is Rocky: {initialCheck}\n" +
                                $"[TEST 2] Transformed to Kuntilanak: {isKunti}\n" +
                                $"[TEST 3] Reverted back to Rocky: {backToInitialHuman}\n" +
                                $"[TEST 4] Mimicked Vincent: {mimickedVincent}\n" +
                                $"[TEST 5] Transformed to Kuntilanak: {roleMgr.IsKuntilanakForm}\n" +
                                $"[TEST 6] Reverted to Initial (Rocky) after previous mimic: {revertedToInitialAfterMimic}\n" +
                                $"[TEST 7] Initial Laras reverted correctly: {larasCheck}\n" +
                                $"[TEST 8] Initial Pak Mamat reverted correctly: {mamatCheck}\n" +
                                $"ALL TESTS PASSED: {allSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/hantu_revert_report.txt", report);

                if (allSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] Hantu selalu kembali ke karakter utama awalnya setelah bertransformasi menjadi Kuntilanak!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] Hantu gagal kembali ke karakter utama awalnya.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/hantu_revert_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [MenuItem("Pucaci/Verify Disguise Selection Menu UI")]
        public static void VerifyDisguiseSelectionMenu()
        {
            var go = new GameObject("Test_Disguise_Player");
            try
            {
                var modelMgr = go.AddComponent<Pucaci.Player.CharacterModelManager>();
                var roleMgr = go.AddComponent<Pucaci.Player.PlayerRoleManager>();
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Impostor, 4); // Rocky

                var ui = Pucaci.UI.DisguiseSelectionUI.EnsureInstance();
                bool instanceExists = (ui != null);
                Debug.Log($"[DISGUISE TEST 1] DisguiseSelectionUI Instance created: {instanceExists}");

                // Buka menu
                ui.OpenMenu(roleMgr);
                bool isOpen = ui.IsOpen;
                Debug.Log($"[DISGUISE TEST 2] UI Open Menu: {isOpen}");

                var cdField = typeof(Pucaci.Player.PlayerRoleManager).GetField("transformCooldownTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var selectMethod = typeof(Pucaci.UI.DisguiseSelectionUI).GetMethod("SelectCharacter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Pilih Laras (0) lewat method reflection/panggilan
                cdField?.SetValue(roleMgr, 0f);
                selectMethod?.Invoke(ui, new object[] { 0 }); // Laras
                bool pickedLaras = (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.Laras) && (!ui.IsOpen);
                Debug.Log($"[DISGUISE TEST 3] Picked Laras and Closed: {pickedLaras} (disguise={roleMgr.HumanDisguise})");

                // Buka lagi dan pilih Pak Vincent (3)
                ui.OpenMenu(roleMgr);
                cdField?.SetValue(roleMgr, 0f);
                selectMethod?.Invoke(ui, new object[] { 3 }); // Pak Vincent
                bool pickedVincent = (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.PakVincent) && (!ui.IsOpen);
                Debug.Log($"[DISGUISE TEST 4] Picked Pak Vincent and Closed: {pickedVincent} (disguise={roleMgr.HumanDisguise})");

                // Buka lagi dan pilih Pak Mamat (1)
                ui.OpenMenu(roleMgr);
                cdField?.SetValue(roleMgr, 0f);
                selectMethod?.Invoke(ui, new object[] { 1 }); // Pak Mamat
                bool pickedMamat = (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.PakMamat) && (!ui.IsOpen);
                Debug.Log($"[DISGUISE TEST 5] Picked Pak Mamat and Closed: {pickedMamat} (disguise={roleMgr.HumanDisguise})");

                // Buka lagi dan pilih Mas Rocky (4)
                ui.OpenMenu(roleMgr);
                cdField?.SetValue(roleMgr, 0f);
                selectMethod?.Invoke(ui, new object[] { 4 }); // Mas Rocky
                bool pickedRocky = (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.Rocky) && (!ui.IsOpen);
                Debug.Log($"[DISGUISE TEST 6] Picked Mas Rocky and Closed: {pickedRocky} (disguise={roleMgr.HumanDisguise})");

                // Tutup menu via CloseMenu
                ui.OpenMenu(roleMgr);
                ui.CloseMenu();
                bool closedProperly = !ui.IsOpen;
                Debug.Log($"[DISGUISE TEST 7] CloseMenu works: {closedProperly}");

                bool allSuccess = instanceExists && isOpen && pickedLaras && pickedVincent && pickedMamat && pickedRocky && closedProperly;

                string report = $"[TEST 1] UI Instance: {instanceExists}\n" +
                                $"[TEST 2] UI Open Menu: {isOpen}\n" +
                                $"[TEST 3] Picked Laras: {pickedLaras}\n" +
                                $"[TEST 4] Picked Vincent: {pickedVincent}\n" +
                                $"[TEST 5] Picked Mamat: {pickedMamat}\n" +
                                $"[TEST 6] Picked Rocky: {pickedRocky}\n" +
                                $"[TEST 7] Closed Properly: {closedProperly}\n" +
                                $"ALL TESTS PASSED: {allSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/disguise_menu_report.txt", report);

                if (allSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] Menu Pemilihan Penyamaran [X] berfungsi sempurna untuk semua karakter!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] Menu Pemilihan Penyamaran gagal verifikasi.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/disguise_menu_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [MenuItem("Pucaci/Verify Objective Mission UI")]
        public static void VerifyObjectiveMissionUI()
        {
            var goPlayer = new GameObject("Test_Mission_Player");
            var goUI = new GameObject("Test_Mission_UI");
            try
            {
                var roleMgr = goPlayer.AddComponent<Pucaci.Player.PlayerRoleManager>();
                var objectiveUI = goUI.AddComponent<Pucaci.UI.SesajenObjectiveUI>();

                var textObj = new GameObject("TestText", typeof(TMPro.TextMeshProUGUI));
                textObj.transform.SetParent(goUI.transform, false);
                var tmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();

                var textField = typeof(Pucaci.UI.SesajenObjectiveUI).GetField("objectiveText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                textField?.SetValue(objectiveUI, tmp);

                var roleField = typeof(Pucaci.UI.SesajenObjectiveUI).GetField("localRoleManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                roleField?.SetValue(objectiveUI, roleMgr);

                // 1. Test Survivor (Player Biasa)
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Survivor, 0); // Laras
                objectiveUI.UpdateObjectiveDisplay(0, 5);
                string survivorText = tmp.text;
                bool survivorCheck = survivorText.Contains("Hancurkan Sesajen") && survivorText.Contains("(0/5)");
                Debug.Log($"[MISSION TEST 1] Survivor Mission UI: {survivorCheck} -> {survivorText}");

                objectiveUI.UpdateObjectiveDisplay(2, 5);
                string survivorProgressText = tmp.text;
                bool survivorProgressCheck = survivorProgressText.Contains("Hancurkan Sesajen") && survivorProgressText.Contains("(2/5)");
                Debug.Log($"[MISSION TEST 2] Survivor Progress UI: {survivorProgressCheck} -> {survivorProgressText}");

                // 2. Test Impostor (Hantu Kuntilanak)
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Impostor, 4); // Rocky
                objectiveUI.UpdateObjectiveDisplay(0, 5);
                string impostorText = tmp.text;
                bool impostorCheck = impostorText.Contains("Bunuh semua karakter") && impostorText.Contains("berubah wujud pencet T");
                Debug.Log($"[MISSION TEST 3] Impostor Mission UI: {impostorCheck} -> {impostorText}");

                bool allSuccess = survivorCheck && survivorProgressCheck && impostorCheck;

                string report = $"[TEST 1] Survivor Mission: {survivorCheck} ({survivorText})\n" +
                                $"[TEST 2] Survivor Progress: {survivorProgressCheck} ({survivorProgressText})\n" +
                                $"[TEST 3] Impostor Mission: {impostorCheck} ({impostorText})\n" +
                                $"ALL TESTS PASSED: {allSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/mission_ui_report.txt", report);

                if (allSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] UI Misi Survivor & Impostor sesuai instruksi!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] UI Misi tidak sesuai.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/mission_ui_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                Object.DestroyImmediate(goPlayer);
                Object.DestroyImmediate(goUI);
            }
        }
    }
}
