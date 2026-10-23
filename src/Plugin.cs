// Studio Chara Filter - 1.3.0
// Adds a filter window next to CharaStudio's character list (Add > Female / Male).
// NOTE: compiled with the Windows built-in csc (C# 5) - no $"", ?. or nameof.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Studio;
using UnityEngine;

namespace StudioCharaFilter
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("CharaStudio")]
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    public class StudioCharaFilterPlugin : BaseUnityPlugin
    {
        public const string GUID = "WolfLikesKKS.StudioCharaFilter";
        public const string PluginName = "Studio Chara Filter";
        public const string Version = "1.3.1";

        internal static ManualLogSource Log;
        internal static StudioCharaFilterPlugin Instance;


        private readonly Dictionary<int, CardIndex> _indexes = new Dictionary<int, CardIndex>();
        private readonly List<ListController> _controllers = new List<ListController>();
        private readonly FilterState _filter = new FilterState();
        private readonly FolderPicker _folders = new FolderPicker();
        private FilterWindow _window;

        private bool _dirty;
        private bool _resetPage;
        private float _applyAt;
        private int _appliedGeneration = -1;
        private bool _appliedWhileBusy;

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            Settings.Init(Config);
            Settings.BindBodySliders(Config, _filter, () => MarkDirty(0f));

            string userData = UserData.Path;
            _indexes[1] = new CardIndex(1, Path.Combine(userData, "chara/female"), Path.Combine(Paths.CachePath, "StudioCharaFilter_female.idx"));
            _indexes[0] = new CardIndex(0, Path.Combine(userData, "chara/male"), Path.Combine(Paths.CachePath, "StudioCharaFilter_male.idx"));

            _window = new FilterWindow(_filter, _folders, MarkDirty, SetMode, SetPage, RemoveCard, RestoreCard, AllCards);
            // Optional sections: keep the filter in sync with their F1 toggles.
            _filter.LegacyEnabled = Settings.LegacyPluginFilter.Value;
            _filter.BirthdayEnabled = Settings.ShowBirthday.Value;
            _filter.UncensorEnabled = Settings.ShowUncensor.Value;
            Settings.LegacyPluginFilter.SettingChanged += (s1, e1) =>
            {
                _filter.LegacyEnabled = Settings.LegacyPluginFilter.Value;
                MarkDirty(0f);
            };
            Settings.ShowBirthday.SettingChanged += (s2, e2) =>
            {
                _filter.BirthdayEnabled = Settings.ShowBirthday.Value;
                MarkDirty(0f);
            };
            Settings.ShowUncensor.SettingChanged += (s3, e3) =>
            {
                _filter.UncensorEnabled = Settings.ShowUncensor.Value;
                MarkDirty(0f);
            };
            Settings.MaySeventeenIsUnset.SettingChanged += (s4, e4) => MarkDirty(0f);
            SyncSections();
            Settings.ShowColors.SettingChanged += (s5, e5) => { SyncSections(); MarkDirty(0f); };
            Settings.ShowProfile.SettingChanged += (s6, e6) => { SyncSections(); MarkDirty(0f); };
            Settings.ShowDetails.SettingChanged += (s7, e7) => { SyncSections(); MarkDirty(0f); };
            Settings.ShowFileSection.SettingChanged += (s8, e8) => { SyncSections(); MarkDirty(0f); };

            Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
            Speedups.Apply(GUID);
            Log.LogInfo("[SCF] Loaded v" + Version);
        }

        private void Start()
        {
            foreach (CardIndex idx in _indexes.Values) idx.RequestScan();
        }

        private static class Hooks
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            [HarmonyPatch(typeof(CharaList), "InitCharaList")]
            private static void InitCharaListPre(out System.Diagnostics.Stopwatch __state)
            {
                __state = System.Diagnostics.Stopwatch.StartNew();
            }

            // Runs after the game, Browser Folders and Studio Character Sort have built the list.
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            [HarmonyPatch(typeof(CharaList), "InitCharaList")]
            private static void InitCharaListPost(CharaList __instance, System.Diagnostics.Stopwatch __state)
            {
                if (__state != null)
                    Log.Log(Settings.TimingLevel, string.Format("[SCF] Character list refresh took {0} ms (reuse rows {1})",
                        __state.ElapsedMilliseconds, Settings.FastRows.Value ? "on" : "off"));
                try
                {
                    if (Instance != null) Instance.OnListRebuilt(__instance);
                }
                catch (Exception e)
                {
                    Log.LogError("[SCF] Error after list rebuild: " + e);
                }
            }
        }

        private ListController GetController(CharaList list)
        {
            _controllers.RemoveAll(c => c.List == null);
            foreach (ListController c in _controllers)
                if (c.List == list) return c;
            ListController nc = new ListController(list);
            _controllers.Add(nc);
            return nc;
        }

        private void SyncSections()
        {
            _filter.ColorsEnabled = Settings.ShowColors.Value;
            _filter.ProfileEnabled = Settings.ShowProfile.Value;
            _filter.DetailsEnabled = Settings.ShowDetails.Value;
            _filter.FileEnabled = Settings.ShowFileSection.Value;
        }

        // Every indexed card (female and male), for the unused zipmods report.
        private List<CardInfo> AllCards()
        {
            List<CardInfo> all = new List<CardInfo>();
            foreach (CardIndex idx in _indexes.Values) all.AddRange(idx.Snapshot());
            return all;
        }

        internal CardIndex IndexForSex(int sex)
        {
            CardIndex idx;
            return _indexes.TryGetValue(sex, out idx) ? idx : null;
        }

        private CardIndex IndexFor(ListController c)
        {
            CardIndex idx;
            return _indexes.TryGetValue(c.Sex, out idx) ? idx : _indexes[1];
        }

        private void OnListRebuilt(CharaList list)
        {
            ListController c = GetController(list);
            CardIndex idx = IndexFor(c);
            c.OnListRebuilt(_filter, idx);
            // Pick up cards added or changed since the last scan (cheap: only checks file sizes and dates).
            if (!idx.Busy) idx.RequestScan();
        }

        private ListController ActiveController()
        {
            foreach (ListController c in _controllers)
                if (c.List != null && c.List.isActiveAndEnabled) return c;
            return null;
        }

        // Filter changed by the user: re-apply after a short delay and go back to page 1.
        private void MarkDirty(float delay)
        {
            Schedule(delay);
            _resetPage = true;
        }

        private void Schedule(float delay)
        {
            _dirty = true;
            _applyAt = Time.realtimeSinceStartup + delay;
        }

        // Safe removal: move the card out, then refresh whichever list is showing.
        private string RemoveCard(ListController c, string path)
        {
            string error = Removal.Remove(path);
            if (error != null) return error;
            RefreshAfterFileChange(c, path);
            return null;
        }

        private string RestoreCard(ListController c, RemovedEntry e)
        {
            string error = Removal.Restore(e);
            if (error != null) return error;
            RefreshAfterFileChange(c, null);
            return null;
        }

        private void RefreshAfterFileChange(ListController c, string removedPath)
        {
            CardIndex idx = IndexFor(c);
            if (removedPath != null) idx.Remove(removedPath);
            try
            {
                if (c.FromIndex) c.Apply(_filter, _folders, idx, Settings.MaxResults.Value);
                else c.List.InitCharaList(true); // re-list the current folder (fast with the index)
            }
            catch (Exception e)
            {
                Log.LogError("[SCF] Refreshing the list failed: " + e);
            }
            idx.RequestScan();
            _appliedWhileBusy = true; // re-apply once the scan has picked up the change (e.g. a restored card)
        }

        private void SetPage(ListController c, int page, bool showAll)
        {
            c.Page = page;
            c.ShowAll = showAll;
            CardIndex idx = IndexFor(c);
            try
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                c.Apply(_filter, _folders, idx, Settings.MaxResults.Value);
                Log.Log(Settings.TimingLevel, string.Format("[SCF] Page {0}/{1}{2}: {3} rows in {4} ms", c.Page + 1, c.PageCount,
                    showAll ? " (show all)" : "", c.Shown, sw.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                Log.LogError("[SCF] Changing page failed: " + e);
            }
        }

        private void SetMode(ListController c, ScopeMode mode)
        {
            CardIndex idx = IndexFor(c);
            try
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                c.SetMode(mode, _filter, _folders, idx, Settings.MaxResults.Value);
                _appliedGeneration = idx.Generation;
                _appliedWhileBusy = idx.Busy;
                Log.Log(Settings.TimingLevel, string.Format("[SCF] Switched to {0}: {1} shown in {2} ms", mode, c.Shown, sw.ElapsedMilliseconds));
            }
            catch (Exception e)
            {
                Log.LogError("[SCF] Switching scope failed: " + e);
            }
        }

        private void Update()
        {
            if (Settings.ToggleKey.Value.IsDown()) Settings.ShowWindow.Value = !Settings.ShowWindow.Value;

            ListController c = ActiveController();
            if (c == null) return;
            CardIndex idx = IndexFor(c);

            // Re-apply as the index fills in (folder mode is cheap; all-folders mode only when the scan finishes).
            if (!_dirty && idx.Generation != _appliedGeneration && _filter.IsActive)
            {
                if (!c.FromIndex || !idx.Busy) Schedule(c.FromIndex ? 0f : 0.5f);
            }
            if (!_dirty && c.FromIndex && _appliedWhileBusy && !idx.Busy) Schedule(0f);

            if (_dirty && Time.realtimeSinceStartup >= _applyAt)
            {
                _dirty = false;
                if (_resetPage)
                {
                    c.Page = 0;
                    _resetPage = false;
                }
                _appliedGeneration = idx.Generation;
                _appliedWhileBusy = idx.Busy;
                try
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    c.Apply(_filter, _folders, idx, Settings.MaxResults.Value);
                    Log.Log(Settings.TimingLevel, string.Format("[SCF] Filter applied ({0}): {1} matches, {2} shown, {3} ms",
                        c.Mode, c.Matched, c.Shown, sw.ElapsedMilliseconds));
                }
                catch (Exception e)
                {
                    Log.LogError("[SCF] Applying filter failed: " + e);
                }
            }
        }

        private void OnGUI()
        {
            if (!Settings.ShowWindow.Value) return;
            ListController c = ActiveController();
            if (c == null) return;
            _window.OnGUI(c, IndexFor(c));
        }
    }
}
