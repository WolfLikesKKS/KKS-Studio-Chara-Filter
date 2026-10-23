// Names and helpers for profile, color and detail filters.
using System;
using System.Collections.Generic;

namespace StudioCharaFilter
{
    internal static class ProfileData
    {
        // Keys as saved in ChaFileParameter (bit order = index).
        public static readonly string[] LikeKeys = { "animal", "eat", "cook", "exercise", "study", "fashionable", "blackCoffee", "spicy", "sweet" };
        public static readonly string[] LikeNames = { "Animals", "Eating", "Cooking", "Exercise", "Studying", "Fashion", "Black coffee", "Spicy food", "Sweets" };

        public static readonly string[] RefusalKeys = { "kiss", "aibu", "anal", "massage", "notCondom" };
        public static readonly string[] RefusalNames = { "Kissing", "Caressing", "Anal", "Vibrator", "No condom" };

#if KK
        public static readonly string[] TraitKeys =
        {
            "hinnyo", "harapeko", "donkan", "choroi", "bitch", "mutturi", "dokusyo", "ongaku", "kappatu", "ukemi", "friendly",
            "kireizuki", "taida", "sinsyutu", "hitori", "undo", "majime", "likeGirls"
        };
        public static readonly string[] TraitNames =
        {
            "Weak bladder", "Always hungry", "Oblivious", "Pushover", "Promiscuous", "Closet pervert", "Bookworm", "Music lover",
            "Lively", "Submissive", "Friendly", "Neat freak", "Lazy", "Elusive", "Loner", "Athletic", "Serious", "Likes girls"
        };
#else
        public static readonly string[] TraitKeys =
        {
            "harapeko", "choroi", "dokusyo", "ongaku", "okute", "friendly", "kireizuki", "sinsyutu", "hitori", "active", "majime", "info",
            "love", "talk", "nakama", "nonbiri", "hinnyo", "likeGirls", "bitch", "mutturi", "lonely", "ukemi", "undo"
        };
        public static readonly string[] TraitNames =
        {
            "Always hungry", "Pushover", "Bookworm", "Music lover", "Late bloomer", "Friendly", "Neat freak", "Elusive", "Loner", "Active",
            "Serious", "Gossip", "Romantic", "Talkative", "Team player", "Laid-back", "Weak bladder", "Likes girls", "Promiscuous",
            "Closet pervert", "Lonely", "Submissive", "Athletic"
        };
#endif

        public static readonly string[] BloodTypes = { "A", "B", "O", "AB" };

        private static Dictionary<int, string> _personalities;
        private static Dictionary<int, string> _clubs;

        public static string Personality(int id)
        {
            if (_personalities == null)
            {
                _personalities = new Dictionary<int, string>();
                try
                {
                    IEnumerable<KeyValuePair<int, VoiceInfo.Param>> table = GameDefs.VoiceTable;
                    if (table != null)
                        foreach (KeyValuePair<int, VoiceInfo.Param> kv in table)
                            if (kv.Value != null) _personalities[kv.Key] = kv.Value.Personality;
                }
                catch (Exception) { }
            }
            string n;
            return _personalities.TryGetValue(id, out n) && !string.IsNullOrEmpty(n) ? n : "Personality " + id;
        }

        public static string ClubName(int id)
        {
            if (_clubs == null)
            {
                _clubs = new Dictionary<int, string>();
                try
                {
                    if (Manager.Game.ClubInfos != null)
                        foreach (KeyValuePair<int, ClubInfo.Param> kv in Manager.Game.ClubInfos)
                            if (kv.Value != null) _clubs[kv.Key] = kv.Value.Name;
                }
                catch (Exception) { }
            }
            string n;
            if (_clubs.TryGetValue(id, out n) && !string.IsNullOrEmpty(n)) return n;
            return id == 0 ? "No club" : "Club " + id;
        }

        // Nipple texture name: zipmod name if modded, otherwise the game's item list (main thread only).
        private static readonly Dictionary<int, string> NipNames = new Dictionary<int, string>();

