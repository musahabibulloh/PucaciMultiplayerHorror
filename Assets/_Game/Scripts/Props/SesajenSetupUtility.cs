#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using Pucaci.Gameplay;
using Pucaci.Player;
using Pucaci.UI;

namespace Pucaci.Props
{
    public static class SesajenSetupUtility
    {
        private const string PrefabDir = "Assets/_Game/Prefabs/Props";
        private const string MaterialDir = "Assets/_Game/Materials/Props";
        private const string PrefabPath = PrefabDir + "/Sesajen.prefab";

        public static GameObject CreateSesajenPrefab()
        {
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

            // 1. Buat Materials
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Material Nampan Bambu
            Material trayMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Tray.mat");
            if (trayMat == null)
            {
                trayMat = new Material(litShader) { name = "M_Sesajen_Tray" };
                trayMat.color = new Color(0.65f, 0.48f, 0.28f, 1f); // Warna anyaman bambu
                AssetDatabase.CreateAsset(trayMat, MaterialDir + "/M_Sesajen_Tray.mat");
            }

            // Material Daun Pisang
            Material leafMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Leaf.mat");
            if (leafMat == null)
            {
                leafMat = new Material(litShader) { name = "M_Sesajen_Leaf" };
                leafMat.color = new Color(0.18f, 0.52f, 0.12f, 1f); // Hijau daun pisang
                AssetDatabase.CreateAsset(leafMat, MaterialDir + "/M_Sesajen_Leaf.mat");
            }

            // Material Batok Kelapa
            Material bowlMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Bowl.mat");
            if (bowlMat == null)
            {
                bowlMat = new Material(litShader) { name = "M_Sesajen_Bowl" };
                bowlMat.color = new Color(0.32f, 0.18f, 0.08f, 1f); // Cokelat tua batok
                AssetDatabase.CreateAsset(bowlMat, MaterialDir + "/M_Sesajen_Bowl.mat");
            }

            // Material Batang Dupa
            Material incenseMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Incense.mat");
            if (incenseMat == null)
            {
                incenseMat = new Material(litShader) { name = "M_Sesajen_Incense" };
                incenseMat.color = new Color(0.75f, 0.15f, 0.12f, 1f); // Merah dupa
                incenseMat.EnableKeyword("_EMISSION");
                incenseMat.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.05f) * 1.5f);
                AssetDatabase.CreateAsset(incenseMat, MaterialDir + "/M_Sesajen_Incense.mat");
            }

            // Material Bunga Mawar / Melati
            Material flowerMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Flower.mat");
            if (flowerMat == null)
            {
                flowerMat = new Material(litShader) { name = "M_Sesajen_Flower" };
                flowerMat.color = new Color(0.95f, 0.15f, 0.25f, 1f); // Merah mawar
                AssetDatabase.CreateAsset(flowerMat, MaterialDir + "/M_Sesajen_Flower.mat");
            }

            // Material Lilin Merah
            Material candleMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Candle.mat");
            if (candleMat == null)
            {
                candleMat = new Material(litShader) { name = "M_Sesajen_Candle" };
                candleMat.color = new Color(0.85f, 0.1f, 0.1f, 1f);
                AssetDatabase.CreateAsset(candleMat, MaterialDir + "/M_Sesajen_Candle.mat");
            }

            // 2. Rakit Root GameObject Sesajen
            GameObject root = new GameObject("Sesajen");
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.38f;
            col.height = 0.55f;
            col.center = new Vector3(0f, 0.25f, 0f);

            var netId = root.AddComponent<Mirror.NetworkIdentity>();
            var sesajenItem = root.AddComponent<SesajenItem>();

            // Visual Container
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // A. Tampah Anyaman Bambu
            GameObject trayObj = new GameObject("Tray_Tampah");
            trayObj.transform.SetParent(visual.transform, false);
            var trayMf = trayObj.AddComponent<MeshFilter>();
            trayMf.sharedMesh = SesajenMeshBuilder.CreateTrayMesh(0.35f, 0.05f, 24);
            var trayMr = trayObj.AddComponent<MeshRenderer>();
            trayMr.sharedMaterial = trayMat;

            // B. Alas Daun Pisang
            GameObject leafObj = new GameObject("Banana_Leaf");
            leafObj.transform.SetParent(visual.transform, false);
            var leafMf = leafObj.AddComponent<MeshFilter>();
            leafMf.sharedMesh = SesajenMeshBuilder.CreateBananaLeafMesh(0.33f, 20);
            var leafMr = leafObj.AddComponent<MeshRenderer>();
            leafMr.sharedMaterial = leafMat;

