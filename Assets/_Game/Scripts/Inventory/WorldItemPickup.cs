using UnityEngine;
using Mirror;

namespace Pucaci.Inventory
{
    /// <summary>
    /// Item yang berada di dunia dan dapat diambil (pickup) oleh pemain dengan menekan tombol [E].
    /// Memiliki efek melayang (hover) dan berputar lembut, serta collider trigger.
    /// Ukuran model 3D dinormalisasi secara presisi agar sesuai dengan skala dunia manusia.
    /// </summary>
    public class WorldItemPickup : NetworkBehaviour
    {
        [Header("Item Data")]
        [SerializeField] private ItemType itemType = ItemType.Kentongan;
        [SerializeField] private float interactRadius = 2.4f;

        [Header("Hover & Rotation Animation")]
        [SerializeField] private float hoverHeight = 0.04f;
        [SerializeField] private float hoverSpeed = 2.0f;
        [SerializeField] private float rotationSpeed = 35f;

        private Transform visualRoot;

        public ItemType ItemType => itemType;
        public float InteractRadius => interactRadius;

        private void Start()
        {
            SetupCollider();
            SetupVisual();
        }

        private void SetupCollider()
        {
            var sphere = GetComponent<SphereCollider>();
            if (sphere == null)
            {
                sphere = gameObject.AddComponent<SphereCollider>();
            }
            sphere.isTrigger = true;
            sphere.radius = interactRadius;
        }

        public void SetupVisual()
        {
            visualRoot = transform.Find("Visual");
            if (visualRoot == null)
            {
                var vObj = new GameObject("Visual");
                vObj.transform.SetParent(transform, false);
                visualRoot = vObj.transform;
            }

            // Cari atau buat model visual anak
            Transform modelTransform = visualRoot.Find("Model");
            if (modelTransform == null && visualRoot.childCount > 0)
            {
                modelTransform = visualRoot.GetChild(0);
            }

            if (modelTransform == null)
            {
                string path = ItemVisualUtility.GetModelAssetPath(itemType);
#if UNITY_EDITOR
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var inst = Instantiate(prefab, visualRoot);
                    inst.name = "Model";
                    modelTransform = inst.transform;
                    foreach (var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
                }
#endif
            }

            if (modelTransform != null)
            {
                ItemVisualUtility.ApplyPickupTransform(modelTransform.gameObject, itemType);
            }

            // Pastikan ada cahaya temaram (glow) penanda item
            var lightObj = visualRoot.Find("ItemGlowLight");
            if (lightObj == null)
            {
                var lo = new GameObject("ItemGlowLight");
                lo.transform.SetParent(visualRoot, false);
                lo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                var l = lo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 2.2f;
                l.intensity = 0.85f;
                l.color = (itemType == ItemType.Totem) ? new Color(0.3f, 0.8f, 1f) :
                          ((itemType == ItemType.Keris) ? new Color(1f, 0.4f, 0.2f) :
                          ((itemType == ItemType.Kentongan) ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 1f, 0.4f)));
            }
        }

        private void Update()
        {
            // Animasi rotasi dan melayang lembut di atas tanah
            if (visualRoot != null)
            {
                visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
                float yOffset = Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
                visualRoot.localPosition = new Vector3(0f, yOffset + 0.30f, 0f);
            }
        }
    }
}
