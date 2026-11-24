using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pucaci.EditorTools
{
    public static class BuildGameUtility
    {
        [MenuItem("Pucaci/Build Windows Standalone Async")]
        public static void BuildWindowsPlayerAsync()
        {
            EditorApplication.delayCall += () =>
            {
                BuildWindowsPlayer();
            };
        }

        [MenuItem("Pucaci/Build Windows Standalone")]
        public static string BuildWindowsPlayer()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputFolder = Path.Combine(projectRoot, "Builds");
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string exePath = Path.Combine(outputFolder, "Pucaci.exe");

            string[] scenes = new string[]
            {
                "Assets/_Game/Scenes/MainMenu.unity",
                "Assets/_Game/Scenes/Gameplay.unity"
            };

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildGameUtility] Memulai kompilasi build Pucaci.exe ke: {exePath}...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string msg = $"SUCCESS: Pucaci.exe berhasil dibuat! Total size: {summary.totalSize / (1024 * 1024)} MB dalam {summary.totalTime.TotalSeconds:F1}s";
                Debug.Log($"<color=#00FF00>[BuildGameUtility] {msg}</color>");
                return msg;
            }
            else
            {
                string msg = $"FAILED: Build gagal dengan status {summary.result}. Total Errors: {summary.totalErrors}";
                Debug.LogError($"[BuildGameUtility] {msg}");

                foreach (var step in report.steps)
                {
                    foreach (var m in step.messages)
                    {
                        if (m.type == LogType.Error || m.type == LogType.Exception)
                        {
                            Debug.LogError($"[BuildErrorDetails] {m.content}");
                        }
                    }
                }
                return msg;
            }
        }
    }
}
