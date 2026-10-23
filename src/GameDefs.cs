// Everything that differs between Koikatsu Sunshine (default build) and Koikatsu / Koikatsu Party (built with /define:KK).
using System.Collections.Generic;

namespace StudioCharaFilter
{
    internal static class GameDefs
    {
#if KK
        public const string GameName = "Koikatsu";
        public const string DllName = "KK_StudioCharaFilter.dll";

        // ChaFileDefine coordinate order in Koikatsu.
        public static readonly string[] OutfitNames = { "School uniform", "Going home", "Gym clothes", "Swimsuit", "Club", "Casual", "Pajamas" };

        public static ChaListControl ChaList
        {
            get { return Singleton<Manager.Character>.Instance != null ? Singleton<Manager.Character>.Instance.chaListCtrl : null; }
        }

        public static IEnumerable<KeyValuePair<int, VoiceInfo.Param>> VoiceTable
        {
            get { return Singleton<Manager.Voice>.Instance != null ? Singleton<Manager.Voice>.Instance.voiceInfoDic : null; }
        }

        // Koikatsu only loads "【KoiKatuChara】" cards with product number <= 100. Party ("SP") cards use the same format and
        // load with the usual compatibility fixes, so they're indexed but flagged so the game still decides when listing a folder.
        public static bool AcceptCard(int productNo, string marker, out bool oddMarker)
        {
            oddMarker = marker != "【KoiKatuChara】";
            return productNo <= 100 && (marker == "【KoiKatuChara】" || marker == "【KoiKatuCharaSP】");
        }
#else
        public const string GameName = "Koikatsu Sunshine";
        public const string DllName = "KKS_StudioCharaFilter.dll";

        public static readonly string[] OutfitNames = { "Casual", "Swimsuit", "Pajamas", "Bathing" };

        public static ChaListControl ChaList
        {
            get { return Manager.Character.chaListCtrl; }
        }

        public static IEnumerable<KeyValuePair<int, VoiceInfo.Param>> VoiceTable
        {
            get { return Manager.Voice.infoTable; }
        }

        // Sunshine loads its own cards and converts Koikatsu / Party / EmotionCreators-era Koikatsu cards.
        public static bool AcceptCard(int productNo, string marker, out bool oddMarker)
        {
            oddMarker = false;
            return marker.IndexOf("KoiKatuChara", System.StringComparison.Ordinal) >= 0;
        }
#endif

        // Number of outfits every card has; More Outfits adds slots after these.
        public static int BaseOutfits { get { return OutfitNames.Length; } }
    }
}
