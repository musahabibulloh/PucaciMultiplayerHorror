using System.Collections.Generic;
using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Menambahkan outline hitam kartunis tebal & tegas (Fortnite / Comic style Inverted Hull) ke Karakter dan Objek 3D.
    /// Menggunakan duplikat mesh dengan normal extrusion dan Cull Front sehingga:
    /// 1. Menggunakan material bawaan yang 100% kompatibel di Windows Standalone Build (tidak akan pernah hilang/distrip oleh Unity).
    /// 2. Mengikuti animasi tulang Mixamo (bones & skeleton) 100% sinkron secara native di GPU skinning.
    /// 3. Menghasilkan garis tepi siluet hitam yang solid, pekat, dan tajam ala kartun/komik.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToonOutlineApplier : MonoBehaviour
    {
        private static readonly List<ToonOutlineApplier> ActiveInstances = new List<ToonOutlineApplier>();

        [Header("Pengaturan Outline (Fortnite / Comic Style)")]
        [Range(1.0f, 15.0f)]
        [Tooltip("Ketebalan garis di layar (3.5 - 5.5 adalah standar tegas ala comic anime / cartoon)")]
        [SerializeField] private float outlineWidth = 4.8f;

        [Tooltip("Warna outline (hitam pekat kartunis)")]
        [SerializeField] private Color outlineColor = Color.black;

        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool includeChildren = true;

        private Material materialInstance;
        private bool isApplied;
        private readonly List<Renderer> outlineRenderers = new List<Renderer>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();

        public static IReadOnlyList<ToonOutlineApplier> All => ActiveInstances;

        private void OnEnable()
        {
            if (!ActiveInstances.Contains(this))
            {
                ActiveInstances.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveInstances.Remove(this);
        }

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyOutline();
            }
        }

        private void OnDestroy()
        {
            RemoveOutline();
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(materialInstance);
                }
                else
                {
                    DestroyImmediate(materialInstance);
                }
            }
        }

        private void EnsureMaterial(Renderer sourceRenderer = null)
        {
            if (materialInstance == null)
            {
                Shader uShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (uShader != null)
                {
                    materialInstance = new Material(uShader);
                }
                else if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
                {
                    materialInstance = new Material(sourceRenderer.sharedMaterial);
                }
            }

            if (materialInstance != null)
            {
                materialInstance.name = "M_ToonOutline_Solid";
                // Hilangkan tekstur diffuse agar outline hitam solid
                if (materialInstance.HasProperty("_BaseMap")) materialInstance.SetTexture("_BaseMap", null);
                if (materialInstance.HasProperty("_MainTex")) materialInstance.SetTexture("_MainTex", null);
                if (materialInstance.HasProperty("_BaseColor")) materialInstance.SetColor("_BaseColor", outlineColor);
                if (materialInstance.HasProperty("_Color")) materialInstance.SetColor("_Color", outlineColor);
                
                // Cull Front agar hanya poligon belakang (backface) yang tampak melebar mengelilingi karakter
                materialInstance.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
                materialInstance.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
                materialInstance.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 10;
            }
        }

        public void ApplyOutline()
        {
            outlineRenderers.Clear();

            var renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                if (rend.name.Contains("ToonOutline"))
                {
                    rend.enabled = true;
                    if (!outlineRenderers.Contains(rend)) outlineRenderers.Add(rend);
                    continue;
                }
                if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer)
                    continue;

                // Abaikan tanah, air, langit agar tidak ada garis kotak di tanah desa
                string nameLower = rend.gameObject.name.ToLower();
                if (nameLower.Contains("plane") || nameLower.Contains("terrain") || nameLower.Contains("ground") || nameLower.Contains("water") || nameLower.Contains("sky"))
                    continue;

                if (rend.CompareTag("Ground"))
                    continue;

                CreateOrUpdateOutlineForRenderer(rend);
            }

            isApplied = true;
        }

        private void CreateOrUpdateOutlineForRenderer(Renderer rend)
        {
            EnsureMaterial(rend);
            if (materialInstance == null) return;

            Transform existingChild = rend.transform.Find("ToonOutline_Backface");

            if (rend is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) return;

                SkinnedMeshRenderer outlineSmr;
                if (existingChild != null)
                {
                    outlineSmr = existingChild.GetComponent<SkinnedMeshRenderer>();
                }
                else
                {
                    var outlineGo = new GameObject("ToonOutline_Backface");
                    outlineGo.transform.SetParent(smr.transform, false);
                    outlineGo.transform.localPosition = Vector3.zero;
                    outlineGo.transform.localRotation = Quaternion.identity;
                    outlineGo.transform.localScale = Vector3.one;
                    outlineSmr = outlineGo.AddComponent<SkinnedMeshRenderer>();
                }

                if (outlineSmr != null)
                {
                    // Buat mesh outline terekstrusi
                    if (outlineSmr.sharedMesh == null || !outlineSmr.sharedMesh.name.EndsWith("_ToonOutline"))
                    {
                        outlineSmr.sharedMesh = CreateExtrudedMesh(smr.sharedMesh, outlineWidth);
                    }
                    outlineSmr.bones = smr.bones;
                    outlineSmr.rootBone = smr.rootBone;

                    int matCount = Mathf.Max(1, smr.sharedMaterials != null ? smr.sharedMaterials.Length : 1);
                    var mats = new Material[matCount];
                    for (int m = 0; m < matCount; m++) mats[m] = materialInstance;
                    outlineSmr.sharedMaterials = mats;

                    outlineSmr.updateWhenOffscreen = smr.updateWhenOffscreen;
                    outlineSmr.enabled = true;
                    if (!outlineRenderers.Contains(outlineSmr)) outlineRenderers.Add(outlineSmr);
                }
            }
            else if (rend is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return;

                MeshRenderer childMr;
                MeshFilter childMf;
                if (existingChild != null)
                {
                    childMr = existingChild.GetComponent<MeshRenderer>();
                    childMf = existingChild.GetComponent<MeshFilter>();
                }
                else
                {
                    var outlineGo = new GameObject("ToonOutline_Backface");
                    outlineGo.transform.SetParent(mr.transform, false);
                    outlineGo.transform.localPosition = Vector3.zero;
                    outlineGo.transform.localRotation = Quaternion.identity;
                    // Skala sedikit melebar untuk props statis desa (tanpa butuh CPU vertex extrusion, 100% aman di build)
                    outlineGo.transform.localScale = Vector3.one * (1.0f + Mathf.Clamp(outlineWidth, 1.0f, 15.0f) * 0.003f);

                    childMf = outlineGo.AddComponent<MeshFilter>();
                    childMr = outlineGo.AddComponent<MeshRenderer>();
                }

                if (childMf != null)
                {
                    childMf.sharedMesh = mf.sharedMesh;
                }

                if (childMr != null)
                {
                    int matCount = Mathf.Max(1, mr.sharedMaterials != null ? mr.sharedMaterials.Length : 1);
                    var mats = new Material[matCount];
                    for (int m = 0; m < matCount; m++) mats[m] = materialInstance;
                    childMr.sharedMaterials = mats;
                    childMr.enabled = true;
                    if (!outlineRenderers.Contains(childMr)) outlineRenderers.Add(childMr);
                }
            }
        }

        private Mesh CreateExtrudedMesh(Mesh sourceMesh, float width)
        {
            if (sourceMesh == null) return null;

            try
            {
                if (!sourceMesh.isReadable)
                {
                    return sourceMesh;
                }

                var outlineMesh = Instantiate(sourceMesh);
                outlineMesh.name = sourceMesh.name + "_ToonOutline";

                var verts = outlineMesh.vertices;
                var norms = outlineMesh.normals;
                if (norms == null || norms.Length != verts.Length)
                {
                    outlineMesh.RecalculateNormals();
                    norms = outlineMesh.normals;
                }

                float offset = Mathf.Clamp(width, 1.0f, 15.0f) * 0.0035f;
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] += norms[i] * offset;
                }

                outlineMesh.vertices = verts;
                outlineMesh.boneWeights = sourceMesh.boneWeights;
                outlineMesh.bindposes = sourceMesh.bindposes;
                outlineMesh.RecalculateBounds();

                generatedMeshes.Add(outlineMesh);
                return outlineMesh;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ToonOutline] Failed to extrude mesh {sourceMesh.name}: {ex.Message}");
                return sourceMesh;
            }
        }

        public void SetOutlineEnabled(bool enabledState)
        {
            foreach (var r in outlineRenderers)
            {
                if (r != null)
                {
                    r.enabled = enabledState;
                }
            }
        }

        public void RemoveOutline()
        {
            var children = GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child != null && child.name.Contains("ToonOutline"))
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            foreach (var m in generatedMeshes)
            {
                if (m != null)
                {
                    if (Application.isPlaying) Destroy(m);
                    else DestroyImmediate(m);
                }
            }
            generatedMeshes.Clear();

            outlineRenderers.Clear();
            isApplied = false;
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = Mathf.Clamp(width, 1.0f, 15.0f);
            ApplyOutline();
        }

        public void SetOutlineColor(Color color)
        {
            outlineColor = color;
            if (materialInstance != null)
            {
                if (materialInstance.HasProperty("_BaseColor")) materialInstance.SetColor("_BaseColor", outlineColor);
                if (materialInstance.HasProperty("_Color")) materialInstance.SetColor("_Color", outlineColor);
            }
        }

        public void ToggleOutline()
        {
            if (isApplied)
            {
                RemoveOutline();
            }
            else
            {
                ApplyOutline();
            }
        }

        /// <summary>
        /// Helper statis untuk menambahkan ToonOutline tegas ke GameObject apapun (karakter atau props).
        /// </summary>
        public static ToonOutlineApplier ApplyTo(GameObject target, float width = 4.8f)
        {
            if (target == null) return null;

            var applier = target.GetComponent<ToonOutlineApplier>();
            if (applier == null)
            {
                applier = target.AddComponent<ToonOutlineApplier>();
            }

            applier.outlineWidth = width;
            applier.ApplyOutline();
            return applier;
        }
    }
}
