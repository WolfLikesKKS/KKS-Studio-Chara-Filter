// Random characters from the filtered list: add N random characters to the scene, or replace the selected characters (or
// every character in the scene) with random ones. The pool is exactly what the filter window shows as matching - the
// current filter in the current scope, all pages - for the list that is open (female or male).
// Replacing uses Studio's own "Change character", so each character keeps its place, pose and animation.
using System;
using System.Collections;
using System.Collections.Generic;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace StudioCharaFilter
{
    internal static class RandomCharacters
    {
        private static readonly System.Random Rng = new System.Random();
        public static bool Busy { get; private set; }

        // count picks from the pool; without repeats while the pool lasts (then it starts over, reshuffled).
        public static List<string> Pick(List<string> pool, int count, bool noRepeats)
        {
            List<string> result = new List<string>(count);
            if (pool == null || pool.Count == 0 || count <= 0) return result;
            if (!noRepeats)
            {
                for (int i = 0; i < count; i++) result.Add(pool[Rng.Next(pool.Count)]);
                return result;
            }
            List<string> bag = new List<string>();
            while (result.Count < count)
            {
                if (bag.Count == 0)
                {
                    bag.AddRange(pool);
                    for (int i = bag.Count - 1; i > 0; i--)   // Fisher-Yates shuffle
                    {
                        int j = Rng.Next(i + 1);
                        string t = bag[i]; bag[i] = bag[j]; bag[j] = t;
                    }
                }
                result.Add(bag[bag.Count - 1]);
                bag.RemoveAt(bag.Count - 1);
            }
            return result;
        }

        // Characters in the scene of this list's sex: the selected ones, or all of them.
        public static List<OCIChar> Targets(int sex, bool selectedOnly)
        {
            List<OCIChar> list = new List<OCIChar>();
            if (selectedOnly)
            {
                foreach (OCIChar oc in StudioAPI.GetSelectedCharacters())
                    if (oc != null && oc.sex == sex) list.Add(oc);
                return list;
            }
            Studio.Studio studio = Singleton<Studio.Studio>.Instance;
            if (studio == null || studio.dicObjectCtrl == null) return list;
            foreach (ObjectCtrlInfo info in studio.dicObjectCtrl.Values)
            {
                OCIChar oc = info as OCIChar;
                if (oc != null && oc.sex == sex) list.Add(oc);
            }
            return list;
        }

        // One character per frame, so the game stays responsive and each change finishes before the next.
        public static void Add(int sex, List<string> files, Action<string> done)
        {
            StudioCharaFilterPlugin.Instance.StartCoroutine(AddCo(sex, files, done));
        }

        public static void Replace(List<OCIChar> targets, List<string> files, Action<string> done)
        {
            StudioCharaFilterPlugin.Instance.StartCoroutine(ReplaceCo(targets, files, done));
        }

        private static IEnumerator AddCo(int sex, List<string> files, Action<string> done)
        {
            Busy = true;
            int ok = 0;
            Studio.Studio studio = Singleton<Studio.Studio>.Instance;
            foreach (string f in files)
            {
                try
                {
                    if (sex == 1) studio.AddFemale(f); else studio.AddMale(f);
                    ok++;
                }
                catch (Exception e) { StudioCharaFilterPlugin.Log.LogWarning("[SCF] Couldn't add " + f + ": " + e.Message); }
                yield return null;
                yield return null;
            }
            Busy = false;
            if (done != null) done("Added " + ok + " random character" + (ok == 1 ? "" : "s") + ".");
        }

        private static IEnumerator ReplaceCo(List<OCIChar> targets, List<string> files, Action<string> done)
        {
            Busy = true;
            int ok = 0;
            for (int i = 0; i < targets.Count && i < files.Count; i++)
            {
                OCIChar oc = targets[i];
                if (oc == null || oc.charInfo == null) continue;
                try { oc.ChangeChara(files[i]); ok++; }
                catch (Exception e) { StudioCharaFilterPlugin.Log.LogWarning("[SCF] Couldn't replace with " + files[i] + ": " + e.Message); }
                yield return null;
                yield return null;
            }
            Busy = false;
            if (done != null) done("Replaced " + ok + " character" + (ok == 1 ? "" : "s") + " with random ones.");
        }
    }
}
