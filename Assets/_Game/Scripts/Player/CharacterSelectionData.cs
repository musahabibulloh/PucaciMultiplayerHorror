using UnityEngine;

namespace Pucaci.Player
{
    public enum CharacterType
    {
        Laras = 0,
        PakMamat = 1,
        Kuntilanak = 2,
        PakVincent = 3
    }

    public static class CharacterSelectionData
    {
        public const string CharacterPrefKey = "Pucaci_SelectedCharacter";

        public static CharacterType CurrentCharacter
        {
            get => (CharacterType)PlayerPrefs.GetInt(CharacterPrefKey, 0);
            set
            {
                PlayerPrefs.SetInt(CharacterPrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string GetCharacterName(CharacterType type)
        {
            return type switch
            {
                CharacterType.Laras => "Laras",
                CharacterType.PakMamat => "Pak Mamat",
                CharacterType.Kuntilanak => "Kuntilanak",
                CharacterType.PakVincent => "Pak Vincent",
                _ => "Laras"
            };
        }

        public static string GetCharacterRole(CharacterType type)
        {
            return type switch
            {
                CharacterType.Laras => "Mahasiswi KKN",
                CharacterType.PakMamat => "Hansip Kampung",
                CharacterType.Kuntilanak => "Hantu Penunggu",
                CharacterType.PakVincent => "Penyelidik Desa",
                _ => "Warga Kampung"
            };
        }

        public static string GetCharacterDescription(CharacterType type)
        {
            return type switch
            {
                CharacterType.Laras => "Gadis pemberani dan lincah. Memiliki reflek gesit untuk melarikan diri dari kejaran dedemit kampung.",
                CharacterType.PakMamat => "Penjaga malam senior kampung. Kenyang pengalaman mistis dan memiliki langkah mantap penuh waspada.",
                CharacterType.Kuntilanak => "Sosok hantu wanita bergaun putih dengan rambut panjang terurai. Melayang tenang di kegelapan malam desa.",
                CharacterType.PakVincent => "Pria tenang dan penuh perhitungan. Memiliki langkah terukur untuk menyelidiki misteri dan bertahan dari teror malam desa.",
                _ => ""
            };
        }
    }
}
