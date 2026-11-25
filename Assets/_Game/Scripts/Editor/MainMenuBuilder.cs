using System.Collections.Generic;
using Mirror;
using Pucaci.Core;
using Pucaci.Network;
using Pucaci.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pucaci.Editor
{
    public static class MainMenuBuilder
    {
        private static readonly Color ColorBg = new Color(11f / 255f, 15f / 255f, 26f / 255f, 0.85f); // #0B0F1A 85%
        private static readonly Color ColorText = new Color(242f / 255f, 232f / 255f, 213f / 255f, 1f); // #F2E8D5
        private static readonly Color ColorAccent = new Color(255f / 255f, 179f / 255f, 71f / 255f, 1f); // #FFB347
        private static readonly Color ColorDanger = new Color(192f / 255f, 57f / 255f, 43f / 255f, 1f); // #C0392B
        private static readonly Color ColorDisabled = new Color(85f / 255f, 85f / 255f, 85f / 255f, 1f); // #555555
        private static readonly Color ColorDarkProp = new Color(15f / 255f, 18f / 255f, 25f / 255f, 1f); // #0F1219

        [MenuItem("Tools/Pucaci/Capture Menu Screenshot")]
        public static void CaptureMenuScreenshot()
        {
            CaptureMenuScreenshot(null);
        }

        public static void CaptureMenuScreenshot(Camera targetCam)
        {
            var cam = targetCam != null ? targetCam : Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[MainMenuBuilder] Camera tidak ditemukan untuk capture!");
                return;
            }

            int width = 1280;
            int height = 720;
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string savePath = "C:/Users/MUSA/.gemini/antigravity/brain/3104c68e-3988-4937-956a-544f03a4f729/pos_ronda_larger_check.png";
            System.IO.File.WriteAllBytes(savePath, bytes);
            Debug.Log($"[MainMenuBuilder] Screenshot berhasil disimpan ke {savePath}");
        }

        [MenuItem("Tools/Pucaci/Build Main Menu Scene")]
        public static void BuildScene()
        {
            string scenePath = "Assets/_Game/Scenes/MainMenu.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Setup Camera & Ambience
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(4f / 255f, 7f / 255f, 13f / 255f, 1f);
            cam.fieldOfView = 40f; // Telephoto compression cinematic framing
            camObj.tag = "MainCamera";
            camObj.AddComponent<AudioListener>();
            camObj.transform.position = new Vector3(-0.05f, 1.32f, -2.85f);
            camObj.transform.rotation = Quaternion.Euler(1.0f, 5.5f, 0f);
            camObj.AddComponent<Pucaci.UI.MenuCameraDrift>();

            // Atmosphere audio (procedural night ambience + UI sfx)
            var atmosphere = camObj.AddComponent<Pucaci.UI.MenuAtmosphereAudio>();

            // Fog settings
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(4f / 255f, 7f / 255f, 13f / 255f, 1f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.048f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.06f, 0.10f, 1f);

            // 2. Setup 3D Horror Diorama (Pos Ronda, Lampu Jalan, Sesajen, Kuntilanak)
            Build3DBackground();

            // 2b. Post-processing volume
            BuildPostProcessing();

            // 3. Setup SettingsManager
            GameObject settingsMgrObj = new GameObject("SettingsManager");
            SettingsManager settingsMgr = settingsMgrObj.AddComponent<SettingsManager>();
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Game/Audio/PucaciMixer.mixer");
            if (mixer != null)
            {
                settingsMgr.SetAudioMixer(mixer);
            }

            // 4. Setup EventSystem with InputSystemUIInputModule
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // 5. Setup Canvas
            GameObject canvasObj = new GameObject("MainMenuCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Main Menu Controller on Canvas
            MainMenuController controller = canvasObj.AddComponent<MainMenuController>();
            var launcher = canvasObj.AddComponent<PucaciMultiplayerLauncher>();

            // Setup NetworkManager persisten di MainMenu
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/PlayerCharacter.prefab");
            GameObject netManagerObj = new GameObject("NetworkManager");
            var kcp = netManagerObj.AddComponent<kcp2k.KcpTransport>();
            kcp.Port = 7777;

            var pucaciNet = netManagerObj.AddComponent<PucaciNetworkManager>();
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

            // 6. UI readability gradient (behind panels)
            BuildLeftVignette(canvasObj.transform);

            // 7. Build Title & Header
            BuildTitleAndHeader(canvasObj.transform);

            // 7. Build Panels
            MenuPanel mainPanel = BuildMainPanel(canvasObj.transform, controller);
            MenuPanel charSelectPanel = BuildCharacterSelectPanel(canvasObj.transform, controller);
            MenuPanel mabarPanel = BuildMabarPanel(canvasObj.transform, controller);
            MenuPanel caraBermainPanel = BuildCaraBermainPanel(canvasObj.transform, controller);
            MenuPanel settingsPanel = BuildSettingsPanel(canvasObj.transform, controller);
            MenuPanel creditsPanel = BuildCreditsPanel(canvasObj.transform, controller);
            MenuPanel quitDialog = BuildQuitDialog(canvasObj.transform, controller);

            // 8. Connect Version Text from Bottom HUD
            var verTMP = canvasObj.transform.Find("VersionCenter")?.GetComponent<TMP_Text>();

            // 9. Setup SceneFader (Full screen overlay)
            BuildSceneFader(canvasObj.transform);

            // Connect Controller Serialized Fields
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            so.FindProperty("characterSelectPanel").objectReferenceValue = charSelectPanel;
            so.FindProperty("mabarPanel").objectReferenceValue = mabarPanel;
            so.FindProperty("caraBermainPanel").objectReferenceValue = caraBermainPanel;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("creditsPanel").objectReferenceValue = creditsPanel;
            so.FindProperty("quitConfirmDialog").objectReferenceValue = quitDialog;
            so.FindProperty("gameSceneName").stringValue = "Gameplay";
            so.FindProperty("multiplayerLauncherComponent").objectReferenceValue = launcher;
            if (verTMP != null) so.FindProperty("versionText").objectReferenceValue = verTMP;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 10. Update Build Settings (MainMenu at 0, PrototypeScene at 1)
            UpdateBuildSettings(scenePath);

            // 11. Save Scene
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CaptureMenuScreenshot(cam);

            Debug.Log($"[MainMenuBuilder] Main Menu Scene berhasil dibangun dan disimpan ke {scenePath}!");
        }

        private static void Build3DBackground()
        {
            GameObject bgParent = new GameObject("Environment_Background");

            // --- Materials ---
            Material groundMat = CreateOrUpdateLitMaterial(
                "Assets/_Game/Materials/M_MainMenu_Ground.mat",
                new Color(0.10f, 0.09f, 0.075f, 1f),
                "Assets/_Game/Models/Environment/Textures/ground.png",
                new Vector2(8f, 8f));

            Material silhouetteMat = CreateOrUpdateLitMaterial(
                "Assets/_Game/Materials/M_MainMenu_Silhouette.mat",
                new Color(0.035f, 0.04f, 0.055f, 1f),
                null, Vector2.one);

            // --- Ground ---
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(bgParent.transform);
            ground.transform.position = new Vector3(0f, 0f, 2f);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            // --- Pos Ronda (arsitektur kayu di belakang hero) ---
            GameObject posPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Object/POS RONDA.fbx");
            if (posPrefab != null)
            {
                var pos = (GameObject)PrefabUtility.InstantiatePrefab(posPrefab);
                pos.name = "PosRonda";
                pos.transform.SetParent(bgParent.transform, false);
                pos.transform.position = new Vector3(3.6f, 0f, 2.7f);
                pos.transform.rotation = Quaternion.Euler(0f, 205f, 0f);
                pos.transform.localScale = Vector3.one * 1.65f;
                StripColliders(pos);

                // Assign authentic textured wood material
                var posMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/pos.mat");
                if (posMat != null)
                {
                    foreach (var mr in pos.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        mr.sharedMaterial = posMat;
                    }
                }

                // Lentera minyak hangat di dalam Pos Ronda
                GameObject lanternObj = new GameObject("PosRondaLantern");
                lanternObj.transform.SetParent(pos.transform, false);
                lanternObj.transform.localPosition = new Vector3(-0.1f, 1.4f, 0.2f);
                var lanternLight = lanternObj.AddComponent<Light>();
                lanternLight.type = LightType.Point;
                lanternLight.color = new Color(1f, 0.55f, 0.15f);
                lanternLight.range = 7.5f;
                lanternLight.intensity = 2.8f;
                lanternLight.shadows = LightShadows.Soft;
                lanternObj.AddComponent<LampFlicker>();
            }

            // --- Hero Character: Pak Vincent (Survivor / Paranormal Investigator) ---
            GameObject vincentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Character/Pak Vincent/Idle.fbx");
            if (vincentPrefab != null)
            {
                var vincent = (GameObject)PrefabUtility.InstantiatePrefab(vincentPrefab);
                vincent.name = "Hero_Vincent";
                vincent.transform.SetParent(bgParent.transform, false);
                vincent.transform.position = new Vector3(2.05f, -0.08f, -0.22f);
                vincent.transform.rotation = Quaternion.Euler(0f, 196f, 0f);
                vincent.transform.localScale = Vector3.one * 1.20f;
                StripColliders(vincent);

                // Zero out the FBX local position offsets on Armature and Plane.001
                Transform arm = vincent.transform.Find("Armature");
                if (arm != null) arm.localPosition = Vector3.zero;
                Transform plane = vincent.transform.Find("Plane.001");
                if (plane != null) plane.localPosition = Vector3.zero;

                // Idle animation
                var animator = vincent.GetComponent<Animator>() ?? vincent.AddComponent<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Game/Models/Character/Pak Vincent/Vincent.controller");
                if (ctrl != null) animator.runtimeAnimatorController = ctrl;

                // Attach flashlight to right hand
                Transform rightHand = vincent.transform.Find("Armature/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand");
                if (rightHand != null)
                {
                    GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/PlayerCharacter.prefab");
                    if (playerPrefab != null)
                    {
                        Transform pfFlashlight = playerPrefab.transform.Find("Model_PakVincent/Armature/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand/Flashlight_Item");
                        if (pfFlashlight != null)
                        {
                            var flashlightItem = Object.Instantiate(pfFlashlight.gameObject, rightHand);
                            flashlightItem.name = "Flashlight_Item";
                            flashlightItem.transform.localPosition = Vector3.zero;
                            flashlightItem.transform.localRotation = Quaternion.Euler(315f, 130f, 15f);
                            flashlightItem.transform.localScale = Vector3.one * 0.01f;

                            // Tune the existing Flashlight_Beam spot so it aims forward-down into the ground instead of backward into background
                            Transform flBeam = flashlightItem.transform.Find("Flashlight_Beam");
                            if (flBeam != null)
                            {
                                var spot = flBeam.GetComponent<Light>();
                                if (spot != null)
                                {
                                    spot.intensity = 5.5f;
                                    spot.range = 7.0f;
                                    spot.spotAngle = 45f;
                                    spot.innerSpotAngle = 20f;
                                    spot.color = new Color(1f, 0.96f, 0.88f);
                                    flBeam.rotation = Quaternion.Euler(60f, 20f, 0f);
                                }
                            }
                        }
                    }
                }

                // Front fill / bounce light to give subtle definition to Vincent's jacket & face
                GameObject fillObj = new GameObject("VincentFrontFillLight");
                fillObj.transform.SetParent(bgParent.transform, false);
                fillObj.transform.position = new Vector3(1.0f, 1.40f, -1.9f);
                var fillLight = fillObj.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.color = new Color(0.35f, 0.45f, 0.60f);
                fillLight.range = 4.5f;
                fillLight.intensity = 0.95f;
                fillLight.shadows = LightShadows.None;

                // Rim light teal / cyan di bahu & punggung karakter (seperti di contoh UI)
                GameObject tealRimObj = new GameObject("VincentTealRimLight");
                tealRimObj.transform.SetParent(bgParent.transform, false);
                tealRimObj.transform.position = new Vector3(0.65f, 1.65f, 0.25f);
                var tealLight = tealRimObj.AddComponent<Light>();
                tealLight.type = LightType.Point;
                tealLight.color = new Color(0.0f, 0.95f, 1.0f); // Intense Eerie Cyan/Teal
                tealLight.range = 3.8f;
                tealLight.intensity = 6.2f;
                tealLight.shadows = LightShadows.Soft;

                // Rim light crimson red di sisi kanan karakter (seperti di contoh UI)
                GameObject redRimObj = new GameObject("VincentRedRimLight");
                redRimObj.transform.SetParent(bgParent.transform, false);
                redRimObj.transform.position = new Vector3(1.85f, 1.45f, -0.50f);
                var redLight = redRimObj.AddComponent<Light>();
                redLight.type = LightType.Point;
                redLight.color = new Color(1.0f, 0.02f, 0.08f); // Deep Saturated Crimson Red
                redLight.range = 3.8f;
                redLight.intensity = 6.8f;
                redLight.shadows = LightShadows.Soft;
            }

            // --- Kuntilanak di latar belakang kiri (menyeramkan melayang halus lebih jauh di kedalaman background) ---
            GameObject kuntiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Hantu/Kuntilanak/kunti.fbx");
            if (kuntiPrefab != null)
            {
                var kunti = (GameObject)PrefabUtility.InstantiatePrefab(kuntiPrefab);
                kunti.name = "Kuntilanak_Silhouette";
                kunti.transform.SetParent(bgParent.transform, false);
                kunti.transform.position = new Vector3(-2.6f, -0.65f, 14.5f);
                kunti.transform.rotation = Quaternion.Euler(0f, 170f, 0f);
                kunti.transform.localScale = Vector3.one * 0.95f;
                StripColliders(kunti);

                var anim = kunti.GetComponent<Animator>();
                if (anim == null) anim = kunti.AddComponent<Animator>();
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Game/Models/Hantu/Kuntilanak/Kuntilanak.controller");
                if (ctrl != null && anim != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                }

                var smr = kunti.GetComponentInChildren<SkinnedMeshRenderer>();
                var kuntiMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Models/Hantu/Kuntilanak/M_Kuntilanak.mat");
                if (smr != null && kuntiMat != null)
                {
                    smr.sharedMaterial = kuntiMat;
                }

                kunti.AddComponent<Pucaci.UI.MenuGhostFloating>();

                // Cahaya lembut dingin di area wajah Kuntilanak agar tampak menyeramkan di kedalaman kegelapan
                GameObject faceObj = new GameObject("KuntiFaceLight");
                faceObj.transform.SetParent(kunti.transform, false);
                faceObj.transform.localPosition = new Vector3(0f, 0.25f, -0.30f);
                var faceLight = faceObj.AddComponent<Light>();
                faceLight.type = LightType.Point;
                faceLight.color = new Color(0.65f, 0.82f, 1.0f);
                faceLight.range = 2.0f;
                faceLight.intensity = 0.70f;
                faceLight.shadows = LightShadows.None;

                // Rim light dingin halus di belakang kepala Kuntilanak
                GameObject rimObj = new GameObject("KuntiRimLight");
                rimObj.transform.SetParent(kunti.transform, false);
                rimObj.transform.localPosition = new Vector3(0f, 0.35f, 0.40f);
                var rimLight = rimObj.AddComponent<Light>();
                rimLight.type = LightType.Point;
                rimLight.color = new Color(0.40f, 0.70f, 1.0f);
                rimLight.range = 2.2f;
                rimLight.intensity = 0.85f;
                rimLight.shadows = LightShadows.None;
            }

            // --- Sesajen di depan (focal point dekat kaki) ---
            GameObject sesajenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Props/Sesajen.prefab");
            if (sesajenPrefab != null)
            {
                var sesajen = (GameObject)PrefabUtility.InstantiatePrefab(sesajenPrefab);
                sesajen.name = "Sesajen_Menu";
                sesajen.transform.SetParent(bgParent.transform, false);
                sesajen.transform.position = new Vector3(1.55f, 0f, -0.85f);
                sesajen.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
                sesajen.transform.localScale = Vector3.one * 1.0f;
                StripNetworkAndColliders(sesajen);

                var softMat = CreateParticleMaterial("Assets/_Game/Materials/M_MainMenu_Particle.mat", new Color(1f, 0.9f, 0.75f, 0.55f));
                foreach (var psr in sesajen.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    psr.material = softMat;
                }
                foreach (var ps in sesajen.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.gameObject.SetActive(true);
                    ps.Play(true);
                }
                foreach (var l in sesajen.GetComponentsInChildren<Light>(true))
                {
                    l.enabled = true;
                }
            }

            // --- Pagar bambu di latar belakang kiri (diturunkan agar tidak memotong teks) ---
            BuildBambooFence(bgParent.transform, new Vector3(-7.5f, -1.2f, 8.5f), 4.5f, silhouetteMat);

            // --- Kabut tanah & kunang-kunang ---
            BuildGroundMist(bgParent.transform);
            BuildFireflies(bgParent.transform);
        }

        private static void StripColliders(GameObject root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
        }

        private static void StripNetworkAndColliders(GameObject root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                if (mb is Mirror.NetworkBehaviour || mb is Mirror.NetworkIdentity)
                {
                    Object.DestroyImmediate(mb);
                }
            }
            StripColliders(root);
        }

        private static void DarkenRenderers(GameObject root, Color color)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = r.sharedMaterials[i];
                    if (src == null) continue;
                    var clone = new Material(src);
                    if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", color);
                    if (clone.HasProperty("_Color")) clone.SetColor("_Color", color);
                    clone.color = color;
                    mats[i] = clone;
                }
                r.sharedMaterials = mats;
            }
        }

        private static Material CreateOrUpdateLitMaterial(string path, Color baseColor, string texturePath, Vector2 tiling)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (!string.IsNullOrEmpty(texturePath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                    mat.SetTextureScale("_BaseMap", tiling);
                    mat.SetTextureScale("_MainTex", tiling);
                }
            }
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildSilhouetteTree(Transform parent, Vector3 pos, float scale, Material mat)
        {
            GameObject tree = new GameObject("Tree_Silhouette");
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;
            tree.transform.localScale = Vector3.one * scale;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            trunk.transform.localScale = new Vector3(0.22f, 2.2f, 0.22f);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(trunk.GetComponent<Collider>());

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(tree.transform, false);
            crown.transform.localPosition = new Vector3(0f, 5.0f, 0f);
            crown.transform.localScale = new Vector3(3.2f, 3.4f, 3.2f);
            crown.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(crown.GetComponent<Collider>());

            GameObject crown2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown2.name = "Crown2";
            crown2.transform.SetParent(tree.transform, false);
            crown2.transform.localPosition = new Vector3(1.1f, 4.2f, 0.4f);
            crown2.transform.localScale = new Vector3(2.0f, 2.2f, 2.0f);
            crown2.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(crown2.GetComponent<Collider>());
        }

        private static void BuildBambooFence(Transform parent, Vector3 pos, float length, Material mat)
        {
            GameObject fence = new GameObject("PagarBambu");
            fence.transform.SetParent(parent, false);
            fence.transform.position = pos;

            int segments = Mathf.CeilToInt(length / 0.45f);
            for (int i = 0; i < segments; i++)
            {
                var slat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                slat.name = $"Bambu_{i}";
                slat.transform.SetParent(fence.transform, false);
                slat.transform.localPosition = new Vector3(i * 0.45f, 0.7f, 0f);
                slat.transform.localScale = new Vector3(0.045f, 0.7f, 0.045f);
                slat.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f));
                slat.GetComponent<MeshRenderer>().sharedMaterial = mat;
                Object.DestroyImmediate(slat.GetComponent<Collider>());
            }

            // Palang horizontal
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rail.name = "Palang";
            rail.transform.SetParent(fence.transform, false);
            rail.transform.localPosition = new Vector3(length * 0.5f, 1.15f, 0f);
            rail.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rail.transform.localScale = new Vector3(0.04f, length * 0.5f, 0.04f);
            rail.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(rail.GetComponent<Collider>());
        }

        private static void BuildGroundMist(Transform parent)
        {
            GameObject mistObj = new GameObject("GroundMist");
            mistObj.transform.SetParent(parent, false);
            mistObj.transform.position = new Vector3(0f, 0.35f, 10f);

            var ps = mistObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 8f;
            main.loop = true;
            main.startLifetime = 14f;
            main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.62f, 0.75f, 0.10f),
                new Color(0.45f, 0.52f, 0.65f, 0.05f));
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 2.2f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 0.5f, 22f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            vel.y = new ParticleSystem.MinMaxCurve(0.0f, 0.0f);
            vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var renderer = mistObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial("Assets/_Game/Materials/M_MainMenu_Mist.mat", new Color(1f, 1f, 1f, 0.45f));
            ps.Play();
        }

        private static void BuildFireflies(Transform parent)
        {
            GameObject ffObj = new GameObject("Fireflies_Embers");
            ffObj.transform.SetParent(parent, false);
            ffObj.transform.position = new Vector3(0f, 1.6f, 8f);

            var ps = ffObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 6f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSpeed = 0.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.72f, 0.28f, 0.9f),
                new Color(1f, 0.9f, 0.55f, 0.75f));
            main.maxParticles = 70;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 4.5f, 16f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.12f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var renderer = ffObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial("Assets/_Game/Materials/M_MainMenu_Firefly.mat", Color.white);
            ps.Play();
        }

        private static Material CreateParticleMaterial(string path, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            // Paksa shader alpha-blended (material lama mungkin memakai shader opaque)
            if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            var softTex = GetOrCreateSoftCircleTexture();
            if (softTex != null)
            {
                mat.mainTexture = softTex;
                mat.SetTexture("_MainTex", softTex);
            }
            mat.SetColor("_Color", color);
            mat.color = color;
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D GetOrCreateSoftCircleTexture()
        {
            string path = "Assets/_Game/Textures/UI/UI_SoftCircle.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BuildPostProcessing()
        {
            GameObject volObj = new GameObject("GlobalVolume_MainMenu");
            var volume = volObj.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;

            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, "Assets/_Game/Materials/MainMenuPostProcessProfile.asset");

            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.intensity.Override(1.65f);
            bloom.threshold.Override(0.80f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.82f));

            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.46f);
            vignette.smoothness.Override(0.58f);
            vignette.color.Override(new Color(0.01f, 0.015f, 0.03f));

            var grain = profile.Add<UnityEngine.Rendering.Universal.FilmGrain>(true);
            grain.type.Override(UnityEngine.Rendering.Universal.FilmGrainLookup.Medium2);
            grain.intensity.Override(0.24f);
            grain.response.Override(0.8f);

            var colorAdj = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.15f);
            colorAdj.contrast.Override(28f);
            colorAdj.saturation.Override(8f);
            colorAdj.colorFilter.Override(new Color(0.96f, 0.98f, 1.05f));

            AssetDatabase.SaveAssets();
            volume.sharedProfile = profile;
        }

        private static void BuildLeftVignette(Transform canvas)
        {
            GameObject gradObj = new GameObject("UI_LeftReadabilityGradient", typeof(RectTransform), typeof(Image));
            gradObj.transform.SetParent(canvas, false);
            gradObj.transform.SetAsFirstSibling();

            RectTransform rt = gradObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.56f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = gradObj.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = CreateHorizontalFadeSprite();
            img.type = Image.Type.Simple;
            img.color = new Color(0.008f, 0.010f, 0.018f, 0.96f);
        }

        private static Sprite CreateHorizontalFadeSprite()
        {
            const int width = 256;
            const int height = 4;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < width; x++)
            {
                float t = (float)x / (width - 1);
                // Gelap pekat di kiri tempat UI, memudar halus menuju karakter di kanan
                float alpha = Mathf.Clamp01(1f - Mathf.Pow(t, 1.25f));
                for (int y = 0; y < height; y++)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            string path = "Assets/_Game/Textures/UI/UI_HorizontalFade.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildTitleAndHeader(Transform canvas)
        {
            GameObject headerObj = new GameObject("TitleHeader", typeof(RectTransform));
            headerObj.transform.SetParent(canvas, false);
            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(0f, 1f);
            headerRect.pivot = new Vector2(0f, 1f);
            headerRect.anchoredPosition = new Vector2(96f, -90f);
            headerRect.sizeDelta = new Vector2(760f, 230f);

            // Eyebrow / kicker
            GameObject kickerObj = CreateTMPText(headerObj.transform, "Kicker", "HOROR KAMPUNG  •  1—4 PEMAIN", 15, TextAlignmentOptions.Left);
            var kicker = kickerObj.GetComponent<TMP_Text>();
            kicker.color = new Color(0.65f, 0.70f, 0.78f, 0.85f);
            kicker.characterSpacing = 14f;
            kicker.fontStyle = FontStyles.Bold;
            RectTransform kickerRect = kickerObj.GetComponent<RectTransform>();
            kickerRect.anchorMin = new Vector2(0f, 1f);
            kickerRect.anchorMax = new Vector2(1f, 1f);
            kickerRect.pivot = new Vector2(0f, 1f);
            kickerRect.anchoredPosition = new Vector2(0f, 0f);
            kickerRect.sizeDelta = new Vector2(0f, 22f);

            // Title: PUCACI (Bone white horror display)
            GameObject titleObj = CreateTMPText(headerObj.transform, "TitleText", "PUCACI", 112, TextAlignmentOptions.Left);
            TMP_Text titleTMP = titleObj.GetComponent<TMP_Text>();
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = new Color(0.96f, 0.97f, 0.98f, 1f); // Bone White
            titleTMP.characterSpacing = 8f;
            titleObj.AddComponent<TitleFlicker>();

            var shadow = titleObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(4f, -4f);

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -22f);
            titleRect.sizeDelta = new Vector2(0f, 120f);

            // Underline 1: Crimson Red Dry-Brush Stroke
            GameObject redLineObj = new GameObject("BrushLine_Red", typeof(RectTransform), typeof(Image));
            redLineObj.transform.SetParent(headerObj.transform, false);
            var redImg = redLineObj.GetComponent<Image>();
            redImg.sprite = MenuHorrorArtGenerator.GetBrushUnderlineSprite();
            redImg.color = new Color(0.85f, 0.08f, 0.14f, 0.88f); // Crimson Red
            redImg.raycastTarget = false;
            RectTransform redLineRect = redLineObj.GetComponent<RectTransform>();
            redLineRect.anchorMin = new Vector2(0f, 1f);
            redLineRect.anchorMax = new Vector2(0f, 1f);
            redLineRect.pivot = new Vector2(0f, 1f);
            redLineRect.anchoredPosition = new Vector2(0f, -148f);
            redLineRect.sizeDelta = new Vector2(300f, 9f);

            // Underline 2: Chalk White Offset Dry-Brush Stroke
            GameObject whiteLineObj = new GameObject("BrushLine_White", typeof(RectTransform), typeof(Image));
            whiteLineObj.transform.SetParent(headerObj.transform, false);
            var whiteImg = whiteLineObj.GetComponent<Image>();
            whiteImg.sprite = MenuHorrorArtGenerator.GetBrushUnderlineSprite();
            whiteImg.color = new Color(0.92f, 0.94f, 0.97f, 0.55f); // Chalk White
            whiteImg.raycastTarget = false;
            RectTransform whiteLineRect = whiteLineObj.GetComponent<RectTransform>();
            whiteLineRect.anchorMin = new Vector2(0f, 1f);
            whiteLineRect.anchorMax = new Vector2(0f, 1f);
            whiteLineRect.pivot = new Vector2(0f, 1f);
            whiteLineRect.anchoredPosition = new Vector2(25f, -156f);
            whiteLineRect.sizeDelta = new Vector2(220f, 6f);

            // Subtitle
            GameObject subObj = CreateTMPText(headerObj.transform, "SubtitleText", "Ronda malam, jangan berisik.\nSesuatu ikut berjalan di belakangmu.", 18, TextAlignmentOptions.TopLeft);
            TMP_Text subTMP = subObj.GetComponent<TMP_Text>();
            subTMP.color = new Color(0.72f, 0.76f, 0.84f, 0.88f);
            subTMP.lineSpacing = 8f;
            subTMP.characterSpacing = 2f;
            RectTransform subRect = subObj.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -174f);
            subRect.sizeDelta = new Vector2(0f, 60f);
        }

        private static MenuPanel BuildMainPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = new GameObject("MainPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelObj.transform.SetParent(canvas, false);
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(96f, 105f);
            panelRect.sizeDelta = new Vector2(380f, 370f);

            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);

            Button btnPlay = CreateMenuActionButton(panelObj.transform, "BtnMulai", "PLAY STORY", true);
            Button btnMabar = CreateMenuActionButton(panelObj.transform, "BtnMabar", "MULTIPLAYER");
            Button btnCara = CreateMenuActionButton(panelObj.transform, "BtnCaraBermain", "HELP & GUIDE");
            Button btnSettings = CreateMenuActionButton(panelObj.transform, "BtnPengaturan", "SETTINGS");
            Button btnCredits = CreateMenuActionButton(panelObj.transform, "BtnKredit", "CREDITS");
            Button btnQuit = CreateMenuActionButton(panelObj.transform, "BtnKeluar", "EXIT");

            // --- Bottom HUD Bar (Gaya The Infected Soul) ---
            // Studio Logo & Watermark (Kiri Bawah)
            GameObject studioObj = new GameObject("StudioWatermark", typeof(RectTransform));
            studioObj.transform.SetParent(canvas, false);
            RectTransform studioRect = studioObj.GetComponent<RectTransform>();
            studioRect.anchorMin = new Vector2(0f, 0f);
            studioRect.anchorMax = new Vector2(0f, 0f);
            studioRect.pivot = new Vector2(0f, 0f);
            studioRect.anchoredPosition = new Vector2(96f, 32f);
            studioRect.sizeDelta = new Vector2(240f, 36f);

            GameObject logoObj = new GameObject("LogoIcon", typeof(RectTransform), typeof(Image));
            logoObj.transform.SetParent(studioObj.transform, false);
            var logoImg = logoObj.GetComponent<Image>();
            logoImg.sprite = MenuHorrorArtGenerator.GetStudioLogoSprite();
            logoImg.color = new Color(0.65f, 0.70f, 0.78f, 0.75f);
            logoImg.raycastTarget = false;
            RectTransform logoRect = logoObj.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0f, 0.5f);
            logoRect.anchorMax = new Vector2(0f, 0.5f);
            logoRect.pivot = new Vector2(0f, 0.5f);
            logoRect.anchoredPosition = new Vector2(16f, 0f);
            logoRect.sizeDelta = new Vector2(30f, 30f);

            GameObject studioTextObj = CreateTMPText(studioObj.transform, "StudioName", "PUCACI INDIE GAMES", 13, TextAlignmentOptions.MidlineLeft);
            var studioTmp = studioTextObj.GetComponent<TMP_Text>();
            studioTmp.color = new Color(0.55f, 0.60f, 0.68f, 0.7f);
            studioTmp.characterSpacing = 6f;
            studioTmp.fontStyle = FontStyles.Bold;
            RectTransform stRect = studioTextObj.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 0.5f);
            stRect.anchorMax = new Vector2(1f, 0.5f);
            stRect.pivot = new Vector2(0f, 0.5f);
            stRect.anchoredPosition = new Vector2(48f, 0f);
            stRect.sizeDelta = new Vector2(200f, 24f);

            // Version Tag (Tengah Bawah)
            GameObject verObj = CreateTMPText(canvas, "VersionCenter", "Pre-Alpha v1.0", 14, TextAlignmentOptions.Center);
            var verTmp = verObj.GetComponent<TMP_Text>();
            verTmp.color = new Color(0.48f, 0.52f, 0.60f, 0.65f);
            verTmp.characterSpacing = 4f;
            RectTransform verRect = verObj.GetComponent<RectTransform>();
            verRect.anchorMin = new Vector2(0.5f, 0f);
            verRect.anchorMax = new Vector2(0.5f, 0f);
            verRect.pivot = new Vector2(0.5f, 0f);
            verRect.anchoredPosition = new Vector2(0f, 36f);
            verRect.sizeDelta = new Vector2(300f, 24f);

            // Exit Hotkey Prompt (Kanan Bawah)
            GameObject exitPromptObj = CreateTMPText(canvas, "ExitPrompt", "[ESC] EXIT", 16, TextAlignmentOptions.Right);
            var epTmp = exitPromptObj.GetComponent<TMP_Text>();
            epTmp.color = new Color(0.68f, 0.74f, 0.82f, 0.85f);
            epTmp.characterSpacing = 6f;
            epTmp.fontStyle = FontStyles.Bold;
            RectTransform epRect = exitPromptObj.GetComponent<RectTransform>();
            epRect.anchorMin = new Vector2(1f, 0f);
            epRect.anchorMax = new Vector2(1f, 0f);
            epRect.pivot = new Vector2(1f, 0f);
            epRect.anchoredPosition = new Vector2(-96f, 36f);
            epRect.sizeDelta = new Vector2(200f, 24f);

            // Connect Serialized properties on controller
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = btnPlay;
            so.FindProperty("mabarButton").objectReferenceValue = btnMabar;
            so.FindProperty("caraBermainButton").objectReferenceValue = btnCara;
            so.FindProperty("settingsButton").objectReferenceValue = btnSettings;
            so.FindProperty("creditsButton").objectReferenceValue = btnCredits;
            so.FindProperty("quitButton").objectReferenceValue = btnQuit;
            so.ApplyModifiedPropertiesWithoutUndo();

            // First selected
            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnPlay;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildCharacterSelectPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "CharacterSelectPanel", new Vector2(1300f, 640f), new Vector2(300f, -10f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(30, 30, 25, 25);

            CreateTMPText(panelObj.transform, "Header", "PILIH KARAKTER", 30, TextAlignmentOptions.Center);
            CreateTMPText(panelObj.transform, "Subtitle", "Pilih siapa yang ingin kamu mainkan dalam ronda malam ini:", 18, TextAlignmentOptions.Center);

            // Cards Row
            GameObject cardsRow = new GameObject("CardsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardsRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup cLayout = cardsRow.GetComponent<HorizontalLayoutGroup>();
            cLayout.spacing = 16f;
            cLayout.childControlWidth = true;
            cLayout.childControlHeight = true;
            cardsRow.GetComponent<RectTransform>().sizeDelta = new Vector2(1240f, 384f);

            // --- Card 1: Laras ---
            GameObject cardLaras = new GameObject("Card_Laras", typeof(RectTransform), typeof(Image));
            cardLaras.transform.SetParent(cardsRow.transform, false);
            Image larasBorder = cardLaras.GetComponent<Image>();
            larasBorder.color = ColorAccent;

            GameObject larasInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            larasInner.transform.SetParent(cardLaras.transform, false);
            RectTransform liRect = larasInner.GetComponent<RectTransform>();
            liRect.anchorMin = Vector2.zero;
            liRect.anchorMax = Vector2.one;
            liRect.offsetMin = new Vector2(4f, 4f);
            liRect.offsetMax = new Vector2(-4f, -4f);
            larasInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup liLayout = larasInner.GetComponent<VerticalLayoutGroup>();
            liLayout.spacing = 10f;
            liLayout.childAlignment = TextAnchor.UpperCenter;
            liLayout.childControlWidth = true;
            liLayout.childControlHeight = false;
            liLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(larasInner.transform, "Name", "LARAS", 30, TextAlignmentOptions.Center);
            var roleLaras = CreateTMPText(larasInner.transform, "Role", "[ MAHASISWI KKN ]", 16, TextAlignmentOptions.Center);
            roleLaras.GetComponent<TMP_Text>().color = new Color(0.5f, 0.8f, 1f, 1f);

            string larasDesc = "Gadis pemberani dan lincah. Memiliki reflek gesit untuk melarikan diri dari kejaran dedemit kampung.";
            CreateTMPText(larasInner.transform, "Desc", larasDesc, 16, TextAlignmentOptions.TopLeft);

            string larasTraits = "• Langkah Lincah & Cepat\n• Reflek Gerak Responsif\n• Pengamatan Senter Tajam";
            var traitLaras = CreateTMPText(larasInner.transform, "Traits", larasTraits, 15, TextAlignmentOptions.TopLeft);
            traitLaras.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectLaras = CreateButton(larasInner.transform, "BtnSelectLaras", "[V] TERPILIH", ColorAccent, ColorBg, 280f);
            TMP_Text larasBadge = btnSelectLaras.GetComponentInChildren<TMP_Text>();

            // --- Card 2: Pak Mamat ---
            GameObject cardMamat = new GameObject("Card_PakMamat", typeof(RectTransform), typeof(Image));
            cardMamat.transform.SetParent(cardsRow.transform, false);
            Image mamatBorder = cardMamat.GetComponent<Image>();
            mamatBorder.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            GameObject mamatInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            mamatInner.transform.SetParent(cardMamat.transform, false);
            RectTransform miRect = mamatInner.GetComponent<RectTransform>();
            miRect.anchorMin = Vector2.zero;
            miRect.anchorMax = Vector2.one;
            miRect.offsetMin = new Vector2(4f, 4f);
            miRect.offsetMax = new Vector2(-4f, -4f);
            mamatInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup miLayout = mamatInner.GetComponent<VerticalLayoutGroup>();
            miLayout.spacing = 10f;
            miLayout.childAlignment = TextAnchor.UpperCenter;
            miLayout.childControlWidth = true;
            miLayout.childControlHeight = false;
            miLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(mamatInner.transform, "Name", "PAK MAMAT", 30, TextAlignmentOptions.Center);
            var roleMamat = CreateTMPText(mamatInner.transform, "Role", "[ HANSIP KAMPUNG ]", 16, TextAlignmentOptions.Center);
            roleMamat.GetComponent<TMP_Text>().color = new Color(1f, 0.7f, 0.4f, 1f);

            string mamatDesc = "Penjaga ronda senior kampung. Kenyang pengalaman mistis dan memiliki langkah mantap penuh waspada.";
            CreateTMPText(mamatInner.transform, "Desc", mamatDesc, 16, TextAlignmentOptions.TopLeft);

            string mamatTraits = "• Langkah Mantap & Siaga\n• Pengalaman Ronda Malam\n• Gaya Gerak Pos Hansip";
            var traitMamat = CreateTMPText(mamatInner.transform, "Traits", mamatTraits, 15, TextAlignmentOptions.TopLeft);
            traitMamat.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectMamat = CreateButton(mamatInner.transform, "BtnSelectMamat", "PILIH PAK MAMAT", ColorText, ColorBg, 280f);
            TMP_Text mamatBadge = btnSelectMamat.GetComponentInChildren<TMP_Text>();

            // --- Card 3: Pak Vincent ---
            GameObject cardVincent = new GameObject("Card_PakVincent", typeof(RectTransform), typeof(Image));
            cardVincent.transform.SetParent(cardsRow.transform, false);
            Image vincentBorder = cardVincent.GetComponent<Image>();
            vincentBorder.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            GameObject vincentInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            vincentInner.transform.SetParent(cardVincent.transform, false);
            RectTransform viRect = vincentInner.GetComponent<RectTransform>();
            viRect.anchorMin = Vector2.zero;
            viRect.anchorMax = Vector2.one;
            viRect.offsetMin = new Vector2(4f, 4f);
            viRect.offsetMax = new Vector2(-4f, -4f);
            vincentInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup viLayout = vincentInner.GetComponent<VerticalLayoutGroup>();
            viLayout.spacing = 10f;
            viLayout.childAlignment = TextAnchor.UpperCenter;
            viLayout.childControlWidth = true;
            viLayout.childControlHeight = false;
            viLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(vincentInner.transform, "Name", "PAK VINCENT", 28, TextAlignmentOptions.Center);
            var roleVincent = CreateTMPText(vincentInner.transform, "Role", "[ PENYELIDIK DESA ]", 16, TextAlignmentOptions.Center);
            roleVincent.GetComponent<TMP_Text>().color = new Color(0.4f, 0.9f, 0.6f, 1f);

            string vincentDesc = "Pria tenang dan penuh perhitungan. Memiliki langkah terukur untuk menyelidiki misteri dan bertahan dari teror malam desa.";
            CreateTMPText(vincentInner.transform, "Desc", vincentDesc, 16, TextAlignmentOptions.TopLeft);

            string vincentTraits = "• Gerakan Taktis & Lincah\n• Pengamatan Waspada\n• Senter Taktis Handal";
            var traitVincent = CreateTMPText(vincentInner.transform, "Traits", vincentTraits, 15, TextAlignmentOptions.TopLeft);
            traitVincent.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectVincent = CreateButton(vincentInner.transform, "BtnSelectVincent", "PILIH PAK VINCENT", ColorText, ColorBg, 280f);
            TMP_Text vincentBadge = btnSelectVincent.GetComponentInChildren<TMP_Text>();

            // --- Card 4: Mas Rocky ---
            GameObject cardRocky = new GameObject("Card_MasRocky", typeof(RectTransform), typeof(Image));
            cardRocky.transform.SetParent(cardsRow.transform, false);
            Image rockyBorder = cardRocky.GetComponent<Image>();
            rockyBorder.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            GameObject rockyInner = new GameObject("Inner", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            rockyInner.transform.SetParent(cardRocky.transform, false);
            RectTransform rkRect = rockyInner.GetComponent<RectTransform>();
            rkRect.anchorMin = Vector2.zero;
            rkRect.anchorMax = Vector2.one;
            rkRect.offsetMin = new Vector2(4f, 4f);
            rkRect.offsetMax = new Vector2(-4f, -4f);
            rockyInner.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            VerticalLayoutGroup rkLayout = rockyInner.GetComponent<VerticalLayoutGroup>();
            rkLayout.spacing = 10f;
            rkLayout.childAlignment = TextAnchor.UpperCenter;
            rkLayout.childControlWidth = true;
            rkLayout.childControlHeight = false;
            rkLayout.padding = new RectOffset(20, 20, 20, 20);

            CreateTMPText(rockyInner.transform, "Name", "MAS ROCKY", 28, TextAlignmentOptions.Center);
            var roleRocky = CreateTMPText(rockyInner.transform, "Role", "[ PEMUDA DESA ]", 16, TextAlignmentOptions.Center);
            roleRocky.GetComponent<TMP_Text>().color = new Color(0.95f, 0.75f, 0.3f, 1f);

            string rockyDesc = "Pemuda desa berfisik prima dan tangguh. Siap berpatroli menjaga keamanan warga dari gangguan makhluk gaib malam hari.";
            CreateTMPText(rockyInner.transform, "Desc", rockyDesc, 16, TextAlignmentOptions.TopLeft);

            string rockyTraits = "• Fisik Prima & Berotot\n• Langkah Tegap & Waspada\n• Keberanian Pemuda Desa";
            var traitRocky = CreateTMPText(rockyInner.transform, "Traits", rockyTraits, 15, TextAlignmentOptions.TopLeft);
            traitRocky.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.9f);

            Button btnSelectRocky = CreateButton(rockyInner.transform, "BtnSelectRocky", "PILIH MAS ROCKY", ColorText, ColorBg, 280f);
            TMP_Text rockyBadge = btnSelectRocky.GetComponentInChildren<TMP_Text>();

            // Bottom Action Row
            GameObject bottomRow = new GameObject("BottomRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bottomRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup bLayout = bottomRow.GetComponent<HorizontalLayoutGroup>();
            bLayout.spacing = 20f;
            bLayout.childControlWidth = true;
            bLayout.childControlHeight = true;
            bottomRow.GetComponent<RectTransform>().sizeDelta = new Vector2(1240f, 50f);

            Button btnStartWithChar = CreateButton(bottomRow.transform, "BtnStartPlay", "MULAI BERMAIN", ColorAccent, ColorBg, 800f);
            Button btnCharBack = CreateButton(bottomRow.transform, "BtnBack", "Kembali", ColorText, ColorBg, 400f);

            // Connect Serialized properties on controller
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("selectLarasCardButton").objectReferenceValue = btnSelectLaras;
            so.FindProperty("selectMamatCardButton").objectReferenceValue = btnSelectMamat;
            so.FindProperty("selectVincentCardButton").objectReferenceValue = btnSelectVincent;
            so.FindProperty("selectRockyCardButton").objectReferenceValue = btnSelectRocky;
            so.FindProperty("larasCardBorder").objectReferenceValue = larasBorder;
            so.FindProperty("mamatCardBorder").objectReferenceValue = mamatBorder;
            so.FindProperty("vincentCardBorder").objectReferenceValue = vincentBorder;
            so.FindProperty("rockyCardBorder").objectReferenceValue = rockyBorder;
            so.FindProperty("larasStatusBadge").objectReferenceValue = larasBadge;
            so.FindProperty("mamatStatusBadge").objectReferenceValue = mamatBadge;
            so.FindProperty("vincentStatusBadge").objectReferenceValue = vincentBadge;
            so.FindProperty("rockyStatusBadge").objectReferenceValue = rockyBadge;
            so.FindProperty("startPlayWithCharacterButton").objectReferenceValue = btnStartWithChar;
            so.FindProperty("characterSelectBackButton").objectReferenceValue = btnCharBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            // First selected
            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnStartWithChar;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildMabarPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "MabarPanel", new Vector2(520f, 540f), new Vector2(330f, -20f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(30, 30, 20, 20);

            CreateTMPText(panelObj.transform, "Header", "MABAR (MULTIPLAYER)", 28, TextAlignmentOptions.Center);

            // Character Info Row in Mabar
            GameObject charRow = new GameObject("CharRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            charRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup chLayout = charRow.GetComponent<HorizontalLayoutGroup>();
            chLayout.spacing = 10f;
            chLayout.childControlWidth = false;
            chLayout.childControlHeight = true;
            chLayout.childForceExpandWidth = false;
            charRow.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 40f);

            GameObject charTextObj = CreateTMPText(charRow.transform, "MabarCharText", "Karakter Aktif: <b>Laras</b>", 15, TextAlignmentOptions.MidlineLeft, 340f);
            Button btnChangeChar = CreateButton(charRow.transform, "BtnChangeChar", "Ganti", ColorAccent, ColorBg, 110f);

            Button btnHost = CreateButton(panelObj.transform, "BtnHost", "Buat Ruangan (Host)", ColorAccent, ColorBg);

            // Join Section: Input Field + Button
            GameObject joinRow = new GameObject("JoinRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            joinRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup joinLayout = joinRow.GetComponent<HorizontalLayoutGroup>();
            joinLayout.spacing = 10f;
            joinLayout.childControlWidth = true;
            joinLayout.childControlHeight = true;
            joinRow.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 48f);

            TMP_InputField roomInput = CreateInputField(joinRow.transform, "RoomCodeInput", "Kode Ruangan...");
            Button btnJoin = CreateButton(joinRow.transform, "BtnJoin", "Gabung", ColorText, ColorBg, 120f);

            Button btnSolo = CreateButton(panelObj.transform, "BtnSolo", "Latihan Sendiri (Singleplayer)", ColorAccent, ColorBg);
            Button btnBack = CreateButton(panelObj.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            // Status Text
            GameObject statusObj = CreateTMPText(panelObj.transform, "MabarStatusText", "", 18, TextAlignmentOptions.Center);
            TMP_Text statusText = statusObj.GetComponent<TMP_Text>();
            statusText.color = ColorAccent;

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("hostButton").objectReferenceValue = btnHost;
            so.FindProperty("joinButton").objectReferenceValue = btnJoin;
            so.FindProperty("roomCodeInput").objectReferenceValue = roomInput;
            so.FindProperty("soloPracticeButton").objectReferenceValue = btnSolo;
            so.FindProperty("mabarBackButton").objectReferenceValue = btnBack;
            so.FindProperty("mabarStatusText").objectReferenceValue = statusText;
            so.FindProperty("mabarCharacterText").objectReferenceValue = charTextObj.GetComponent<TMP_Text>();
            so.FindProperty("mabarChangeCharacterButton").objectReferenceValue = btnChangeChar;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnHost;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildCaraBermainPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "CaraBermainPanel", new Vector2(650f, 600f), new Vector2(330f, -10f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(35, 35, 25, 25);

            CreateTMPText(panelObj.transform, "Header", "CARA BERMAIN", 28, TextAlignmentOptions.Center);

            string guideText =
                "<color=#FFB347><b>SURVIVOR</b></color>\n" +
                "• Hancurkan 5 sesajen di kampung sebelum waktu habis.\n" +
                "• Tahan <b>[E]</b> di depan sesajen untuk merusaknya.\n" +
                "• Jangan sendirian — hantu bisa menyerupai rekanmu.\n\n" +
                "<color=#FF6B5B><b>KUNTILANAK (IMPOSTOR)</b></color>\n" +
                "• Bunuh semua survivor tanpa ketahuan.\n" +
                "• <b>[1/2/3/T]</b> menyamar jadi manusia, <b>[F]</b> wujud gaib.\n" +
                "• <b>[M1/E]</b> cekik korban, <b>[Q]</b> sabotase lampu jalan.\n" +
                "• Dalam wujud manusia, kau bisa merusak / memperbaiki sesajen untuk mengelabui tim.\n\n" +
                "<color=#8FB3FF><b>TIPS</b></color>\n" +
                "• Lampu mati = hantu sedang dekat. Nyalakan kembali untuk bertahan.\n" +
                "• Suara keras menarik perhatian. Berbisiklah saat bersembunyi.";

            GameObject descObj = CreateTMPText(panelObj.transform, "DescText", guideText, 17, TextAlignmentOptions.TopLeft);
            descObj.GetComponent<RectTransform>().sizeDelta = new Vector2(580f, 400f);
            var descTmp = descObj.GetComponent<TMP_Text>();
            descTmp.lineSpacing = 6f;

            Button btnBack = CreateButton(panelObj.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("caraBermainBackButton").objectReferenceValue = btnBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnBack;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildSettingsPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "SettingsPanel", new Vector2(740f, 620f), new Vector2(330f, -10f));
            SettingsPanel panel = panelObj.AddComponent<SettingsPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(35, 35, 20, 20);

            CreateTMPText(panelObj.transform, "Header", "PENGATURAN", 28, TextAlignmentOptions.Center);

            // Audio Section
            Slider sldMaster = CreateSliderRow(panelObj.transform, "MasterVolumeRow", "Volume Utama", 0.8f);
            Slider sldMusic = CreateSliderRow(panelObj.transform, "MusicVolumeRow", "Musik", 0.8f);
            Slider sldSfx = CreateSliderRow(panelObj.transform, "SfxVolumeRow", "Efek Suara (SFX)", 0.8f);
            Slider sldVoice = CreateSliderRow(panelObj.transform, "VoiceVolumeRow", "Suara Pemain", 0.8f);

            // Microphone Section
            TMP_Dropdown micDrop = CreateDropdownRow(panelObj.transform, "MicDeviceRow", "Mikrofon");
            Slider sldMicSens = CreateSliderRow(panelObj.transform, "MicSensRow", "Sensitivitas Suara", 0.5f);

            // Mic Level Bar
            GameObject micMeterRow = new GameObject("MicMeterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            micMeterRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup mLayout = micMeterRow.GetComponent<HorizontalLayoutGroup>();
            mLayout.childControlWidth = false;
            mLayout.childControlHeight = false;
            mLayout.spacing = 20f;
            micMeterRow.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 24f);

            CreateTMPText(micMeterRow.transform, "Label", "Level Mikrofon:", 16, TextAlignmentOptions.MidlineLeft, 160f);
            GameObject meterBg = new GameObject("MeterBg", typeof(RectTransform), typeof(Image));
            meterBg.transform.SetParent(micMeterRow.transform, false);
            meterBg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);
            meterBg.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 16f);

            GameObject meterFill = new GameObject("MeterFill", typeof(RectTransform), typeof(Image));
            meterFill.transform.SetParent(meterBg.transform, false);
            Image fillImg = meterFill.GetComponent<Image>();
            fillImg.color = ColorAccent;
            RectTransform fillRect = meterFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f); // default 0 width
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;

            // Graphics Section
            TMP_Dropdown resDrop = CreateDropdownRow(panelObj.transform, "ResRow", "Resolusi");
            TMP_Dropdown fsDrop = CreateDropdownRow(panelObj.transform, "FullscreenRow", "Mode Layar");
            Toggle vsyncTog = CreateToggleRow(panelObj.transform, "VsyncRow", "VSync");
            TMP_Dropdown qualDrop = CreateDropdownRow(panelObj.transform, "QualityRow", "Kualitas Grafis");

            // Mouse Sensitivity
            Slider sldMouse = CreateSliderRow(panelObj.transform, "MouseRow", "Sensitivitas Mouse", 0.5f);

            // Bottom Action Buttons
            GameObject btnRow = new GameObject("ActionBtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup bLayout = btnRow.GetComponent<HorizontalLayoutGroup>();
            bLayout.spacing = 15f;
            bLayout.childControlWidth = true;
            bLayout.childControlHeight = false;
            btnRow.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 50f);

            Button btnApply = CreateButton(btnRow.transform, "BtnApply", "Terapkan", ColorAccent, ColorBg);
            Button btnDefaults = CreateButton(btnRow.transform, "BtnDefaults", "Kembalikan Default", ColorText, ColorBg);
            Button btnBack = CreateButton(btnRow.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            // Connect SettingsPanel Serialized fields
            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("masterSlider").objectReferenceValue = sldMaster;
            pSo.FindProperty("musicSlider").objectReferenceValue = sldMusic;
            pSo.FindProperty("sfxSlider").objectReferenceValue = sldSfx;
            pSo.FindProperty("voiceSlider").objectReferenceValue = sldVoice;
            pSo.FindProperty("micDropdown").objectReferenceValue = micDrop;
            pSo.FindProperty("micSensitivitySlider").objectReferenceValue = sldMicSens;
            pSo.FindProperty("micLevelBar").objectReferenceValue = fillImg;
            pSo.FindProperty("resolutionDropdown").objectReferenceValue = resDrop;
            pSo.FindProperty("fullscreenDropdown").objectReferenceValue = fsDrop;
            pSo.FindProperty("vSyncToggle").objectReferenceValue = vsyncTog;
            pSo.FindProperty("qualityDropdown").objectReferenceValue = qualDrop;
            pSo.FindProperty("mouseSensitivitySlider").objectReferenceValue = sldMouse;
            pSo.FindProperty("applyButton").objectReferenceValue = btnApply;
            pSo.FindProperty("defaultsButton").objectReferenceValue = btnDefaults;
            pSo.FindProperty("backButton").objectReferenceValue = btnBack;
            pSo.FindProperty("firstSelected").objectReferenceValue = sldMaster;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("settingsPanel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildCreditsPanel(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "CreditsPanel", new Vector2(550f, 420f), new Vector2(330f, -20f));
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(35, 35, 30, 30);

            CreateTMPText(panelObj.transform, "Header", "KREDIT", 28, TextAlignmentOptions.Center);

            string creditsContent = "PUCACI\n\nDibuat oleh:\nMusa & Tim Pengembang Pucaci\n\nGenre: Co-op Horor-Komedi Kampung\nDibuat dengan Unity 2022 LTS & Universal Render Pipeline";
            CreateTMPText(panelObj.transform, "Content", creditsContent, 20, TextAlignmentOptions.Center);

            Button btnBack = CreateButton(panelObj.transform, "BtnBack", "Kembali", ColorText, ColorBg);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("creditsBackButton").objectReferenceValue = btnBack;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnBack;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static MenuPanel BuildQuitDialog(Transform canvas, MainMenuController controller)
        {
            GameObject panelObj = CreatePanelContainer(canvas, "QuitConfirmDialog", new Vector2(460f, 260f), Vector2.zero);
            MenuPanel panel = panelObj.AddComponent<MenuPanel>();

            VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(30, 30, 30, 30);

            CreateTMPText(panelObj.transform, "Prompt", "Yakin mau keluar?", 26, TextAlignmentOptions.Center);

            GameObject btnRow = new GameObject("BtnRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(panelObj.transform, false);
            HorizontalLayoutGroup bLayout = btnRow.GetComponent<HorizontalLayoutGroup>();
            bLayout.spacing = 20f;
            bLayout.childControlWidth = true;
            bLayout.childControlHeight = false;
            btnRow.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 50f);

            Button btnYes = CreateButton(btnRow.transform, "BtnYes", "Ya", ColorDanger, ColorBg);
            Button btnCancel = CreateButton(btnRow.transform, "BtnCancel", "Batal", ColorText, ColorBg);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("confirmQuitButton").objectReferenceValue = btnYes;
            so.FindProperty("cancelQuitButton").objectReferenceValue = btnCancel;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject pSo = new SerializedObject(panel);
            pSo.FindProperty("firstSelected").objectReferenceValue = btnCancel;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static void BuildSceneFader(Transform canvas)
        {
            GameObject faderObj = new GameObject("SceneFader", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            faderObj.transform.SetParent(canvas, false);
            RectTransform fRect = faderObj.GetComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = Vector2.zero;
            fRect.offsetMax = Vector2.zero;

            Image img = faderObj.GetComponent<Image>();
            img.color = Color.black;

            faderObj.AddComponent<SceneFader>();
        }

        #region UI Helpers
        private static GameObject CreatePanelContainer(Transform parent, string name, Vector2 size, Vector2 anchoredPos)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Image img = panel.GetComponent<Image>();
            img.color = new Color(0.015f, 0.02f, 0.035f, 0.94f);

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.42f, 0.55f, 0.28f);
            outline.effectDistance = new Vector2(1f, -1f);

            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(0f, -8f);

            // Garis aksen brush darah tipis di bagian atas panel
            GameObject topLine = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
            topLine.transform.SetParent(panel.transform, false);
            var lineImg = topLine.GetComponent<Image>();
            lineImg.sprite = MenuHorrorArtGenerator.GetBrushUnderlineSprite();
            lineImg.color = new Color(0.85f, 0.12f, 0.16f, 0.85f);
            lineImg.raycastTarget = false;
            RectTransform lineRt = topLine.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0f, 1f);
            lineRt.anchorMax = new Vector2(1f, 1f);
            lineRt.pivot = new Vector2(0.5f, 1f);
            lineRt.offsetMin = new Vector2(0f, -4f);
            lineRt.offsetMax = new Vector2(0f, 0f);

            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string text, Color textColor, Color bgColor, float width = -1f)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width > 0 ? width : 360f, 48f);

            Image img = btnObj.GetComponent<Image>();
            img.color = new Color(0.045f, 0.065f, 0.095f, 0.95f);

            Button btn = btnObj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.disabledColor = ColorDisabled;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.38f, 0.45f, 0.58f, 0.35f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject textObj = CreateTMPText(btnObj.transform, "Text", text, 19, TextAlignmentOptions.Center);
            TMP_Text tmp = textObj.GetComponent<TMP_Text>();
            tmp.color = textColor;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 4f;
            tmp.enableWordWrapping = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 0f);
            textRt.offsetMax = new Vector2(-12f, 0f);

            var feedback = btnObj.AddComponent<ButtonFeedback>();
            var fbSo = new SerializedObject(feedback);
            var pBorder = fbSo.FindProperty("borderImage"); if (pBorder != null) pBorder.objectReferenceValue = img;
            var pNorm = fbSo.FindProperty("normalTextColor"); if (pNorm != null) pNorm.colorValue = textColor;
            var pHi = fbSo.FindProperty("highlightTextColor"); if (pHi != null) pHi.colorValue = new Color(1f, 1f, 1f, 1f);
            var pTarget = fbSo.FindProperty("targetText"); if (pTarget != null) pTarget.objectReferenceValue = tmp;
            fbSo.ApplyModifiedPropertiesWithoutUndo();

            return btn;
        }

        private static Button CreateMenuActionButton(Transform parent, string name, string text, bool isDefaultSelected = false)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360f, 50f);

            // Background transparan penuh (tanpa kotak / border apapun)
            Image bg = btnObj.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);

            Button btn = btnObj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = ColorDisabled;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.transition = Selectable.Transition.None;

            // Brush Tag di belakang teks (gaya The Infected Soul)
            GameObject tagObj = new GameObject("BrushTag", typeof(RectTransform), typeof(Image));
            tagObj.transform.SetParent(btnObj.transform, false);
            var tagImg = tagObj.GetComponent<Image>();
            tagImg.sprite = MenuHorrorArtGenerator.GetBrushTagSprite();
            tagImg.type = Image.Type.Simple;
            tagImg.raycastTarget = false;
            tagImg.color = isDefaultSelected ? new Color(0.96f, 0.96f, 0.98f, 1f) : new Color(1f, 1f, 1f, 0f);

            RectTransform tagRt = tagObj.GetComponent<RectTransform>();
            tagRt.anchorMin = Vector2.zero;
            tagRt.anchorMax = Vector2.one;
            tagRt.offsetMin = new Vector2(-15f, -6f);
            tagRt.offsetMax = new Vector2(15f, 6f);

            // Teks utama (huruf kapital, spasi lebar, font horror modern)
            GameObject textObj = CreateTMPText(btnObj.transform, "Text", text, 28, TextAlignmentOptions.MidlineLeft);
            TMP_Text tmp = textObj.GetComponent<TMP_Text>();
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 8f;
            tmp.color = isDefaultSelected ? new Color(0.08f, 0.09f, 0.12f, 1f) : new Color(0.72f, 0.76f, 0.82f, 0.80f);
            tmp.enableWordWrapping = false;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(14f, 0f);
            textRt.offsetMax = new Vector2(-14f, 0f);

            var feedback = btnObj.AddComponent<ButtonFeedback>();
            var fbSo = new SerializedObject(feedback);
            var pSlide = fbSo.FindProperty("hoverSlideX"); if (pSlide != null) pSlide.floatValue = 10f;
            var pScale = fbSo.FindProperty("hoverScale"); if (pScale != null) pScale.floatValue = 1.025f;
            var pClick = fbSo.FindProperty("clickScale"); if (pClick != null) pClick.floatValue = 0.98f;
            var pTarget = fbSo.FindProperty("slideTarget"); if (pTarget != null) pTarget.objectReferenceValue = textRt;
            var pSelected = fbSo.FindProperty("isSelectedOrHovered"); if (pSelected != null) pSelected.boolValue = isDefaultSelected;
            var pBrushTag = fbSo.FindProperty("brushTagImage"); if (pBrushTag != null) pBrushTag.objectReferenceValue = tagImg;
            var pTagNorm = fbSo.FindProperty("brushTagNormalColor"); if (pTagNorm != null) pTagNorm.colorValue = new Color(1f, 1f, 1f, 0f);
            var pTagHi = fbSo.FindProperty("brushTagHighlightColor"); if (pTagHi != null) pTagHi.colorValue = new Color(0.96f, 0.96f, 0.98f, 1f);
            var pNormText = fbSo.FindProperty("normalTextColor"); if (pNormText != null) pNormText.colorValue = new Color(0.72f, 0.76f, 0.82f, 0.80f);
            var pHiText = fbSo.FindProperty("highlightTextColor"); if (pHiText != null) pHiText.colorValue = new Color(0.08f, 0.09f, 0.12f, 1f);
            var pText = fbSo.FindProperty("targetText"); if (pText != null) pText.objectReferenceValue = tmp;
            fbSo.ApplyModifiedPropertiesWithoutUndo();

            return btn;
        }

        private static GameObject CreateTMPText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions align, float width = -1f)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Materials/BaronNeue_TMP.asset");
            if (font == null)
            {
                var fontSource = AssetDatabase.LoadAssetAtPath<Font>("Assets/Mirror/Hosting/Edgegap/Editor/Fonts/Src/BaronNeue.otf");
                if (fontSource != null)
                {
                    font = TMP_FontAsset.CreateFontAsset(fontSource);
                    font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    AssetDatabase.CreateAsset(font, "Assets/_Game/Materials/BaronNeue_TMP.asset");
                    AssetDatabase.AddObjectToAsset(font.material, font);
                    AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                    AssetDatabase.SaveAssets();
                }
            }
            if (font != null)
            {
                tmp.font = font;
            }
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = ColorText;

            RectTransform rt = obj.GetComponent<RectTransform>();
            if (width > 0)
            {
                rt.sizeDelta = new Vector2(width, 36f);
            }

            return obj;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText)
        {
            GameObject inputObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputObj.transform.SetParent(parent, false);

            RectTransform rt = inputObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 48f);

            Image img = inputObj.GetComponent<Image>();
            img.color = new Color(0.08f, 0.11f, 0.18f, 1f);

            TMP_InputField input = inputObj.GetComponent<TMP_InputField>();

            // Text
            GameObject textObj = CreateTMPText(inputObj.transform, "Text", "", 18, TextAlignmentOptions.MidlineLeft);
            TMP_Text text = textObj.GetComponent<TMP_Text>();
            text.color = ColorText;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            // Placeholder
            GameObject phObj = CreateTMPText(inputObj.transform, "Placeholder", placeholderText, 18, TextAlignmentOptions.MidlineLeft);
            TMP_Text ph = phObj.GetComponent<TMP_Text>();
            ph.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            RectTransform phRt = phObj.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(10f, 0f);
            phRt.offsetMax = new Vector2(-10f, 0f);

            input.textViewport = textRt;
            input.textComponent = text;
            input.placeholder = ph;

            return input;
        }

        private static Slider CreateSliderRow(Transform parent, string rowName, string labelText, float defaultValue)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup rLayout = row.GetComponent<HorizontalLayoutGroup>();
            rLayout.childControlWidth = false;
            rLayout.childControlHeight = false;
            rLayout.spacing = 15f;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 28f);

            CreateTMPText(row.transform, "Label", labelText, 16, TextAlignmentOptions.MidlineLeft, 180f);

            GameObject sliderObj = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(row.transform, false);
            RectTransform sRect = sliderObj.GetComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(440f, 20f);

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = defaultValue;

            // Background (relatif ke tinggi slider, tinggi 8px di tengah)
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderObj.transform, false);
            bg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgRt.sizeDelta = Vector2.zero;

            // Fill Area (dibatasi agar pas di dalam rel slider)
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = new Vector2(5f, 0f);
            faRt.offsetMax = new Vector2(-15f, 0f);
            faRt.sizeDelta = new Vector2(-20f, 0f);

            // Fill (WAJIB sizeDelta = 0 dan offset = 0 agar tidak meluber menjadi kotak raksasa!)
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = ColorAccent;
            RectTransform fRt = fill.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.zero;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fRt.sizeDelta = new Vector2(10f, 0f);
            slider.fillRect = fRt;

            // Handle Slide Area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10f, 0f);
            haRt.offsetMax = new Vector2(-10f, 0f);
            haRt.sizeDelta = new Vector2(-20f, 0f);

            // Handle (tombol geser rapi, tinggi 20px lebar 16px)
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<Image>().color = ColorText;
            RectTransform hRt = handle.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(0f, 1f);
            hRt.offsetMin = new Vector2(-8f, 0f);
            hRt.offsetMax = new Vector2(8f, 0f);
            hRt.sizeDelta = new Vector2(16f, 0f);
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();

            return slider;
        }

        private static TMP_Dropdown CreateDropdownRow(Transform parent, string rowName, string labelText)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup rLayout = row.GetComponent<HorizontalLayoutGroup>();
            rLayout.childControlWidth = false;
            rLayout.childControlHeight = false;
            rLayout.spacing = 15f;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 32f);

            CreateTMPText(row.transform, "Label", labelText, 16, TextAlignmentOptions.MidlineLeft, 180f);

            GameObject dropObj = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            dropObj.transform.SetParent(row.transform, false);
            RectTransform dRect = dropObj.GetComponent<RectTransform>();
            dRect.sizeDelta = new Vector2(440f, 30f);

            dropObj.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.24f, 1f);

            TMP_Dropdown dropdown = dropObj.GetComponent<TMP_Dropdown>();

            GameObject caption = CreateTMPText(dropObj.transform, "Label", "Pilih...", 16, TextAlignmentOptions.MidlineLeft);
            RectTransform cRect = caption.GetComponent<RectTransform>();
            cRect.anchorMin = Vector2.zero;
            cRect.anchorMax = Vector2.one;
            cRect.offsetMin = new Vector2(10f, 0f);
            cRect.offsetMax = new Vector2(-10f, 0f);
            dropdown.captionText = caption.GetComponent<TMP_Text>();

            return dropdown;
        }

        private static Toggle CreateToggleRow(Transform parent, string rowName, string labelText)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup rLayout = row.GetComponent<HorizontalLayoutGroup>();
            rLayout.childControlWidth = false;
            rLayout.childControlHeight = false;
            rLayout.spacing = 15f;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 28f);

            CreateTMPText(row.transform, "Label", labelText, 16, TextAlignmentOptions.MidlineLeft, 180f);

            GameObject togObj = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            togObj.transform.SetParent(row.transform, false);
            RectTransform tRect = togObj.GetComponent<RectTransform>();
            tRect.sizeDelta = new Vector2(30f, 26f);

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(togObj.transform, false);
            bg.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            check.GetComponent<Image>().color = ColorAccent;
            RectTransform chRt = check.GetComponent<RectTransform>();
            chRt.anchorMin = new Vector2(0.2f, 0.2f);
            chRt.anchorMax = new Vector2(0.8f, 0.8f);
            chRt.sizeDelta = Vector2.zero;

            Toggle toggle = togObj.GetComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.isOn = true;

            return toggle;
        }

        private static void UpdateBuildSettings(string mainMenuScenePath)
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();

            // 0: MainMenu
            list.Add(new EditorBuildSettingsScene(mainMenuScenePath, true));

            // 1: PrototypeScene
            string prototypePath = "Assets/Scenes/Prototype/PrototypeScene.unity";
            list.Add(new EditorBuildSettingsScene(prototypePath, true));

            // Tambahkan scene lainnya
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path != mainMenuScenePath && s.path != prototypePath)
                {
                    list.Add(s);
                }
            }

            EditorBuildSettings.scenes = list.ToArray();
        }
        #endregion
    }
}
