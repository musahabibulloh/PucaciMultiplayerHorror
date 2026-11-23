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
            psRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));

            // Hubungkan referensi ke SesajenItem
            var visField = typeof(SesajenItem).GetField("visualContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lightField = typeof(SesajenItem).GetField("candleLight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var smokeField = typeof(SesajenItem).GetField("incenseSmokeParticles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            visField?.SetValue(sesajenItem, visual);
            lightField?.SetValue(sesajenItem, light);
            smokeField?.SetValue(sesajenItem, ps);

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
    }
}
