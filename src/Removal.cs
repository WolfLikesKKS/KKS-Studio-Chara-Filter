// Safe card removal: cards are never deleted, only moved to "-UserRequest2Delete-" next to CharaStudio.exe
// (keeping their folders), with a log so they can be restored. Emptying that folder sends files to the
// Windows Recycle Bin, so even that step can be undone until the Recycle Bin is emptied.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;

namespace StudioCharaFilter
{
    internal sealed class RemovedEntry
    {
        public DateTime Time;
        public string Original;
        public string Stored;
    }

    internal static class Removal
    {
        public const string FolderName = "-UserRequest2Delete-";
        private const string LogName = "removed_cards.txt";

        public static string Folder
        {
            get { return Path.Combine(Paths.GameRootPath, FolderName); }
        }

        private static string LogPath
        {
            get { return Path.Combine(Folder, LogName); }
        }

        private static List<RemovedEntry> _cache;
        public static int Version; // bumped on every change so the window can refresh

        public static List<RemovedEntry> Entries()
        {
            if (_cache != null) return _cache;
            List<RemovedEntry> list = new List<RemovedEntry>();
            try
            {
                if (File.Exists(LogPath))
                {
                    foreach (string line in File.ReadAllLines(LogPath, Encoding.UTF8))
                    {
                        string[] p = line.Split('\t');
                        if (p.Length < 3) continue;
                        long ticks;
                        RemovedEntry e = new RemovedEntry();
                        e.Time = long.TryParse(p[0], out ticks) ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime() : DateTime.MinValue;
                        e.Original = p[1];
                        e.Stored = p[2];
                        if (File.Exists(e.Stored)) list.Add(e); // skip entries whose file is gone (e.g. folder emptied by hand)
                    }
                }
            }
            catch (Exception ex)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Could not read the removed cards log: " + ex.Message);
            }
            list.Reverse(); // newest first
            _cache = list;
            return list;
        }

        private static void SaveLog(List<RemovedEntry> newestFirst)
        {
            Directory.CreateDirectory(Folder);
            StringBuilder sb = new StringBuilder();
            for (int i = newestFirst.Count - 1; i >= 0; i--)
            {
                RemovedEntry e = newestFirst[i];
                sb.Append(e.Time.ToUniversalTime().Ticks).Append('\t').Append(e.Original).Append('\t').Append(e.Stored).Append("\r\n");
            }
            File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);
            _cache = newestFirst;
            Version++;
        }

        // Moves a card into the removed folder. Returns an error message, or null on success.
        public static string Remove(string cardPath)
        {
            try
            {
                string full = Path.GetFullPath(cardPath);
                if (!File.Exists(full)) return "The card file no longer exists.";

                // Keep the path relative to the game folder (e.g. UserData\chara\female\Animal\x.png).
                string root = Path.GetFullPath(Paths.GameRootPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                string rel = full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : Path.GetFileName(full);
                string target = UniquePath(Path.Combine(Folder, rel));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Move(full, target);

                List<RemovedEntry> list = new List<RemovedEntry>(Entries());
                RemovedEntry e = new RemovedEntry();
                e.Time = DateTime.Now;
                e.Original = full;
                e.Stored = target;
                list.Insert(0, e);
                SaveLog(list);
                StudioCharaFilterPlugin.Log.LogInfo("[SCF] Moved card to removed folder: " + full + " -> " + target);
                return null;
            }
            catch (Exception ex)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Could not remove card " + cardPath + ": " + ex.Message);
                return ex.Message;
            }
        }

        // Moves a removed card back to where it was. Returns an error message, or null on success.
        public static string Restore(RemovedEntry e)
        {
            try
            {
                if (!File.Exists(e.Stored)) return "The file is no longer in the removed folder.";
                string target = UniquePath(e.Original);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Move(e.Stored, target);
                List<RemovedEntry> list = new List<RemovedEntry>(Entries());
                list.Remove(e);
                SaveLog(list);
                StudioCharaFilterPlugin.Log.LogInfo("[SCF] Restored card: " + target);
                return null;
            }
            catch (Exception ex)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Could not restore card " + e.Original + ": " + ex.Message);
                return ex.Message;
            }
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 2; ; i++)
            {
                string p = Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!File.Exists(p)) return p;
            }
        }

        public static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + Folder + "\"");
            }
            catch (Exception ex)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Could not open folder: " + ex.Message);
            }
        }

        // Sends every removed card to the Windows Recycle Bin. Returns a status message.
        public static string EmptyToRecycleBin()
        {
            List<RemovedEntry> entries = Entries();
            if (entries.Count == 0) return "Nothing to empty.";
            StringBuilder from = new StringBuilder();
            foreach (RemovedEntry e in entries) from.Append(e.Stored).Append('\0');
            from.Append('\0');

            SHFILEOPSTRUCT op = new SHFILEOPSTRUCT();
            op.wFunc = FO_DELETE;
            op.pFrom = from.ToString();
            op.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI;
            int result;
            try
            {
                result = SHFileOperation(ref op);
            }
            catch (Exception ex)
            {
                return "Recycle Bin not available (" + ex.Message + "). Use Open folder and delete the files yourself.";
            }
            if (result != 0 || op.fAnyOperationsAborted)
                return "Windows reported a problem (code " + result + "). Use Open folder and check the files yourself.";

            int moved = entries.Count;
            SaveLog(new List<RemovedEntry>(Entries().FindAll(x => File.Exists(x.Stored))));
            StudioCharaFilterPlugin.Log.LogInfo("[SCF] Sent " + moved + " removed cards to the Recycle Bin");
            return "Sent " + moved + " cards to the Recycle Bin.";
        }

        public static void Invalidate()
        {
            _cache = null;
            Version++;
        }

        private const uint FO_DELETE = 3;
        private const ushort FOF_SILENT = 0x0004;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOERRORUI = 0x0400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
    }
}
