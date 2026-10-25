// Applies filters to one Studio character list (female or male).
// "This folder": hides rows that don't match (the list itself is untouched, so Browser Folders keeps working).
// "All folders": replaces the rows with matching cards from the index, without reading card files.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace StudioCharaFilter
{
    internal enum ScopeMode
    {
        CurrentFolder = 0,   // whatever the game / Browser Folders listed; non-matching rows are hidden
        ChosenFolders = 1,   // folders picked in our folder tree, listed from the index
        AllFolders = 2       // every card, listed from the index
    }

    internal sealed class ListController
    {
        private static readonly FieldInfo F_charaFileSort = AccessTools.Field(typeof(CharaList), "charaFileSort");
        private static readonly FieldInfo F_objectNode = AccessTools.Field(typeof(CharaList), "objectNode");
        private static readonly FieldInfo F_imageChara = AccessTools.Field(typeof(CharaList), "imageChara");
        private static readonly FieldInfo F_buttonLoad = AccessTools.Field(typeof(CharaList), "buttonLoad");
        private static readonly FieldInfo F_buttonChange = AccessTools.Field(typeof(CharaList), "buttonChange");
        private static readonly FieldInfo F_sex = AccessTools.Field(typeof(CharaList), "sex");
        private static readonly FieldInfo F_sortType = AccessTools.Field(typeof(CharaFileSort), "sortType");
        private static readonly MethodInfo M_OnSelectChara = AccessTools.Method(typeof(CharaList), "OnSelectChara");
        private static readonly MethodInfo M_LoadCharaImage = AccessTools.Method(typeof(CharaList), "LoadCharaImage");

        private struct RowData
        {
            public string File;
            public string Name;
            public DateTime Time;
        }

        public readonly CharaList List;
        public readonly int Sex;
        public ScopeMode Mode { get; private set; }

        // True when the rows come from our index instead of the game's own folder list.
        public bool FromIndex
        {
            get { return Mode != ScopeMode.CurrentFolder; }
        }
        public int Shown;
        public int Matched;
        public int Total;
        public bool Capped;     // more matches than one page
        public int Page;        // 0-based
        public int PageCount = 1;
        public bool ShowAll;    // ignore the page size (slow for thousands of cards)

        // Every card matching the current filter in the current scope (all pages) - the pool for the Random buttons.
        public readonly List<string> MatchedFiles = new List<string>();

        private List<RowData> _folderBackup;

        // Cards in the game's own folder list (the Current folder scope), as normalized paths.
        public HashSet<string> FolderKeys = new HashSet<string>();
        public int ScopeVersion;

        private void CaptureFolderKeys()
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (CharaFileInfo cfi in Cfs.cfiList) keys.Add(CardInfo.NormalizePath(cfi.file));
            FolderKeys = keys;
            ScopeVersion++;
        }

        // The card currently selected in the game's list, or null.
        public string SelectedPath
        {
            get
            {
                CharaFileSort cfs = Cfs;
                string p = cfs.selectPath;
                return string.IsNullOrEmpty(p) ? null : p;
            }
        }

        public string SelectedName
        {
            get
            {
                CharaFileSort cfs = Cfs;
                int i = cfs.select;
                return i >= 0 && i < cfs.cfiList.Count ? cfs.cfiList[i].name : "";
            }
        }

        public ListController(CharaList list)
        {
            List = list;
            Sex = (int)F_sex.GetValue(list);
        }

        private CharaFileSort Cfs
        {
            get { return (CharaFileSort)F_charaFileSort.GetValue(List); }
        }

        // The game (or Browser Folders) just rebuilt the list from a folder.
        public void OnListRebuilt(FilterState filter, CardIndex index)
        {
            Mode = ScopeMode.CurrentFolder;
            _folderBackup = null;
            Page = 0;
            PageCount = 1;
            CaptureFolderKeys();
            ApplyFolderFilter(filter, index);
        }

        public void Apply(FilterState filter, FolderPicker folders, CardIndex index, int maxResults)
        {
            if (FromIndex) RebuildFromIndex(filter, folders, index, maxResults);
            else ApplyFolderFilter(filter, index);
        }

        public void SetMode(ScopeMode mode, FilterState filter, FolderPicker folders, CardIndex index, int maxResults)
        {
            if (mode == Mode) return;
            if (mode != ScopeMode.CurrentFolder)
            {
                // Remember the game's folder list so we can put it back without re-reading cards.
                if (!FromIndex)
                {
                    _folderBackup = new List<RowData>();
                    foreach (CharaFileInfo cfi in Cfs.cfiList)
                    {
                        RowData rd = new RowData();
                        rd.File = cfi.file;
                        rd.Name = cfi.name;
                        rd.Time = cfi.time;
                        _folderBackup.Add(rd);
                    }
                }
                Mode = mode;
                ScopeVersion++;
                RebuildFromIndex(filter, folders, index, maxResults);
            }
            else
            {
                Mode = ScopeMode.CurrentFolder;
                if (_folderBackup != null) BuildRows(_folderBackup);
                _folderBackup = null;
                CaptureFolderKeys();
                ApplyFolderFilter(filter, index);
            }
        }

        private void ApplyFolderFilter(FilterState filter, CardIndex index)
        {
            bool active = filter.IsActive;
            int shown = 0, total = 0;
            MatchedFiles.Clear();
            foreach (CharaFileInfo cfi in Cfs.cfiList)
            {
                if (cfi.node == null) continue;
                total++;
                bool show = !active || filter.Matches(index.Get(cfi.file));
                if (cfi.node.gameObject.activeSelf != show) cfi.node.gameObject.SetActive(show);
                if (show) { shown++; MatchedFiles.Add(cfi.file); }
            }
            Shown = shown;
            Matched = shown;
            Total = total;
            Capped = false;
            PageCount = 1;
        }

        private void RebuildFromIndex(FilterState filter, FolderPicker folders, CardIndex index, int maxResults)
        {
            bool active = filter.IsActive;
            List<CardInfo> all = index.Snapshot();
            List<CardInfo> matches = new List<CardInfo>();
            int total = 0;
            foreach (CardInfo ci in all)
            {
                if (!ci.Ok) continue;
                if (Mode == ScopeMode.ChosenFolders && !folders.Matches(ci)) continue;
                total++;
                if (active && !filter.Matches(ci)) continue;
                matches.Add(ci);
            }

            // Sort the whole result first (same order the list uses) so pages are consistent.
            CharaFileSort cfs = Cfs;
            int kind = cfs.sortKind < 0 ? 0 : cfs.sortKind;
            bool[] sortType = F_sortType.GetValue(cfs) as bool[];
            bool ascend = sortType != null && kind < sortType.Length && sortType[kind];
            if (kind == 1)
                matches.Sort((a, b) => ascend ? a.MTimeTicks.CompareTo(b.MTimeTicks) : b.MTimeTicks.CompareTo(a.MTimeTicks));
            else
            {
                Dictionary<CardInfo, string> names = new Dictionary<CardInfo, string>(matches.Count);
                foreach (CardInfo ci in matches) names[ci] = Names.FullName(ci.FirstName, ci.LastName);
                matches.Sort((a, b) => ascend
                    ? string.Compare(names[a], names[b], StringComparison.CurrentCulture)
                    : string.Compare(names[b], names[a], StringComparison.CurrentCulture));
            }

            int pageSize = ShowAll ? int.MaxValue : Math.Max(1, maxResults);
            PageCount = ShowAll ? 1 : Math.Max(1, (matches.Count + pageSize - 1) / pageSize);
            if (Page >= PageCount) Page = PageCount - 1;
            if (Page < 0) Page = 0;
            int start = ShowAll ? 0 : Page * pageSize;
            int end = ShowAll ? matches.Count : Math.Min(matches.Count, start + pageSize);

            List<RowData> rows = new List<RowData>(Math.Max(0, end - start));
            for (int i = start; i < end; i++)
            {
                CardInfo ci = matches[i];
                RowData rd = new RowData();
                rd.File = ci.FilePath;
                rd.Name = Names.FullName(ci.FirstName, ci.LastName);
                rd.Time = new DateTime(ci.MTimeTicks, DateTimeKind.Utc).ToLocalTime();
                rows.Add(rd);
            }
            BuildRows(rows);
            MatchedFiles.Clear();
            foreach (CardInfo ci in matches) MatchedFiles.Add(ci.FilePath);
            Shown = rows.Count;
            Matched = matches.Count;
            Total = total;
            Capped = PageCount > 1;
        }

        // Same steps as CharaList.InitCharaList, but from our own row data instead of reading card files.
        private void BuildRows(List<RowData> rows)
        {
            CharaFileSort cfs = Cfs;
            RowBuilder.ClearRows(cfs);
            cfs.cfiList.Clear();
            foreach (RowData rd in rows)
            {
                CharaFileInfo info = new CharaFileInfo(rd.File, rd.Name);
                info.time = rd.Time;
                cfs.cfiList.Add(info);
            }
            RowBuilder.CreateRows(List, cfs);

            RawImage img = F_imageChara.GetValue(List) as RawImage;
            if (img != null) img.color = Color.clear;

            int kind = cfs.sortKind < 0 ? 0 : cfs.sortKind;
            bool[] sortType = F_sortType.GetValue(cfs) as bool[];
            bool ascend = sortType != null && kind < sortType.Length && sortType[kind];
            cfs.Sort(kind, ascend);
            RowBuilder.SetButtons(List);
        }
    }

    internal static class Names
    {
        private static int _usOrder = -1;

        // Matches ChaFileParameter.fullname, which depends on the game's language setting.
        public static string FullName(string first, string last)
        {
            if (_usOrder < 0)
            {
                _usOrder = 0;
                try
                {
                    ChaFileParameter p = new ChaFileParameter();
                    p.firstname = "F";
                    p.lastname = "L";
                    _usOrder = p.fullname.StartsWith("F") ? 1 : 0;
                }
                catch (Exception) { }
            }
            return _usOrder == 1 ? first + " " + last : last + " " + first;
        }
    }
}
