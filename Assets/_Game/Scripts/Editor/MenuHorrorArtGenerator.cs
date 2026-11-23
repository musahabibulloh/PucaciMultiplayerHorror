using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pucaci.Editor
{
    public static class MenuHorrorArtGenerator
    {
        private const string TextureDir = "Assets/_Game/Textures/UI";

        [MenuItem("Tools/Pucaci/Generate Horror UI Textures")]
        public static void GenerateAll()
        {
            if (!Directory.Exists(TextureDir))
            {
                Directory.CreateDirectory(TextureDir);
            }

            GenerateBrushStrokeTag();
            GenerateBrushUnderline();
            GenerateHorrorTitleTexture();
            GenerateStudioLogo();

            AssetDatabase.Refresh();
            Debug.Log("[MenuHorrorArtGenerator] Semua tekstur UI Horror berhasil di-generate!");
        }

        public static Sprite GetBrushTagSprite()
        {
            string path = $"{TextureDir}/UI_BrushStrokeTag.png";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                GenerateBrushStrokeTag();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sp;
        }

        public static Sprite GetBrushUnderlineSprite()
        {
            string path = $"{TextureDir}/UI_BrushLine.png";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                GenerateBrushUnderline();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sp;
        }

        public static Sprite GetHorrorTitleSprite()
        {
            string path = $"{TextureDir}/UI_HorrorTitle_Pucaci.png";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                GenerateHorrorTitleTexture();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sp;
        }

        public static Sprite GetStudioLogoSprite()
        {
            string path = $"{TextureDir}/UI_StudioLogo.png";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                GenerateStudioLogo();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sp;
        }

        private static void GenerateBrushStrokeTag()
        {
            const int width = 512;
            const int height = 96;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];
            float midY = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float ny = (float)y / height;
                float dy = Mathf.Abs(y - midY);

                // Variasi batas kiri dan kanan organik berdasarkan serat kuas
                float leftBound = 0.03f + Mathf.PerlinNoise(12.5f, ny * 16f) * 0.12f;
                float rightBound = 0.97f - Mathf.PerlinNoise(42.8f, ny * 16f) * 0.12f;

                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;

                    // Ragged dry-brush noise pada tepi atas dan bawah
                    float topBottomNoise = Mathf.PerlinNoise(nx * 14f, ny * 8f) * 10f - 5f;
                    topBottomNoise += Mathf.PerlinNoise(nx * 38f, ny * 14f) * 5f - 2.5f;

                    float effectiveHalfH = 32f + topBottomNoise;
                    float yDist = dy / Mathf.Max(1f, effectiveHalfH);

                    float alpha = 0f;
                    if (nx >= leftBound && nx <= rightBound && yDist < 1f)
                    {
                        // Fade halus pada serat ujung kuas
                        float xProgress = (nx - leftBound) / (rightBound - leftBound);
                        float xFade = 1f;
                        if (xProgress < 0.15f)
                        {
                            float t = xProgress / 0.15f;
                            float bristle = Mathf.PerlinNoise(nx * 50f, ny * 30f);
                            xFade = Mathf.Pow(t, 0.7f) * (0.3f + 0.7f * bristle);
                        }
                        else if (xProgress > 0.85f)
                        {
                            float t = (1f - xProgress) / 0.15f;
                            float bristle = Mathf.PerlinNoise(nx * 50f, ny * 30f);
                            xFade = Mathf.Pow(t, 0.7f) * (0.3f + 0.7f * bristle);
                        }

                        // Tekstur guratan serat kuas horizontal
                        float streak = Mathf.PerlinNoise(nx * 2.5f, ny * 26f);
                        float streakAlpha = Mathf.Lerp(0.85f, 1f, streak);

                        float yFade = Mathf.SmoothStep(1f, 0f, Mathf.Pow(yDist, 3.5f));
                        alpha = yFade * xFade * streakAlpha;
                    }

                    // Cipratan cat kecil di luar kuas
                    if (alpha <= 0.05f && dy < 44f)
                    {
                        float splatter = Mathf.PerlinNoise(nx * 45f + 25f, ny * 45f + 25f);
                        if (splatter > 0.82f && nx > 0.05f && nx < 0.95f)
                        {
                            alpha = (splatter - 0.82f) * 4.2f;
                        }
                    }

                    alpha = Mathf.Clamp01(alpha);
                    pixels[y * width + x] = new Color(0.96f, 0.96f, 0.98f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveAndImportPng(tex, $"{TextureDir}/UI_BrushStrokeTag.png", Vector4.zero);
        }

        private static void GenerateBrushUnderline()
        {
            const int width = 512;
            const int height = 32;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];
            float midY = height * 0.5f;
            float halfH = height * 0.3f;

            for (int y = 0; y < height; y++)
            {
                float dy = Mathf.Abs(y - midY);
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;

                    float edgeNoise = Mathf.PerlinNoise(nx * 24f, ny * 8f) * 4f - 2f;
                    float xFade = Mathf.Sin(nx * Mathf.PI);
                    xFade = Mathf.Pow(Mathf.Clamp01(xFade), 0.5f);

                    float bristle = Mathf.PerlinNoise(nx * 8f, ny * 28f);

                    float yDist = dy / Mathf.Max(1f, halfH + edgeNoise);
                    float alpha = (yDist < 1f) ? Mathf.SmoothStep(1f, 0f, yDist) * xFade * bristle : 0f;

                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveAndImportPng(tex, $"{TextureDir}/UI_BrushLine.png", Vector4.zero);
        }

        private static void GenerateStudioLogo()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float dist = Vector2.Distance(pos, center);
                    float alpha = 0f;

                    // Diamond / Pentagram horror minimalist badge
                    float dx = Mathf.Abs(x - center.x) / (size * 0.45f);
                    float dy = Mathf.Abs(y - center.y) / (size * 0.45f);
                    float diamond = dx + dy;

                    if (diamond <= 1f && diamond >= 0.84f)
                    {
                        alpha = 0.92f;
                    }
                    else if (diamond < 0.84f && diamond > 0.72f)
                    {
                        alpha = 0.35f;
                    }
                    else if (dist < size * 0.16f)
                    {
                        alpha = 0.88f;
                    }

                    pixels[y * size + x] = new Color(0.85f, 0.88f, 0.92f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveAndImportPng(tex, $"{TextureDir}/UI_StudioLogo.png", Vector4.zero);
        }

        private static void GenerateHorrorTitleTexture()
        {
            // Tekstur overlay aksen darah / kuas grunge untuk judul
            const int width = 512;
            const int height = 128;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;

                    // Grunge streaks
                    float grunge = Mathf.PerlinNoise(nx * 14f, ny * 35f);
                    float grunge2 = Mathf.PerlinNoise(nx * 38f, ny * 12f);
                    float val = Mathf.Clamp01(grunge * grunge2 * 1.8f);

                    pixels[y * width + x] = new Color(0.85f, 0.12f, 0.15f, val * 0.55f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveAndImportPng(tex, $"{TextureDir}/UI_HorrorTitle_Grunge.png", Vector4.zero);
        }

        private static void SaveAndImportPng(Texture2D tex, string path, Vector4 border)
        {
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                if (border != Vector4.zero)
                {
                    importer.spriteBorder = border;
                }
                importer.SaveAndReimport();
            }
        }
    }
}