            // C. Mangkuk Batok Kelapa Tempat Dupa
            GameObject bowlObj = new GameObject("Coconut_Bowl");
            bowlObj.transform.SetParent(visual.transform, false);
            bowlObj.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            var bowlMf = bowlObj.AddComponent<MeshFilter>();
            bowlMf.sharedMesh = SesajenMeshBuilder.CreateBowlMesh(0.08f, 0.05f, 16);
            var bowlMr = bowlObj.AddComponent<MeshRenderer>();
            bowlMr.sharedMaterial = bowlMat;

            // D. 3 Batang Dupa Berdiri dari Mangkuk
            for (int i = 0; i < 3; i++)
            {
                GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stick.name = "Incense_Stick_" + (i + 1);
                stick.transform.SetParent(visual.transform, false);
                float angle = i * 120f * Mathf.Deg2Rad;
                stick.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.025f, 0.13f, Mathf.Sin(angle) * 0.025f);
                stick.transform.localRotation = Quaternion.Euler(Mathf.Sin(angle) * 12f, 0f, -Mathf.Cos(angle) * 12f);
                stick.transform.localScale = new Vector3(0.007f, 0.11f, 0.007f);

                Object.DestroyImmediate(stick.GetComponent<Collider>());
                stick.GetComponent<MeshRenderer>().sharedMaterial = incenseMat;
            }

            // E. Lilin Merah
            GameObject candle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            candle.name = "Candle";
            candle.transform.SetParent(visual.transform, false);
            candle.transform.localPosition = new Vector3(-0.16f, 0.06f, 0.08f);
            candle.transform.localScale = new Vector3(0.045f, 0.055f, 0.045f);
            Object.DestroyImmediate(candle.GetComponent<Collider>());
            candle.GetComponent<MeshRenderer>().sharedMaterial = candleMat;

            // F. Bunga Kembang 7 Rupa
            for (int i = 0; i < 10; i++)
            {
                GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                petal.name = "Flower_Petal_" + i;
                petal.transform.SetParent(visual.transform, false);
                float a = i * 36f * Mathf.Deg2Rad;
                float dist = Random.Range(0.12f, 0.28f);
                petal.transform.localPosition = new Vector3(Mathf.Cos(a) * dist, 0.022f, Mathf.Sin(a) * dist);
                petal.transform.localScale = new Vector3(0.035f, 0.015f, 0.035f);
                Object.DestroyImmediate(petal.GetComponent<Collider>());
                petal.GetComponent<MeshRenderer>().sharedMaterial = flowerMat;
            }

            // G. Point Light Lilin Temaram
            GameObject lightObj = new GameObject("Candle_Light");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(-0.16f, 0.16f, 0.08f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.5f;
            light.intensity = 1.6f;
            light.color = new Color(1.0f, 0.62f, 0.22f); // Warna api lilin hangat

            // H. Asap Dupa Partikel
            GameObject smokeObj = new GameObject("Incense_Smoke");
            smokeObj.transform.SetParent(root.transform, false);
            smokeObj.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            var ps = smokeObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2.0f;
            main.loop = true;
            main.startLifetime = 1.8f;
            main.startSpeed = 0.25f;
            main.startSize = 0.06f;
            main.startColor = new Color(0.9f, 0.9f, 0.95f, 0.25f);

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.03f;

            var psRenderer = smokeObj.GetComponent<ParticleSystemRenderer>();
            Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Smoke.mat");
            if (smokeMat == null)
            {
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                smokeMat = new Material(particleShader) { name = "M_Sesajen_Smoke" };
                smokeMat.SetFloat("_Surface", 1); // Transparent
                smokeMat.SetFloat("_Blend", 0); // Alpha
                smokeMat.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.95f, 0.35f));
                AssetDatabase.CreateAsset(smokeMat, MaterialDir + "/M_Sesajen_Smoke.mat");
            }
            psRenderer.sharedMaterial = smokeMat;

            // Hubungkan referensi ke SesajenItem
            var visField = typeof(SesajenItem).GetField("visualContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lightField = typeof(SesajenItem).GetField("candleLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var smokeField = typeof(SesajenItem).GetField("incenseSmokeParticles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            visField?.SetValue(sesajenItem, visual);
            lightField?.SetValue(sesajenItem, light);
            smokeField?.SetValue(sesajenItem, ps);

            // Bangun kontainer bekas reruntuhan sesajen hancur
            BuildRuinsContainer(root);

            // Simpan Prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return prefab;
        }

        public static void SetupPlayerPrefab()
        {
            string playerPrefabPath = "Assets/_Game/Prefabs/Player/PlayerCharacter.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab == null) return;

            if (playerPrefab.GetComponent<PlayerSesajenInteractor>() == null)
            {
                playerPrefab.AddComponent<PlayerSesajenInteractor>();
                EditorUtility.SetDirty(playerPrefab);
                PrefabUtility.SavePrefabAsset(playerPrefab);
                Debug.Log("[SESAJEN] PlayerSesajenInteractor ditambahkan ke PlayerCharacter.prefab");
            }
        }

        /// <summary>
        /// Membangun objek visual bekas reruntuhan sesajen yang hancur (abu, tampah patah, arang, bara dan sinyal gaib hantu).
        /// </summary>
        public static GameObject BuildRuinsContainer(GameObject root)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Material Abu Hangus
            Material ashMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Ash.mat");
            if (ashMat == null)
            {
                ashMat = new Material(litShader) { name = "M_Sesajen_Ash" };
                ashMat.color = new Color(0.12f, 0.11f, 0.11f, 1f); // Hitam abu gosong
                ashMat.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(ashMat, MaterialDir + "/M_Sesajen_Ash.mat");
            }

            // Material Kayu / Bambu Hangus
            Material burntWoodMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_BurntWood.mat");
            if (burntWoodMat == null)
            {
                burntWoodMat = new Material(litShader) { name = "M_Sesajen_BurntWood" };
                burntWoodMat.color = new Color(0.18f, 0.14f, 0.11f, 1f); // Cokelat arang tua
                burntWoodMat.EnableKeyword("_EMISSION");
                burntWoodMat.SetColor("_EmissionColor", new Color(0.35f, 0.08f, 0.02f) * 0.8f);
                AssetDatabase.CreateAsset(burntWoodMat, MaterialDir + "/M_Sesajen_BurntWood.mat");
            }

            // Material Bunga Layu
            Material wiltedFlowerMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_WiltedFlower.mat");
            if (wiltedFlowerMat == null)
            {
                wiltedFlowerMat = new Material(litShader) { name = "M_Sesajen_WiltedFlower" };
                wiltedFlowerMat.color = new Color(0.28f, 0.10f, 0.14f, 1f); // Merah tua layu kehitaman
                AssetDatabase.CreateAsset(wiltedFlowerMat, MaterialDir + "/M_Sesajen_WiltedFlower.mat");
            }

            Material bowlMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Bowl.mat");
            Material candleMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Candle.mat");
            Material incenseMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Sesajen_Incense.mat");

            // Hapus ruins lama jika ada
            var oldRuins = root.transform.Find("Ruins");
            if (oldRuins != null) Object.DestroyImmediate(oldRuins.gameObject);

            // Buat root Ruins
            GameObject ruinsObj = new GameObject("Ruins");
            ruinsObj.transform.SetParent(root.transform, false);

            // 1. Hamparan Abu Hitam (Ash Decal)
            GameObject ashObj = new GameObject("Ash_Decal");
            ashObj.transform.SetParent(ruinsObj.transform, false);
            var ashMf = ashObj.AddComponent<MeshFilter>();
            ashMf.sharedMesh = SesajenMeshBuilder.CreateAshDecalMesh(0.44f, 24);
            var ashMr = ashObj.AddComponent<MeshRenderer>();
            ashMr.sharedMaterial = ashMat;

            // 2. Pecahan Tampah Bambu 1
            GameObject trayPiece1 = new GameObject("Broken_Tray_1");
            trayPiece1.transform.SetParent(ruinsObj.transform, false);
            trayPiece1.transform.localPosition = new Vector3(-0.05f, 0.008f, 0.03f);
            trayPiece1.transform.localRotation = Quaternion.Euler(10f, -20f, 5f);
            var tp1Mf = trayPiece1.AddComponent<MeshFilter>();
            tp1Mf.sharedMesh = SesajenMeshBuilder.CreateBrokenTrayPieceMesh(0.35f, 0.04f, 2.6f, 14);
            var tp1Mr = trayPiece1.AddComponent<MeshRenderer>();
            tp1Mr.sharedMaterial = burntWoodMat;

            // 3. Pecahan Tampah Bambu 2 (terbalik miring)
            GameObject trayPiece2 = new GameObject("Broken_Tray_2");
            trayPiece2.transform.SetParent(ruinsObj.transform, false);
            trayPiece2.transform.localPosition = new Vector3(0.07f, 0.006f, -0.04f);
            trayPiece2.transform.localRotation = Quaternion.Euler(-6f, 150f, -4f);
            var tp2Mf = trayPiece2.AddComponent<MeshFilter>();
            tp2Mf.sharedMesh = SesajenMeshBuilder.CreateBrokenTrayPieceMesh(0.32f, 0.035f, 2.0f, 12);
            var tp2Mr = trayPiece2.AddComponent<MeshRenderer>();
            tp2Mr.sharedMaterial = burntWoodMat;

            // 4. Pecahan Batok Kelapa Terbelah
            GameObject bowlFrg1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bowlFrg1.name = "Broken_Bowl_1";
            bowlFrg1.transform.SetParent(ruinsObj.transform, false);
            bowlFrg1.transform.localPosition = new Vector3(-0.06f, 0.02f, -0.03f);
            bowlFrg1.transform.localScale = new Vector3(0.065f, 0.028f, 0.05f);
            bowlFrg1.transform.localRotation = Quaternion.Euler(20f, 35f, 15f);
            Object.DestroyImmediate(bowlFrg1.GetComponent<Collider>());
            if (bowlMat != null) bowlFrg1.GetComponent<MeshRenderer>().sharedMaterial = bowlMat;

            GameObject bowlFrg2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bowlFrg2.name = "Broken_Bowl_2";
            bowlFrg2.transform.SetParent(ruinsObj.transform, false);
            bowlFrg2.transform.localPosition = new Vector3(0.05f, 0.015f, 0.06f);
            bowlFrg2.transform.localScale = new Vector3(0.045f, 0.022f, 0.04f);
            bowlFrg2.transform.localRotation = Quaternion.Euler(-15f, -60f, 25f);
            Object.DestroyImmediate(bowlFrg2.GetComponent<Collider>());
            if (bowlMat != null) bowlFrg2.GetComponent<MeshRenderer>().sharedMaterial = bowlMat;

            // 5. Lilin Tumbang & Padam di Abu
            GameObject candleStump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            candleStump.name = "Extinguished_Candle";
            candleStump.transform.SetParent(ruinsObj.transform, false);
            candleStump.transform.localPosition = new Vector3(0.12f, 0.018f, 0.05f);
            candleStump.transform.localRotation = Quaternion.Euler(0f, 45f, 85f);
            candleStump.transform.localScale = new Vector3(0.038f, 0.035f, 0.038f);
            Object.DestroyImmediate(candleStump.GetComponent<Collider>());
            if (candleMat != null) candleStump.GetComponent<MeshRenderer>().sharedMaterial = candleMat;

            // 6. Batang Dupa Patah Berserakan di Tanah
            for (int i = 0; i < 3; i++)
            {
                GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stick.name = "Broken_Incense_" + (i + 1);
                stick.transform.SetParent(ruinsObj.transform, false);
                float angle = i * 75f;
                stick.transform.localPosition = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.14f, 0.01f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.14f);
                stick.transform.localRotation = Quaternion.Euler(0f, angle + 30f, 90f);
                stick.transform.localScale = new Vector3(0.005f, 0.06f, 0.005f);
                Object.DestroyImmediate(stick.GetComponent<Collider>());
                if (incenseMat != null) stick.GetComponent<MeshRenderer>().sharedMaterial = incenseMat;
            }

            // 7. Bunga Mawar / Melati Layu Berserakan
            for (int i = 0; i < 8; i++)
            {
                GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                petal.name = "Wilted_Petal_" + i;
                petal.transform.SetParent(ruinsObj.transform, false);
                float a = i * 45f * Mathf.Deg2Rad;
                float dist = Random.Range(0.15f, 0.32f);
                petal.transform.localPosition = new Vector3(Mathf.Cos(a) * dist, 0.012f, Mathf.Sin(a) * dist);
                petal.transform.localScale = new Vector3(0.032f, 0.01f, 0.032f);
                Object.DestroyImmediate(petal.GetComponent<Collider>());
                petal.GetComponent<MeshRenderer>().sharedMaterial = wiltedFlowerMat;
            }

            // 8. Cursed Spectral Beacon Light (Pendaran Merah Mistis Penanda untuk Hantu)
            GameObject beaconObj = new GameObject("Ruins_Beacon");
            beaconObj.transform.SetParent(ruinsObj.transform, false);
            beaconObj.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            var beaconLight = beaconObj.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.range = 3.6f;
            beaconLight.intensity = 0.65f;
            beaconLight.color = new Color(1.0f, 0.18f, 0.10f); // Merah bara mistis

            // Default saat masih utuh: Ruins dinonaktifkan
            ruinsObj.SetActive(false);

            // Sambungkan referensi ke SesajenItem
            var sesajenItem = root.GetComponent<SesajenItem>();
            if (sesajenItem != null)
            {
                var so = new SerializedObject(sesajenItem);
                so.FindProperty("ruinsContainer").objectReferenceValue = ruinsObj;
                so.FindProperty("ruinsBeaconLight").objectReferenceValue = beaconLight;
                so.ApplyModifiedProperties();
            }

            return ruinsObj;
        }
    }
}
#endif
