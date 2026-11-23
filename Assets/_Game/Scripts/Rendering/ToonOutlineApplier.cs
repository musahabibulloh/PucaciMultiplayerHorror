using System.Collections.Generic;
using UnityEngine;

namespace Pucaci.Rendering
{
    /// <summary>
    /// Menambahkan outline hitam kartunis tipis (Inverted Hull) ke Karakter dan Objek 3D.
    /// Tidak mempengaruhi pencahayaan dunia/environment sama sekali, sehingga scene tetap terang dan normal.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToonOutlineApplier : MonoBehaviour
    {
        private static readonly List<ToonOutlineApplier> ActiveInstances = new List<ToonOutlineApplier>();

        [Header("Pengaturan Outline")]
        [Tooltip("Material outline kartunis (Pucaci/ToonMeshOutline)")]
        [SerializeField] private Material outlineMaterial;

        [Range(0.5f, 3.5f)]
        [Tooltip("Ketebalan garis di layar (1.0 - 1.5 sangat tipis dan halus ala anime/kartun)")]
        [SerializeField] private float outlineWidth = 1.2f;

        [Tooltip("Warna outline (hitam kartunis)")]
        [SerializeField] private Color outlineColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool includeChildren = true;

        private Material materialInstance;
        private bool isApplied;

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

            var renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (var rend in renderers)
            {
                // Jangan beri outline pada particle systems, trails, atau lights
                if (rend is ParticleSystemRenderer || rend is TrailRenderer || rend is LineRenderer)
                    continue;

                var mats = rend.sharedMaterials;
                bool alreadyHas = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null && mats[i].shader.name == "Pucaci/ToonMeshOutline")
                    {
                        alreadyHas = true;
                        break;
                    }
                }

                if (!alreadyHas)
                {
                    var newMats = new Material[mats.Length + 1];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        newMats[i] = mats[i];
                    }
                    newMats[newMats.Length - 1] = materialInstance;
                    rend.sharedMaterials = newMats;
                }
            }

            isApplied = true;
        }

        public void RemoveOutline()
        {
            if (!isApplied) return;

            var renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (var rend in renderers)
            {
                if (rend == null) continue;

                var mats = rend.sharedMaterials;
                var list = new List<Material>();
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null && mats[i].shader.name == "Pucaci/ToonMeshOutline")
                    {
                        continue;
                    }
                    list.Add(mats[i]);
                }

                if (list.Count != mats.Length)
                {
                    rend.sharedMaterials = list.ToArray();
                }
            }

            isApplied = false;
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = Mathf.Clamp(width, 0.2f, 4.0f);
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
        /// Helper statis untuk menambahkan ToonOutline ke GameObject apapun (karakter atau props).
        /// </summary>
        public static ToonOutlineApplier ApplyTo(GameObject target, float width = 1.2f)
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
