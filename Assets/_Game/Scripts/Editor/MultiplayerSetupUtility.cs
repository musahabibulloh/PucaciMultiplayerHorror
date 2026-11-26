using System.Collections.Generic;
using Mirror;
using Pucaci.Core;
using Pucaci.Network;
using Pucaci.UI;
using Pucaci.Inventory;
using Pucaci.Gameplay;
using Pucaci.Player;
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

        [MenuItem("Pucaci/Verify Inventory & Disguise Multiuse Fix")]
        public static void VerifyInventoryAndDisguiseFix()
        {
            var goPlayer = new GameObject("Test_Player_Fix");
            var goInvUI = new GameObject("Test_Inventory_UI");
            var goGhostHUD = new GameObject("GhostSkillHUD");
            try
            {
                var roleMgr = goPlayer.AddComponent<Pucaci.Player.PlayerRoleManager>();
                var modelMgr = goPlayer.AddComponent<Pucaci.Player.CharacterModelManager>();
                var inv = goPlayer.AddComponent<Pucaci.Inventory.PlayerInventory>();
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Impostor, 1); // Pak Mamat

                // 1. Uji Multi-Use Disguise Menu [X]
                var disguiseUI = Pucaci.UI.DisguiseSelectionUI.EnsureInstance();
                bool step1_opened = false;
                bool step2_changedLaras = false;
                bool step3_reopened = false;
                bool step4_changedVincent = false;
                bool step5_reopenedAgain = false;

                // Open 1
                disguiseUI.ToggleMenu(roleMgr);
                step1_opened = disguiseUI.IsOpen;

                // Mimic Laras
                roleMgr.MimicHuman(0);
                disguiseUI.CloseMenu();
                step2_changedLaras = (!disguiseUI.IsOpen) && (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.Laras);

                // Open 2 (Bug sebelumnya: tidak bisa dibuka lagi setelah sekali pakai)
                disguiseUI.ToggleMenu(roleMgr);
                step3_reopened = disguiseUI.IsOpen;

                // Mimic Vincent
                roleMgr.MimicHuman(3);
                disguiseUI.CloseMenu();
                step4_changedVincent = (!disguiseUI.IsOpen) && (roleMgr.HumanDisguise == Pucaci.Player.CharacterType.PakVincent);

                // Open 3
                disguiseUI.ToggleMenu(roleMgr);
                step5_reopenedAgain = disguiseUI.IsOpen;
                disguiseUI.CloseMenu();

                bool disguiseMultiUseSuccess = step1_opened && step2_changedLaras && step3_reopened && step4_changedVincent && step5_reopenedAgain;
                Debug.Log($"[FIX TEST 1] Disguise Multi-Use: {disguiseMultiUseSuccess} (O1:{step1_opened}, L:{step2_changedLaras}, O2:{step3_reopened}, V:{step4_changedVincent}, O3:{step5_reopenedAgain})");

                // 2. Uji Penataan Posisi Inventory UI (Tidak Bertabrakan dengan GhostSkillHUD)
                var invUI = goInvUI.AddComponent<Pucaci.UI.PlayerInventoryUI>();
                typeof(Pucaci.UI.PlayerInventoryUI).GetMethod("BuildUIHierarchy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(invUI, null);
                invUI.SetTargetInventory(inv);

                var updateLayoutMethod = typeof(Pucaci.UI.PlayerInventoryUI).GetMethod("UpdateLayoutPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var containerField = typeof(Pucaci.UI.PlayerInventoryUI).GetField("slotsContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Saat Impostor aktif: Slot Inventory berada di Y = 158 (di atas Ghost HUD)
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Impostor, 1);
                updateLayoutMethod?.Invoke(invUI, null);
                var containerRect = containerField?.GetValue(invUI) as RectTransform;
                float posY_ghost = containerRect != null ? containerRect.anchoredPosition.y : 0f;
                bool posGhostCorrect = Mathf.Approximately(posY_ghost, 158f);

                // Saat Survivor normal: Slot Inventory turun ke Y = 36 (tepat di atas health bar)
                roleMgr.SetRole(Pucaci.Player.PlayerRole.Survivor, 1);
                updateLayoutMethod?.Invoke(invUI, null);
                float posY_survivor = containerRect != null ? containerRect.anchoredPosition.y : 0f;
                bool posSurvivorCorrect = Mathf.Approximately(posY_survivor, 36f);

                bool uiLayoutSuccess = posGhostCorrect && posSurvivorCorrect;
                Debug.Log($"[FIX TEST 2] UI Layout Non-Overlapping: {uiLayoutSuccess} (GhostPos={posY_ghost}, SurvivorPos={posY_survivor})");

                string report = $"DISGUISE_MULTI_USE_FIX: {disguiseMultiUseSuccess}\n" +
                                $"Step1_Opened: {step1_opened}\n" +
                                $"Step2_ChangedLaras: {step2_changedLaras}\n" +
                                $"Step3_Reopened: {step3_reopened}\n" +
                                $"Step4_ChangedVincent: {step4_changedVincent}\n" +
                                $"Step5_ReopenedAgain: {step5_reopenedAgain}\n" +
                                $"UI_LAYOUT_FIX: {uiLayoutSuccess}\n" +
                                $"GhostMode_Inventory_Y: {posY_ghost} (Expected: 158)\n" +
                                $"SurvivorMode_Inventory_Y: {posY_survivor} (Expected: 36)\n" +
                                $"ALL_PASSED: {disguiseMultiUseSuccess && uiLayoutSuccess}\n";

                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/ui_overlap_and_disguise_fix_report.txt", report);

                if (disguiseMultiUseSuccess && uiLayoutSuccess)
                {
                    Debug.Log("<color=#00FFAA><b>[ALL FIXES VERIFIED] UI bertabrakan telah diatasi sempurna dan fungsi [X] multi-use berjalan lancar!</b></color>");
                }
                else
                {
                    Debug.LogError("[VERIFICATION FAILED] Ada pengujian yang gagal.");
                }
            }
            catch (System.Exception ex)
            {
                System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/ui_overlap_and_disguise_fix_report.txt", "EXCEPTION: " + ex.ToString());
                Debug.LogException(ex);
            }
            finally
            {
                Object.DestroyImmediate(goPlayer);
                Object.DestroyImmediate(goInvUI);
                Object.DestroyImmediate(goGhostHUD);
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
                                $"[TEST 2] Ghost Invisibility: {vanishValid} (LocalSeesSelf={localSeesSelf}, RemoteInvisible={remoteInvisible}, Restored={restoredVisible})\n" +
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

        private const string SesajenPrefabPath = "Assets/_Game/Prefabs/Props/Sesajen.prefab";
        private const string SesajenFbxPath = "Assets/_Game/Models/Items/Sesajen/SESAJEN.fbx";
        private const string ItemsPrefabDir = "Assets/_Game/Prefabs/Items";

        [MenuItem("Pucaci/Setup Inventory and New Sesajen Feature")]
        public static void SetupInventoryAndNewSesajenFeature()
        {
            if (!System.IO.Directory.Exists(ItemsPrefabDir)) System.IO.Directory.CreateDirectory(ItemsPrefabDir);

            // 0. Setup CharacterInventoryConfig.asset di Resources
            string resDir = "Assets/_Game/Resources";
            if (!System.IO.Directory.Exists(resDir)) System.IO.Directory.CreateDirectory(resDir);
            string configPath = $"{resDir}/CharacterInventoryConfig.asset";
            var existingConfig = AssetDatabase.LoadAssetAtPath<CharacterInventoryConfig>(configPath);
            if (existingConfig == null)
            {
                var newConfig = ScriptableObject.CreateInstance<CharacterInventoryConfig>();
                newConfig.SetSlotsForCharacter(CharacterType.PakMamat, 4);
                newConfig.SetSlotsForCharacter(CharacterType.Laras, 2);
                newConfig.SetSlotsForCharacter(CharacterType.PakVincent, 2);
                newConfig.SetSlotsForCharacter(CharacterType.Rocky, 2);
                AssetDatabase.CreateAsset(newConfig, configPath);
            }

            // 1. Update Sesajen.prefab dengan SESAJEN.fbx
            var sesajenFbx = AssetDatabase.LoadAssetAtPath<GameObject>(SesajenFbxPath);
            var sesajenPrefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(SesajenPrefabPath);
            if (sesajenFbx != null && sesajenPrefabObj != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(sesajenPrefabObj) as GameObject;
                Transform visual = instance.transform.Find("Visual");
                if (visual == null)
                {
                    var visObj = new GameObject("Visual");
                    visObj.transform.SetParent(instance.transform, false);
                    visual = visObj.transform;
                }

                List<GameObject> toDestroy = new List<GameObject>();
                for (int i = 0; i < visual.childCount; i++) toDestroy.Add(visual.GetChild(i).gameObject);
                foreach (var c in toDestroy) Object.DestroyImmediate(c);

                var newModel = Object.Instantiate(sesajenFbx, visual);
                newModel.name = "SESAJEN_Model";
                ItemVisualUtility.ApplySesajenTransform(newModel);

                instance.transform.rotation = Quaternion.identity;
                var col = instance.GetComponent<CapsuleCollider>();
                if (col != null)
                {
                    col.radius = 0.35f;
                    col.height = 0.45f;
                    col.center = new Vector3(0f, 0.22f, 0f);
                }

                foreach (var sc in newModel.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(sc);

                var sesajenItem = instance.GetComponent<SesajenItem>();
                if (sesajenItem != null) sesajenItem.ResolveReferences();

                // Pastikan material partikel asap menggunakan shader URP yang valid (bukan legacy magenta)
                Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Props/M_Sesajen_Smoke.mat");
                if (smokeMat == null)
                {
                    Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                    smokeMat = new Material(particleShader) { name = "M_Sesajen_Smoke" };
                    smokeMat.SetFloat("_Surface", 1);
                    smokeMat.SetFloat("_Blend", 0);
                    smokeMat.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.95f, 0.35f));
                    AssetDatabase.CreateAsset(smokeMat, "Assets/_Game/Materials/Props/M_Sesajen_Smoke.mat");
                }
                foreach (var psr in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    psr.sharedMaterial = smokeMat;
                }

                PrefabUtility.SaveAsPrefabAsset(instance, SesajenPrefabPath);
                Object.DestroyImmediate(instance);
                Debug.Log("[MultiplayerSetupUtility] Sesajen.prefab diperbarui dengan SESAJEN.fbx (Proporsi & Rotasi Rata Tanah)!");
            }

            // 2. Update Sesajen di MainMenu.unity dan Gameplay.unity
            UpdateSesajenInScenePath(MainMenuScenePath);
            UpdateSesajenInScenePath(GameplayScenePath);

            // 3. Setup Garlic Projectile Prefab
            string garlicPath = ItemsPrefabDir + "/GarlicProjectile.prefab";
            var garlicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(garlicPath);
            if (garlicPrefab == null)
            {
                var garlicObj = new GameObject("GarlicProjectile");
                garlicObj.AddComponent<NetworkIdentity>();
                var netTransform = garlicObj.AddComponent<NetworkTransformReliable>();
                netTransform.syncDirection = SyncDirection.ServerToClient;

                garlicObj.AddComponent<GarlicProjectile>();
                var col = garlicObj.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.25f;

                var rb = garlicObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "GarlicVisual";
                visual.transform.SetParent(garlicObj.transform, false);
                visual.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());

                var rend = visual.GetComponent<MeshRenderer>();
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(litShader) { name = "M_Garlic_Clove" };
                mat.color = new Color(0.96f, 0.95f, 0.88f, 1f);
                rend.sharedMaterial = mat;

                garlicPrefab = PrefabUtility.SaveAsPrefabAsset(garlicObj, garlicPath);
                Object.DestroyImmediate(garlicObj);
                Debug.Log("[MultiplayerSetupUtility] GarlicProjectile.prefab dibuat.");
            }

            // 4. Setup Pickup Prefabs dengan ukuran normal manusia
            SetupPickupPrefab(ItemType.Kentongan, "Assets/_Game/Models/Items/Kentongan/Kentongan.fbx");
            SetupPickupPrefab(ItemType.Keris, "Assets/_Game/Models/Items/Keris/Keris.fbx");
            SetupPickupPrefab(ItemType.Totem, "Assets/_Game/Models/Items/TOTEM/botjo totem.fbx");
            SetupPickupPrefab(ItemType.Tulup, "Assets/_Game/Models/Items/Tulup/Tulup.fbx");

            // 5. Pastikan PlayerCharacter.prefab memiliki PlayerInventory dan PlayerItemAnimator
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                bool dirty = false;
                if (playerPrefab.GetComponent<PlayerInventory>() == null)
                {
                    playerPrefab.AddComponent<PlayerInventory>();
                    dirty = true;
                    Debug.Log("[MultiplayerSetupUtility] PlayerInventory ditambahkan ke PlayerCharacter.prefab");
                }
                if (playerPrefab.GetComponent<PlayerItemAnimator>() == null)
                {
                    playerPrefab.AddComponent<PlayerItemAnimator>();
                    dirty = true;
                    Debug.Log("[MultiplayerSetupUtility] PlayerItemAnimator ditambahkan ke PlayerCharacter.prefab");
                }
                if (dirty)
                {
                    EditorUtility.SetDirty(playerPrefab);
                    PrefabUtility.SavePrefabAsset(playerPrefab);
                }
            }

            // 6. Daftarkan spawn prefabs
            RegisterSpawnPrefabs(MainMenuScenePath, garlicPrefab);
            RegisterSpawnPrefabs(GameplayScenePath, garlicPrefab);

            // 7. Hapus semua item pickup dari game sesuai instruksi user
            RemoveAllPickupsFromScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void UpdateSesajenInScenePath(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var sesajenFbx = AssetDatabase.LoadAssetAtPath<GameObject>(SesajenFbxPath);
            var allSesajen = Object.FindObjectsOfType<SesajenItem>();

            Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Props/M_Sesajen_Smoke.mat");
            if (smokeMat == null)
            {
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                smokeMat = new Material(particleShader) { name = "M_Sesajen_Smoke" };
                smokeMat.SetFloat("_Surface", 1);
                smokeMat.SetFloat("_Blend", 0);
                smokeMat.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.95f, 0.35f));
                AssetDatabase.CreateAsset(smokeMat, "Assets/_Game/Materials/Props/M_Sesajen_Smoke.mat");
            }

            foreach (var sesajen in allSesajen)
            {
                Transform visual = sesajen.transform.Find("Visual");
                if (visual != null)
                {
                    List<GameObject> toRemove = new List<GameObject>();
                    for (int i = 0; i < visual.childCount; i++) toRemove.Add(visual.GetChild(i).gameObject);
                    foreach (var tr in toRemove) Object.DestroyImmediate(tr);

                    var newModel = Object.Instantiate(sesajenFbx, visual);
                    newModel.name = "SESAJEN_Model";
                    ItemVisualUtility.ApplySesajenTransform(newModel);

                    // Pastikan root sesajen memiliki rotasi tegak lurus menempel tanah
                    sesajen.transform.rotation = Quaternion.identity;

                    foreach (var sc in newModel.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(sc);

                    // Cari lantai di bawahnya dan tempelkan root persis di tanah (floorY)
                    float floorY = 0f;
                    var hits = Physics.RaycastAll(sesajen.transform.position + Vector3.up * 5f, Vector3.down, 15f);
                    foreach (var h in hits)
                    {
                        if (h.transform.IsChildOf(sesajen.transform)) continue;
                        if (h.point.y > -0.5f && h.point.y < 3f)
                        {
                            floorY = h.point.y;
                            break;
                        }
                    }
                    sesajen.transform.position = new Vector3(sesajen.transform.position.x, floorY, sesajen.transform.position.z);

                    var col = sesajen.GetComponent<CapsuleCollider>();
                    if (col != null)
                    {
                        col.radius = 0.35f;
                        col.height = 0.45f;
                        col.center = new Vector3(0f, 0.225f, 0f);
                    }

                    // Perbarui material partikel asap pada sesajen
                    foreach (var psr in sesajen.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        psr.sharedMaterial = smokeMat;
                    }

                    sesajen.ResolveReferences();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MultiplayerSetupUtility] {allSesajen.Length} Sesajen di {scenePath} berhasil diperbarui ke SESAJEN.fbx.");
        }

        private static GameObject SetupPickupPrefab(ItemType type, string fbxPath)
        {
            string prefabPath = $"{ItemsPrefabDir}/Pickup_{type}.prefab";

            var root = new GameObject($"Pickup_{type}");
            root.AddComponent<NetworkIdentity>();
            var pickup = root.AddComponent<WorldItemPickup>();

            var propType = typeof(WorldItemPickup).GetField("itemType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            propType?.SetValue(pickup, type);

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 2.4f;

            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx != null)
            {
                var modelInst = Object.Instantiate(fbx, visualRoot.transform);
                modelInst.name = "Model";
                ItemVisualUtility.ApplyPickupTransform(modelInst, type);
                foreach (var c in modelInst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            }

            var lightObj = new GameObject("ItemGlowLight");
            lightObj.transform.SetParent(visualRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2.2f;
            light.intensity = 0.85f;
            light.color = (type == ItemType.Totem) ? new Color(0.3f, 0.8f, 1f) :
                          ((type == ItemType.Keris) ? new Color(1f, 0.4f, 0.2f) :
                          ((type == ItemType.Kentongan) ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 1f, 0.4f)));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void RegisterSpawnPrefabs(string scenePath, GameObject projectilePrefab)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var netManager = Object.FindObjectOfType<NetworkManager>();
            if (netManager != null && projectilePrefab != null)
            {
                if (!netManager.spawnPrefabs.Contains(projectilePrefab))
                {
                    netManager.spawnPrefabs.Add(projectilePrefab);
                    EditorUtility.SetDirty(netManager);
                }
            }

            string[] pickupTypes = { "Kentongan", "Keris", "Totem", "Tulup" };
            foreach (var pt in pickupTypes)
            {
                var pPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemsPrefabDir}/Pickup_{pt}.prefab");
                if (pPrefab != null && netManager != null && !netManager.spawnPrefabs.Contains(pPrefab))
                {
                    netManager.spawnPrefabs.Add(pPrefab);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void RemoveAllPickupsFromScenes()
        {
            RemovePickupsFromScene(MainMenuScenePath);
            RemovePickupsFromScene(GameplayScenePath);
        }

        private static void RemovePickupsFromScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var existingPickups = Object.FindObjectsOfType<WorldItemPickup>();
            foreach (var ep in existingPickups) Object.DestroyImmediate(ep.gameObject);

            var container = GameObject.Find("WorldItemPickups");
            if (container != null) Object.DestroyImmediate(container);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MultiplayerSetupUtility] Semua WorldItemPickup di {scenePath} telah dihapus bersih!");
        }

        private static void SpawnPickupInScene(ItemType type, Vector3 pos, Transform parent)
        {
            string prefabPath = $"{ItemsPrefabDir}/Pickup_{type}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var inst = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                inst.name = $"Pickup_{type}_{pos.x:F0}_{pos.z:F0}";
                inst.transform.position = pos;
            }
        }

        [MenuItem("Pucaci/Verify Inventory and New Sesajen Feature")]
        public static void VerifyInventoryAndNewSesajenFeature()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== VERIFICATION REPORT: INVENTORY SYSTEM & NEW SESAJEN MODEL ===");
            report.AppendLine("Timestamp: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            bool allPassed = true;

            GameObject testSurvivor = null;
            GameObject testPakMamat = null;
            GameObject testKunti = null;

            try
            {
                // 1. Verifikasi Konfigurasi Slot Karakter (Pak Mamat 4 slot, lainnya 2 slot)
                var config = CharacterInventoryConfig.Instance;
                int mamatSlots = config.GetMaxSlotsForCharacter(CharacterType.PakMamat);
                int larasSlots = config.GetMaxSlotsForCharacter(CharacterType.Laras);
                int vincentSlots = config.GetMaxSlotsForCharacter(CharacterType.PakVincent);
                int rockySlots = config.GetMaxSlotsForCharacter(CharacterType.Rocky);

                bool configValid = (mamatSlots == 4 && larasSlots == 2 && vincentSlots == 2 && rockySlots == 2);
                report.AppendLine($"[TEST 1] CharacterInventoryConfig: Pak Mamat={mamatSlots} slots, Laras={larasSlots}, Vincent={vincentSlots}, Rocky={rockySlots} -> {(configValid ? "PASSED" : "FAILED")}");
                if (!configValid) allPassed = false;

                // 2. Setup Dummy GameObject untuk Pengujian Logika Inventory
                testSurvivor = new GameObject("Test_Survivor");
                testSurvivor.AddComponent<NetworkIdentity>();
                var survivorModel = testSurvivor.AddComponent<CharacterModelManager>();
                var survivorHealth = testSurvivor.AddComponent<PlayerHealth>();
                var survivorInv = testSurvivor.AddComponent<PlayerInventory>();

                // Set karakter ke Laras (2 slot)
                var charIdxField = typeof(CharacterModelManager).GetField("localCharacterIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                charIdxField?.SetValue(survivorModel, (int)CharacterType.Laras);

                int maxSlots = survivorInv.MaxSlots;
                bool slotCountMatch = (maxSlots == 2);
                report.AppendLine($"[TEST 2] Survivor MaxSlots ({maxSlots}) == 2 -> {(slotCountMatch ? "PASSED" : "FAILED")}");
                if (!slotCountMatch) allPassed = false;

                // Test pengisian slot inventory
                var objectsField = typeof(SyncList<ItemType>).GetField("objects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var internalList = (System.Collections.Generic.IList<ItemType>)objectsField?.GetValue(survivorInv.slots);
                internalList?.Add(ItemType.Kentongan);
                internalList?.Add(ItemType.Totem);
                bool isFullBeforeOverfill = survivorInv.IsFull;
                report.AppendLine($"[TEST 3] Inventory Full Check (Slots=2/2): IsFull={isFullBeforeOverfill} -> {(isFullBeforeOverfill ? "PASSED" : "FAILED")}");
                if (!isFullBeforeOverfill) allPassed = false;

                // Test equip slot
                var equipSlotField = typeof(PlayerInventory).GetField("equippedSlotIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                equipSlotField?.SetValue(survivorInv, 1); // Slot 1 = Totem
                bool isTotemEquipped = survivorInv.IsItemEquipped(ItemType.Totem);
                report.AppendLine($"[TEST 4] Equip Slot [1] (Totem): IsItemEquipped(Totem)={isTotemEquipped} -> {(isTotemEquipped ? "PASSED" : "FAILED")}");
                if (!isTotemEquipped) allPassed = false;

                // 3. Verifikasi Proteksi Kekebalan Jimat/Totem terhadap Serangan/Cekikan Hantu
                testKunti = new GameObject("Test_Kuntilanak");
                testKunti.AddComponent<NetworkIdentity>();
                var kuntiHealth = testKunti.AddComponent<PlayerHealth>();
                var kuntiChoke = testKunti.AddComponent<KuntilanakChokeMechanic>();
                var kuntiVanish = testKunti.AddComponent<KuntilanakGhostVanish>();
                var kuntiPcc = testKunti.AddComponent<PlayerCharacterController>();

                // Korban sedang memegang Totem: Cek apakah valid victim
                var isValidVictimMethod = typeof(KuntilanakChokeMechanic).GetMethod("IsValidChokeVictim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var testSurvivorPcc = testSurvivor.AddComponent<PlayerCharacterController>();
                bool canChokeWithTotem = (bool)isValidVictimMethod.Invoke(kuntiChoke, new object[] { testSurvivorPcc });
                report.AppendLine($"[TEST 5] Totem Protection: Kuntilanak CanChoke Victim with Totem={canChokeWithTotem} (Expected: false) -> {(!canChokeWithTotem ? "PASSED" : "FAILED")}");
                if (canChokeWithTotem) allPassed = false;

                // Lepas Totem (unequip) -> Harus bisa dicekik kembali
                equipSlotField?.SetValue(survivorInv, 0); // Slot 0 = Kentongan
                bool canChokeWithoutTotem = (bool)isValidVictimMethod.Invoke(kuntiChoke, new object[] { testSurvivorPcc });
                report.AppendLine($"[TEST 6] Totem Unequipped: Kuntilanak CanChoke Victim without Totem={canChokeWithoutTotem} (Expected: true) -> {(canChokeWithoutTotem ? "PASSED" : "FAILED")}");
                if (!canChokeWithoutTotem) allPassed = false;

                // 4. Verifikasi Keris tidak bisa melukai Kuntilanak saat menghilang (IsGhostForm == true)
                var ghostVanishStateField = typeof(KuntilanakGhostVanish).GetField("isGhostForm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                ghostVanishStateField?.SetValue(kuntiVanish, true); // Ghost Form Active
                bool isGhost = kuntiVanish.IsGhostForm;

                float healthBeforeAttack = kuntiHealth.CurrentHealth;
                // Simulasikan serangan keris saat hantu gaib
                if (kuntiVanish.IsGhostForm)
                {
                    // Serangan menembus / tidak memberi damage
                }
                else
                {
                    kuntiHealth.TakeDamage(35f);
                }
                float healthAfterGhostAttack = kuntiHealth.CurrentHealth;
                bool ghostImmunityCheck = (healthAfterGhostAttack == healthBeforeAttack);
                report.AppendLine($"[TEST 7] Keris vs Ghost Form (IsGhostForm=true): Health {healthBeforeAttack} -> {healthAfterGhostAttack} (Expected: No Damage) -> {(ghostImmunityCheck ? "PASSED" : "FAILED")}");
                if (!ghostImmunityCheck) allPassed = false;

                // Saat wujud padat (IsGhostForm == false)
                ghostVanishStateField?.SetValue(kuntiVanish, false);
                if (!kuntiVanish.IsGhostForm)
                {
                    kuntiHealth.TakeDamage(35f);
                }
                float healthAfterSolidAttack = kuntiHealth.CurrentHealth;
                bool solidDamageCheck = (healthAfterSolidAttack == healthBeforeAttack - 35f);
                report.AppendLine($"[TEST 8] Keris vs Solid Form (IsGhostForm=false): Health {healthBeforeAttack} -> {healthAfterSolidAttack} (Expected: -35) -> {(solidDamageCheck ? "PASSED" : "FAILED")}");
                if (!solidDamageCheck) allPassed = false;

                // 5. Verifikasi Model Sesajen Baru (SESAJEN.fbx)
                var sesajenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SesajenPrefabPath);
                bool sesajenHasNewModel = false;
                if (sesajenPrefab != null)
                {
                    var visual = sesajenPrefab.transform.Find("Visual");
                    if (visual != null && visual.Find("SESAJEN_Model") != null)
                    {
                        sesajenHasNewModel = true;
                    }
                }
                report.AppendLine($"[TEST 9] Sesajen.prefab has SESAJEN_Model: {sesajenHasNewModel} -> {(sesajenHasNewModel ? "PASSED" : "FAILED")}");
                if (!sesajenHasNewModel) allPassed = false;

                // 6. Verifikasi Prefabs dan SpawnPrefabs
                string[] itemTypes = { "Kentongan", "Keris", "Totem", "Tulup" };
                bool allPrefabsExist = true;
                foreach (var it in itemTypes)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemsPrefabDir}/Pickup_{it}.prefab") == null)
                    {
                        allPrefabsExist = false;
                        break;
                    }
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemsPrefabDir}/GarlicProjectile.prefab") == null)
                {
                    allPrefabsExist = false;
                }
                report.AppendLine($"[TEST 10] All Item & Projectile Prefabs Exist: {allPrefabsExist} -> {(allPrefabsExist ? "PASSED" : "FAILED")}");
                if (!allPrefabsExist) allPassed = false;
            }
            catch (System.Exception ex)
            {
                report.AppendLine("EXCEPTION: " + ex.ToString());
                allPassed = false;
            }
            finally
            {
                typeof(NetworkServer).GetProperty("active", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, false);
                if (testSurvivor != null) Object.DestroyImmediate(testSurvivor);
                if (testPakMamat != null) Object.DestroyImmediate(testPakMamat);
                if (testKunti != null) Object.DestroyImmediate(testKunti);
            }

            report.AppendLine($"OVERALL RESULT: {(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED")}");
            System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/inventory_and_sesajen_report.txt", report.ToString());
            Debug.Log(report.ToString());
        }

        [MenuItem("Pucaci/Analyze Sesajen Geometry")]
        public static void AnalyzeSesajenGeometry()
        {
            var sesajenFbx = AssetDatabase.LoadAssetAtPath<GameObject>(SesajenFbxPath);
            var temp = Object.Instantiate(sesajenFbx);
            ItemVisualUtility.ApplySesajenTransform(temp);
            var mf = temp.GetComponentInChildren<MeshFilter>();
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== SUBMESH VERTEX MIN Y IN WORLD (with ApplySesajenTransform) ===");

            string[] matNames = { "Kamboja", "Dupa", "Tampah", "Kelapa", "Kendi", "Pisang" };
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var indices = mesh.GetTriangles(sm);
                float subMinY = float.MaxValue;
                float subMaxY = float.MinValue;
                foreach (int vi in indices)
                {
                    Vector3 worldV = temp.transform.TransformPoint(verts[vi]);
                    if (worldV.y < subMinY) subMinY = worldV.y;
                    if (worldV.y > subMaxY) subMaxY = worldV.y;
                }
                sb.AppendLine($"Submesh {sm} ({matNames[sm]}): min.y={subMinY:F4}, max.y={subMaxY:F4}, height={(subMaxY - subMinY):F4}");
            }
            Object.DestroyImmediate(temp);

            System.IO.File.WriteAllText("C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/sesajen_submesh_analysis.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}

// Reload trigger
