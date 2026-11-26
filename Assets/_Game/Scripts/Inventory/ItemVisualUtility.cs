using UnityEngine;

namespace Pucaci.Inventory
{
    /// <summary>
    /// Helper utilitas untuk normalisasi ukuran dan orientasi model 3D item (Kentongan, Keris, Totem, Tulup, Sesajen).
    /// Memastikan ukuran item di dunia (pickup) maupun di tangan pemain memiliki skala manusia yang realistis.
    /// Dihitung secara matematis dari mesh bounds masing-masing FBX.
    /// </summary>
    public static class ItemVisualUtility
    {
        // 1. Kentongan: mesh height = 17.00m, center Y = 8.61m
        // Target pickup: ~0.36m; Target hand: ~0.30m (pegangan tangan kanan)
        public static readonly Vector3 KentonganPickupScale = Vector3.one * 0.021f;
        public static readonly Vector3 KentonganPickupOffset = new Vector3(0.015f, -0.180f, 0f);
        public static readonly Vector3 KentonganHandScale = Vector3.one * 0.0175f;
        public static readonly Vector3 KentonganHandPos = new Vector3(0.03f, 0.05f, 0.02f);
        public static readonly Vector3 KentonganHandRot = new Vector3(80f, -20f, -85f); // Tangan kanan menghadap ke depan

        // 2. Keris: mesh length = 31.19m, center Y = 9.74m
        // Target pickup: ~0.40m; Target hand: ~0.36m (bilah keris mengarah ke depan/atas)
        public static readonly Vector3 KerisPickupScale = Vector3.one * 0.0128f;
        public static readonly Vector3 KerisPickupOffset = new Vector3(0.005f, -0.125f, 0f);
        public static readonly Vector3 KerisHandScale = Vector3.one * 0.0115f;
        public static readonly Vector3 KerisHandPos = new Vector3(0.02f, 0.07f, 0.04f);
        public static readonly Vector3 KerisHandRot = new Vector3(-85f, 0f, 0f); // Bilah mengarah ke depan

        // 3. Totem: mesh max dim = 11.92m, center = (0.23, 0.67, -1.51)
        // Target pickup: ~0.22m; Target hand: ~0.18m (di telapak tangan)
        public static readonly Vector3 TotemPickupScale = Vector3.one * 0.0185f;
        public static readonly Vector3 TotemPickupOffset = new Vector3(-0.005f, -0.012f, 0.025f);
        public static readonly Vector3 TotemHandScale = Vector3.one * 0.0150f;
        public static readonly Vector3 TotemHandPos = new Vector3(0.01f, 0.04f, 0.02f);
        public static readonly Vector3 TotemHandRot = new Vector3(0f, -90f, 0f);

        // 4. Tulup: mesh length = 9.41m, center Y = 3.70m
        // Target pickup: ~0.62m; Target hand: ~0.55m (pipa tiup menghadap ke depan)
        public static readonly Vector3 TulupPickupScale = Vector3.one * 0.065f;
        public static readonly Vector3 TulupPickupOffset = new Vector3(0f, -0.240f, 0f);
        public static readonly Vector3 TulupHandScale = Vector3.one * 0.058f;
        public static readonly Vector3 TulupHandPos = new Vector3(0.02f, 0.08f, 0.12f);
        public static readonly Vector3 TulupHandRot = new Vector3(-80f, 0f, 0f); // Pipa menghadap ke depan

        // 5. Sesajen Baru (SESAJEN.fbx): Bounds size = (49.95, 34.72, 57.62)
        // Target diameter tampah di tanah: ~0.68m
        // Scale yang pas = 0.012f
        // Rotasi (0f, 0f, 31.51f) dan offset (-0.20f, 0f, 0f) membuat tampah 100% rata dan menempel pas di atas tanah (Y = 0)
        public static readonly Vector3 SesajenWorldScale = Vector3.one * 0.012f;
        public static readonly Vector3 SesajenWorldOffset = new Vector3(-0.20f, 0f, 0f);
        public static readonly Vector3 SesajenWorldRot = new Vector3(0f, 0f, 31.51f);

        public static string GetModelAssetPath(ItemType type)
        {
            return type switch
            {
                ItemType.Kentongan => "Assets/_Game/Models/Items/Kentongan/Kentongan.fbx",
                ItemType.Keris => "Assets/_Game/Models/Items/Keris/Keris.fbx",
                ItemType.Totem => "Assets/_Game/Models/Items/TOTEM/botjo totem.fbx",
                ItemType.Tulup => "Assets/_Game/Models/Items/Tulup/Tulup.fbx",
                _ => ""
            };
        }

        public static void ApplyPickupTransform(GameObject modelObj, ItemType type)
        {
            if (modelObj == null) return;
            switch (type)
            {
                case ItemType.Kentongan:
                    modelObj.transform.localScale = KentonganPickupScale;
                    modelObj.transform.localPosition = KentonganPickupOffset;
                    modelObj.transform.localRotation = Quaternion.identity;
                    break;
                case ItemType.Keris:
                    modelObj.transform.localScale = KerisPickupScale;
                    modelObj.transform.localPosition = KerisPickupOffset;
                    modelObj.transform.localRotation = Quaternion.identity;
                    break;
                case ItemType.Totem:
                    modelObj.transform.localScale = TotemPickupScale;
                    modelObj.transform.localPosition = TotemPickupOffset;
                    modelObj.transform.localRotation = Quaternion.identity;
                    break;
                case ItemType.Tulup:
                    modelObj.transform.localScale = TulupPickupScale;
                    modelObj.transform.localPosition = TulupPickupOffset;
                    modelObj.transform.localRotation = Quaternion.identity;
                    break;
            }
        }

        public static void ApplyHandTransform(GameObject modelObj, ItemType type)
        {
            if (modelObj == null) return;
            switch (type)
            {
                case ItemType.Kentongan:
                    modelObj.transform.localScale = KentonganHandScale;
                    modelObj.transform.localPosition = KentonganHandPos;
                    modelObj.transform.localRotation = Quaternion.Euler(KentonganHandRot);
                    break;
                case ItemType.Keris:
                    modelObj.transform.localScale = KerisHandScale;
                    modelObj.transform.localPosition = KerisHandPos;
                    modelObj.transform.localRotation = Quaternion.Euler(KerisHandRot);
                    break;
                case ItemType.Totem:
                    modelObj.transform.localScale = TotemHandScale;
                    modelObj.transform.localPosition = TotemHandPos;
                    modelObj.transform.localRotation = Quaternion.Euler(TotemHandRot);
                    break;
                case ItemType.Tulup:
                    modelObj.transform.localScale = TulupHandScale;
                    modelObj.transform.localPosition = TulupHandPos;
                    modelObj.transform.localRotation = Quaternion.Euler(TulupHandRot);
                    break;
            }
        }

        public static void ApplySesajenTransform(GameObject sesajenModelObj)
        {
            if (sesajenModelObj == null) return;
            sesajenModelObj.transform.localScale = SesajenWorldScale;
            sesajenModelObj.transform.localPosition = SesajenWorldOffset;
            sesajenModelObj.transform.localRotation = Quaternion.Euler(SesajenWorldRot);
        }
    }
}
