using System.Collections.Generic;
using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Menambahkan outline hitam kartunis tebal & tegas (Fortnite / Comic style Inverted Hull) ke Karakter dan Objek 3D.
    /// Menggunakan child renderer terpisah (Duplicate SkinnedMeshRenderer / MeshRenderer) sehingga:
    /// 1. Tidak memodifikasi mesh atau submesh asli sama sekali (0% risiko bug missing texture / warna ungu).
    /// 2. Tidak mengganggu material asli atau mekanik transparan seperti Mode Hilang Kuntilanak.
    /// 3. Mengikuti animasi tulang (bones & skeleton) 100% sinkron secara native di GPU.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToonOutlineApplier : MonoBehaviour
    {
        private static readonly List<ToonOutlineApplier> ActiveInstances = new List<ToonOutlineApplier>();

        [Header("Pengaturan Outline (Fortnite Style)")]
        [Tooltip("Material outline kartunis (Pucaci/ToonMeshOutline)")]
        [SerializeField] private Material outlineMaterial;

        [Range(1.0f, 15.0f)]
        [Tooltip("Ketebalan garis di layar (5.0 - 8.0 adalah standar tegas ala Fortnite / comic anime)")]
        [SerializeField] private float outlineWidth = 7.5f;

        [Tooltip("Warna outline (hitam pekat kartunis)")]
        [SerializeField] private Color outlineColor = Color.black;

        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool includeChildren = true;

        private Material materialInstance;
        private bool isApplied;
        private readonly List<Renderer> outlineRenderers = new List<Renderer>();

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

        private void EnsureMaterial()
        {
            if (materialInstance == null)
            {
                if (outlineMaterial == null)
                {
                    var shader = Shader.Find("Pucaci/ToonMeshOutline");
                    if (shader != null)
                    {
                        materialInstance = new Material(shader);
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

            outlineRenderers.Clear();

            var renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                if (rend.name.Contains("ToonOutline"))
                {
                    rend.sharedMaterial = materialInstance;
                    rend.enabled = true;
                    if (!outlineRenderers.Contains(rend)) outlineRenderers.Add(rend);
                    continue;
                }
                if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer)
                    continue;

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
            // Cek apakah sudah ada child ToonOutline_Backface
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
                    outlineSmr.sharedMesh = smr.sharedMesh;
                    outlineSmr.bones = smr.bones;
                    outlineSmr.rootBone = smr.rootBone;
                    outlineSmr.sharedMaterial = materialInstance;
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
                if (existingChild != null)
                {
                    childMr = existingChild.GetComponent<MeshRenderer>();
                }
                else
                {
                    var outlineGo = new GameObject("ToonOutline_Backface");
                    outlineGo.transform.SetParent(mr.transform, false);
                    outlineGo.transform.localPosition = Vector3.zero;
                    outlineGo.transform.localRotation = Quaternion.identity;
                    outlineGo.transform.localScale = Vector3.one;

                    var childMf = outlineGo.AddComponent<MeshFilter>();
                    childMf.sharedMesh = mf.sharedMesh;
                    childMr = outlineGo.AddComponent<MeshRenderer>();
                }

                if (childMr != null)
                {
                    childMr.sharedMaterial = materialInstance;
                    childMr.enabled = true;
                    if (!outlineRenderers.Contains(childMr)) outlineRenderers.Add(childMr);
                }
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
            outlineRenderers.Clear();
            isApplied = false;
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = Mathf.Clamp(width, 1.0f, 15.0f);
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
        public static ToonOutlineApplier ApplyTo(GameObject target, float width = 7.5f)
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
