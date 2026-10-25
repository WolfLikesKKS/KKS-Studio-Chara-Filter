// The IMGUI filter window. Structural changes (adding/removing ranges, resets) are queued and
// applied at the next Layout event, so IMGUI always sees the same set of controls within one event.
using System;
using System.Collections.Generic;
using System.Linq;
using KKAPI.Utilities;
using UnityEngine;

namespace StudioCharaFilter
{
    internal sealed partial class FilterWindow
    {
        private const int WindowId = 0x5C4F1;

        private readonly FilterState _filter;
        private readonly Action<float> _markDirty;
        private readonly FolderPicker _folders;
        private readonly Action<ListController, ScopeMode> _setMode;
        private readonly Action<ListController, int, bool> _setPage;
        private readonly Func<ListController, string, string> _removeCard;
        private readonly Func<ListController, RemovedEntry, string> _restoreCard;
        private readonly Func<List<CardInfo>> _allCards;

        public Rect WindowRect;
        private Vector2 _scroll;
        private string[] _sectionOrder = Settings.SectionIds;   // snapshot taken at Layout
        private readonly List<string> _drawnSections = new List<string>();
        private readonly HashSet<string> _collapsed = new HashSet<string>();
        private bool _arrange;
        private string _outfitNameText = "";
        private string _nameText = "";

        // Safe removal UI state
        private string _selPath;
        private string _selName = "";
        private string _confirmRemove;          // path awaiting confirmation
        private string _removeMessage = "";
        private bool _showRemoved;
        private bool _removalEnabled;
        private List<RemovedEntry> _removedList = new List<RemovedEntry>();
        private int _outfitSlotsShown = GameDefs.BaseOutfits;
        private readonly int[,] _outfitCounts = new int[FilterState.MaxOutfitSlots, 5]; // [slot, 0 nothing/1 underwear/2 dressed/3 towel/4 has slot]
        private readonly string[] _outfitCommonName = new string[FilterState.MaxOutfitSlots];
        private int _extraOutfitCards;
        private readonly int[] _anyOutfitCounts = new int[4];
        private bool _showOutfitSlots;
        private List<string> _missingSites = new List<string>();
        private int _siteCursor;
        private bool _showMissing;
        private bool _showUnused;
        private ZipmodReportResult _unused;
        private string _unusedSearch = "";
        private string _unusedSearchApplied = "";
        private string _unusedMessage = "";
        private bool _hideStudioPacks = true;
        private bool _uncensorEnabled;
        private List<KeyValuePair<string, int>> _uncensorCounts = new List<KeyValuePair<string, int>>();
        private bool _birthdayEnabled;
        private readonly int[] _monthCounts = new int[13]; // 0 = not set
        private int _maySeventeen;
        private bool _showLegacy;
        private string _authorSearch = "";
        private string _authorSearchApplied = "";
        private string _missingSearch = "";
        private string _missingSearchApplied = "";
        private string _exportMessage = "";

        // Stats for the content / author sections (recomputed when the index changes).
        private readonly int[] _regionUseCounts = new int[Regions.Count + 1];
        private readonly int[] _regionMissingCounts = new int[Regions.Count + 1];
        private List<KeyValuePair<string, int>> _missingMods = new List<KeyValuePair<string, int>>();
        private int _missingCards;
        private List<KeyValuePair<string, int>> _authorCounts = new List<KeyValuePair<string, int>>();
        private CardIndex _statsIndex;
        private bool _showHistograms = true;
        private readonly List<PropertyFilter> _visibleProps = new List<PropertyFilter>();
        private string _modSearch = "";
        private string _modSearchApplied = "";
        private string _folderSearch = "";
        private string _folderSearchApplied = "";
        private bool _showFolderPicker;
        private bool _showPager;
        private Vector2 _folderScroll;
        private List<KeyValuePair<string, int>> _modCounts = new List<KeyValuePair<string, int>>();
        private int _statsGeneration = -1;
        private int _statsSex = -1;
        private float _statsTrim = -1;
        private ScopeMode _statsMode;
        private int _statsScopeVersion = -1;
        private int _statsFolderVersion = -1;
        private bool _statsMay;
        private int _spanGeneration = -1;
        private int _spanSex = -1;
        private float _spanTrim = -1;
        private readonly List<Action> _pending = new List<Action>();

        private static readonly GUIContent[] ScopeButtons =
        {
            new GUIContent("Current folder", "Filters the folder currently shown in the character list (the one picked in Browser Folders, if installed)."),
            new GUIContent("Choose folders", "Pick one or more folders to list and filter their characters together."),
            new GUIContent("All folders", "Shows all characters from all folders to allow filtering across your whole collection."),
        };

        private GUIStyle _leftLabel;
        private GUIStyle _smallLabel;
        private GUIStyle _header, _subHeader, _arrowButton;
        private GUIStyle _smallButton;
        private GUIStyle _toggleRich;
        private GUIStyle _tooltipStyle;

        public FilterWindow(FilterState filter, FolderPicker folders, Action<float> markDirty, Action<ListController, ScopeMode> setMode,
            Action<ListController, int, bool> setPage, Func<ListController, string, string> removeCard,
            Func<ListController, RemovedEntry, string> restoreCard, Func<List<CardInfo>> allCards)
        {
            _allCards = allCards;
            _removeCard = removeCard;
            _restoreCard = restoreCard;
            _setPage = setPage;
            _filter = filter;
            _folders = folders;
            _markDirty = markDirty;
            _setMode = setMode;
        }

        public void OnGUI(ListController c, CardIndex idx)
        {
            float scale = Mathf.Clamp(Settings.WindowScale.Value, 0.75f, 2.5f);
            _scale = scale;
            float screenW = Screen.width / scale, screenH = Screen.height / scale;

            if (WindowRect.width < 1 || _loadedRectVersion != Settings.RectVersion)
            {
                _loadedRectVersion = Settings.RectVersion;
                WindowRect = Settings.LoadRect();
                if (WindowRect.width < 1)
                    WindowRect = new Rect(screenW - 490, screenH * 0.08f, 480, screenH * 0.84f);
            }

            // Resizing follows GUI mouse events: Input.GetMouseButton can't be used because eating input
            // over the window resets it every frame.
            if (_resizing)
            {
                Event re = Event.current;
                if (re.rawType == EventType.MouseUp) _resizing = false;
                else
                {
                    Vector2 m = re.mousePosition / scale;
                    WindowRect.width = _resizeStartSize.x + m.x - _resizeStartMouse.x;
                    WindowRect.height = _resizeStartSize.y + m.y - _resizeStartMouse.y;
                }
            }

            // Always keep the window on screen (after drags, resolution or scale changes, or VR screen changes),
            // so it can't get lost off the edge.
            WindowRect.width = Mathf.Clamp(WindowRect.width, 300, Mathf.Max(300, screenW));
            WindowRect.height = Mathf.Clamp(WindowRect.height, 200, Mathf.Max(200, screenH));
            WindowRect.x = Mathf.Clamp(WindowRect.x, 0, Mathf.Max(0, screenW - 80));
            WindowRect.y = Mathf.Clamp(WindowRect.y, 0, Mathf.Max(0, screenH - 40));
            if (float.IsNaN(WindowRect.x) || float.IsNaN(WindowRect.y)) WindowRect.position = new Vector2(20, 20);
            CurrentRect = WindowRect;

            GUISkin prevSkin = GUI.skin;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.skin = IMGUIUtils.SolidBackgroundGuiSkin;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            EnsureStyles();
            WindowRect = GUILayout.Window(WindowId, WindowRect, id => DrawWindow(id, c, idx),
                "Chara Filter (" + (c.Sex == 1 ? "female" : "male") + ")");
            GUI.matrix = prevMatrix;
            GUI.skin = prevSkin;

            // Stop clicks/scrolls over the window from reaching the Studio camera.
            IMGUIUtils.EatInputInRect(new Rect(WindowRect.x * scale, WindowRect.y * scale, WindowRect.width * scale, WindowRect.height * scale));
        }

        // Last on-screen rect, used by the F1 "Save current position" button.
        public static Rect CurrentRect;
        private int _loadedRectVersion = -1;

        // Resize handle in the bottom-right corner; works in window coordinates so it respects the window scale.
        private bool _resizing;
        private Vector2 _resizeStartMouse;
        private Vector2 _resizeStartSize;

