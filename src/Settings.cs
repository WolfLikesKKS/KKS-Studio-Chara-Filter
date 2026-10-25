// User settings, shown in the F1 Configuration Manager under "Studio Chara Filter".
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace StudioCharaFilter
{
    // Recognized by Configuration Manager by name; only the fields we use.
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? Browsable;
        public int? Order;
        public System.Action<ConfigEntryBase> CustomDrawer;
        public bool? HideDefaultButton;
    }

    internal static class Settings
    {
        public static ConfigEntry<bool> ShowWindow;
        public static ConfigEntry<KeyboardShortcut> ToggleKey;
        public static ConfigEntry<float> WindowScale;
        public static ConfigEntry<bool> ShowHistograms;
        public static ConfigEntry<bool> ShowRandom;
        public static ConfigEntry<int> HistogramHeight;
        public static ConfigEntry<bool> ShowModIds;
        public static ConfigEntry<float> OutlierTrim;
        public static ConfigEntry<int> MaxResults;
        public static ConfigEntry<int> FolderListHeight;
        public static ConfigEntry<bool> LegacyPluginFilter;
        public static ConfigEntry<int> ListRowLimit;
        public static ConfigEntry<int> LinkBatchSize;
        public static ConfigEntry<bool> FastFolderLoading;
        public static ConfigEntry<bool> FastThumbnails;
        public static ConfigEntry<bool> FastRows;
        public static ConfigEntry<int> IndexThreads;
        public static ConfigEntry<bool> LogTimings;
        public static ConfigEntry<bool> ShowBirthday;
        public static ConfigEntry<bool> ShowUncensor;
        public static ConfigEntry<bool> ShowColors, ShowProfile, ShowDetails, ShowFileSection;
        public static ConfigEntry<bool> MaySeventeenIsUnset;
        private static ConfigEntry<string> _removedCards;
        public static ConfigEntry<bool> EnableRemoval;
        private static bool _emptyConfirm;
        private static string _emptyMessage = "";
        public static ConfigEntry<string> WindowRect;
        public static int RectVersion; // bumped when the saved position changes, so the window reloads it

        public static void Init(ConfigFile cfg)
        {
            ShowWindow = cfg.Bind("Window", "Show filter window", true,
                Desc("Show the filter window while the Studio character list is open.", 10));
            ToggleKey = cfg.Bind("Window", "Toggle window hotkey", KeyboardShortcut.Empty,
                Desc("Shows/hides the filter window. Not set by default.", 9));
            WindowScale = cfg.Bind("Window", "Window scale", 1.0f,
                new ConfigDescription("Size of the filter window and its text. Try 1.25-1.5 on 1440p or 4K screens.",
                    new AcceptableValueRange<float>(0.75f, 2.5f), new ConfigurationManagerAttributes { Order = 8 }));

            ShowRandom = cfg.Bind("Display", "Show random character buttons", true,
                Desc("Show the Random row: add N random characters from the matching cards, or replace the selected / all characters " +
                    "in the scene with random ones.", 11));
            ShowHistograms = cfg.Bind("Display", "Show distribution graphs", true,
                Desc("Show the small bar graph of where your cards fall above each body slider.", 10));
            HistogramHeight = cfg.Bind("Display", "Distribution graph height", 20,
                new ConfigDescription("Height of the bar graphs, in pixels (before window scale).",
                    new AcceptableValueRange<int>(10, 60), new ConfigurationManagerAttributes { Order = 9 }));
            ShowModIds = cfg.Bind("Display", "Show technical mod IDs", true,
                Desc("Show the plugin ID in gray next to each mod name in the Mod data list.", 8));
            OutlierTrim = cfg.Bind("Display", "Slider scale outlier trim (%)", 0.5f,
                new ConfigDescription("The slider scale ignores this percentage of the most extreme cards at each end, " +
                    "so a few odd cards don't squash the scale. Handles at the ends still include them.",
                    new AcceptableValueRange<float>(0f, 5f), new ConfigurationManagerAttributes { Order = 7 }));

            FolderListHeight = cfg.Bind("Display", "Folder list height", 320,
                new ConfigDescription("Height of the folder list in Choose folders mode, in pixels (before window scale).",
                    new AcceptableValueRange<int>(80, 800), new ConfigurationManagerAttributes { Order = 6 }));

            MaxResults = cfg.Bind("Behavior", "Cards per page", 1500,
                new ConfigDescription("Choose folders / All folders list this many cards per page (creating list rows is slow for huge lists). " +
                    "Use the page buttons or Show all in the window to see the rest.",
                    new AcceptableValueRange<int>(100, 10000)));
            EnableRemoval = cfg.Bind("Removed cards", "Enable card removal", false,
                new ConfigDescription("Adds a \"Card removal\" section at the bottom of the filter window, where the highlighted card can be moved to \"" +
                    Removal.FolderName + "\" next to CharaStudio.exe (nothing is deleted) and removed cards can be restored. Off by default.",
                    null, new ConfigurationManagerAttributes { Order = 10 }));
            _removedCards = cfg.Bind("Removed cards", "Removed cards folder", "",
                new ConfigDescription("Cards removed with \"Remove card...\" are moved to \"" + Removal.FolderName + "\" next to CharaStudio.exe. " +
                    "Emptying sends them to the Windows Recycle Bin, so they can still be recovered from there.", null,
                    new ConfigurationManagerAttributes { CustomDrawer = DrawRemovedCards, HideDefaultButton = true }));
            FastFolderLoading = cfg.Bind("Performance", "Fast folder loading", true,
                "Suggested to keep on: speeds up opening character folders in Studio by reading card names from this plugin's index " +
                "instead of loading every card in full. New or changed cards are still read normally. " +
                "Feel free to disable if it causes issues.");
            FastThumbnails = cfg.Bind("Performance", "Fast thumbnails", true,
                "Suggested to keep on: removes the stutter when moving the mouse over character names by caching thumbnails " +
                "instead of running a full memory cleanup on every hover. Feel free to disable if it causes issues.");
            FastRows = cfg.Bind("Performance", "Reuse list rows", true,
                "Suggested to keep on: instead of destroying and re-creating every row in the character list each time it changes, " +
                "rows are kept and reused, which makes switching folders and pages faster. Feel free to disable if it causes issues.");
            IndexThreads = cfg.Bind("Performance", "Indexing threads", Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1)),
                new ConfigDescription("How many cards are read at the same time when building the index (only new or changed cards are read). " +
                    "SSD: 2-4 is a good range. Hard drive: use 1, since reading several files at once makes the drive slower. " +
                    "The first full index competes with the game's own startup either way.", new AcceptableValueRange<int>(1, 8)));
            ShowColors = cfg.Bind("Display", "Show colors filter", true,
                new ConfigDescription("Shows the Colors section (hair, eye and skin color).", null, new ConfigurationManagerAttributes { Order = 6 }));
            ShowDetails = cfg.Bind("Display", "Show body details filter", true,
                new ConfigDescription("Shows the Body details section (pubic hair, tan lines, tattoos, fangs, futa, ABMX edits, nipple type).",
                    null, new ConfigurationManagerAttributes { Order = 6 }));
            ShowProfile = cfg.Bind("Display", "Show profile filter", false,
                new ConfigDescription("Shows the Profile section (personality, blood type, club, voice pitch, traits, likes, H refusals).",
                    null, new ConfigurationManagerAttributes { Order = 5 }));
            ShowFileSection = cfg.Bind("Display", "Show card file section", true,
                new ConfigDescription("Shows the Card file section (date added/modified, duplicates, unreadable files).",
                    null, new ConfigurationManagerAttributes { Order = 4 }));
            ShowUncensor = cfg.Bind("Display", "Show genital type filter", true,
                new ConfigDescription("Shows the Genital type section (which Uncensor Selector body each card uses).", null,
                    new ConfigurationManagerAttributes { Order = 5 }));
            ShowBirthday = cfg.Bind("Display", "Show birthday filter", false,
                new ConfigDescription("Adds a Birthday section (filter by birth month, or by whether a birthday was set). " +
                    "Cards don't store an age, only a birthday.", null, new ConfigurationManagerAttributes { Order = 4 }));
            MaySeventeenIsUnset = cfg.Bind("Display", "Treat May 17 as no birthday", true,
                new ConfigDescription("May 17 is the Maker's default birthday, so most cards with it never had one set. " +
                    "Turn off to count May 17 as a real birthday.", null, new ConfigurationManagerAttributes { Order = 3 }));
            LogTimings = cfg.Bind("Performance", "Log timings", false,
                "Write how long list loading, row building and indexing take to the BepInEx log. Useful for troubleshooting.");
            LinkBatchSize = cfg.Bind("Behavior", "Missing mod sites per batch", 5,
                new ConfigDescription("How many missing-mod websites the \"Open next sites\" button opens in your browser at once. " +
                    "Each site is opened once even if several missing mods share it.",
                    new AcceptableValueRange<int>(1, 50)));
            LegacyPluginFilter = cfg.Bind("Behavior", "Show plugin data filter (legacy)", false,
                "Show the older filter that includes/excludes cards by which plugins saved data on them (any / HAS / NOT).");
            ListRowLimit = cfg.Bind("Display", "Max rows in author / plugin lists", 150,
                new ConfigDescription("Long lists in the window only draw this many rows at a time; use the search box to find the rest.",
                    new AcceptableValueRange<int>(30, 2000), new ConfigurationManagerAttributes { Order = 5 }));

            WindowRect = cfg.Bind("Window", "Startup position", "",
                new ConfigDescription("Where the window opens on startup. Move/resize the window, then press Save here. " +
                    "Empty = default spot (right side of the screen).", null,
                    new ConfigurationManagerAttributes { Order = 7, CustomDrawer = DrawPositionSetting, HideDefaultButton = true }));
            WindowRect.SettingChanged += (s, e) => RectVersion++;

            SectionOrder = cfg.Bind("Window", "Section order", string.Join("|", SectionIds),
                new ConfigDescription("Order of the sections in the filter window. You can also press \"Arrange\" at the top of the window. " +
                    "Card removal always stays last.", null,
                    new ConfigurationManagerAttributes { Order = 6, CustomDrawer = DrawSectionOrder, HideDefaultButton = true }));
            CollapsedSections = cfg.Bind("Window", "Collapsed sections", "Details.Nipples",
                new ConfigDescription("Sections folded closed in the window (remembered between sessions).", null,
                    new ConfigurationManagerAttributes { Browsable = false }));
        }

        // ---------- Section order ----------

        public static readonly string[] SectionIds =
            { "Body", "Face", "Colors", "Details", "Clothing", "Content", "Uncensor", "Profile", "File", "Birthday", "Authors", "Legacy" };
        public static readonly string[] SectionTitles =
        {
            "Body (Maker slider values)", "Face (Maker slider values)", "Colors", "Body details", "Clothing (outfits)",
            "Content mods (zipmod items by body region)", "Genital type (Uncensor Selector)", "Profile", "Card file",
            "Birthday", "Card authors", "Plugin data saved on the card (legacy)"
        };
        public static ConfigEntry<string> SectionOrder;
        public static ConfigEntry<string> CollapsedSections;

        public static string SectionTitle(string id)
        {
            int i = Array.IndexOf(SectionIds, id);
            return i < 0 ? id : SectionTitles[i];
        }

        // Saved order, with unknown ids dropped and any missing ones added in their default place at the end.
        public static string[] GetSectionOrder()
        {
            List<string> list = new List<string>();
            foreach (string s in (SectionOrder.Value ?? "").Split('|'))
                if (Array.IndexOf(SectionIds, s) >= 0 && !list.Contains(s)) list.Add(s);
            foreach (string s in SectionIds) if (!list.Contains(s)) list.Add(s);
            return list.ToArray();
        }

        // Swaps two sections in the saved order.
        public static void SwapSections(string a, string b)
        {
            string[] order = GetSectionOrder();
            int ia = Array.IndexOf(order, a), ib = Array.IndexOf(order, b);
            if (ia < 0 || ib < 0) return;
            order[ia] = b; order[ib] = a;
            SectionOrder.Value = string.Join("|", order);
        }

        public static bool IsCollapsed(string id)
        {
            return Array.IndexOf((CollapsedSections.Value ?? "").Split('|'), id) >= 0;
        }

        public static void SetCollapsed(string id, bool collapsed)
        {
            List<string> list = new List<string>((CollapsedSections.Value ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            list.Remove(id);
            if (collapsed) list.Add(id);
            CollapsedSections.Value = string.Join("|", list.ToArray());
        }

        private static void DrawSectionOrder(ConfigEntryBase entry)
        {
            string[] order = GetSectionOrder();
            GUILayout.BeginVertical(GUILayout.MinWidth(0));
            for (int i = 0; i < order.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label((i + 1) + ". " + SectionTitle(order[i]), GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                GUI.enabled = i > 0;
                if (GUILayout.Button("Up", GUILayout.ExpandWidth(false))) SwapSections(order[i], order[i - 1]);
                GUI.enabled = i < order.Length - 1;
                if (GUILayout.Button("Down", GUILayout.ExpandWidth(false))) SwapSections(order[i], order[i + 1]);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Reset to default order", GUILayout.ExpandWidth(false)))
                SectionOrder.Value = string.Join("|", SectionIds);
            GUILayout.EndVertical();
        }

        private static void DrawRemovedCards(ConfigEntryBase entry)
        {
            int count = Removal.Entries().Count;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(count + " removed cards", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Open folder", GUILayout.ExpandWidth(false))) Removal.OpenFolder();
            if (!_emptyConfirm)
            {
                GUI.enabled = count > 0;
                if (GUILayout.Button("Empty to Recycle Bin...", GUILayout.ExpandWidth(false)))
                {
                    _emptyConfirm = true;
                    _emptyMessage = "";
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            if (_emptyConfirm)
            {
                GUILayout.Label("Send all " + count + " removed cards to the Windows Recycle Bin?");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes, send to Recycle Bin", GUILayout.ExpandWidth(false)))
                {
                    _emptyMessage = Removal.EmptyToRecycleBin();
                    _emptyConfirm = false;
                }
                if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false))) _emptyConfirm = false;
                GUILayout.EndHorizontal();
            }
            if (_emptyMessage.Length > 0) GUILayout.Label(_emptyMessage);
            GUILayout.EndVertical();
        }

        private static void DrawPositionSetting(ConfigEntryBase entry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(WindowRect.Value) ? "default" : WindowRect.Value, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Save current position", GUILayout.ExpandWidth(false)))
            {
                Rect r = FilterWindow.CurrentRect;
                if (r.width > 1) WindowRect.Value = string.Format("{0:0},{1:0},{2:0},{3:0}", r.x, r.y, r.width, r.height);
            }
            if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                WindowRect.Value = "";
            GUILayout.EndHorizontal();
        }

        private static FilterState _bodyFilter;
        private static System.Action _onSlidersChanged;

        private const int PresetCount = 3;
        private static readonly Dictionary<string, ConfigEntry<string>> ShownLists = new Dictionary<string, ConfigEntry<string>>();
        private static readonly Dictionary<string, ConfigEntry<string>[]> Presets = new Dictionary<string, ConfigEntry<string>[]>();
        private static readonly HashSet<string> OpenLists = new HashSet<string>();
        private static GUIStyle _noWrapToggle, _noWrapLabel;

        public static void BindBodySliders(ConfigFile cfg, FilterState filter, System.Action onChanged)
        {
            _bodyFilter = filter;
            _onSlidersChanged = onChanged;
            BindSliderSection(cfg, filter, "Body", "Body sliders", "body");
            BindSliderSection(cfg, filter, "Face", "Face sliders", "face");
        }

        // One compact F1 entry per section: quick buttons, custom presets, and a fold-out checkbox grid.
        // Which sliders are shown is stored as one list ("Height|Bust size|..."), so F1 stays short.
        private static void BindSliderSection(ConfigFile cfg, FilterState filter, string section, string category, string word)
        {
            string sec = section;
            List<string> defaults = new List<string>();
            foreach (PropertyFilter p in filter.Properties)
            {
                if (p.Section != section) continue;
                p.HasToggle = true;
                if (p.DefaultShown) defaults.Add(p.Label);
            }

            ConfigEntry<string> shown = cfg.Bind(category, "Sliders shown", string.Join("|", defaults.ToArray()),
                new ConfigDescription("Which " + word + " sliders appear in the filter window. Hiding a slider also clears its filter.", null,
                    new ConfigurationManagerAttributes { HideDefaultButton = true, CustomDrawer = e => DrawSliderSection(sec) }));
            ShownLists[section] = shown;

            ConfigEntry<string>[] presets = new ConfigEntry<string>[PresetCount];
            for (int i = 0; i < PresetCount; i++)
                presets[i] = cfg.Bind(category, "Custom preset " + (i + 1), "",
                    new ConfigDescription("Saved with the Save buttons.", null, new ConfigurationManagerAttributes { Browsable = false }));
            Presets[section] = presets;

            ApplyShown(section, false);
            shown.SettingChanged += (s1, e1) =>
            {
                ApplyShown(sec, true);
                if (_onSlidersChanged != null) _onSlidersChanged();
            };
        }

        private static void ApplyShown(string section, bool resetHidden)
        {
            HashSet<string> on = new HashSet<string>((ShownLists[section].Value ?? "").Split('|'));
            foreach (PropertyFilter p in _bodyFilter.Properties)
            {
                if (p.Section != section) continue;
                bool v = on.Contains(p.Label);
                if (resetHidden && p.ShownFlag && !v) p.Reset();
                p.ShownFlag = v;
            }
        }

        private static string CurrentPreset(string section)
        {
            List<string> on = new List<string>();
            foreach (PropertyFilter p in _bodyFilter.Properties)
                if (p.Section == section && p.ShownFlag) on.Add(p.Label);
            return string.Join("|", on.ToArray());
        }

        private static void SetShown(string section, string list)
        {
            if (ShownLists[section].Value != list) ShownLists[section].Value = list;
        }

        private static void SetAllSliders(int mode, string section)
        {
            List<string> on = new List<string>();
            foreach (PropertyFilter p in _bodyFilter.Properties)
                if (p.Section == section && (mode == 0 || (mode == 1 && p.DefaultShown))) on.Add(p.Label);
            SetShown(section, string.Join("|", on.ToArray()));
        }

        private static void DrawSliderSection(string section)
        {
            if (_noWrapToggle == null)
            {
                _noWrapToggle = new GUIStyle(GUI.skin.toggle);
                _noWrapToggle.wordWrap = false;
                _noWrapToggle.clipping = TextClipping.Clip;
                _noWrapLabel = new GUIStyle(GUI.skin.label);
                _noWrapLabel.wordWrap = false;
                _noWrapLabel.clipping = TextClipping.Clip;
            }
            GUILayoutOption shrink = GUILayout.MinWidth(0);

            int total = 0, onCount = 0;
            foreach (PropertyFilter p in _bodyFilter.Properties)
                if (p.Section == section) { total++; if (p.ShownFlag) onCount++; }

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Show all", shrink)) SetAllSliders(0, section);
            if (GUILayout.Button("Default set", shrink)) SetAllSliders(1, section);
            if (GUILayout.Button("Hide all", shrink)) SetAllSliders(2, section);
            GUILayout.EndHorizontal();

            ConfigEntry<string>[] presets = Presets[section];
            for (int i = 0; i < presets.Length; i++)
            {
                string saved = presets[i].Value ?? "";
                int count = saved.Length == 0 ? 0 : saved.Split('|').Length;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Custom " + (i + 1) + ": " + (count == 0 ? "empty" : count + " sliders"), _noWrapLabel, shrink, GUILayout.ExpandWidth(true));
                bool prev = GUI.enabled;
                GUI.enabled = count > 0;
                if (GUILayout.Button("Load", shrink, GUILayout.ExpandWidth(false))) SetShown(section, saved);
                GUI.enabled = prev;
                if (GUILayout.Button("Save", shrink, GUILayout.ExpandWidth(false))) presets[i].Value = CurrentPreset(section);
                GUILayout.EndHorizontal();
            }

            bool open = OpenLists.Contains(section);
            if (GUILayout.Button((open ? "Hide list" : "Choose sliders") + " (" + onCount + " of " + total + " on)", shrink))
            {
                if (open) OpenLists.Remove(section); else OpenLists.Add(section);
            }
            if (open)
            {
                // Two columns of checkboxes.
                List<PropertyFilter> props = new List<PropertyFilter>();
                foreach (PropertyFilter p in _bodyFilter.Properties) if (p.Section == section) props.Add(p);
                for (int i = 0; i < props.Count; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Math.Min(props.Count, i + 2); j++)
                    {
                        PropertyFilter p = props[j];
                        bool now = GUILayout.Toggle(p.ShownFlag, new GUIContent(p.Label, p.Label), _noWrapToggle, shrink, GUILayout.ExpandWidth(true));
                        if (now != p.ShownFlag)
                        {
                            List<string> on = new List<string>();
                            foreach (PropertyFilter q in props)
                                if (q == p ? now : q.ShownFlag) on.Add(q.Label);
                            SetShown(section, string.Join("|", on.ToArray()));
                        }
                    }
                    if (i + 1 >= props.Count) GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }

        public static BepInEx.Logging.LogLevel TimingLevel
        {
            get { return LogTimings != null && LogTimings.Value ? BepInEx.Logging.LogLevel.Info : BepInEx.Logging.LogLevel.Debug; }
        }

        private static ConfigDescription Desc(string text, int order)
        {
            return new ConfigDescription(text, null, new ConfigurationManagerAttributes { Order = order });
        }

        public static Rect LoadRect()
        {
            string[] p = (WindowRect.Value ?? "").Split(',');
            float x, y, w, h;
            if (p.Length == 4 && float.TryParse(p[0], out x) && float.TryParse(p[1], out y) &&
                float.TryParse(p[2], out w) && float.TryParse(p[3], out h) && w > 50 && h > 50)
                return new Rect(x, y, w, h);
            return new Rect(0, 0, 0, 0);
        }

    }
}
