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

        [Range(1.0f, 10.0f)]
        [Tooltip("Ketebalan garis di layar (3.5 - 5.0 adalah standar tegas ala Fortnite / comic anime)")]
        [SerializeField] private float outlineWidth = 4.0f;

        [Tooltip("Warna outline (hitam pekat kartunis)")]
        [SerializeField] private Color outlineColor = Color.black;

        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool includeChildren = true;

        private Material materialInstance;
        private bool isApplied;
        private readonly List<GameObject> spawnedOutlineObjects = new List<GameObject>();

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
            if (applyOnStart && !isApplied)
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

            RemoveOutline();

            var renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                if (rend.name.Contains("ToonOutline")) continue;
                if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer)
                    continue;

                CreateOutlineForRenderer(rend);
            }

            isApplied = true;
        }

        private void CreateOutlineForRenderer(Renderer rend)
        {
            if (rend is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) return;

                var outlineGo = new GameObject("ToonOutline_Backface");
                outlineGo.transform.SetParent(smr.transform, false);
                outlineGo.transform.localPosition = Vector3.zero;
                outlineGo.transform.localRotation = Quaternion.identity;
                outlineGo.transform.localScale = Vector3.one;

                var outlineSmr = outlineGo.AddComponent<SkinnedMeshRenderer>();
                outlineSmr.sharedMesh = smr.sharedMesh;
                outlineSmr.bones = smr.bones;
                outlineSmr.rootBone = smr.rootBone;
                outlineSmr.sharedMaterial = materialInstance;
                outlineSmr.updateWhenOffscreen = smr.updateWhenOffscreen;

                spawnedOutlineObjects.Add(outlineGo);
            }
            else if (rend is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return;

                var outlineGo = new GameObject("ToonOutline_Backface");
                outlineGo.transform.SetParent(mr.transform, false);
                outlineGo.transform.localPosition = Vector3.zero;
                outlineGo.transform.localRotation = Quaternion.identity;
                outlineGo.transform.localScale = Vector3.one;

                var childMf = outlineGo.AddComponent<MeshFilter>();
                childMf.sharedMesh = mf.sharedMesh;

                var childMr = outlineGo.AddComponent<MeshRenderer>();
                childMr.sharedMaterial = materialInstance;

                spawnedOutlineObjects.Add(outlineGo);
            }
        }

        public void RemoveOutline()
        {
            foreach (var go in spawnedOutlineObjects)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }
            spawnedOutlineObjects.Clear();
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
        public static ToonOutlineApplier ApplyTo(GameObject target, float width = 4.0f)
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