        public static string NipName(CardInfo ci)
        {
            if (!string.IsNullOrEmpty(ci.NipModName)) return ci.NipModName;
            string n;
            if (NipNames.TryGetValue(ci.NipId, out n)) return n;
            n = "Type " + ci.NipId;
            try
            {
                ListInfoBase info = GameDefs.ChaList.GetListInfo(ChaListDefine.CategoryNo.mt_nip, ci.NipId);
                if (info != null && !string.IsNullOrEmpty(info.Name)) n = info.Name;
            }
            catch (Exception) { }
            NipNames[ci.NipId] = n;
            return n;
        }
    }

    // Sorts colors into simple named buckets.
    internal static class ColorBuckets
    {
        public static readonly string[] HairNames = { "Black", "Gray / silver", "White", "Brown", "Blonde", "Orange", "Red", "Pink", "Purple", "Blue", "Teal", "Green" };
        public static readonly float[][] HairSwatches =
        {
            new[] { 0.08f, 0.08f, 0.08f }, new[] { 0.6f, 0.6f, 0.62f }, new[] { 0.95f, 0.95f, 0.95f }, new[] { 0.42f, 0.27f, 0.15f },
            new[] { 0.95f, 0.85f, 0.45f }, new[] { 0.95f, 0.55f, 0.15f }, new[] { 0.8f, 0.12f, 0.12f }, new[] { 0.98f, 0.6f, 0.75f },
            new[] { 0.6f, 0.3f, 0.8f }, new[] { 0.2f, 0.4f, 0.9f }, new[] { 0.15f, 0.75f, 0.75f }, new[] { 0.25f, 0.7f, 0.3f }
        };
        public static readonly string[] SkinNames = { "Pale", "Fair", "Tan", "Dark", "Unusual (colored)" };
        public static readonly float[][] SkinSwatches =
        {
            new[] { 1f, 0.94f, 0.92f }, new[] { 0.98f, 0.82f, 0.72f }, new[] { 0.82f, 0.62f, 0.45f }, new[] { 0.45f, 0.3f, 0.2f }, new[] { 0.5f, 0.7f, 0.9f }
        };

        private static void ToHsv(float[] c, out float h, out float s, out float v)
        {
            float r = c[0], g = c[1], b = c[2];
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            v = max;
            float d = max - min;
            s = max <= 0 ? 0 : d / max;
            if (d <= 0.00001f) { h = 0; return; }
            if (max == r) h = 60f * (((g - b) / d) % 6f);
            else if (max == g) h = 60f * (((b - r) / d) + 2f);
            else h = 60f * (((r - g) / d) + 4f);
            if (h < 0) h += 360f;
        }

        // Hair / eye color bucket index, or -1 if unknown.
        public static int Hue(float[] c)
        {
            if (c == null) return -1;
            float h, s, v;
            ToHsv(c, out h, out s, out v);
            if (v < 0.17f) return 0;                                   // black
            if (s < 0.14f) return v > 0.85f ? 2 : 1;                   // white / gray
            if (h >= 15f && h < 50f && v < 0.62f) return 3;            // brown
            if (h < 12f || h >= 345f) return (s < 0.45f && v > 0.7f) ? 7 : 6; // red (light & soft = pink)
            if (h < 38f) return 5;                                     // orange
            if (h < 70f) return 4;                                     // blonde / yellow
            if (h < 160f) return 11;                                   // green
            if (h < 195f) return 10;                                   // teal
            if (h < 255f) return 9;                                    // blue
            if (h < 290f) return 8;                                    // purple
            return 7;                                                  // pink / magenta
        }

        public static int Skin(float[] c)
        {
            if (c == null) return -1;
            float h, s, v;
            ToHsv(c, out h, out s, out v);
            bool natural = (h < 50f || h >= 340f) && s < 0.75f;
            if (!natural && s > 0.12f) return 4;
            float lum = 0.299f * c[0] + 0.587f * c[1] + 0.114f * c[2];
            if (lum > 0.86f) return 0;
            if (lum > 0.7f) return 1;
            if (lum > 0.5f) return 2;
            return 3;
        }
    }
}
