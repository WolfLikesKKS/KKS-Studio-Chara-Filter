// "Installed zipmods not used by any character card" - information only, nothing is changed.
// Uses Sideloader's list of loaded zipmods (by reflection) and the zipmod IDs our index found on cards.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;

namespace StudioCharaFilter
{
    internal sealed class InstalledMod
    {
        public string Guid = "";
        public string Name = "";
        public string Author = "";
        public string Version = "";
        public string Website = "";
        public string File = "";
        public string Folder = "";   // top folder inside "mods", e.g. "Sideloader Modpack - Studio"
        public long Size;
    }

    internal sealed class ZipmodReportResult
    {
        public int Installed;
        public int UsedByCards;
        public List<InstalledMod> Unused = new List<InstalledMod>();
        public List<KeyValuePair<string, int>> FolderCounts = new List<KeyValuePair<string, int>>();
        public Dictionary<string, long> FolderSizes = new Dictionary<string, long>();
        public long UnusedSize;
        public string Error;
    }

    internal static class ZipmodReport
    {
        public static ZipmodReportResult Build(IEnumerable<CardInfo> cards)
        {
            ZipmodReportResult r = new ZipmodReportResult();
            List<InstalledMod> installed;
            try { installed = GetInstalled(); }
            catch (Exception e) { r.Error = "Could not read Sideloader's zipmod list: " + e.Message; return r; }
            if (installed == null) { r.Error = "Sideloader's zipmod list isn't available in this version."; return r; }

            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CardInfo ci in cards)
                if (ci.Ok) foreach (string id in ci.ModIds) used.Add(id);

            r.Installed = installed.Count;
            Dictionary<string, int> folders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (InstalledMod m in installed)
            {
                if (used.Contains(m.Guid)) { r.UsedByCards++; continue; }
                try { if (m.File.Length > 0 && System.IO.File.Exists(m.File)) m.Size = new FileInfo(m.File).Length; }
                catch (Exception) { }
                r.Unused.Add(m);
                r.UnusedSize += m.Size;
                int n;
                folders.TryGetValue(m.Folder, out n);
                folders[m.Folder] = n + 1;
                long sz;
                r.FolderSizes.TryGetValue(m.Folder, out sz);
                r.FolderSizes[m.Folder] = sz + m.Size;
            }
            r.Unused = r.Unused.OrderBy(m => m.Folder, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            r.FolderCounts = folders.OrderByDescending(kv => kv.Value).ToList();
            return r;
        }

        private static List<InstalledMod> GetInstalled()
        {
            Type t = AccessTools.TypeByName("Sideloader.Sideloader");
            if (t == null) return null;
            object holder = UnityEngine.Object.FindObjectOfType(t);
            object val = null;
            foreach (string name in new[] { "Manifests", "LoadedManifests" })
            {
                PropertyInfo p = AccessTools.Property(t, name);
                if (p != null) { val = p.GetValue(p.GetGetMethod(true).IsStatic ? null : holder, null); }
                if (val == null)
                {
                    FieldInfo f = AccessTools.Field(t, name);
                    if (f != null) val = f.GetValue(f.IsStatic ? null : holder);
                }
                if (val != null) break;
            }
            if (val == null) return null;

            IEnumerable items = val is IDictionary ? ((IDictionary)val).Values : val as IEnumerable;
            if (items == null) return null;

            string modsDir = Path.GetFullPath(Path.Combine(Paths.GameRootPath, "mods")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            List<InstalledMod> list = new List<InstalledMod>();
            foreach (object m in items)
            {
                if (m == null) continue;
                InstalledMod im = new InstalledMod();
                im.Guid = Get(m, "GUID");
                if (im.Guid.Length == 0) continue;
                im.Name = Get(m, "Name");
                im.Author = Get(m, "Author");
                im.Version = Get(m, "Version");
                im.Website = Get(m, "Website");
                im.File = Get(m, "ArchiveFilename");
                if (im.File.Length == 0) im.File = Get(m, "FileName");
                try
                {
                    string full = im.File.Length > 0 ? Path.GetFullPath(im.File) : "";
                    if (full.StartsWith(modsDir, StringComparison.OrdinalIgnoreCase))
                    {
                        string rel = full.Substring(modsDir.Length);
                        int slash = rel.IndexOfAny(new[] { '\\', '/' });
                        im.Folder = slash > 0 ? rel.Substring(0, slash) : "(mods folder)";
                    }
                    else im.Folder = "(other)";
                }
                catch (Exception) { im.Folder = "(other)"; }
                list.Add(im);
            }
            return list;
        }

        private static string Get(object o, string name)
        {
            try
            {
                PropertyInfo p = AccessTools.Property(o.GetType(), name);
                if (p != null) { object v = p.GetValue(o, null); return v == null ? "" : v.ToString(); }
                FieldInfo f = AccessTools.Field(o.GetType(), name);
                if (f != null) { object v = f.GetValue(o); return v == null ? "" : v.ToString(); }
            }
            catch (Exception) { }
            return "";
        }

        public static string FormatSize(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0") + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0") + " MB";
            return (bytes / 1024.0).ToString("0") + " KB";
        }

        public static string Export(ZipmodReportResult r)
        {
            string path = Path.Combine(UserData.Path, "StudioCharaFilter_UnusedZipmods.txt");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Installed zipmods not used by any character card (Studio items, maps, scenes and Maker-only content are NOT checked).");
            sb.AppendLine("Folder\tFile\tMod ID\tName\tAuthor\tVersion\tSize\tWebsite");
            foreach (InstalledMod m in r.Unused)
                sb.AppendLine(m.Folder + "\t" + m.File + "\t" + m.Guid + "\t" + m.Name + "\t" + m.Author + "\t" + m.Version + "\t" + FormatSize(m.Size) + "\t" + m.Website);
            System.IO.File.WriteAllText(path, sb.ToString());
            return path;
        }
    }
}
