using System.Collections.Generic;
using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Menambahkan outline hitam kartunis tebal & tegas (Fortnite style Inverted Hull) ke Karakter dan Objek 3D.
    /// Menggunakan teknik duplikasi submesh runtime sehingga 100% diproses oleh pipeline rendering Unity
    /// untuk SkinnedMeshRenderer (animasi tulang) dan MeshRenderer statis.
    /// Tidak mempengaruhi pencahayaan dunia/environment sama sekali.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToonOutlineApplier : MonoBehaviour
    {
        private static readonly List<ToonOutlineApplier> ActiveInstances = new List<ToonOutlineApplier>();

        private struct OriginalRendererState
        {
            public Mesh originalMesh;
            public Material[] originalMaterials;
        }

        [Header("Pengaturan Outline (Fortnite Style)")]
        [Tooltip("Material outline kartunis (Pucaci/ToonMeshOutline)")]
        [SerializeField] private Material outlineMaterial;

        [Range(1.0f, 8.0f)]
        [Tooltip("Ketebalan garis di layar (3.0 - 5.0 adalah standar tegas ala Fortnite / comic anime)")]
        [SerializeField] private float outlineWidth = 3.5f;

        [Tooltip("Warna outline (hitam pekat kartunis)")]
        [SerializeField] private Color outlineColor = Color.black;

        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool includeChildren = true;

        private Material materialInstance;
        private bool isApplied;
        private readonly Dictionary<Renderer, OriginalRendererState> originalStates = new Dictionary<Renderer, OriginalRendererState>();

        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

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

        private void Awake()
        {
            EnsureMaterial();
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
                Destroy(materialInstance);
            }
        }

        private void EnsureMaterial()
        {
            if (materialInstance == null)
            {
                if (outlineMaterial == null)
                {
                    var mat = Resources.Load<Material>("M_ToonOutline");
                    if (mat == null)
                    {
                        var shader = Shader.Find("Pucaci/ToonMeshOutline");
                        if (shader != null)
                        {
                            materialInstance = new Material(shader);
                        }
                    }
                    else
                    {
                        materialInstance = new Material(mat);
                    }
                }
                else
                {
                    materialInstance = new Material(outlineMaterial);
                }
            }

            if (materialInstance != null)
            {
                materialInstance.SetFloat(OutlineWidthId, outlineWidth);
                materialInstance.SetColor(OutlineColorId, outlineColor);
            }
        }

        public void ApplyOutline()
        {
            EnsureMaterial();
            if (materialInstance == null) return;

            var renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer)
                    continue;

                ApplyToRenderer(rend);
            }

            isApplied = true;
        }

        private void ApplyToRenderer(Renderer rend)
        {
            if (rend is SkinnedMeshRenderer smr)
            {
                var origMesh = smr.sharedMesh;
                if (origMesh == null) return;

                // Jika sudah ada state tersimpan, jangan timpa dengan mesh kloningan
                if (!originalStates.ContainsKey(rend))
                {
                    originalStates[rend] = new OriginalRendererState
                    {
                        originalMesh = origMesh,
                        originalMaterials = smr.sharedMaterials
                    };
                }

                int origSubmeshCount = origMesh.subMeshCount;
                var clonedMesh = Instantiate(origMesh);
                clonedMesh.name = origMesh.name + "_ToonOutlined";
                clonedMesh.subMeshCount = origSubmeshCount + 1;

                var allTris = new List<int>();
                for (int i = 0; i < origSubmeshCount; i++)
                {
                    allTris.AddRange(origMesh.GetTriangles(i));
                }
                clonedMesh.SetTriangles(allTris, origSubmeshCount);
                smr.sharedMesh = clonedMesh;

                var origMats = originalStates[rend].originalMaterials;
                var newMats = new Material[origSubmeshCount + 1];
                for (int i = 0; i < origSubmeshCount; i++)
                {
                    newMats[i] = (i < origMats.Length && origMats[i] != null) ? origMats[i] : origMats[0];
                }
                newMats[origSubmeshCount] = materialInstance;
                smr.sharedMaterials = newMats;
            }
            else if (rend is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return;

                var origMesh = mf.sharedMesh;
                if (!originalStates.ContainsKey(rend))
                {
                    originalStates[rend] = new OriginalRendererState
                    {
                        originalMesh = origMesh,
                        originalMaterials = mr.sharedMaterials
                    };
                }

                int origSubmeshCount = origMesh.subMeshCount;
                var clonedMesh = Instantiate(origMesh);
                clonedMesh.name = origMesh.name + "_ToonOutlined";
                clonedMesh.subMeshCount = origSubmeshCount + 1;

                var allTris = new List<int>();
                for (int i = 0; i < origSubmeshCount; i++)
                {
                    allTris.AddRange(origMesh.GetTriangles(i));
                }
                clonedMesh.SetTriangles(allTris, origSubmeshCount);
                mf.sharedMesh = clonedMesh;

                var origMats = originalStates[rend].originalMaterials;
                var newMats = new Material[origSubmeshCount + 1];
                for (int i = 0; i < origSubmeshCount; i++)
                {
                    newMats[i] = (i < origMats.Length && origMats[i] != null) ? origMats[i] : origMats[0];
                }
                newMats[origSubmeshCount] = materialInstance;
                mr.sharedMaterials = newMats;
            }
        }

        public void RemoveOutline()
        {
            if (!isApplied) return;

            foreach (var kvp in originalStates)
            {
                var rend = kvp.Key;
                var state = kvp.Value;
                if (rend == null) continue;

                if (rend is SkinnedMeshRenderer smr)
                {
                    var currentMesh = smr.sharedMesh;
                    smr.sharedMesh = state.originalMesh;
                    smr.sharedMaterials = state.originalMaterials;
                    if (currentMesh != null && currentMesh != state.originalMesh)
                    {
                        Destroy(currentMesh);
                    }
                }
                else if (rend is MeshRenderer mr)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf != null)
                    {
                        var currentMesh = mf.sharedMesh;
                        mf.sharedMesh = state.originalMesh;
                        if (currentMesh != null && currentMesh != state.originalMesh)
                        {
                            Destroy(currentMesh);
                        }
                    }
                    mr.sharedMaterials = state.originalMaterials;
                }
            }

            originalStates.Clear();
            isApplied = false;
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = Mathf.Clamp(width, 0.5f, 10.0f);
            if (materialInstance != null)
            {
                materialInstance.SetFloat(OutlineWidthId, outlineWidth);
            }
        }

        public void SetOutlineColor(Color color)
        {
            outlineColor = color;
            if (materialInstance != null)
            {
                materialInstance.SetColor(OutlineColorId, outlineColor);
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
        public static ToonOutlineApplier ApplyTo(GameObject target, float width = 3.5f)
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