        private void HandleResizeAndDrag()
        {
            GUI.Box(new Rect(WindowRect.width - 16, WindowRect.height - 16, 16, 16), new GUIContent("", "Drag to resize"));
            if (!_resizing) GUI.DragWindow(new Rect(0, 0, WindowRect.width, 20));
        }

        private float _scale = 1f;

        // Mouse position in window-scale GUI coordinates, read from Input so it works outside the window too.
        private Vector2 MouseInGui()
        {
            return new Vector2(Input.mousePosition.x / _scale, (Screen.height - Input.mousePosition.y) / _scale);
        }

        private void DrawTooltip()
        {
            string tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip) || Event.current.type != EventType.Repaint) return;
            GUIContent content = new GUIContent(tip);
            float w = Mathf.Min(280, WindowRect.width - 20);
            float h = _tooltipStyle.CalcHeight(content, w);
            Vector2 m = Event.current.mousePosition;
            float x = Mathf.Clamp(m.x + 14, 4, WindowRect.width - w - 4);
            float y = m.y + 20;
            if (y + h > WindowRect.height - 4) y = m.y - h - 6;
            GUI.Box(new Rect(x, y, w, h), content, _tooltipStyle);
        }

        private void EnsureStyles()
        {
            if (_leftLabel != null) return;
            _leftLabel = new GUIStyle(GUI.skin.label);
            _leftLabel.alignment = TextAnchor.MiddleLeft;
            _leftLabel.wordWrap = false;
            _leftLabel.clipping = TextClipping.Clip;
            _leftLabel.richText = true;
            _smallLabel = new GUIStyle(_leftLabel);
            int baseSize = GUI.skin.label.fontSize > 0 ? GUI.skin.label.fontSize : 13;
            _smallLabel.fontSize = Math.Max(9, baseSize - 2);
            _smallLabel.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            _smallButton = new GUIStyle(GUI.skin.button);
            _smallButton.fontSize = Math.Max(9, baseSize - 1);
            _toggleRich = new GUIStyle(GUI.skin.toggle);
            _toggleRich.richText = true;
            _toggleRich.wordWrap = false;
            _tooltipStyle = new GUIStyle(GUI.skin.box);
            _tooltipStyle.wordWrap = true;
            _tooltipStyle.alignment = TextAnchor.UpperLeft;
            _tooltipStyle.padding = new RectOffset(6, 6, 4, 4);
            _tooltipStyle.normal.background = IMGUIUtils.SolidBackgroundGuiSkin.window.normal.background;
            _tooltipStyle.normal.textColor = Color.white;
            _header = new GUIStyle(GUI.skin.button);
            _header.alignment = TextAnchor.MiddleLeft;
            _header.fontStyle = FontStyle.Bold;
            _subHeader = new GUIStyle(GUI.skin.button);
            _subHeader.alignment = TextAnchor.MiddleLeft;
            _subHeader.richText = true;
            _subHeader.wordWrap = false;
            _subHeader.clipping = TextClipping.Clip;
        }

        private void Queue(Action a)
        {
            _pending.Add(a);
        }

        private void DrawWindow(int id, ListController c, CardIndex idx)
        {
            // Check the resize corner before anything else can take the click (the scroll bar sits under it).
            Event ev = Event.current;
            Rect handle = new Rect(WindowRect.width - 18, WindowRect.height - 18, 18, 18);
            if (ev.type == EventType.MouseDown && ev.button == 0 && handle.Contains(ev.mousePosition))
            {
                _resizing = true;
                _resizeStartMouse = ev.mousePosition + new Vector2(WindowRect.x, WindowRect.y);
                _resizeStartSize = new Vector2(WindowRect.width, WindowRect.height);
                ev.Use();
            }

            if (Event.current.type == EventType.Layout)
            {
                if (_pending.Count > 0)
                {
                    foreach (Action a in _pending.ToArray()) a();
                    _pending.Clear();
                }
                _modSearchApplied = _modSearch ?? "";
                _showHistograms = Settings.ShowHistograms.Value;
                _visibleProps.Clear();
                foreach (PropertyFilter vp in _filter.Properties) if (vp.Visible) _visibleProps.Add(vp);
                UpdateStats(idx, c);
                _folders.Update(idx);
                _folderSearchApplied = _folderSearch ?? "";
                _showFolderPicker = c.Mode == ScopeMode.ChosenFolders;
                _authorSearchApplied = _authorSearch ?? "";
                _missingSearchApplied = _missingSearch ?? "";
                _unusedSearchApplied = _unusedSearch ?? "";
                _showLegacy = Settings.LegacyPluginFilter.Value;
                _birthdayEnabled = Settings.ShowBirthday.Value;
                _uncensorEnabled = Settings.ShowUncensor.Value;
                _colorsEnabled = Settings.ShowColors.Value;
                _profileEnabled = Settings.ShowProfile.Value;
                _detailsEnabled = Settings.ShowDetails.Value;
                _fileEnabled = Settings.ShowFileSection.Value;
                _randomEnabled = Settings.ShowRandom.Value && KKAPI.Studio.StudioAPI.InsideStudio;
                _sectionOrder = Settings.GetSectionOrder();
                _drawnSections.Clear();
                foreach (string sid in _sectionOrder) if (SectionEnabled(sid)) _drawnSections.Add(sid);
                _collapsed.Clear();
                foreach (string sid in (Settings.CollapsedSections.Value ?? "").Split('|')) if (sid.Length > 0) _collapsed.Add(sid);
                _nipSelected = _filter.NipTypes.Count;
                _dupSearchApplied = _dupSearch ?? "";
                _showPager = c.FromIndex && (c.PageCount > 1 || c.ShowAll);
                _selPath = c.SelectedPath;
                _selName = c.SelectedName;
                if (_confirmRemove != null && _confirmRemove != _selPath) _confirmRemove = null;
                _removedList = Removal.Entries();
                _removalEnabled = Settings.EnableRemoval.Value;
                if (!_removalEnabled) { _showRemoved = false; _confirmRemove = null; }
            }

            // Status + scope
            GUILayout.BeginHorizontal();
            GUILayout.Label(idx.Busy ? string.Format("Indexing cards... {0} / {1}", idx.Done, idx.Total) : "Index: " + idx.LastResult, _leftLabel, Shrink);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(_arrange ? "Done" : "Arrange", _arrange
                    ? "Finish rearranging the sections."
                    : "Change the order of the sections below. The order is saved (also editable in F1 under \"Section order\")."), GUILayout.Width(80)))
                Queue(delegate { _arrange = !_arrange; });
            if (GUILayout.Button(new GUIContent("Random filters", "Clears the filters and sets a random few (slider ranges, sometimes a " +
                "hair or eye color) picked from your cards. How narrow follows your collection: about 3 x the square root of the " +
                "cards in scope stay (100 -> ~30, 5,000 -> ~210). Press again for another mix."), GUILayout.Width(100)))
                Queue(delegate
                {
                    List<PropertyFilter> sliders = new List<PropertyFilter>();
                    foreach (PropertyFilter pf in _filter.Properties) if (_filter.PropertyApplies(pf)) sliders.Add(pf);
                    RandomFilter.RandomAll(_filter, sliders, _scopeCards);
                    _nameText = ""; _outfitNameText = "";
                    _markDirty(0f);
                });
            if (GUILayout.Button(new GUIContent("Reset all", "Clears every body range and mod data filter."), GUILayout.Width(80)))
                Queue(delegate { _filter.ResetAll(); _outfitNameText = ""; _nameText = ""; _markDirty(0f); });
            GUILayout.EndHorizontal();

            int scope = GUILayout.Toolbar((int)c.Mode, ScopeButtons);
            if (scope != (int)c.Mode)
            {
                ScopeMode mode = (ScopeMode)scope;
                Queue(delegate { _setMode(c, mode); });
            }

            if (_showFolderPicker) DrawFolderPicker(c);

            GUILayout.Label(c.Capped
                ? string.Format("{0} matches ({1} cards in scope) - page {2} of {3}", c.Matched, c.Total, c.Page + 1, c.PageCount)
                : (c.Mode == ScopeMode.ChosenFolders && _folders.Selected.Count == 0
                    ? "Tick one or more folders above to list their cards"
                    : string.Format("Showing {0} of {1} cards", c.Shown, c.Total)), _leftLabel, Shrink);
            if (_showPager) DrawPager(c);
            if (_randomEnabled) DrawRandomRow(c);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("<b>Name</b>", "Search by character name or card file name in the current scope. " +
                "Several words must all match (e.g. \"rei aya\")."), _leftLabel, GUILayout.Width(50));
            string nameText = GUILayout.TextField(_nameText ?? "");
            if (nameText != _nameText)
            {
                _nameText = nameText;
                _filter.SetNameQuery(nameText);
                _markDirty(c.FromIndex ? 0.5f : 0.3f);
            }
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);

            DrawSections(c, idx);

            // Kept last and collapsed on purpose, so removing a card is never one stray click away.
            if (_removalEnabled)
            {
                GUILayout.Space(12);
                if (GUILayout.Button(new GUIContent((_showRemoved ? "[-] " : "[+] ") + "Card removal (" + _removedList.Count + " removed)",
                    "Move the highlighted card out of your card folders, or restore removed cards. Can be turned off in F1."), _header))
                    Queue(delegate { _showRemoved = !_showRemoved; _confirmRemove = null; });
                if (_showRemoved)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    DrawSelectedCard(c);
                    GUILayout.Space(6);
                    DrawRemoved(c);
                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndScrollView();

            HandleResizeAndDrag();
            DrawTooltip();
        }

        // Lets long text shrink (and clip) instead of pushing the window wider than it is.
        private static readonly GUILayoutOption[] Shrink = { GUILayout.MinWidth(0), GUILayout.ExpandWidth(true) };

        private const float NumBoxWidth = 46;
        private const float RowExtras = NumBoxWidth * 2 + 10 + 24 + 20; // number boxes, dash, x button, spacing

        private void DrawProperty(PropertyFilter p, ListController c)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + p.Label + "</b>", _leftLabel, GUILayout.Width(130));
            GUILayout.Label(p.Summary(), _leftLabel, Shrink);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Random", "A random range for this value, picked from the cards that match the other " +
                "filters (it keeps a good share of them)."), GUILayout.Width(60)))
                Queue(delegate { RandomFilter.RandomRange(_filter, p, _scopeCards, false); _markDirty(0f); });
            bool prevEnabled = GUI.enabled;
            GUI.enabled = p.Active || p.Ranges.Count > 1;
            if (GUILayout.Button("Reset", GUILayout.Width(56)))
                Queue(delegate { p.Reset(); _markDirty(0f); });
            GUI.enabled = prevEnabled;
            GUILayout.EndHorizontal();

            if (_showHistograms) DrawHistogram(p);

            for (int i = 0; i < p.Ranges.Count; i++)
            {
                ValueRange r = p.Ranges[i];
                GUILayout.BeginHorizontal();
                Rect sr = GUILayoutUtility.GetRect(100, 18, GUILayout.ExpandWidth(true));
                float min = r.Min, max = r.Max;
                if (RangeSlider.Draw(sr, ref min, ref max, p.SpanMin, p.SpanMax, p.IsRangeActive(r)))
                {
                    r.Set(min, max);
                    _markDirty(c.FromIndex ? 0.6f : 0.25f);
                }

                string minText = GUILayout.TextField(r.MinText, GUILayout.Width(NumBoxWidth));
                GUILayout.Label("-", GUILayout.Width(10));
                string maxText = GUILayout.TextField(r.MaxText, GUILayout.Width(NumBoxWidth));
                if (minText != r.MinText || maxText != r.MaxText)
                {
                    r.MinText = minText;
                    r.MaxText = maxText;
                    float a, b;
                    if (float.TryParse(minText, out a) && float.TryParse(maxText, out b) && a <= b)
                    {
                        r.Min = a;
                        r.Max = b;
                        _markDirty(c.FromIndex ? 0.8f : 0.4f);
                    }
                }

                // Always drawn (disabled on the first range) so the control layout never changes mid-drag.
                prevEnabled = GUI.enabled;
                GUI.enabled = i > 0;
                int index = i;
                if (GUILayout.Button("x", GUILayout.Width(24)))
                    Queue(delegate { if (index < p.Ranges.Count) p.Ranges.RemoveAt(index); _markDirty(0f); });
                GUI.enabled = prevEnabled;
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("+ add range", "Add another range for this value. Cards matching any of the ranges are shown."), _smallButton, GUILayout.Width(90)))
                Queue(delegate { p.Ranges.Add(new ValueRange(p.SpanMin, p.SpanMax)); });
            if (GUILayout.Button(new GUIContent("+ random range", "Add another, random range for this value (cards in either range are shown)."),
                _smallButton, GUILayout.Width(110)))
                Queue(delegate { RandomFilter.RandomRange(_filter, p, _scopeCards, true); _markDirty(0f); });
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawHistogram(PropertyFilter p)
        {
            Rect hr = GUILayoutUtility.GetRect(100, Settings.HistogramHeight.Value, GUILayout.ExpandWidth(true));
            // Line the histogram up with the slider (which shares its row with the number boxes and x button).
            hr.width -= RowExtras;
            hr.x += 6;
            hr.width -= 12;
            if (Event.current.type != EventType.Repaint) return;

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.25f);
            GUI.DrawTexture(hr, Texture2D.whiteTexture);
            if (p.HistogramMax > 0)
            {
                float bw = hr.width / PropertyFilter.Bins;
                float binSpan = (p.SpanMax - p.SpanMin) / PropertyFilter.Bins;
                bool active = p.Active;
                for (int b = 0; b < PropertyFilter.Bins; b++)
                {
                    int count = p.Histogram[b];
                    if (count == 0) continue;
                    // Square-root scale so small groups of cards are still visible next to big ones.
                    float h = Mathf.Max(1f, hr.height * Mathf.Sqrt((float)count / p.HistogramMax));
                    float center = p.SpanMin + (b + 0.5f) * binSpan;
                    bool inRange = !active;
                    if (active)
                    {
                        foreach (ValueRange r in p.Ranges)
                        {
                            if (!p.IsRangeActive(r)) continue;
                            if (center >= r.Min - binSpan * 0.5f && center <= r.Max + binSpan * 0.5f) { inRange = true; break; }
                        }
                    }
                    GUI.color = inRange ? new Color(0.45f, 0.8f, 0.45f, 0.9f) : new Color(0.6f, 0.6f, 0.6f, 0.5f);
                    GUI.DrawTexture(new Rect(hr.x + b * bw + 1, hr.yMax - h, bw - 2, h), Texture2D.whiteTexture);
                }
            }
            GUI.color = prev;
        }

        private void DrawFolderPicker(ListController c)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Folders:</b> " + _folders.Summary(), _leftLabel, Shrink);
            GUILayout.FlexibleSpace();
            bool sub = GUILayout.Toggle(_folders.IncludeSubfolders, new GUIContent("Include subfolders",
                "Ticking a folder also includes every folder inside it."), GUILayout.ExpandWidth(false));
            if (sub != _folders.IncludeSubfolders)
            {
                _folders.IncludeSubfolders = sub;
                _folders.Version++;
                _markDirty(0.3f);
            }
            bool prevEnabled = GUI.enabled;
            GUI.enabled = _folders.Selected.Count > 0;
            if (GUILayout.Button("Clear", GUILayout.Width(52)))
                Queue(delegate { _folders.Selected.Clear(); _folders.Version++; _markDirty(0f); });
            GUI.enabled = prevEnabled;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            _folderSearch = GUILayout.TextField(_folderSearch ?? "");
            GUILayout.EndHorizontal();

            _folderScroll = GUILayout.BeginScrollView(_folderScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(Settings.FolderListHeight.Value));
            bool searching = _folderSearchApplied.Length > 0;
            foreach (FolderNode n in _folders.Nodes)
            {
                if (searching)
                {
                    if (n.Path.IndexOf(_folderSearchApplied, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                else if (!_folders.IsVisible(n)) continue;

                GUILayout.BeginHorizontal();
                if (!searching) GUILayout.Space(n.Depth * 14);

                // Expand/collapse arrow (a fixed-width label when the folder has no subfolders, to keep columns aligned).
                if (!searching && n.HasChildren)
                {
                    bool open = _folders.Expanded.Contains(n.Key);
                    string key = n.Key;
                    if (GUILayout.Button(open ? "-" : "+", GUILayout.Width(22)))
                        Queue(delegate { if (!_folders.Expanded.Remove(key)) _folders.Expanded.Add(key); });
                }
                else
                {
                    GUILayout.Label("", GUILayout.Width(22));
                }

                bool sel = _folders.Selected.Contains(n.Key);
                string label = (searching && n.Key.Length > 0 ? n.Path : n.Name) +
                    "  <color=#9a9a9a>(" + (n.HasChildren && n.Total != n.Direct ? n.Direct + " / " + n.Total : n.Total.ToString()) + ")</color>";
                bool now = GUILayout.Toggle(sel, label, _toggleRich, Shrink);
                if (now != sel)
                {
                    if (now) _folders.Selected.Add(n.Key);
                    else _folders.Selected.Remove(n.Key);
                    _folders.Version++;
                    _markDirty(0.4f);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label("Numbers: cards in the folder / including its subfolders.", _smallLabel, Shrink);
            GUILayout.EndVertical();
        }

        private static readonly string[] OutfitModeText = { "any", "dressed", "towel", "underwear", "nothing" };
        private static readonly string[] ExtraText = { "any", "has extra", "only base " + GameDefs.BaseOutfits };

        private static readonly string[] AnyOutfitLabels = { "Nothing on", "Underwear / swimwear", "Dressed", "Bath towel" };
        private static readonly string[] AnyOutfitTips =
        {
            "An outfit with no top, bottom, bra, underwear or pantyhose (shoes, socks and gloves don't count).",
            "An outfit with only bra / underwear / pantyhose - most swimsuits count here.",
            "An outfit with a top or bottom (towels excluded).",
            "An outfit whose top is a bath towel, with no bottom.",
        };
        private static readonly int[] AnyOutfitOrder = { TowelNames.Towel, TowelNames.Nothing, TowelNames.Underwear, TowelNames.Dressed };
        private static readonly string[] HasText = { "any", "has one", "none" };

        private void DrawClothing()
        {
            GUILayout.Label("Does the character have at least one outfit (in any outfit slot) that is...", _smallLabel, Shrink);
            Color prevColor0 = GUI.color;
            foreach (int st in AnyOutfitOrder)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(AnyOutfitLabels[st], AnyOutfitTips[st]), _leftLabel, GUILayout.Width(150));
                ModFilterMode am = _filter.AnyOutfit[st];
                GUI.color = ModeColor(am, prevColor0);
                if (GUILayout.Button(new GUIContent(HasText[(int)am], "any = ignore, has one = at least one outfit like this, none = no outfit like this"),
                    GUILayout.Width(86)))
                {
                    _filter.AnyOutfit[st] = (ModFilterMode)(((int)am + 1) % 3);
                    _markDirty(0.1f);
                }
                GUI.color = prevColor0;
                GUILayout.Space(4);
                GUILayout.Label("<color=#9a9a9a>" + _anyOutfitCounts[st] + " cards have one</color>", _leftLabel, Shrink);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Extra outfits", "Outfits beyond the standard " + GameDefs.BaseOutfits + ", added with More Outfits."), _leftLabel, GUILayout.Width(150));
            ModFilterMode e = _filter.ExtraOutfits;
            GUI.color = ModeColor(e, prevColor0);
            if (GUILayout.Button(ExtraText[(int)e], GUILayout.Width(86)))
            {
                _filter.ExtraOutfits = (ModFilterMode)(((int)e + 1) % 3);
                _markDirty(0.1f);
            }
            GUI.color = prevColor0;
            GUILayout.Space(4);
            GUILayout.Label("<color=#9a9a9a>" + _extraOutfitCards + " cards</color>", _leftLabel, Shrink);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Outfit name", "Shows cards with an extra outfit whose name contains this text (e.g. maid, school)."),
                _leftLabel, GUILayout.Width(150));
            string text = GUILayout.TextField(_outfitNameText ?? "");
            if (text != _outfitNameText)
            {
                _outfitNameText = text;
                _filter.OutfitNameQuery = text.Trim();
                _markDirty(0.6f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            if (GUILayout.Button((_showOutfitSlots ? "[-] " : "[+] ") + "Per outfit slot (advanced)", _smallButton))
                Queue(delegate { _showOutfitSlots = !_showOutfitSlots; });
            if (_showOutfitSlots)
            {
                GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Outfit</b>", _leftLabel, GUILayout.Width(150));
            GUILayout.Label("<b>Filter</b>", _leftLabel, GUILayout.Width(90));
            GUILayout.Label("<color=#9a9a9a>dressed / towel / underwear / nothing</color>", _leftLabel, Shrink);
            GUILayout.EndHorizontal();

            Color prevColor = GUI.color;
            for (int i = 0; i < _outfitSlotsShown; i++)
            {
                GUILayout.BeginHorizontal();
                string label = i < GameDefs.BaseOutfits ? GameDefs.OutfitNames[i] : "Outfit " + (i + 1);
                string common = _outfitCommonName[i];
                bool showCommon = i >= GameDefs.BaseOutfits && common != null && !string.Equals(common, label, StringComparison.OrdinalIgnoreCase);
                GUILayout.Label(new GUIContent(label + (showCommon ? "  <color=#9a9a9a>" + common + "</color>" : ""),
                    i >= GameDefs.BaseOutfits ? "Extra outfit from More Outfits. Gray text = most common name for this slot." : ""), _leftLabel, GUILayout.Width(150));
                int m = _filter.OutfitModes[i];
                GUI.color = m == 0 ? prevColor : new Color(0.5f, 1f, 0.5f);
                if (GUILayout.Button(new GUIContent(OutfitModeText[m], "Click to cycle: any -> dressed -> towel -> underwear -> nothing"), GUILayout.Width(86)))
                {
                    _filter.OutfitModes[i] = (m + 1) % FilterState.OutfitModeCount;
                    _markDirty(0.1f);
                }
                GUI.color = prevColor;
                GUILayout.Space(4);
                GUILayout.Label("<color=#9a9a9a>" + _outfitCounts[i, 2] + " / " + _outfitCounts[i, 3] + " / " + _outfitCounts[i, 1] + " / " + _outfitCounts[i, 0] +
                    (i >= GameDefs.BaseOutfits ? "  (" + _outfitCounts[i, 4] + " cards have it)" : "") + "</color>", _leftLabel, Shrink);
                GUILayout.EndHorizontal();
            }

                GUILayout.EndVertical();
            }
        }

        private void DrawSelectedCard(ListController c)
        {
            GUILayout.BeginHorizontal();
            if (_selPath == null)
            {
                GUILayout.Label("<color=#9a9a9a>Highlight a card in the character list first.</color>", _leftLabel, Shrink);
                GUI.enabled = false;
                GUILayout.Button("Remove highlighted card...", GUILayout.Width(170));
                GUI.enabled = true;
            }
            else if (_confirmRemove == null)
            {
                GUILayout.Label("Highlighted: <b>" + _selName + "</b>", _leftLabel, Shrink);
                string path = _selPath;
                if (GUILayout.Button(new GUIContent("Remove highlighted card...", "Moves this card out of your card folders into \"" + Removal.FolderName +
                    "\" next to CharaStudio.exe. Nothing is deleted, and it can be restored from the list below."), GUILayout.Width(170)))
                    Queue(delegate { _confirmRemove = path; _removeMessage = ""; });
            }
            else
            {
                GUILayout.Label("Remove <b>" + _selName + "</b>? (can be restored)", _leftLabel, Shrink);
                string path = _confirmRemove;
                Color prev = GUI.color;
                GUI.color = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Yes, remove", GUILayout.Width(90)))
                {
                    Queue(delegate
                    {
                        _confirmRemove = null;
                        string err = _removeCard(c, path);
                        _removeMessage = err == null ? "Moved to " + Removal.FolderName + " - it can be restored from the list below." : "Could not remove: " + err;
                    });
                }
                GUI.color = prev;
                if (GUILayout.Button("Cancel", GUILayout.Width(60)))
                    Queue(delegate { _confirmRemove = null; });
            }
            GUILayout.EndHorizontal();
            if (_removeMessage.Length > 0) GUILayout.Label(_removeMessage, _smallLabel, Shrink);
        }

        private void DrawRemoved(ListController c)
        {
            GUILayout.Label("Removed cards are kept in \"" + Removal.FolderName + "\" next to CharaStudio.exe. Nothing is deleted. " +
                "To clear them out for good, use F1 > Removed cards (sends them to the Recycle Bin).", _smallLabel, Shrink);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open folder", GUILayout.Width(100))) Removal.OpenFolder();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            int shown = 0, limit = Settings.ListRowLimit.Value;
            foreach (RemovedEntry e in _removedList)
            {
                if (++shown > limit) break;
                GUILayout.BeginHorizontal();
                string name = System.IO.Path.GetFileNameWithoutExtension(e.Original);
                string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(e.Original));
                GUILayout.Label(new GUIContent(name + "  <color=#9a9a9a>" + folder + "  " + e.Time.ToString("yyyy-MM-dd HH:mm") + "</color>",
                    "Original location: " + e.Original), _leftLabel, Shrink);
                RemovedEntry entry = e;
                if (GUILayout.Button(new GUIContent("Restore", "Move the card back to where it was."), GUILayout.Width(70)))
                {
                    Queue(delegate
                    {
                        string err = _restoreCard(c, entry);
                        _removeMessage = err == null ? "Restored " + System.IO.Path.GetFileName(entry.Original) + "." : "Could not restore: " + err;
                    });
                }
                GUILayout.EndHorizontal();
            }
            if (shown > limit)
                GUILayout.Label("More not shown (row limit can be changed in F1).", _smallLabel, Shrink);
            if (_removedList.Count == 0)
                GUILayout.Label("No removed cards.", _smallLabel, Shrink);
        }

        private void DrawUncensor()
        {
            GUILayout.Label("Which uncensor body each card asks Uncensor Selector for. Tick one or more to show only those cards. " +
                "\"Default\" cards use whatever default you set in Uncensor Selector.", _smallLabel, Shrink);
            foreach (KeyValuePair<string, int> kv in _uncensorCounts)
            {
                bool on = _filter.UncensorBodies.Contains(kv.Key);
                string name = UncensorNames.Get(kv.Key);
                string label = name + (name != kv.Key && kv.Key.Length > 0 && Settings.ShowModIds.Value ? "  <color=#9a9a9a><size=10>" + kv.Key + "</size></color>" : "") +
                    "  <color=#9a9a9a>(" + kv.Value + ")</color>";
                bool now = GUILayout.Toggle(on, label, _toggleRich, Shrink);
                if (now != on)
                {
                    if (now) _filter.UncensorBodies.Add(kv.Key);
                    else _filter.UncensorBodies.Remove(kv.Key);
                    _markDirty(0.2f);
                }
            }
            bool prevEn = GUI.enabled;
            GUI.enabled = _filter.UncensorBodies.Count > 0;
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                Queue(delegate { _filter.UncensorBodies.Clear(); _markDirty(0f); });
            GUI.enabled = prevEn;
        }

        private static readonly string[] MonthNames = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        private void DrawBirthday()
        {
            GUILayout.Label("Cards store a birthday (month and day), not an age. Tick months to show only those birthdays." +
                (Settings.MaySeventeenIsUnset.Value ? " May 17 is the Maker's default and counts as \"not set\" (changeable in F1)." : ""),
                _smallLabel, Shrink);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Birthday set", _leftLabel, GUILayout.Width(110));
            ModFilterMode bs = _filter.BirthdaySet;
            Color prev = GUI.color;
            GUI.color = ModeColor(bs, prev);
            if (GUILayout.Button(new GUIContent(bs == ModFilterMode.Require ? "set" : bs == ModFilterMode.Exclude ? "not set" : "any",
                "any = ignore, set = only cards with a real birthday, not set = only cards without one"), GUILayout.Width(80)))
            {
                _filter.BirthdaySet = (ModFilterMode)(((int)bs + 1) % 3);
                _markDirty(0.1f);
            }
            GUI.color = prev;
            GUILayout.Space(4);
            GUILayout.Label("<color=#9a9a9a>" + _monthCounts[0] + " cards not set" +
                (Settings.MaySeventeenIsUnset.Value ? " (" + _maySeventeen + " on May 17)" : "") + "</color>", _leftLabel, Shrink);
            GUILayout.EndHorizontal();

            for (int row = 0; row < 3; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 4; col++)
                {
                    int month = row * 4 + col + 1;
                    bool on = _filter.BirthMonths.Contains(month);
                    bool now = GUILayout.Toggle(on, MonthNames[month] + " <color=#9a9a9a>(" + _monthCounts[month] + ")</color>", _toggleRich, GUILayout.Width(95));
                    if (now != on)
                    {
                        if (now) _filter.BirthMonths.Add(month);
                        else _filter.BirthMonths.Remove(month);
                        _markDirty(0.2f);
                    }
                }
                GUILayout.EndHorizontal();
            }
            bool prevEn = GUI.enabled;
            GUI.enabled = _filter.BirthMonths.Count > 0;
            if (GUILayout.Button("Clear months", GUILayout.Width(100)))
                Queue(delegate { _filter.BirthMonths.Clear(); _markDirty(0f); });
            GUI.enabled = prevEn;
        }

        private void DrawPager(ListController c)
        {
            GUILayout.BeginHorizontal();
            bool prev = GUI.enabled;
            GUI.enabled = !c.ShowAll && c.Page > 0;
            if (GUILayout.Button("<", GUILayout.Width(30)))
            {
                int p = c.Page - 1;
                Queue(delegate { _setPage(c, p, false); });
            }
            GUI.enabled = prev;
            GUILayout.Label(c.ShowAll ? "All " + c.Matched + " matches listed" : string.Format("Page {0} / {1}", c.Page + 1, c.PageCount),
                _leftLabel, GUILayout.Width(170));
            GUI.enabled = !c.ShowAll && c.Page < c.PageCount - 1;
            if (GUILayout.Button(">", GUILayout.Width(30)))
            {
                int p = c.Page + 1;
                Queue(delegate { _setPage(c, p, false); });
            }
            GUI.enabled = prev;
            GUILayout.FlexibleSpace();
            if (c.ShowAll)
            {
                if (GUILayout.Button(new GUIContent("Use pages", "Go back to listing one page at a time."), GUILayout.Width(110)))
                    Queue(delegate { _setPage(c, 0, false); });
            }
            else if (GUILayout.Button(new GUIContent("Show all (slow)",
                "Lists every match at once. With thousands of cards this can take several seconds and makes the list slower to scroll."),
                GUILayout.Width(110)))
                Queue(delegate { _setPage(c, 0, true); });
            GUILayout.EndHorizontal();
        }

        private static readonly string[] UsesText = { "any", "yes", "no" };
        private static readonly string[] MissingText = { "any", "missing", "complete" };

        private void DrawContent()
        {
            if (!SideloaderBridge.Available)
                GUILayout.Label("Sideloader wasn't found, so missing-mod checks are off.", _smallLabel, Shrink);
            GUILayout.Label("Uses mods: the card has zipmod items in that region. Missing: some of them aren't installed. " +
                "Hair includes hair-as-accessory mods.", _smallLabel, Shrink);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Region</b>", _leftLabel, GUILayout.Width(120));
            GUILayout.Label("<b>Uses mods</b>", _leftLabel, GUILayout.Width(80));
            GUILayout.Label("<b>Missing</b>", _leftLabel, GUILayout.Width(80));
            GUILayout.Label(new GUIContent("<color=#9a9a9a>cards using / missing</color>", "Counts are for the current scope."), _leftLabel, Shrink);
            GUILayout.EndHorizontal();

            Color prevColor = GUI.color;
            for (int i = 0; i <= Regions.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(i == 0 ? "<b>Any region</b>" : Regions.Names[i - 1], _leftLabel, GUILayout.Width(120));

                ModFilterMode u = _filter.RegionUses[i];
                GUI.color = ModeColor(u, prevColor);
                if (GUILayout.Button(new GUIContent(UsesText[(int)u], "any = ignore, yes = must use zipmod items here, no = must not"), GUILayout.Width(76)))
                {
                    _filter.RegionUses[i] = (ModFilterMode)(((int)u + 1) % 3);
                    _markDirty(0.1f);
                }
                GUILayout.Space(4);
                ModFilterMode m = _filter.RegionMissing[i];
                GUI.color = ModeColor(m, prevColor);
                GUI.enabled = SideloaderBridge.Available;
                if (GUILayout.Button(new GUIContent(MissingText[(int)m],
                    "any = ignore, missing = has zipmods here that aren't installed, complete = nothing missing here"), GUILayout.Width(76)))
                {
                    _filter.RegionMissing[i] = (ModFilterMode)(((int)m + 1) % 3);
                    _markDirty(0.1f);
                }
                GUI.enabled = true;
                GUI.color = prevColor;
                GUILayout.Space(4);
                GUILayout.Label("<color=#9a9a9a>" + _regionUseCounts[i] + " / " + _regionMissingCounts[i] + "</color>", _leftLabel, Shrink);
                GUILayout.EndHorizontal();
            }
            GUI.color = prevColor;

            GUILayout.Space(4);
            if (GUILayout.Button((_showMissing ? "[-] " : "[+] ") + string.Format("Missing mods report - all folders ({0} zipmods missing on {1} cards)",
                _missingMods.Count, _missingCards), _smallButton))
                Queue(delegate { _showMissing = !_showMissing; });
            if (_showMissing) DrawMissingReport();

            GUILayout.Space(2);
            if (GUILayout.Button(new GUIContent((_showUnused ? "[-] " : "[+] ") + "Installed zipmods not used by any card" +
                (_unused != null && _unused.Error == null ? " (" + _unused.Unused.Count + ", " + ZipmodReport.FormatSize(_unused.UnusedSize) + ")" : ""),
                "Information only - helps find mods you may not need. Nothing is changed or deleted."), _smallButton))
                Queue(delegate { _showUnused = !_showUnused; });
            if (_showUnused) DrawUnusedReport();
        }

        private static Color ModeColor(ModFilterMode m, Color normal)
        {
            return m == ModFilterMode.Require ? new Color(0.5f, 1f, 0.5f) : m == ModFilterMode.Exclude ? new Color(1f, 0.55f, 0.55f) : normal;
        }

        private void DrawMissingReport()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            _missingSearch = GUILayout.TextField(_missingSearch ?? "");
            if (GUILayout.Button(new GUIContent("Save list", "Writes the full list (cards, mod ID, name, author, website) to a text file in UserData."),
                GUILayout.Width(80)))
                Queue(ExportMissing);
            GUILayout.EndHorizontal();
            if (_exportMessage.Length > 0) GUILayout.Label(_exportMessage, _smallLabel, Shrink);

            GUILayout.BeginHorizontal();
            int batch = Settings.LinkBatchSize.Value;
            bool prevEn = GUI.enabled;
            GUI.enabled = _siteCursor < _missingSites.Count;
            if (GUILayout.Button(new GUIContent("Open next " + batch + " sites",
                "Opens the websites of the most-needed missing mods in your browser, a batch at a time (batch size is set in F1). " +
                "Each site is opened once even if several missing mods share it."), GUILayout.Width(130)))
                Queue(OpenNextSites);
            GUI.enabled = _siteCursor > 0;
            if (GUILayout.Button(new GUIContent("Restart", "Start again from the first site."), GUILayout.Width(64)))
                Queue(delegate { _siteCursor = 0; });
            GUI.enabled = prevEn;
            GUILayout.Label(string.Format("{0} of {1} sites opened", _siteCursor, _missingSites.Count), _leftLabel, Shrink);
            GUILayout.EndHorizontal();

            int shown = 0, limit = Settings.ListRowLimit.Value;
            foreach (KeyValuePair<string, int> kv in _missingMods)
            {
                ModMeta meta = _statsIndex != null ? _statsIndex.GetMeta(kv.Key) : null;
                string name = meta != null && meta.Name.Length > 0 ? meta.Name : kv.Key;
                if (_missingSearchApplied.Length > 0 &&
                    kv.Key.IndexOf(_missingSearchApplied, StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf(_missingSearchApplied, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (meta == null || meta.Author.IndexOf(_missingSearchApplied, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                if (++shown > limit) break;

                string website = meta != null ? meta.Website : "";
                GUILayout.BeginHorizontal();
                string author = meta != null && meta.Author.Length > 0 ? " by " + meta.Author : "";
                GUILayout.Label(new GUIContent(name + "<color=#9a9a9a>" + author + "  " + kv.Key + "</color>",
                    website.Length > 0 ? website : "No website saved on the cards for this mod"), _leftLabel, Shrink);
                GUILayout.Label(kv.Value + " cards", _leftLabel, GUILayout.Width(70));
                GUI.enabled = website.Length > 0;
                if (GUILayout.Button(new GUIContent("Copy", "Copy the mod's website link"), GUILayout.Width(46)))
                    GUIUtility.systemCopyBuffer = website;
                if (GUILayout.Button(new GUIContent("Open", "Open the mod's website in your browser"), GUILayout.Width(46)))
                    Application.OpenURL(website);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (shown > limit)
                GUILayout.Label("More not shown - use the search box (row limit can be changed in F1).", _smallLabel, Shrink);
            if (_missingMods.Count == 0)
                GUILayout.Label(SideloaderBridge.Available ? "No missing zipmods found on your cards." : "Sideloader not found.", _smallLabel, Shrink);
            GUILayout.EndVertical();
        }

        private void OpenNextSites()
        {
            int batch = Math.Max(1, Settings.LinkBatchSize.Value);
            int end = Math.Min(_missingSites.Count, _siteCursor + batch);
            for (int i = _siteCursor; i < end; i++)
            {
                try { Application.OpenURL(_missingSites[i]); }
                catch (Exception e) { StudioCharaFilterPlugin.Log.LogWarning("[SCF] Could not open " + _missingSites[i] + ": " + e.Message); }
            }
            StudioCharaFilterPlugin.Log.LogInfo(string.Format("[SCF] Opened missing-mod sites {0}-{1} of {2}", _siteCursor + 1, end, _missingSites.Count));
            _siteCursor = end;
        }

        private void DrawUnusedReport()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Zipmods that are installed but not used by any of your character cards (female and male). " +
                "<b>Only character cards are checked</b> - Studio items, maps, scene content and Maker-only items can still need these mods, " +
                "so this is a starting point for cleanup, not a delete list. Nothing is changed.", _smallLabel, Shrink);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(_unused == null ? "Check now" : "Check again", "Takes a moment with many zipmods."), GUILayout.Width(100)))
                Queue(delegate
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    _unused = ZipmodReport.Build(_allCards());
                    _unusedMessage = _unused.Error ?? string.Format("Checked {0} installed zipmods in {1} ms.", _unused.Installed, sw.ElapsedMilliseconds);
                });
            bool prevEn = GUI.enabled;
            GUI.enabled = _unused != null && _unused.Error == null && _unused.Unused.Count > 0;
            if (GUILayout.Button(new GUIContent("Save list", "Writes the full list to UserData\\StudioCharaFilter_UnusedZipmods.txt"), GUILayout.Width(80)))
                Queue(delegate
                {
                    try { _unusedMessage = "Saved to " + CardInfo.DisplayPath(ZipmodReport.Export(_unused)); }
                    catch (Exception e) { _unusedMessage = "Could not save: " + e.Message; }
                });
            GUI.enabled = prevEn;
            bool hide = GUILayout.Toggle(_hideStudioPacks, new GUIContent("Hide Studio / map packs",
                "Hides folders with \"Studio\" or \"Map\" in their name - those mods are for scenes, so they never show up on cards."));
            if (hide != _hideStudioPacks) Queue(delegate { _hideStudioPacks = hide; }); // changes the row count, so wait for the next Layout
            GUILayout.EndHorizontal();
            if (_unusedMessage.Length > 0) GUILayout.Label(_unusedMessage, _smallLabel, Shrink);

            if (_unused != null && _unused.Error == null)
            {
                GUILayout.Label(string.Format("<b>{0}</b> of {1} installed zipmods ({2}) aren't used by any character card; {3} are.",
                    _unused.Unused.Count, _unused.Installed, ZipmodReport.FormatSize(_unused.UnusedSize), _unused.UsedByCards), _leftLabel, Shrink);

                GUILayout.Label("<b>By folder</b>", _leftLabel);
                foreach (KeyValuePair<string, int> kv in _unused.FolderCounts)
                {
                    if (_hideStudioPacks && IsStudioPack(kv.Key)) continue;
                    long sz;
                    _unused.FolderSizes.TryGetValue(kv.Key, out sz);
                    GUILayout.Label("  " + kv.Key + "  <color=#9a9a9a>" + kv.Value + " zipmods, " + ZipmodReport.FormatSize(sz) + "</color>", _leftLabel, Shrink);
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Search", GUILayout.Width(50));
                _unusedSearch = GUILayout.TextField(_unusedSearch ?? "");
                GUILayout.EndHorizontal();

                int shown = 0, limit = Settings.ListRowLimit.Value;
                foreach (InstalledMod m in _unused.Unused)
                {
                    if (_hideStudioPacks && IsStudioPack(m.Folder)) continue;
                    if (_unusedSearchApplied.Length > 0 &&
                        m.Name.IndexOf(_unusedSearchApplied, StringComparison.OrdinalIgnoreCase) < 0 &&
                        m.Guid.IndexOf(_unusedSearchApplied, StringComparison.OrdinalIgnoreCase) < 0 &&
                        m.Author.IndexOf(_unusedSearchApplied, StringComparison.OrdinalIgnoreCase) < 0 &&
                        m.Folder.IndexOf(_unusedSearchApplied, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (++shown > limit) break;
                    string name = m.Name.Length > 0 ? m.Name : m.Guid;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(new GUIContent(name + "<color=#9a9a9a>" + (m.Author.Length > 0 ? " by " + m.Author : "") + "  " + m.Folder + "</color>",
                        m.File), _leftLabel, Shrink);
                    GUILayout.Label(ZipmodReport.FormatSize(m.Size), _leftLabel, GUILayout.Width(60));
                    GUILayout.EndHorizontal();
                }
                if (shown > limit)
                    GUILayout.Label("More not shown - use the search box or Save list (row limit can be changed in F1).", _smallLabel, Shrink);
            }
            GUILayout.EndVertical();
        }

        private static bool IsStudioPack(string folder)
        {
            return folder.IndexOf("studio", StringComparison.OrdinalIgnoreCase) >= 0 || folder.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ExportMissing()
        {
            try
            {
                string path = System.IO.Path.Combine(UserData.Path, "StudioCharaFilter_MissingMods.txt");
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("Cards\tMod ID\tName\tAuthor\tWebsite");
                foreach (KeyValuePair<string, int> kv in _missingMods)
                {
                    ModMeta meta = _statsIndex != null ? _statsIndex.GetMeta(kv.Key) : null;
                    sb.AppendLine(kv.Value + "\t" + kv.Key + "\t" + (meta != null ? meta.Name : "") + "\t" +
                        (meta != null ? meta.Author : "") + "\t" + (meta != null ? meta.Website : ""));
                }
                System.IO.File.WriteAllText(path, sb.ToString());
                _exportMessage = "Saved to " + CardInfo.DisplayPath(path);
                StudioCharaFilterPlugin.Log.LogInfo("[SCF] Missing mods list saved to " + path);
            }
            catch (Exception e)
            {
                _exportMessage = "Could not save: " + e.Message;
            }
        }

        private void DrawAuthors()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            _authorSearch = GUILayout.TextField(_authorSearch ?? "");
            bool prev = GUI.enabled;
            GUI.enabled = _filter.Authors.Count > 0;
            if (GUILayout.Button("Clear", GUILayout.Width(52)))
                Queue(delegate { _filter.Authors.Clear(); _markDirty(0f); });
            GUI.enabled = prev;
            GUILayout.EndHorizontal();
            GUILayout.Label(_filter.Authors.Count == 0
                ? "Tick authors to show only their cards. Counts are for the current scope (Current folder / Choose folders / All folders)."
                : "Selected: " + string.Join(", ", _filter.Authors.ToArray()), _smallLabel, Shrink);

            int shown = 0, limit = Settings.ListRowLimit.Value;
            foreach (KeyValuePair<string, int> kv in _authorCounts)
            {
                bool sel = _filter.Authors.Contains(kv.Key);
                if (_authorSearchApplied.Length > 0 && kv.Key.IndexOf(_authorSearchApplied, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++shown > limit) break;
                bool now = GUILayout.Toggle(sel, kv.Key + "  <color=#9a9a9a>(" + kv.Value + ")</color>", _toggleRich, Shrink);
                if (now != sel)
                {
                    if (now) _filter.Authors.Add(kv.Key);
                    else _filter.Authors.Remove(kv.Key);
                    _markDirty(0.3f);
                }
            }
            if (shown > limit)
                GUILayout.Label("More authors not shown - use the search box (row limit can be changed in F1).", _smallLabel, Shrink);
            if (_authorCounts.Count == 0)
                GUILayout.Label("No author info found on these cards.", _smallLabel, Shrink);
        }

        private void DrawMods()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            _modSearch = GUILayout.TextField(_modSearch ?? "");
            GUILayout.EndHorizontal();
            GUILayout.Label("Click the button to cycle: any -> HAS -> NOT. Count = cards with that data.", _smallLabel, Shrink);

            Color prevColor = GUI.color;
            foreach (KeyValuePair<string, int> kv in _modCounts)
            {
                string name = ModNames.Get(kv.Key);
                if (_modSearchApplied.Length > 0 &&
                    kv.Key.IndexOf(_modSearchApplied, StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf(_modSearchApplied, StringComparison.OrdinalIgnoreCase) < 0) continue;

                ModFilterMode mode;
                _filter.Mods.TryGetValue(kv.Key, out mode);
                GUILayout.BeginHorizontal();
                GUI.color = mode == ModFilterMode.Require ? new Color(0.5f, 1f, 0.5f)
                    : mode == ModFilterMode.Exclude ? new Color(1f, 0.55f, 0.55f) : prevColor;
                string stateText = mode == ModFilterMode.Require ? "HAS" : mode == ModFilterMode.Exclude ? "NOT" : "any";
                if (GUILayout.Button(new GUIContent(stateText, "any = ignore, HAS = card must have this data, NOT = card must not have it"), GUILayout.Width(46)))
                {
                    ModFilterMode next = (ModFilterMode)(((int)mode + 1) % 3);
                    if (next == ModFilterMode.Ignore) _filter.Mods.Remove(kv.Key);
                    else _filter.Mods[kv.Key] = next;
                    _markDirty(0.1f);
                }
                GUI.color = prevColor;
                string label = (name == kv.Key || !Settings.ShowModIds.Value) ? name : name + "  <color=#9a9a9a><size=10>" + kv.Key + "</size></color>";
                GUILayout.Label(label, _leftLabel, Shrink);
                GUILayout.Label(kv.Value.ToString(), _leftLabel, GUILayout.Width(44));
                GUILayout.EndHorizontal();
            }
            GUI.color = prevColor;
        }

        // Counts and graphs describe the current scope (Current folder / Choose folders / All folders);
        // slider spans and the missing mods report use the whole collection.
        private List<CardInfo> _scopeCards = new List<CardInfo>();   // the cards in the current scope (for random filters)

        private void UpdateStats(CardIndex idx, ListController c)
        {
            if (_statsGeneration == idx.Generation && _statsSex == idx.Sex && _statsTrim == Settings.OutlierTrim.Value &&
                _statsMode == c.Mode && _statsScopeVersion == c.ScopeVersion && _statsFolderVersion == _folders.Version &&
                _statsMay == Settings.MaySeventeenIsUnset.Value) return;
            _statsMay = Settings.MaySeventeenIsUnset.Value;
            _statsTrim = Settings.OutlierTrim.Value;
            _statsGeneration = idx.Generation;
            _statsSex = idx.Sex;
            _statsMode = c.Mode;
            _statsScopeVersion = c.ScopeVersion;
            _statsFolderVersion = _folders.Version;

            List<CardInfo> all = idx.Snapshot();
            List<CardInfo> cards = new List<CardInfo>(all.Count);
            foreach (CardInfo ci in all)
            {
                if (c.Mode == ScopeMode.CurrentFolder && !c.FolderKeys.Contains(ci.Key)) continue;
                if (c.Mode == ScopeMode.ChosenFolders && !_folders.Matches(ci)) continue;
                cards.Add(ci);
            }

            _scopeCards = cards;
            bool spanChanged = _spanGeneration != idx.Generation || _spanTrim != Settings.OutlierTrim.Value || _statsSex != _spanSex;
            if (spanChanged)
            {
                _spanGeneration = idx.Generation;
                _spanTrim = Settings.OutlierTrim.Value;
                _spanSex = idx.Sex;
                foreach (PropertyFilter p in _filter.Properties) p.UpdateSpan(all, Settings.OutlierTrim.Value);
            }
            foreach (PropertyFilter p in _filter.Properties) p.UpdateHistogram(cards);

            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                foreach (string k in ci.ExtKeys)
                {
                    if (!ModNames.IsSaneKey(k)) continue;
                    int n;
                    counts.TryGetValue(k, out n);
                    counts[k] = n + 1;
                }
            }
            _modCounts = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => ModNames.Get(kv.Key)).ToList();

            // Content regions, missing zipmods, authors.
            _statsIndex = idx;
            Array.Clear(_regionUseCounts, 0, _regionUseCounts.Length);
            Array.Clear(_regionMissingCounts, 0, _regionMissingCounts.Length);
            Dictionary<string, int> missing = new Dictionary<string, int>();
            Dictionary<string, int> authors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int missingCards = 0;
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                int miss = SideloaderBridge.MissingMask(ci);
                if (ci.RegionMask != 0) _regionUseCounts[0]++;
                if (miss != 0) _regionMissingCounts[0]++;
                for (int r = 0; r < Regions.Count; r++)
                {
                    if ((ci.RegionMask & (1 << r)) != 0) _regionUseCounts[r + 1]++;
                    if ((miss & (1 << r)) != 0) _regionMissingCounts[r + 1]++;
                }
                foreach (string a in ci.Authors)
                {
                    int n;
                    authors.TryGetValue(a, out n);
                    authors[a] = n + 1;
                }
            }
            // Missing mods report: whole collection, since it's for finding what to download.
            foreach (CardInfo ci in all)
            {
                if (!ci.Ok) continue;
                int miss = SideloaderBridge.MissingMask(ci);
                if (miss != 0) missingCards++;
                if (miss != 0)
                {
                    HashSet<string> seen = new HashSet<string>();
                    foreach (string id in ci.ModIds)
                    {
                        if (SideloaderBridge.IsInstalled(id) || !seen.Add(id)) continue;
                        int n;
                        missing.TryGetValue(id, out n);
                        missing[id] = n + 1;
                    }
                }
            }
            _missingCards = missingCards;
            Dictionary<string, int> unc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                string k = ci.UncensorBody ?? "";
                int n;
                unc.TryGetValue(k, out n);
                unc[k] = n + 1;
            }
            foreach (string sel in _filter.UncensorBodies)
                if (!unc.ContainsKey(sel)) unc[sel] = 0; // keep ticked entries visible
            UpdateExtraStats(cards, idx);
            _uncensorCounts = unc.OrderBy(kv => kv.Key.Length == 0 ? 0 : 1).ThenByDescending(kv => kv.Value).ToList();
            Array.Clear(_monthCounts, 0, _monthCounts.Length);
            _maySeventeen = 0;
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                if (FilterState.IsBirthdayUnset(ci))
                {
                    _monthCounts[0]++;
                    if (ci.BirthMonth == 5 && ci.BirthDay == 17) _maySeventeen++;
                }
                else _monthCounts[ci.BirthMonth]++;
            }
            // Ticked authors stay listed (with 0) even if none of their cards are in this scope, so they can be unticked.
            foreach (string a in _filter.Authors)
                if (!authors.ContainsKey(a)) authors[a] = 0;

            // Outfits
            Array.Clear(_outfitCounts, 0, _outfitCounts.Length);
            _extraOutfitCards = 0;
            int maxSlots = GameDefs.BaseOutfits;
            Dictionary<string, int>[] nameCounts = new Dictionary<string, int>[FilterState.MaxOutfitSlots];
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                if (ci.OutfitStates.Length > GameDefs.BaseOutfits) _extraOutfitCards++;
                int slots = Math.Min(ci.OutfitStates.Length, FilterState.MaxOutfitSlots);
                if (slots > maxSlots) maxSlots = slots;
                for (int i = 0; i < slots; i++)
                {
                    _outfitCounts[i, TowelNames.EffectiveState(ci, i)]++;
                    _outfitCounts[i, 4]++;
                    if (i < ci.OutfitNames.Length && ci.OutfitNames[i] != null)
                    {
                        if (nameCounts[i] == null) nameCounts[i] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        int n;
                        nameCounts[i].TryGetValue(ci.OutfitNames[i], out n);
                        nameCounts[i][ci.OutfitNames[i]] = n + 1;
                    }
                }
            }
            _outfitSlotsShown = maxSlots;
            Array.Clear(_anyOutfitCounts, 0, _anyOutfitCounts.Length);
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                bool[] seenState = new bool[4];
                for (int i = 0; i < ci.OutfitStates.Length; i++) seenState[TowelNames.EffectiveState(ci, i)] = true;
                for (int st = 0; st < 4; st++) if (seenState[st]) _anyOutfitCounts[st]++;
            }
            for (int i = 0; i < FilterState.MaxOutfitSlots; i++)
                _outfitCommonName[i] = nameCounts[i] == null ? null : nameCounts[i].OrderByDescending(kv => kv.Value).First().Key;
            _missingMods = missing.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
            // Unique websites, most-needed first, for the batch opener.
            List<string> sites = new List<string>();
            HashSet<string> seenSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> kv in _missingMods)
            {
                ModMeta meta = idx.GetMeta(kv.Key);
                if (meta == null) continue;
                string w = (meta.Website ?? "").Trim();
                if (w.StartsWith("http", StringComparison.OrdinalIgnoreCase) && seenSites.Add(w.TrimEnd('/'))) sites.Add(w);
            }
            _missingSites = sites;
            if (_siteCursor > sites.Count) _siteCursor = sites.Count;
            _authorCounts = authors.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    // Two-handle slider: drag either handle; clicking the track moves the nearest handle.
    internal static class RangeSlider
    {
        private static int _dragThumb;

        public static bool Draw(Rect r, ref float min, ref float max, float lo, float hi, bool active)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            bool changed = false;
            float span = Mathf.Max(0.0001f, hi - lo);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition))
                    {
                        float v = ValueAt(r, e.mousePosition.x, lo, span);
                        float dMin = Mathf.Abs(v - min), dMax = Mathf.Abs(v - max);
                        if (Mathf.Abs(dMin - dMax) < 0.001f) _dragThumb = v < min ? 0 : 1;
                        else _dragThumb = dMin < dMax ? 0 : 1;
                        GUIUtility.hotControl = id;
                        changed = Move(v, ref min, ref max);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        changed = Move(ValueAt(r, e.mousePosition.x, lo, span), ref min, ref max);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
                case EventType.Repaint:
                    Paint(r, min, max, lo, span, GUIUtility.hotControl == id, active);
                    break;
            }
            return changed;
        }

        private static float ValueAt(Rect r, float x, float lo, float span)
        {
            float t = Mathf.Clamp01((x - r.x - 6) / Mathf.Max(1f, r.width - 12));
            return Mathf.Round(lo + t * span);
        }

        private static bool Move(float v, ref float min, ref float max)
        {
            float oldMin = min, oldMax = max;
            if (_dragThumb == 0) min = Mathf.Min(v, max);
            else max = Mathf.Max(v, min);
            return oldMin != min || oldMax != max;
        }

        private static void Paint(Rect r, float min, float max, float lo, float span, bool hot, bool active)
        {
            Color prev = GUI.color;
            float x0 = r.x + 6, w = r.width - 12;
            float cy = r.y + r.height * 0.5f;
            float xMin = x0 + (min - lo) / span * w;
            float xMax = x0 + (max - lo) / span * w;

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x0, cy - 2, w, 4), Texture2D.whiteTexture);
            // Green = this range is filtering; gray = covers everything (no limit).
            GUI.color = active ? new Color(0.45f, 0.8f, 0.45f, 0.9f) : new Color(0.55f, 0.55f, 0.55f, 0.8f);
            GUI.DrawTexture(new Rect(xMin, cy - 2, Mathf.Max(1f, xMax - xMin), 4), Texture2D.whiteTexture);
            GUI.color = hot ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            GUI.DrawTexture(new Rect(xMin - 5, cy - 7, 10, 14), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(xMax - 5, cy - 7, 10, 14), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
