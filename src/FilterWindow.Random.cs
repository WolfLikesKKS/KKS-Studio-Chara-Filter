// The "Random" row: add N random characters from the matching cards, replace the selected characters, or replace every
// character of this list's sex in the scene (asks once more first). Below it, every replacement made this session (by
// this row or Studio's own "Change character") with an Undo button each (ReplaceHistory.cs).
using System.Collections.Generic;
using UnityEngine;

namespace StudioCharaFilter
{
    internal sealed partial class FilterWindow
    {
        private string _randomCountText = "5";
        private bool _noRepeats = true;
        private bool _confirmReplaceAll;
        private string _randomMessage = "";
        private bool _randomEnabled;   // snapshot taken at Layout
        private bool _showHistory = true;
        private bool _historyBusy;
        private readonly List<ReplaceHistory.Entry> _historySnap = new List<ReplaceHistory.Entry>();

        private void DrawRandomRow(ListController c)
        {
            int pool = c.MatchedFiles.Count;
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("<b>Random</b>", "Picks from the cards matching the filter in the current scope (" + pool +
                " cards, all pages). Replacing keeps each character's place, pose and animation."), _leftLabel, GUILayout.Width(60));
            _randomCountText = GUILayout.TextField(_randomCountText ?? "", GUILayout.Width(34));
            bool busy = RandomCharacters.Busy;
            GUI.enabled = pool > 0 && !busy;
            if (GUILayout.Button(new GUIContent("Add", "Add this many random characters to the scene."), GUILayout.Width(44)))
            {
                int n = Count();
                List<string> files = RandomCharacters.Pick(c.MatchedFiles, n, _noRepeats);
                Queue(delegate { _randomMessage = "Adding " + files.Count + "..."; _confirmReplaceAll = false; });
                RandomCharacters.Add(c.Sex, files, Done);
            }
            if (GUILayout.Button(new GUIContent("Replace selected", "Replace the characters selected in the Workspace with random ones " +
                "(only characters of this list's sex)."), GUILayout.Width(118)))
            {
                List<Studio.OCIChar> targets = RandomCharacters.Targets(c.Sex, true);
                if (targets.Count == 0) Queue(delegate { _randomMessage = "Select characters in the Workspace first."; });
                else
                {
                    List<string> files = RandomCharacters.Pick(c.MatchedFiles, targets.Count, _noRepeats);
                    Queue(delegate { _randomMessage = "Replacing " + targets.Count + "..."; _confirmReplaceAll = false; });
                    RandomCharacters.Replace(targets, files, Done);
                }
            }
            if (GUILayout.Button(new GUIContent(_confirmReplaceAll ? "Sure? Replace all" : "Replace all",
                "Replace every character of this list's sex in the scene with random ones. Click twice. Each one can be undone below."), GUILayout.Width(110)))
            {
                if (!_confirmReplaceAll) Queue(delegate { _confirmReplaceAll = true; _randomMessage = "Click again to replace every character in the scene."; });
                else
                {
                    List<Studio.OCIChar> targets = RandomCharacters.Targets(c.Sex, false);
                    List<string> files = RandomCharacters.Pick(c.MatchedFiles, targets.Count, _noRepeats);
                    Queue(delegate { _confirmReplaceAll = false; _randomMessage = "Replacing " + targets.Count + "..."; });
                    RandomCharacters.Replace(targets, files, Done);
                }
            }
            GUI.enabled = true;
            _noRepeats = GUILayout.Toggle(_noRepeats, new GUIContent("No repeats", "Each card is used once until all matching cards have been used."));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (_randomMessage.Length > 0) GUILayout.Label(_randomMessage, _smallLabel, Shrink);
            DrawHistory();
        }

        // Replacements made this session, newest first, each with Undo.
        private void DrawHistory()
        {
            // The list can change between IMGUI's layout and paint passes (undo/replace run in coroutines): draw a copy
            // taken at layout, so both passes see the same rows.
            if (Event.current.type == EventType.Layout)
            {
                ReplaceHistory.Prune();
                _historySnap.Clear();
                _historySnap.AddRange(ReplaceHistory.Entries);
                _historyBusy = ReplaceHistory.Busy || RandomCharacters.Busy;
            }
            List<ReplaceHistory.Entry> list = _historySnap;
            if (list.Count == 0) return;
            bool busy = _historyBusy;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((_showHistory ? "[-] " : "[+] ") + "Replaced characters (" + list.Count + ")", _leftLabel))
                Queue(delegate { _showHistory = !_showHistory; });
            GUILayout.FlexibleSpace();
            GUI.enabled = !busy;
            if (GUILayout.Button(new GUIContent("Undo selected", "Undo the latest replacement of each character selected in the Workspace."), GUILayout.Width(100)))
            {
                int n = ReplaceHistory.UndoSelected(Done);
                if (n == 0) Queue(delegate { _randomMessage = "None of the selected characters was replaced."; });
            }
            if (GUILayout.Button(new GUIContent("Clear", "Forget these (the characters stay as they are now)."), GUILayout.Width(50)))
                Queue(delegate { ReplaceHistory.Clear(); });
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (!_showHistory) return;
            int shown = Mathf.Min(list.Count, 30);
            for (int i = 0; i < shown; i++)
            {
                ReplaceHistory.Entry e = list[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(e.When.ToString("HH:mm:ss") + "  " + e.OldName + "  ->  " + e.NewName, _smallLabel, Shrink);
                GUILayout.FlexibleSpace();
                GUI.enabled = !busy;
                if (GUILayout.Button(new GUIContent("Undo", "Change this character back to " + e.OldName +
                    " (as it was, with its plugin settings). Later replacements of the same character are undone with it."), GUILayout.Width(50)))
                {
                    ReplaceHistory.Entry target = e;
                    Queue(delegate { _randomMessage = "Undoing..."; });
                    ReplaceHistory.Undo(target, Done);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (list.Count > shown) GUILayout.Label("... and " + (list.Count - shown) + " older", _smallLabel, Shrink);
        }

        private int Count()
        {
            int n;
            if (!int.TryParse(_randomCountText, out n)) n = 1;
            return Mathf.Clamp(n, 1, 100);
        }

        private void Done(string message)
        {
            Queue(delegate { _randomMessage = message; });
        }
    }
}
