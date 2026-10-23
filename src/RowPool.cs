// Reusable rows for Studio's character list (F1 > Performance > Reuse list rows).
// The game destroys every row and instantiates a new one per card whenever the list changes; this keeps
// old rows in a hidden pool and reuses them, which is noticeably cheaper for big folders.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace StudioCharaFilter
{
    internal static class RowBuilder
    {
        private static readonly FieldInfo F_charaFileSort = AccessTools.Field(typeof(CharaList), "charaFileSort");
        private static readonly FieldInfo F_objectNode = AccessTools.Field(typeof(CharaList), "objectNode");
        private static readonly FieldInfo F_imageChara = AccessTools.Field(typeof(CharaList), "imageChara");
        private static readonly FieldInfo F_buttonLoad = AccessTools.Field(typeof(CharaList), "buttonLoad");
        private static readonly FieldInfo F_buttonChange = AccessTools.Field(typeof(CharaList), "buttonChange");
        private static readonly FieldInfo F_sex = AccessTools.Field(typeof(CharaList), "sex");
        private static readonly FieldInfo F_select = AccessTools.Field(typeof(CharaFileSort), "m_Select");
        private static readonly MethodInfo M_OnSelectChara = AccessTools.Method(typeof(CharaList), "OnSelectChara");
        private static readonly MethodInfo M_LoadCharaImage = AccessTools.Method(typeof(CharaList), "LoadCharaImage");
        private static readonly MethodInfo M_InitFemaleList = AccessTools.Method(typeof(CharaList), "InitFemaleList");
        private static readonly MethodInfo M_InitMaleList = AccessTools.Method(typeof(CharaList), "InitMaleList");
        private static readonly MethodInfo M_SetIsInit = AccessTools.PropertySetter(typeof(CharaList), "isInit");

        // Same steps as CharaList.InitCharaList, with pooled rows.
        public static void InitCharaList(CharaList list, bool force)
        {
            if (list.isInit && !force) return;
            Stopwatch sw = Stopwatch.StartNew();
            CharaFileSort cfs = (CharaFileSort)F_charaFileSort.GetValue(list);
            ClearRows(cfs);
            int sex = (int)F_sex.GetValue(list);
            (sex == 1 ? M_InitFemaleList : M_InitMaleList).Invoke(list, null);
            long readMs = sw.ElapsedMilliseconds;

            CreateRows(list, cfs);

            RawImage img = F_imageChara.GetValue(list) as RawImage;
            if (img != null) img.color = Color.clear;
            cfs.Sort(0, false);
            SetButtons(list);
            M_SetIsInit.Invoke(list, new object[] { true });
            StudioCharaFilterPlugin.Log.Log(Settings.TimingLevel, string.Format("[SCF] List built: {0} cards, reading {1} ms, rows {2} ms (reused rows)",
                cfs.cfiList.Count, readMs, sw.ElapsedMilliseconds - readMs));
        }

        public static void ClearRows(CharaFileSort cfs)
        {
            if (Settings.FastRows.Value) RowPool.ReleaseAll(cfs.root);
            else cfs.DeleteAllNode();
            F_select.SetValue(cfs, -1);
        }

        // Creates (or reuses) one row per entry in cfs.cfiList and wires it up like the game does.
        public static void CreateRows(CharaList list, CharaFileSort cfs)
        {
            GameObject objectNode = (GameObject)F_objectNode.GetValue(list);
            bool pooled = Settings.FastRows.Value;
            for (int i = 0; i < cfs.cfiList.Count; i++)
            {
                CharaFileInfo info = cfs.cfiList[i];
                info.index = i;
                GameObject go = pooled ? RowPool.Get(objectNode, cfs.root) : null;
                if (go == null)
                {
                    go = UnityEngine.Object.Instantiate<GameObject>(objectNode);
                    if (!go.activeSelf) go.SetActive(true);
                    go.transform.SetParent(cfs.root, false);
                }
                info.node = go.GetComponent<ListNode>();
                info.button = go.GetComponent<Button>();
                info.node.AddActionToButton(delegate { M_OnSelectChara.Invoke(list, new object[] { info.index }); });
                info.node.text = info.name;
                info.node.listEnterAction.Add(delegate { M_LoadCharaImage.Invoke(list, new object[] { info.index }); });
            }
        }

        public static void SetButtons(CharaList list)
        {
            Button bl = F_buttonLoad.GetValue(list) as Button;
            if (bl != null) bl.interactable = false;
            Button bc = F_buttonChange.GetValue(list) as Button;
            if (bc != null) bc.interactable = false;
        }

        // Replaces CharaList.InitCharaList when row reuse is on. Runs after Browser Folders' prefix;
        // other plugins' postfixes (Browser Folders, Studio Character Sort, ours) still run afterwards.
        [HarmonyPrefix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(CharaList), "InitCharaList")]
        private static bool InitCharaListPre(CharaList __instance, bool _force, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (!Settings.FastRows.Value) return true;
            try
            {
                InitCharaList(__instance, _force);
                return false;
            }
            catch (Exception e)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Reusing list rows failed, using the game's normal list building: " + e.Message);
                return true;
            }
        }
    }

    internal static class RowPool
    {
        private const int MaxPooled = 6000;
        private static Transform _poolRoot;
        private static readonly Stack<GameObject> Free = new Stack<GameObject>();

        private static Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    GameObject go = new GameObject("StudioCharaFilter_RowPool");
                    go.SetActive(false); // pooled rows sit under an inactive object, so they're hidden and inert
                    _poolRoot = go.transform;
                    Free.Clear();       // a scene change destroyed the old pool
                }
                return _poolRoot;
            }
        }

        public static void ReleaseAll(Transform root)
        {
            Transform pool = PoolRoot;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (Free.Count < MaxPooled && child.GetComponent<ListNode>() != null)
                {
                    child.SetParent(pool, false);
                    Free.Push(child.gameObject);
                }
                else
                {
                    child.SetParent(null, false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        public static GameObject Get(GameObject template, Transform root)
        {
            Transform unused = PoolRoot;
            while (Free.Count > 0)
            {
                GameObject go = Free.Pop();
                if (go == null) continue;
                Reset(go);
                go.transform.SetParent(root, false);
                if (!go.activeSelf) go.SetActive(true);
                return go;
            }
            return null;
        }

        // Undo what the list added to the row last time (click handler, hover handler, selection colour).
        private static void Reset(GameObject go)
        {
            ListNode node = go.GetComponent<ListNode>();
            Button button = go.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = true;
                if (button.image != null) button.image.color = Color.white;
            }
            if (node != null)
            {
                // ListNode.Awake adds 2 enter actions and 1 exit action of its own; keep those.
                if (node.listEnterAction.Count > 2) node.listEnterAction.RemoveRange(2, node.listEnterAction.Count - 2);
                if (node.listExitAction.Count > 1) node.listExitAction.RemoveRange(1, node.listExitAction.Count - 1);
                node.select = false;
            }
        }
    }
}
