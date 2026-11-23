using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Pucaci.Rendering;

namespace Pucaci.Editor
{
    public static class CartoonOutlineSetupUtility
    {
        private const string PcHighRendererPath = "Assets/Settings/PC/PC_High_Renderer.asset";
        private const string PcLowRendererPath = "Assets/Settings/PC/PC_Low_Renderer.asset";
        private const string MobileHighRendererPath = "Assets/Settings/Mobile/Mobile_High_Renderer.asset";
        private const string MobileLowRendererPath = "Assets/Settings/Mobile/Mobile_Low_Renderer.asset";

        [MenuItem("Pucaci/Visuals/Apply Cartoon Outline (Cel-Shading)")]
        public static void ApplyCartoonOutline()
        {
            string[] rendererPaths = {
                PcHighRendererPath,
                PcLowRendererPath,
                MobileHighRendererPath,
                MobileLowRendererPath
            };

            int addedCount = 0;
            foreach (var path in rendererPaths)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null)
                {
                    Debug.LogWarning($"[CartoonOutlineSetup] Renderer data tidak ditemukan di: {path}");
                    continue;
                }

                if (AddOutlineFeatureToRenderer(rendererData))
                {
                    addedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FFAA><b>[CartoonOutlineSetup] Berhasil menerapkan Cartoon Outline ke {addedCount} URP Renderer!</b></color>");
        }

        public static bool AddOutlineFeatureToRenderer(ScriptableRendererData rendererData)
        {
            if (rendererData == null) return false;

            // Cek apakah CartoonOutlineFeature sudah ada
            SerializedObject so = new SerializedObject(rendererData);
            SerializedProperty featuresProp = so.FindProperty("m_RendererFeatures");

            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var elem = featuresProp.GetArrayElementAtIndex(i);
                if (elem.objectReferenceValue != null && elem.objectReferenceValue is CartoonOutlineFeature)
                {
                    Debug.Log($"[CartoonOutlineSetup] CartoonOutlineFeature sudah aktif di: {rendererData.name}");
                    return true;
                }
            }

            // Buat instance CartoonOutlineFeature baru sebagai sub-asset
            var feature = ScriptableObject.CreateInstance<CartoonOutlineFeature>();
            feature.name = "CartoonOutlineFeature";

            // Konfigurasi nilai default outline hitam kartunis yang solid dan estetik
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Game/Shaders/CartoonOutline.shader");
            SerializedObject featureSO = new SerializedObject(feature);
            if (shader != null)
            {
                var shaderProp = featureSO.FindProperty("outlineShader");
                if (shaderProp != null) shaderProp.objectReferenceValue = shader;
            }

            var settingsProp = featureSO.FindProperty("settings");
            if (settingsProp != null)
            {
                var colorProp = settingsProp.FindPropertyRelative("outlineColor");
                if (colorProp != null) colorProp.colorValue = new Color(0.01f, 0.01f, 0.01f, 1f);

                var thickProp = settingsProp.FindPropertyRelative("outlineThickness");
                if (thickProp != null) thickProp.floatValue = 1.25f;

                var depthSensProp = settingsProp.FindPropertyRelative("depthSensitivity");
                if (depthSensProp != null) depthSensProp.floatValue = 12f;

                var depthThreshProp = settingsProp.FindPropertyRelative("depthThreshold");
                if (depthThreshProp != null) depthThreshProp.floatValue = 0.035f;

                var normSensProp = settingsProp.FindPropertyRelative("normalSensitivity");
                if (normSensProp != null) normSensProp.floatValue = 1.8f;

                var normThreshProp = settingsProp.FindPropertyRelative("normalThreshold");
                if (normThreshProp != null) normThreshProp.floatValue = 0.22f;
            }
            featureSO.ApplyModifiedProperties();

            AssetDatabase.AddObjectToAsset(feature, rendererData);

            featuresProp.arraySize++;
            featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = feature;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(rendererData);
            return true;
        }
    }
}
