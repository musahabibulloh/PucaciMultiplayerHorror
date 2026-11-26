using System;
using System.Collections.Generic;
using UnityEngine;
using Pucaci.Player;

namespace Pucaci.Inventory
{
    /// <summary>
    /// Konfigurasi kapasitas slot inventory per karakter.
    /// Dibuat modular dan dapat diubah tanpa merusak logika gameplay.
    /// Default: 2 slot untuk seluruh karakter, 4 slot khusus Pak Mamat.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterInventoryConfig", menuName = "Pucaci/Inventory/Character Inventory Config")]
    public class CharacterInventoryConfig : ScriptableObject
    {
        [System.Serializable]
        public struct CharacterSlotSetting
        {
            public CharacterType characterType;
            public int maxSlots;
        }

        [Header("Slot Capacity Settings")]
        [Tooltip("Kapasitas default slot inventory untuk karakter biasa")]
        [SerializeField] private int defaultSlotCount = 2;

        [Tooltip("Daftar kustomisasi slot per karakter")]
        [SerializeField] private List<CharacterSlotSetting> characterSlotOverrides = new List<CharacterSlotSetting>
        {
            new CharacterSlotSetting { characterType = CharacterType.PakMamat, maxSlots = 4 },
            new CharacterSlotSetting { characterType = CharacterType.Laras, maxSlots = 2 },
            new CharacterSlotSetting { characterType = CharacterType.PakVincent, maxSlots = 2 },
            new CharacterSlotSetting { characterType = CharacterType.Rocky, maxSlots = 2 }
        };

        private static CharacterInventoryConfig runtimeInstance;

        public static CharacterInventoryConfig Instance
        {
            get
            {
                if (runtimeInstance == null)
                {
                    runtimeInstance = Resources.Load<CharacterInventoryConfig>("CharacterInventoryConfig");
                    if (runtimeInstance == null)
                    {
                        runtimeInstance = CreateInstance<CharacterInventoryConfig>();
                    }
                }
                return runtimeInstance;
            }
        }

        /// <summary>
        /// Mendapatkan jumlah maksimal slot inventory berdasarkan karakter.
        /// Hanya Pak Mamat yang memiliki 4 slot inventory, karakter lainnya (Laras, Vincent, Rocky) memiliki 2 slot.
        /// </summary>
        public int GetMaxSlotsForCharacter(CharacterType characterType)
        {
            if (characterType == CharacterType.PakMamat)
            {
                return 4;
            }

            if (characterSlotOverrides != null)
            {
                for (int i = 0; i < characterSlotOverrides.Count; i++)
                {
                    if (characterSlotOverrides[i].characterType == characterType)
                    {
                        return Mathf.Max(1, characterSlotOverrides[i].maxSlots);
                    }
                }
            }

            return 2;
        }

        public void SetSlotsForCharacter(CharacterType characterType, int slots)
        {
            if (characterSlotOverrides == null) characterSlotOverrides = new List<CharacterSlotSetting>();

            for (int i = 0; i < characterSlotOverrides.Count; i++)
            {
                if (characterSlotOverrides[i].characterType == characterType)
                {
                    var setting = characterSlotOverrides[i];
                    setting.maxSlots = slots;
                    characterSlotOverrides[i] = setting;
                    return;
                }
            }

            characterSlotOverrides.Add(new CharacterSlotSetting
            {
                characterType = characterType,
                maxSlots = slots
            });
        }
    }
}
