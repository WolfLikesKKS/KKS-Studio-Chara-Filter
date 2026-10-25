// Undo for replaced characters, one at a time. Right before any character in the scene is replaced (Studio's own
// "Change character" or the Random row's Replace), the character as it is - card data with everything plugins store in
// it - is saved to UserData\StudioCharaFilter\undo\. The Random row lists every replacement with an Undo button that
// changes that character back. Undoing an older replacement of a character that was replaced again since also drops
// the later ones (they are what was undone). The backups are deleted at the next start of Studio.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace StudioCharaFilter
{
    internal static class ReplaceHistory
    {
        internal sealed class Entry
        {
            public OCIChar Oc;
            public string Backup;
            public string OldName, NewName;
            public DateTime When;
        }

        // Newest first.
        public static readonly List<Entry> Entries = new List<Entry>();
        private static bool _undoing;
        private static string _dir;

        private static string Dir
        {
            get
            {
                if (_dir == null) _dir = Path.Combine(BepInEx.Paths.GameRootPath, "UserData/StudioCharaFilter/undo");
                return _dir;
            }
        }

        public static void Init(string guid)
        {
            // Backups of the last session are no longer needed.
            try
            {
                if (Directory.Exists(Dir))
                    foreach (string f in Directory.GetFiles(Dir, "*.png"))
                        try { File.Delete(f); } catch (Exception) { }
            }
            catch (Exception) { }
            try { Harmony.CreateAndPatchAll(typeof(Hooks), guid + ".undo"); }
            catch (Exception e) { StudioCharaFilterPlugin.Log.LogWarning("[SCF] Replace undo unavailable: " + e.Message); }
        }

        // Entries whose character is still in the scene.
        public static void Prune()
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (Entries[i].Oc == null || Entries[i].Oc.charInfo == null) Forget(i);
        }

        public static bool Busy { get { return _undoing; } }

        // Change the character back to what it was before this replacement (next frame, from a coroutine).
        public static void Undo(Entry e, Action<string> done)
        {
            if (e == null || _undoing) return;
            StudioCharaFilterPlugin.Instance.StartCoroutine(UndoCo(new List<Entry> { e }, done));
        }

        // The latest replacement of each selected character.
        public static int UndoSelected(Action<string> done)
        {
            List<Entry> list = new List<Entry>();
            foreach (OCIChar oc in StudioAPI.GetSelectedCharacters())
                foreach (Entry e in Entries)
                    if (e.Oc == oc) { list.Add(e); break; }
            if (list.Count > 0 && !_undoing) StudioCharaFilterPlugin.Instance.StartCoroutine(UndoCo(list, done));
            return list.Count;
        }

        public static void Clear()
        {
            for (int i = Entries.Count - 1; i >= 0; i--) Forget(i);
        }

        private static IEnumerator UndoCo(List<Entry> list, Action<string> done)
        {
            _undoing = true;
            int ok = 0;
            foreach (Entry e in list)
            {
                int index = Entries.IndexOf(e);
                if (index < 0) continue;
                if (e.Oc == null || e.Oc.charInfo == null || !File.Exists(e.Backup)) { Forget(index); continue; }
                try
                {
                    e.Oc.ChangeChara(e.Backup);
                    ok++;
                    // This one and the newer replacements of the same character are undone now.
                    for (int i = index; i >= 0; i--)
                        if (Entries[i].Oc == e.Oc) Forget(i);
                }
                catch (Exception ex) { StudioCharaFilterPlugin.Log.LogWarning("[SCF] Couldn't undo the replacement of " + e.NewName + ": " + ex.Message); }
                yield return null;
                yield return null;
            }
            _undoing = false;
            if (done != null) done(ok == 0 ? "Nothing undone." : "Undid " + ok + " replacement" + (ok == 1 ? "" : "s") + ".");
        }

        private static void Forget(int i)
        {
            Entry e = Entries[i];
            Entries.RemoveAt(i);
            bool used = false;
            foreach (Entry o in Entries) if (o.Backup == e.Backup) used = true;
            if (!used) try { File.Delete(e.Backup); } catch (Exception) { }
        }

        private static string Save(OCIChar oc)
        {
            ChaFileControl file = oc.charInfo.chaFile;
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".png");
            byte[] keep = file.pngData;
            if (keep == null || keep.Length == 0)
            {
                // A character from a scene can have no card picture; a card file needs one to load again.
                Texture2D t = new Texture2D(4, 4);
                file.pngData = t.EncodeToPNG();
                UnityEngine.Object.Destroy(t);
            }
            bool ok;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    ok = file.SaveCharaFile(fs, true);   // the stream version: also writes plugin data, changes nothing else
            }
            finally { file.pngData = keep; }
            if (!ok) { try { File.Delete(path); } catch (Exception) { } return null; }
            return path;
        }

        private static class Hooks
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(OCIChar), "ChangeChara")]
            private static void Before(OCIChar __instance, out Entry __state)
            {
                __state = null;
                if (_undoing || __instance == null || __instance.charInfo == null) return;
                try
                {
                    string backup = Save(__instance);
                    if (backup == null) return;
                    Entry e = new Entry();
                    e.Oc = __instance;
                    e.Backup = backup;
                    e.OldName = __instance.charInfo.chaFile.parameter.fullname;
                    e.When = DateTime.Now;
                    __state = e;
                }
                catch (Exception ex) { StudioCharaFilterPlugin.Log.LogWarning("[SCF] Couldn't back up a character before replacing it: " + ex.Message); }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(OCIChar), "ChangeChara")]
            private static void After(OCIChar __instance, Entry __state)
            {
                if (__state == null) return;
                try { __state.NewName = __instance.charInfo.chaFile.parameter.fullname; } catch (Exception) { __state.NewName = "?"; }
                Entries.Insert(0, __state);
                if (Entries.Count > 200) Forget(Entries.Count - 1);
            }
        }
    }
}
