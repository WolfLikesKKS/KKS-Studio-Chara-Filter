// Optional speedups for CharaStudio's own character list (both can be turned off in F1 > Performance).
//  1. Fast folder loading: fill the list from our card index instead of fully loading every card just to read its name.
//     Cards that are new or changed since the last index scan are still read the normal way.
//  2. Fast thumbnails: the game unloads all unused assets and forces a garbage collection every time the mouse moves
//     onto a name. We keep a small cache of thumbnails instead, and free the ones that fall out of it.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace StudioCharaFilter
{
    internal static class Speedups
    {
        public static void Apply(string guid)
        {
            try
            {
                Harmony.CreateAndPatchAll(typeof(Speedups), guid + ".speedups");
            }
            catch (Exception e)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Speedups could not be enabled, the game's normal loading is used: " + e.Message);
            }
            try
            {
                Harmony.CreateAndPatchAll(typeof(RowBuilder), guid + ".rows");
            }
            catch (Exception e)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Row reuse could not be enabled, the game's normal list building is used: " + e.Message);
            }
        }

        // ---------- 1. Fast folder loading ----------

        // Runs after Browser Folders' prefix, so its folder choice (and its cached-folder shortcut) still apply.
        [HarmonyPrefix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(CharaList), "InitFemaleList")]
        private static bool InitFemaleListPre(bool __runOriginal, CharaFileSort ___charaFileSort)
        {
            return FastFill(1, "chara/female", __runOriginal, ___charaFileSort);
        }

        [HarmonyPrefix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(CharaList), "InitMaleList")]
        private static bool InitMaleListPre(bool __runOriginal, CharaFileSort ___charaFileSort)
        {
            return FastFill(0, "chara/male", __runOriginal, ___charaFileSort);
        }

        private static bool FastFill(int sex, string folder, bool runOriginal, CharaFileSort cfs)
        {
            if (!runOriginal) return false; // another plugin already filled the list
            if (!Settings.FastFolderLoading.Value || StudioCharaFilterPlugin.Instance == null) return true;
            CardIndex idx = StudioCharaFilterPlugin.Instance.IndexForSex(sex);
            if (idx == null || !idx.CacheReady) return true; // index not loaded yet: use the game's way

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                List<string> files = new List<string>();
                Illusion.Utils.File.GetAllFiles(UserData.Path + folder, "*.png", ref files);

                List<CharaFileInfo> result = new List<CharaFileInfo>(files.Count);
                int fromIndex = 0, readNormally = 0;
                foreach (string file in files)
                {
                    CardInfo ci = idx.Get(file);
                    bool fresh = false;
                    if (ci != null && ci.Ok && !ci.OddMarker)
                    {
                        FileInfo fi = new FileInfo(file);
                        fresh = fi.Exists && fi.Length == ci.Size && fi.LastWriteTimeUtc.Ticks == ci.MTimeTicks;
                    }
                    if (fresh)
                    {
                        CharaFileInfo info = new CharaFileInfo(file, Names.FullName(ci.FirstName, ci.LastName));
                        info.time = new DateTime(ci.MTimeTicks, DateTimeKind.Utc).ToLocalTime();
                        result.Add(info);
                        fromIndex++;
                    }
                    else
                    {
                        // Same as the game does.
                        ChaFileControl cfc = new ChaFileControl();
                        if (cfc.LoadCharaFile(file, (byte)sex, true, true))
                        {
                            CharaFileInfo info = new CharaFileInfo(file, cfc.parameter.fullname);
                            info.time = File.GetLastWriteTime(file);
                            result.Add(info);
                        }
                        readNormally++;
                    }
                }
                cfs.cfiList.Clear();
                cfs.cfiList.AddRange(result);
                StudioCharaFilterPlugin.Log.Log(Settings.TimingLevel, string.Format("[SCF] Fast folder loading: {0} cards ({1} from index, {2} read normally) in {3} ms",
                    result.Count, fromIndex, readNormally, sw.ElapsedMilliseconds));
                if (readNormally > 0 && !idx.Busy) idx.RequestScan();
                return false;
            }
            catch (Exception e)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Fast folder loading failed, using the game's normal loading: " + e.Message);
                return true;
            }
        }

        // ---------- 2. Fast thumbnails ----------

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CharaList), "LoadCharaImage")]
        private static bool LoadCharaImagePre(int _idx, bool ___isDelay, CharaFileSort ___charaFileSort, RawImage ___imageChara)
        {
            if (!Settings.FastThumbnails.Value) return true;
            if (___isDelay) return false;
            try
            {
                CharaFileInfo cfi = ___charaFileSort.cfiList[_idx];
                Texture2D tex = ThumbnailCache.Get(cfi.file, ___imageChara);
                ___imageChara.texture = tex;
                ___imageChara.color = Color.white;
                return false;
            }
            catch (Exception)
            {
                return true; // fall back to the game's own code
            }
        }
    }

    internal static class ThumbnailCache
    {
        private const int Capacity = 40;

        private sealed class Entry
        {
            public string Path;
            public long MTime;
            public Texture2D Texture;
        }

        private static readonly Dictionary<string, LinkedListNode<Entry>> Map = new Dictionary<string, LinkedListNode<Entry>>();
        private static readonly LinkedList<Entry> Order = new LinkedList<Entry>();
        private static readonly HashSet<RawImage> Displays = new HashSet<RawImage>();

        public static Texture2D Get(string path, RawImage display)
        {
            Displays.Add(display);
            long mtime = File.GetLastWriteTimeUtc(path).Ticks;
            LinkedListNode<Entry> node;
            if (Map.TryGetValue(path, out node))
            {
                if (node.Value.MTime == mtime && node.Value.Texture != null)
                {
                    Order.Remove(node);
                    Order.AddFirst(node);
                    return node.Value.Texture;
                }
                Remove(node);
            }

            Entry e = new Entry();
            e.Path = path;
            e.MTime = mtime;
            e.Texture = PngAssist.LoadTexture(path);
            node = Order.AddFirst(e);
            Map[path] = node;

            while (Order.Count > Capacity) Remove(Order.Last);
            return e.Texture;
        }

        private static void Remove(LinkedListNode<Entry> node)
        {
            Order.Remove(node);
            Map.Remove(node.Value.Path);
            Texture2D tex = node.Value.Texture;
            if (tex == null) return;
            // Don't destroy a thumbnail that's still on screen; the game's regular cleanup will free it later.
            foreach (RawImage d in Displays)
                if (d != null && d.texture == tex) return;
            UnityEngine.Object.Destroy(tex);
        }
    }
}
