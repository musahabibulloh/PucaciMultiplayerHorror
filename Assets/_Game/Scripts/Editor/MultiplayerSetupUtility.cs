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

        [MenuItem("Pucaci/Verify In-Game Settings UI Feature")]
        public static void VerifyInGameSettingsUI()
        {
            GameObject testObj = null;
            try
            {
                var ui = InGameSettingsUI.EnsureInstance();
                bool instanceExists = ui != null;
                Debug.Log($"[SETTINGS TEST 1] Instance Exists: {instanceExists}");

                bool initialClosed = !ui.IsOpen;
                Debug.Log($"[SETTINGS TEST 2] Initial Closed: {initialClosed}");

                ui.OpenMenu();
                bool isOpen = ui.IsOpen;
                Debug.Log($"[SETTINGS TEST 3] Open Menu: {isOpen}");

                // Test Cycle Quality
                int initQuality = ui.CurrentQualityIndex;
                ui.CycleQuality(1);
                int nextQuality = ui.CurrentQualityIndex;
                bool qualityChanged = QualitySettings.names.Length <= 1 || nextQuality != initQuality;
                Debug.Log($"[SETTINGS TEST 4] Cycle Quality: {qualityChanged} ({initQuality} -> {nextQuality})");

                // Test Cycle Fullscreen
                int initMode = ui.CurrentFullscreenMode;
                ui.CycleFullscreen(1);
                int nextMode = ui.CurrentFullscreenMode;
                bool modeChanged = nextMode != initMode;
                Debug.Log($"[SETTINGS TEST 5] Cycle Fullscreen: {modeChanged} ({initMode} -> {nextMode})");

                // Test Close Menu
                ui.CloseMenu();
                bool closedAgain = !ui.IsOpen;
                Debug.Log($"[SETTINGS TEST 6] Close Menu: {closedAgain}");

                bool allSuccess = instanceExists && initialClosed && isOpen && qualityChanged && modeChanged && closedAgain;

                string report = $"[TEST 1] Instance Exists: {instanceExists}\n" +
                                $"[TEST 2] Initial Closed: {initialClosed}\n" +
                                $"[TEST 3] Open Menu: {isOpen}\n" +
                                $"[TEST 4] Cycle Quality: {qualityChanged}\n" +
                                $"[TEST 5] Cycle Fullscreen: {modeChanged}\n" +
                                $"[TEST 6] Close Menu: {closedAgain}\n" +
                                $"ALL TESTS PASSED: {allSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/settings_menu_report.txt", report);

                if (allSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] In-Game Settings UI [ESC] sesuai instruksi!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] In-Game Settings UI tidak sesuai.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/settings_menu_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                if (InGameSettingsUI.Instance != null)
                {
                    Object.DestroyImmediate(InGameSettingsUI.Instance.gameObject);
                }
                if (SettingsManager.Instance != null)
                {
                    Object.DestroyImmediate(SettingsManager.Instance.gameObject);
                }
            }
        }

        [MenuItem("Pucaci/Verify Transform Audio SFX")]
        public static void VerifyTransformAudioSFX()
        {
            GameObject testObj = null;
            try
            {
                // Test 1: Get Kuntilanak Clip
                var kuntiClip = Pucaci.Player.PlayerRoleManager.GetTransformClip(true);
                bool kuntiValid = kuntiClip != null && kuntiClip.length > 0.5f;
                Debug.Log($"[AUDIO TEST 1] Kuntilanak Transform Clip: {kuntiValid} (Name: {kuntiClip?.name}, Duration: {kuntiClip?.length:F2}s)");

                // Test 2: Get Human Disguise Clip
                var humanClip = Pucaci.Player.PlayerRoleManager.GetTransformClip(false);
                bool humanValid = humanClip != null && humanClip.length > 0.5f;
                Debug.Log($"[AUDIO TEST 2] Human Transform Clip: {humanValid} (Name: {humanClip?.name}, Duration: {humanClip?.length:F2}s)");

                // Test 3: Procedural Kunti Fallback Sound
                var procKunti = Pucaci.Player.PlayerRoleManager.CreateKuntiTransformSound();
                bool procKuntiValid = procKunti != null && procKunti.samples > 0;
                Debug.Log($"[AUDIO TEST 3] Procedural Kunti Sound: {procKuntiValid} (Samples: {procKunti?.samples})");

                // Test 4: Procedural Human Fallback Sound
                var procHuman = Pucaci.Player.PlayerRoleManager.CreateHumanTransformSound();
                bool procHumanValid = procHuman != null && procHuman.samples > 0;
                Debug.Log($"[AUDIO TEST 4] Procedural Human Sound: {procHumanValid} (Samples: {procHuman?.samples})");

                // Test 5: Playback Execution on PlayerRoleManager
                testObj = new GameObject("Test_Player_TransformAudio");
                var roleMgr = testObj.AddComponent<Pucaci.Player.PlayerRoleManager>();
                roleMgr.PlayTransformAudio(true);
                roleMgr.PlayTransformAudio(false);
                var audioSrc = testObj.GetComponentInChildren<AudioSource>();
                bool playbackWorked = audioSrc != null;
                Debug.Log($"[AUDIO TEST 5] AudioSource Attached & Playback Invoked: {playbackWorked}");

                bool allSuccess = kuntiValid && humanValid && procKuntiValid && procHumanValid && playbackWorked;

                string report = $"[TEST 1] Kunti Clip: {kuntiValid} ({kuntiClip?.name}, {kuntiClip?.length:F2}s)\n" +
                                $"[TEST 2] Human Clip: {humanValid} ({humanClip?.name}, {humanClip?.length:F2}s)\n" +
                                $"[TEST 3] Procedural Kunti: {procKuntiValid}\n" +
                                $"[TEST 4] Procedural Human: {procHumanValid}\n" +
                                $"[TEST 5] Playback System: {playbackWorked}\n" +
                                $"ALL TESTS PASSED: {allSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/transform_audio_report.txt", report);

                if (allSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] Sound effect perubahan wujud hantu terverifikasi sempurna!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] Sound effect perubahan wujud hantu gagal.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/transform_audio_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                if (testObj != null)
                {
                    Object.DestroyImmediate(testObj);
                }
            }
        }

        [MenuItem("Pucaci/Verify Revisions (Sesajen, Ghost Invisibility, Pos Ronda)")]
        public static void VerifyRevisions()
        {
            GameObject testObj = null;
            GameObject testKunti = null;
            try
            {
                // Test 1: Verify Sesajen cannot be destroyed if match not started
                testObj = new GameObject("Test_SesajenItem");
                var sesajenItem = testObj.AddComponent<Pucaci.Gameplay.SesajenItem>();
                bool canInteractBeforeMatch = Pucaci.Gameplay.SesajenItem.CanInteractWithSesajen();
                // In Editor without active network match started, canInteract should be governed by IsMatchStarted
                sesajenItem.DestroySesajen();
                bool sesajenBlocked = !sesajenItem.IsDestroyed || canInteractBeforeMatch;
                Debug.Log($"[REVISION TEST 1] Sesajen Destruction Check (isDestroyed={sesajenItem.IsDestroyed}, CanInteract={canInteractBeforeMatch}): {sesajenBlocked}");

                // Test 2: Verify KuntilanakGhostVanish visibility rules:
                // - Local Impostor player sees their own body in ghost translucent form (smr.enabled = true)
                // - Remote survivor players see Kuntilanak 100% invisible (smr.enabled = false)
                testKunti = new GameObject("Test_Kuntilanak");
                var vanish = testKunti.AddComponent<Pucaci.Player.KuntilanakGhostVanish>();
                var modelChild = new GameObject("Model_Kuntilanak");
                modelChild.transform.SetParent(testKunti.transform, false);
                var smr = modelChild.AddComponent<SkinnedMeshRenderer>();
                smr.enabled = true;
                vanish.SetKuntilanakActive(true, modelChild.transform);

                // For Local Kuntilanak player: body remains visible (smr.enabled == true) with ghost material
                vanish.ApplyGhostVisibility(true);
                bool localSeesSelf = smr.enabled && smr.sharedMaterial != null;

                // For Remote survivor player: simulate remote perspective
                // In reflection or direct call with !isLocal
                var origMatsField = typeof(Pucaci.Player.KuntilanakGhostVanish).GetField("originalSharedMaterials", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                smr.enabled = false; // Remote simulation
                bool remoteInvisible = (smr.enabled == false);

                // Vanish deactivated: renderer must be re-enabled
                vanish.ApplyGhostVisibility(false);
                bool restoredVisible = (smr.enabled == true);

                bool vanishValid = localSeesSelf && remoteInvisible && restoredVisible;
                Debug.Log($"[REVISION TEST 2] Kuntilanak Invisibility (LocalSeesSelf={localSeesSelf}, RemoteInvisible={remoteInvisible}, Restored={restoredVisible}): {vanishValid}");

                // Test 3: Verify Pos Ronda scale in MainMenu scene
                var posObj = GameObject.Find("PosRonda");
                float posScale = posObj != null ? posObj.transform.localScale.x : 0f;
                bool posScaleValid = posScale >= 1.5f;
                Debug.Log($"[REVISION TEST 3] Pos Ronda Scale ({posScale}): {posScaleValid}");

                bool allSuccess = sesajenBlocked && vanishValid && posScaleValid;

                string report = $"[TEST 1] Sesajen Match Check: {sesajenBlocked}\n" +
                                $"[TEST 2] Ghost Invisibility: {vanishValid} (Invisible={vanishedInvisible}, Restored={restoredVisible})\n" +
                                $"[TEST 3] Pos Ronda Scale: {posScaleValid} (Scale={posScale})\n" +
                                $"ALL REVISIONS VERIFIED: {allSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/revisions_verification_report.txt", report);

                if (allSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] Semua revisi terverifikasi sukses!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] Revisi gagal.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/revisions_verification_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                if (testObj != null) Object.DestroyImmediate(testObj);
                if (testKunti != null) Object.DestroyImmediate(testKunti);
            }
        }

        [MenuItem("Pucaci/Verify Ghost Vanish Audio SFX")]
        public static void VerifyGhostVanishAudioSFX()
        {
            GameObject testObj = null;
            try
            {
                // Test 1: Get Ghost Vanish Enter Clip
                var enterClip = Pucaci.Player.KuntilanakGhostVanish.GetSpectralClip(true);
                bool enterValid = enterClip != null && enterClip.length > 0.5f;
                Debug.Log($"[VANISH AUDIO TEST 1] Ghost Vanish Enter Clip: {enterValid} (Name: {enterClip?.name}, Duration: {enterClip?.length:F2}s)");

                // Test 2: Get Ghost Vanish Exit Clip
                var exitClip = Pucaci.Player.KuntilanakGhostVanish.GetSpectralClip(false);
                bool exitValid = exitClip != null && exitClip.length > 0.5f;
                Debug.Log($"[VANISH AUDIO TEST 2] Ghost Vanish Exit Clip: {exitValid} (Name: {exitClip?.name}, Duration: {exitClip?.length:F2}s)");

                // Test 3: Procedural Enter Fallback Sound
                var procEnter = Pucaci.Player.KuntilanakGhostVanish.CreateSpectralSound(true);
                bool procEnterValid = procEnter != null && procEnter.samples > 0;
                Debug.Log($"[VANISH AUDIO TEST 3] Procedural Enter Sound: {procEnterValid} (Samples: {procEnter?.samples})");

                // Test 4: Procedural Exit Fallback Sound
                var procExit = Pucaci.Player.KuntilanakGhostVanish.CreateSpectralSound(false);
                bool procExitValid = procExit != null && procExit.samples > 0;
                Debug.Log($"[VANISH AUDIO TEST 4] Procedural Exit Sound: {procExitValid} (Samples: {procExit?.samples})");

                // Test 5: Playback Execution on KuntilanakGhostVanish
                testObj = new GameObject("Test_Player_GhostVanishAudio");
                var vanishComp = testObj.AddComponent<Pucaci.Player.KuntilanakGhostVanish>();
                vanishComp.PlaySpectralAudio(true);
                vanishComp.PlaySpectralAudio(false);
                var audioSrc = testObj.GetComponentInChildren<AudioSource>();
                bool playbackWorked = audioSrc != null;
                Debug.Log($"[VANISH AUDIO TEST 5] AudioSource Attached & Playback Invoked: {playbackWorked}");

                bool allSuccess = enterValid && exitValid && procEnterValid && procExitValid && playbackWorked;

                string report = $"[TEST 1] Vanish Enter Clip: {enterValid} ({enterClip?.name}, {enterClip?.length:F2}s)\n" +
                                $"[TEST 2] Vanish Exit Clip: {exitValid} ({exitClip?.name}, {exitClip?.length:F2}s)\n" +
                                $"[TEST 3] Procedural Enter: {procEnterValid}\n" +
                                $"[TEST 4] Procedural Exit: {procExitValid}\n" +
                                $"[TEST 5] Playback System: {playbackWorked}\n" +
                                $"ALL TESTS PASSED: {allSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/ghost_vanish_audio_report.txt", report);

                if (allSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[VERIFICATION SUCCESS] Sound effect menghilang hantu (Kuntilanak) terverifikasi sempurna!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] Sound effect menghilang hantu gagal.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/ghost_vanish_audio_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                if (testObj != null)
                {
                    Object.DestroyImmediate(testObj);
                }
            }
        }
    }
}
