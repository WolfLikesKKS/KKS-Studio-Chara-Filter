// Zipmod content on cards (from Sideloader's per-card resolve list): body regions, mod info,
// and whether each zipmod is installed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace StudioCharaFilter
{
    internal sealed class ModMeta
    {
        public string Name = "";
        public string Author = "";
        public string Website = "";
    }

    internal static class Regions
    {
        public const int Count = 9;
        public static readonly string[] Names =
        {
            "Hair", "Head", "Face", "Neck & chest", "Body & waist", "Arms & hands", "Legs", "Clothing", "Other"
        };
        public const int Hair = 0, Head = 1, Face = 2, NeckChest = 3, Body = 4, ArmsHands = 5, Legs = 6, Clothing = 7, Other = 8;

        // Category numbers from ChaListDefine.CategoryNo.
        public static int Classify(int category, string modId, string name)
        {
            bool accessory = category >= 120 && category <= 130;
            // Hair-as-accessory mods (e.g. "Accessory Hair Front") matter as much as real hairstyles.
            if (accessory && (Contains(modId, "hair") || Contains(name, "hair"))) return Hair;
            switch (category)
            {
                case 101: case 102: case 103: case 104: case 121: case 433: return Hair;
                case 100: case 122: return Head;
                case 123: return Face;
                case 124: case 423: return NeckChest;
                case 125: case 126: case 130: case 420: case 421: case 422: case 424: return Body;
                case 128: case 129: return ArmsHands;
                case 127: return Legs;
            }
            if (category >= 400 && category <= 416) return Face;           // face/eye/makeup textures
            if (category >= 105 && category <= 112) return Clothing;        // co_top .. co_shoes
            if (category >= 200 && category <= 215) return Clothing;        // uniform parts
            if (category == 430 || category == 431) return Clothing;        // cloth patterns / emblems
            return Other;
        }

        private static bool Contains(string s, string what)
        {
            return s != null && s.IndexOf(what, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // Readable names for Uncensor Selector bodies, read from the installed uncensors (best effort).
    internal static class UncensorNames
    {
        private static Dictionary<string, string> _names;

        public static string Get(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "Default (none chosen on card)";
            if (_names == null) Load();
            string n;
            return _names.TryGetValue(guid, out n) && !string.IsNullOrEmpty(n) ? n : guid;
        }

        private static void Load()
        {
            _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Type t = AccessTools.TypeByName("KK_Plugins.UncensorSelector");
                if (t == null) return;
                object dict = null;
                FieldInfo f = AccessTools.Field(t, "BodyDictionary");
                if (f != null) dict = f.GetValue(f.IsStatic ? null : UnityEngine.Object.FindObjectOfType(t));
                if (dict == null)
                {
                    PropertyInfo p = AccessTools.Property(t, "BodyDictionary");
                    if (p != null) dict = p.GetValue(null, null);
                }
                IDictionary d = dict as IDictionary;
                if (d == null) return;
                foreach (DictionaryEntry e in d)
                {
                    object body = e.Value;
                    if (body == null) continue;
                    string name = null;
                    FieldInfo nf = AccessTools.Field(body.GetType(), "DisplayName");
                    if (nf != null) name = nf.GetValue(body) as string;
                    else
                    {
                        PropertyInfo np = AccessTools.Property(body.GetType(), "DisplayName");
                        if (np != null) name = np.GetValue(body, null) as string;
                    }
                    _names[e.Key.ToString()] = name;
                }
            }
            catch (Exception ex)
            {
                StudioCharaFilterPlugin.Log.LogDebug("[SCF] Uncensor names unavailable: " + ex.Message);
            }
        }
    }

    internal static class TowelNames
    {
        private static readonly Dictionary<int, bool> Cache = new Dictionary<int, bool>();

        public static bool IsTowelName(string name)
        {
            return name != null && (name.IndexOf("towel", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("\u30BF\u30AA\u30EB", StringComparison.Ordinal) >= 0);
        }

        // Is this outfit's top a bath towel? Vanilla ids are looked up in the game's item list (main thread only).
        public static bool IsTowel(int topId)
        {
            if (topId == -2) return true;
            if (topId <= 0) return false;
            bool result;
            if (Cache.TryGetValue(topId, out result)) return result;
            result = false;
            try
            {
                ListInfoBase info = GameDefs.ChaList.GetListInfo(ChaListDefine.CategoryNo.co_top, topId);
                if (info != null) result = IsTowelName(info.Name);
            }
            catch (Exception) { }
            Cache[topId] = result;
            return result;
        }

        // Outfit slot state including towels: 0 nothing, 1 underwear, 2 dressed, 3 towel.
        public const int Nothing = 0, Underwear = 1, Dressed = 2, Towel = 3;

        public static int EffectiveState(CardInfo ci, int slot)
        {
            byte s = ci.OutfitStates[slot];
            if (s == CardReader.OutfitTopOnly)
                return slot < ci.OutfitTops.Length && IsTowel(ci.OutfitTops[slot]) ? Towel : Dressed;
            return s;
        }
    }

    // Asks Sideloader (by reflection, so we don't depend on its exact version) whether a zipmod is installed.
    internal static class SideloaderBridge
    {
        private static bool _init;
        private static MethodInfo _getManifest;
        private static object _instance;
        private static IDictionary _manifests;
        private static readonly Dictionary<string, bool> Cache = new Dictionary<string, bool>();
        public static bool Available { get; private set; }

        private static void Init()
        {
            _init = true;
            try
            {
                Type t = AccessTools.TypeByName("Sideloader.Sideloader");
                if (t == null)
                {
                    StudioCharaFilterPlugin.Log.LogWarning("[SCF] Sideloader not found - missing-mod checks are off");
                    return;
                }
                _getManifest = AccessTools.Method(t, "GetManifest", new[] { typeof(string) });
                if (_getManifest != null && !_getManifest.IsStatic)
                    _instance = UnityEngine.Object.FindObjectOfType(t);
                if (_getManifest == null || (!_getManifest.IsStatic && _instance == null))
                {
                    _getManifest = null;
                    // Fallback: a Manifests dictionary keyed by GUID.
                    object holder = null;
                    PropertyInfo p = AccessTools.Property(t, "Manifests");
                    FieldInfo f = AccessTools.Field(t, "Manifests");
                    bool isStatic = (p != null && p.GetGetMethod(true).IsStatic) || (f != null && f.IsStatic);
                    if (!isStatic) holder = UnityEngine.Object.FindObjectOfType(t);
                    object val = p != null ? p.GetValue(holder, null) : (f != null ? f.GetValue(holder) : null);
                    _manifests = val as IDictionary;
                }
                Available = _getManifest != null || _manifests != null;
                StudioCharaFilterPlugin.Log.LogInfo("[SCF] Sideloader check: " +
                    (_getManifest != null ? "GetManifest" : _manifests != null ? "Manifests dictionary" : "unavailable - missing-mod checks are off"));
            }
            catch (Exception e)
            {
                Available = false;
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Sideloader check failed, missing-mod checks are off: " + e.Message);
            }
        }

        public static bool IsInstalled(string guid)
        {
            if (!_init) Init();
            if (!Available || string.IsNullOrEmpty(guid)) return true;
            bool result;
            if (Cache.TryGetValue(guid, out result)) return result;
            try
            {
                if (_getManifest != null) result = _getManifest.Invoke(_instance, new object[] { guid }) != null;
                else result = _manifests.Contains(guid);
            }
            catch (Exception)
            {
                result = true;
            }
            Cache[guid] = result;
            return result;
        }

        // Bit mask of regions with at least one missing zipmod (cached per card; installed mods don't change during a session).
        public static int MissingMask(CardInfo ci)
        {
            if (ci.MissingComputed) return ci.MissingMask;
            int mask = 0;
            for (int i = 0; i < ci.ModIds.Length; i++)
                if (!IsInstalled(ci.ModIds[i])) mask |= 1 << ci.ModRegions[i];
            ci.MissingMask = mask;
            ci.MissingComputed = true;
            return mask;
        }
    }
}
