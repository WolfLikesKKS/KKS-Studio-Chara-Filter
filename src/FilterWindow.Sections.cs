// Draws the filter sections in the order the user picked (Arrange button / F1 "Section order"),
// and remembers which ones are folded. Card removal is not part of this: it always stays last.
using System;
using UnityEngine;

namespace StudioCharaFilter
{
    internal sealed partial class FilterWindow
    {
        private bool SectionEnabled(string id)
        {
            switch (id)
            {
                case "Colors": return _colorsEnabled;
                case "Details": return _detailsEnabled;
                case "Uncensor": return _uncensorEnabled;
                case "Profile": return _profileEnabled;
                case "File": return _fileEnabled;
                case "Birthday": return _birthdayEnabled;
                case "Legacy": return _showLegacy;
                default: return true;
            }
        }

        private static string SectionTip(string id)
        {
            switch (id)
            {
                case "Body": return "Choose which body sliders appear here in F1, under \"Body sliders\".";
                case "Face": return "Choose which face sliders appear here in F1, under \"Face sliders\".";
                default: return "";
            }
        }

        private void DrawSections(ListController c, CardIndex idx)
        {
            if (_arrangeStyleMissing) MakeArrowStyle();
            if (_arrange)
            {
                GUILayout.Label("Use the arrows to move sections up or down, then press Done. The order is saved.", _smallLabel, Shrink);
                if (GUILayout.Button("Reset to default order", _smallButton, GUILayout.ExpandWidth(false)))
                    Queue(delegate { Settings.SectionOrder.Value = string.Join("|", Settings.SectionIds); });
            }

            for (int i = 0; i < _drawnSections.Count; i++)
            {
                string id = _drawnSections[i];
                if (i > 0) GUILayout.Space(_arrange ? 2 : 6);
                bool open = !_arrange && !_collapsed.Contains(id);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent((_arrange ? "" : open ? "[-] " : "[+] ") + Settings.SectionTitle(id), SectionTip(id)), _header, Shrink)
                    && !_arrange)
                    Queue(delegate { Settings.SetCollapsed(id, open); });
                if (_arrange)
                {
                    GUI.enabled = i > 0;
                    if (GUILayout.Button(new GUIContent("▲", "Move up"), _arrowButton, GUILayout.Width(30)))
                    {
                        string other = _drawnSections[i - 1];
                        Queue(delegate { Settings.SwapSections(id, other); });
                    }
                    GUI.enabled = i < _drawnSections.Count - 1;
                    if (GUILayout.Button(new GUIContent("▼", "Move down"), _arrowButton, GUILayout.Width(30)))
                    {
                        string other = _drawnSections[i + 1];
                        Queue(delegate { Settings.SwapSections(id, other); });
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();

                if (open) DrawSectionBody(id, c, idx);
            }
        }

        private bool _arrangeStyleMissing = true;

        private void MakeArrowStyle()
        {
            _arrowButton = new GUIStyle(GUI.skin.button);
            _arrowButton.alignment = TextAnchor.MiddleCenter;
            _arrangeStyleMissing = false;
        }

        private void DrawSectionBody(string id, ListController c, CardIndex idx)
        {
            switch (id)
            {
                case "Body":
                case "Face":
                    int n = 0;
                    foreach (PropertyFilter p in _visibleProps)
                    {
                        if (p.Section != id) continue;
                        DrawProperty(p, c);
                        n++;
                    }
                    if (n == 0)
                        GUILayout.Label("No " + id.ToLowerInvariant() + " sliders selected - pick which ones to show in F1 under \"" +
                            id + " sliders\".", _smallLabel, Shrink);
                    break;
                case "Colors": DrawColors(); break;
                case "Details": DrawDetails(); break;
                case "Clothing": DrawClothing(); break;
                case "Content": DrawContent(); break;
                case "Uncensor": DrawUncensor(); break;
                case "Profile": DrawProfile(c); break;
                case "File": DrawFileSection(c, idx); break;
                case "Birthday": DrawBirthday(); break;
                case "Authors": DrawAuthors(); break;
                case "Legacy": DrawMods(); break;
            }
        }
    }
}
