// Extra filter sections: Colors, Body details, Profile, Card file (date, duplicates, unreadable files).
// Same rules as the rest of the window: anything that changes how many controls are drawn goes through Queue().
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StudioCharaFilter
{
    internal sealed partial class FilterWindow
    {
        private bool _colorsEnabled, _detailsEnabled, _profileEnabled, _fileEnabled;
        private int _nipSelected;                 // snapshot taken at Layout
        private Vector2 _nipScroll;

        private readonly int[] _hairCounts = new int[ColorBuckets.HairNames.Length];
        private readonly int[] _eyeCounts = new int[ColorBuckets.HairNames.Length];
        private readonly int[] _skinCounts = new int[ColorBuckets.SkinNames.Length];
        private readonly int[] _detailCounts = new int[FilterState.DetailNames.Length];
        private readonly int[] _bloodCounts = new int[4];
        private readonly int[] _traitCounts = new int[ProfileData.TraitKeys.Length];
        private readonly int[] _likeCounts = new int[ProfileData.LikeKeys.Length];
        private readonly int[] _refusalCounts = new int[ProfileData.RefusalKeys.Length];
        private List<KeyValuePair<int, int>> _personalityCounts = new List<KeyValuePair<int, int>>();
        private List<KeyValuePair<int, int>> _clubCounts = new List<KeyValuePair<int, int>>();
        private List<KeyValuePair<string, int>> _nipCounts = new List<KeyValuePair<string, int>>();

        private List<List<CardInfo>> _duplicates;
        private List<CardInfo> _unreadable;
        private string _dupSearch = "", _dupSearchApplied = "";
        private string _fileMessage = "";

        private void UpdateExtraStats(List<CardInfo> cards, CardIndex idx)
        {
            Array.Clear(_hairCounts, 0, _hairCounts.Length);
            Array.Clear(_eyeCounts, 0, _eyeCounts.Length);
            Array.Clear(_skinCounts, 0, _skinCounts.Length);
            Array.Clear(_detailCounts, 0, _detailCounts.Length);
            Array.Clear(_bloodCounts, 0, _bloodCounts.Length);
            Array.Clear(_traitCounts, 0, _traitCounts.Length);
            Array.Clear(_likeCounts, 0, _likeCounts.Length);
            Array.Clear(_refusalCounts, 0, _refusalCounts.Length);
            Dictionary<int, int> pers = new Dictionary<int, int>();
            Dictionary<int, int> clubs = new Dictionary<int, int>();
            Dictionary<string, int> nips = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                int h = ColorBuckets.Hue(ci.HairColor); if (h >= 0) _hairCounts[h]++;
                int e = ColorBuckets.Hue(ci.EyeColor); if (e >= 0) _eyeCounts[e]++;
                int s = ColorBuckets.Skin(ci.SkinColor); if (s >= 0) _skinCounts[s]++;
                for (int d = 0; d < _detailCounts.Length; d++) if (FilterState.DetailValue(ci, d)) _detailCounts[d]++;
                if (ci.BloodType >= 0 && ci.BloodType < 4) _bloodCounts[ci.BloodType]++;
                for (int i = 0; i < _traitCounts.Length; i++) if ((ci.Traits & (1 << i)) != 0) _traitCounts[i]++;
                for (int i = 0; i < _likeCounts.Length; i++) if ((ci.Likes & (1 << i)) != 0) _likeCounts[i]++;
                for (int i = 0; i < _refusalCounts.Length; i++) if ((ci.Refusals & (1 << i)) != 0) _refusalCounts[i]++;
                int n;
                pers.TryGetValue(ci.Personality, out n); pers[ci.Personality] = n + 1;
                clubs.TryGetValue(ci.Club, out n); clubs[ci.Club] = n + 1;
                string nip = ProfileData.NipName(ci);
                nips.TryGetValue(nip, out n); nips[nip] = n + 1;
            }
            foreach (int p in _filter.Personalities) if (!pers.ContainsKey(p)) pers[p] = 0;
            foreach (int c in _filter.Clubs) if (!clubs.ContainsKey(c)) clubs[c] = 0;
            foreach (string t in _filter.NipTypes) if (!nips.ContainsKey(t)) nips[t] = 0;
            _personalityCounts = pers.OrderBy(kv => ProfileData.Personality(kv.Key), StringComparer.OrdinalIgnoreCase).ToList();
            _clubCounts = clubs.OrderBy(kv => kv.Key).ToList();
            _nipCounts = nips.OrderByDescending(kv => kv.Value).ToList();
        }

        // ---------- small helpers ----------

        private void Swatch(float[] rgb)
        {
            Rect r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(14));
            if (Event.current.type != EventType.Repaint) return;
            Color prev = GUI.color;
            GUI.color = new Color(rgb[0], rgb[1], rgb[2], 1f);
            GUI.DrawTexture(new Rect(r.x, r.y + 2, 12, 12), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // A row of toggles for a set of int choices, 'perRow' per row.
        private void ToggleGrid(HashSet<int> set, string[] names, int[] counts, float[][] swatches, int perRow, float width)
        {
            for (int i = 0; i < names.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(names.Length, i + perRow); j++)
                {
                    if (swatches != null) Swatch(swatches[j]);
                    bool on = set.Contains(j);
                    bool now = GUILayout.Toggle(on, names[j] + " <color=#9a9a9a>(" + counts[j] + ")</color>", _toggleRich, GUILayout.Width(width));
                    if (now != on)
                    {
                        if (now) set.Add(j); else set.Remove(j);
                        _markDirty(0.2f);
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private static readonly string[] HasNoText = { "any", "has", "doesn't" };

        // Grid of any / has / doesn't buttons for a list of flags.
        private void ModeGrid(ModFilterMode[] modes, string[] names, int[] counts, int perRow)
        {
            Color prev = GUI.color;
            for (int i = 0; i < names.Length; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(names.Length, i + perRow); j++)
                {
                    ModFilterMode m = modes[j];
                    GUI.color = ModeColor(m, prev);
                    string text = m == ModFilterMode.Ignore ? names[j] : names[j] + ": " + (m == ModFilterMode.Require ? "yes" : "no");
                    if (GUILayout.Button(new GUIContent(text + "  (" + counts[j] + ")", "Click to cycle: any -> yes -> no"), _smallButton))
                    {
                        modes[j] = (ModFilterMode)(((int)m + 1) % 3);
                        _markDirty(0.1f);
                    }
                }
                GUI.color = prev;
                GUILayout.EndHorizontal();
            }
            GUI.color = prev;
        }

        // ---------- Colors ----------

        private void DrawColors()
        {
            GUILayout.Label("Tick colors to show only those cards. Hair = back hair color, eyes = left eye. Colors are sorted into buckets " +
                "automatically, so unusual shades may land in a neighbouring bucket.", _smallLabel, Shrink);
            GUILayout.Label("<b>Hair</b>", _leftLabel);
            ToggleGrid(_filter.HairColors, ColorBuckets.HairNames, _hairCounts, ColorBuckets.HairSwatches, 3, 125);
            GUILayout.Label("<b>Eyes</b>", _leftLabel);
            ToggleGrid(_filter.EyeColors, ColorBuckets.HairNames, _eyeCounts, ColorBuckets.HairSwatches, 3, 125);
            GUILayout.Label("<b>Skin</b>", _leftLabel);
            ToggleGrid(_filter.SkinTones, ColorBuckets.SkinNames, _skinCounts, ColorBuckets.SkinSwatches, 3, 125);
            bool prevEn = GUI.enabled;
            GUI.enabled = _filter.HairColors.Count + _filter.EyeColors.Count + _filter.SkinTones.Count > 0;
            if (GUILayout.Button("Clear colors", GUILayout.Width(100)))
                Queue(delegate { _filter.HairColors.Clear(); _filter.EyeColors.Clear(); _filter.SkinTones.Clear(); _markDirty(0f); });
            GUI.enabled = prevEn;
        }

        // ---------- Body details ----------

        private void DrawDetails()
        {
            Color prev = GUI.color;
            for (int d = 0; d < FilterState.DetailNames.Length; d++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(FilterState.DetailNames[d], _leftLabel, GUILayout.Width(150));
                ModFilterMode m = _filter.DetailModes[d];
                GUI.color = ModeColor(m, prev);
                if (GUILayout.Button(new GUIContent(HasNoText[(int)m], "any = ignore, has = only cards with it, doesn't = only cards without it"), GUILayout.Width(76)))
                {
                    _filter.DetailModes[d] = (ModFilterMode)(((int)m + 1) % 3);
                    _markDirty(0.1f);
                }
                GUI.color = prev;
                GUILayout.Space(4);
                GUILayout.Label("<color=#9a9a9a>" + _detailCounts[d] + " cards have it</color>", _leftLabel, Shrink);
                GUILayout.EndHorizontal();
            }

            // Folded by default: a long list of nipple mods otherwise buries the sections below it.
            GUILayout.Space(4);
            bool nipOpen = !_collapsed.Contains("Details.Nipples");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((nipOpen ? "[-] " : "[+] ") + "Nipple type  <color=#9a9a9a>" + _nipCounts.Count + " types" +
                (_nipSelected > 0 ? "</color>  <color=#7fd17f>" + _nipSelected + " selected" : "") + "</color>", _subHeader, Shrink))
                Queue(delegate { Settings.SetCollapsed("Details.Nipples", nipOpen); });
            if (_nipSelected > 0 && GUILayout.Button(new GUIContent("Clear", "Untick all nipple types."), _smallButton, GUILayout.Width(50)))
                Queue(delegate { _filter.NipTypes.Clear(); _markDirty(0.1f); });
            GUILayout.EndHorizontal();
            if (!nipOpen) return;

            int limit = Settings.ListRowLimit.Value;
            int rows = Math.Min(_nipCounts.Count, limit);
            _nipScroll = GUILayout.BeginScrollView(_nipScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Height(Mathf.Min(rows * 22 + 6, 230)));
            int shown = 0;
            foreach (KeyValuePair<string, int> kv in _nipCounts)
            {
                if (++shown > limit) break;
                bool on = _filter.NipTypes.Contains(kv.Key);
                bool now = GUILayout.Toggle(on, kv.Key + "  <color=#9a9a9a>(" + kv.Value + ")</color>", _toggleRich, Shrink);
                if (now != on)
                {
                    if (now) _filter.NipTypes.Add(kv.Key); else _filter.NipTypes.Remove(kv.Key);
                    _markDirty(0.2f);
                }
            }
            GUILayout.EndScrollView();
        }

        // ---------- Profile ----------

        private void DrawProfile(ListController c)
        {
            foreach (PropertyFilter p in _visibleProps)
                if (p.Section == "Profile") DrawProperty(p, c);

            GUILayout.Label("<b>Personality</b>", _leftLabel);
            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (KeyValuePair<int, int> kv in _personalityCounts)
            {
                if (col == 2) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); col = 0; }
                bool on = _filter.Personalities.Contains(kv.Key);
                bool now = GUILayout.Toggle(on, ProfileData.Personality(kv.Key) + " <color=#9a9a9a>(" + kv.Value + ")</color>", _toggleRich, GUILayout.Width(190));
                if (now != on)
                {
                    if (now) _filter.Personalities.Add(kv.Key); else _filter.Personalities.Remove(kv.Key);
                    _markDirty(0.2f);
                }
                col++;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Blood type</b>", _leftLabel);
            ToggleGrid(_filter.BloodTypes, ProfileData.BloodTypes, _bloodCounts, null, 4, 90);

            GUILayout.Label("<b>Club</b>", _leftLabel);
            col = 0;
            GUILayout.BeginHorizontal();
            foreach (KeyValuePair<int, int> kv in _clubCounts)
            {
                if (col == 2) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); col = 0; }
                bool on = _filter.Clubs.Contains(kv.Key);
                bool now = GUILayout.Toggle(on, ProfileData.ClubName(kv.Key) + " <color=#9a9a9a>(" + kv.Value + ")</color>", _toggleRich, GUILayout.Width(190));
                if (now != on)
                {
                    if (now) _filter.Clubs.Add(kv.Key); else _filter.Clubs.Remove(kv.Key);
                    _markDirty(0.2f);
                }
                col++;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Traits</b> <color=#9a9a9a>(click to cycle any / yes / no)</color>", _leftLabel);
            ModeGrid(_filter.TraitModes, ProfileData.TraitNames, _traitCounts, 3);
            GUILayout.Label("<b>Likes</b>", _leftLabel);
            ModeGrid(_filter.LikeModes, ProfileData.LikeNames, _likeCounts, 3);
            GUILayout.Label("<b>H refusals</b>", _leftLabel);
            ModeGrid(_filter.RefusalModes, ProfileData.RefusalNames, _refusalCounts, 3);
        }

        // ---------- Card file ----------

        private void DrawFileSection(ListController c, CardIndex idx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Added / modified", "Based on the card file's last modified date."), _leftLabel, GUILayout.Width(130));
            Color prev = GUI.color;
            GUI.color = _filter.FileAgeMode == 0 ? prev : new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button(new GUIContent(FilterState.FileAgeNames[_filter.FileAgeMode], "Click to cycle"), GUILayout.Width(130)))
            {
                _filter.FileAgeMode = (_filter.FileAgeMode + 1) % FilterState.FileAgeNames.Length;
                _markDirty(0.1f);
            }
            GUI.color = prev;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Duplicates
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(_duplicates == null ? "Find duplicates" : "Find again",
                "Finds cards with the same name and identical face and body sliders (the same character saved more than once)."), GUILayout.Width(120)))
                Queue(delegate { _duplicates = FindDuplicates(idx); _fileMessage = _duplicates.Count + " groups of duplicates found."; });
            if (GUILayout.Button(new GUIContent(_unreadable == null ? "Unreadable files" : "Refresh unreadable",
                "Lists .png files in the card folders that aren't readable character cards (not real PNGs, damaged, or not cards)."), GUILayout.Width(130)))
                Queue(delegate
                {
                    _unreadable = idx.Snapshot().Where(x => !x.Ok).OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase).ToList();
                    _fileMessage = _unreadable.Count + " unreadable files.";
                });
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (_fileMessage.Length > 0) GUILayout.Label(_fileMessage, _smallLabel, Shrink);

            if (_duplicates != null && _duplicates.Count > 0)
            {
                GUILayout.Label("<b>Duplicates</b> <color=#9a9a9a>(Show lists every copy using All folders + name search)</color>", _leftLabel, Shrink);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Search", GUILayout.Width(50));
                _dupSearch = GUILayout.TextField(_dupSearch ?? "");
                GUILayout.EndHorizontal();
                int shown = 0, limit = Settings.ListRowLimit.Value;
                foreach (List<CardInfo> g in _duplicates)
                {
                    string name = Names.FullName(g[0].FirstName, g[0].LastName);
                    if (_dupSearchApplied.Length > 0 && name.IndexOf(_dupSearchApplied, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (++shown > limit) break;
                    string tip = string.Join("\n", g.Select(x => CardInfo.DisplayPath(x.FilePath)).ToArray());
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent(name + "  <color=#9a9a9a>" + g.Count + " copies</color>", tip), _leftLabel, Shrink);
                    string query = (g[0].FirstName + " " + g[0].LastName).Trim();
                    if (GUILayout.Button(new GUIContent("Show", "List every copy (switches to All folders and searches this name)."), GUILayout.Width(56)))
                        Queue(delegate
                        {
                            _nameText = query;
                            _filter.SetNameQuery(query);
                            if (c.Mode != ScopeMode.AllFolders) _setMode(c, ScopeMode.AllFolders);
                            _markDirty(0f);
                        });
                    GUILayout.EndHorizontal();
                }
                if (shown > limit) GUILayout.Label("More not shown - use the search box.", _smallLabel, Shrink);
            }

            if (_unreadable != null && _unreadable.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Unreadable files</b>", _leftLabel, Shrink);
                if (GUILayout.Button(new GUIContent("Save list", "Writes the list to UserData\\\\StudioCharaFilter_UnreadableFiles.txt"), GUILayout.Width(80)))
                    Queue(delegate
                    {
                        try
                        {
                            string path = System.IO.Path.Combine(UserData.Path, "StudioCharaFilter_UnreadableFiles.txt");
                            System.IO.File.WriteAllLines(path, _unreadable.Select(x => CardInfo.DisplayPath(x.FilePath)).ToArray());
                            _fileMessage = "Saved to " + CardInfo.DisplayPath(path);
                        }
                        catch (Exception ex) { _fileMessage = "Could not save: " + ex.Message; }
                    });
                GUILayout.EndHorizontal();
                int shown = 0, limit = Settings.ListRowLimit.Value;
                foreach (CardInfo ci in _unreadable)
                {
                    if (++shown > limit) break;
                    GUILayout.Label(new GUIContent(System.IO.Path.GetFileName(ci.FilePath) + "  <color=#9a9a9a>" + ci.Folder + "</color>",
                        CardInfo.DisplayPath(ci.FilePath)), _leftLabel, Shrink);
                }
                if (shown > limit) GUILayout.Label("More not shown - use Save list.", _smallLabel, Shrink);
            }
        }

        // Same name + identical face and body sliders = the same character saved more than once.
        private static List<List<CardInfo>> FindDuplicates(CardIndex idx)
        {
            Dictionary<string, List<CardInfo>> groups = new Dictionary<string, List<CardInfo>>();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (CardInfo ci in idx.Snapshot())
            {
                if (!ci.Ok) continue;
                sb.Length = 0;
                sb.Append(ci.LastName).Append('|').Append(ci.FirstName).Append('|');
                foreach (float f in ci.Body) sb.Append(Math.Round(f, 3)).Append(',');
                sb.Append('|');
                foreach (float f in ci.Face) sb.Append(Math.Round(f, 3)).Append(',');
                string key = sb.ToString();
                List<CardInfo> g;
                if (!groups.TryGetValue(key, out g)) { g = new List<CardInfo>(); groups[key] = g; }
                g.Add(ci);
            }
            return groups.Values.Where(g => g.Count > 1)
                .OrderByDescending(g => g.Count).ThenBy(g => Names.FullName(g[0].FirstName, g[0].LastName), StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
