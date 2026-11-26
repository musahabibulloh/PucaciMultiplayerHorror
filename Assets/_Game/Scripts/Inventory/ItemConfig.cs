using System;
using UnityEngine;

namespace Pucaci.Inventory
{
    /// <summary>
    /// Konfigurasi data & balancing untuk masing-masing item (Kentongan, Keris, Totem, Tulup).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemConfig", menuName = "Pucaci/Inventory/Item Config")]
    public class ItemConfig : ScriptableObject
    {
        [Header("General Item Info")]
        public ItemType itemType;
        public string itemName;
        [TextArea] public string itemDescription;
        public Sprite itemIcon;

        [Header("Balance Settings")]
        [Tooltip("Jeda waktu antar penggunaan (detik)")]
        public float cooldown = 2.0f;

        [Tooltip("Jangkauan serangan atau radius efek (meter)")]
        public float range = 2.5f;

        [Tooltip("Damage yang diberikan pada hantu saat mengenai wujud padat")]
        public float damage = 25f;

        [Tooltip("Kecepatan proyektil (khusus Tulup)")]
        public float projectileSpeed = 22f;

        [Header("Kentongan Specific")]
        [Tooltip("Radius sinyal peringatan kentongan ke player lain (meter)")]
        public float alertRadius = 35f;

        [Tooltip("Durasi banner peringatan muncul di layar player lain (detik)")]
        public float alertDisplayDuration = 4.0f;

        [Header("Visual Prefabs")]
        public GameObject handModelPrefab;
        public GameObject worldPickupPrefab;

        [Header("Audio")]
        public AudioClip useSound;
        public AudioClip hitSound;
        public AudioClip failSound;

        private static ItemConfig[] defaultConfigs;

        public static ItemConfig GetDefaultConfig(ItemType type)
        {
            var config = ScriptableObject.CreateInstance<ItemConfig>();
            config.itemType = type;

            switch (type)
            {
                case ItemType.Kentongan:
                    config.itemName = "Kentongan";
                    config.itemDescription = "Dipukul untuk memperingatkan warga bahwa hantu berada di sekitar.";
                    config.cooldown = 4.0f;
                    config.range = 35f;
                    config.alertRadius = 35f;
                    config.alertDisplayDuration = 4.0f;
                    break;

                case ItemType.Keris:
                    config.itemName = "Keris Pusaka";
                    config.itemDescription = "Senjata pusaka jarak dekat. Melukai hantu saat berwujud padat (tidak berkhasiat saat hantu menghilang).";
                    config.cooldown = 1.2f;
                    config.range = 2.4f;
                    config.damage = 35f;
                    break;

                case ItemType.Totem:
                    config.itemName = "Jimat Perlindungan";
                    config.itemDescription = "Jimat penolak bala. Selama dipegang (equip), pemain kebal dari serangan/cekikan hantu.";
                    config.cooldown = 1.0f;
                    config.range = 0f;
                    config.damage = 0f;
                    break;

                case ItemType.Tulup:
                    config.itemName = "Tulup Bawang Putih";
                    config.itemDescription = "Senjata tiup jarak jauh penembak siung bawang putih ke arah hantu.";
                    config.cooldown = 1.8f;
                    config.range = 40f;
                    config.damage = 25f;
                    config.projectileSpeed = 24f;
                    break;
            }

            return config;
        }

        public static string GetItemDisplayName(ItemType type)
        {
            return type switch
            {
                ItemType.Kentongan => "Kentongan",
                ItemType.Keris => "Keris Pusaka",
                ItemType.Totem => "Jimat Perlindungan",
                ItemType.Tulup => "Tulup Bawang Putih",
                _ => "Item"
            };
        }
    }
}
