// "Choose folders" scope: a folder tree built from the card index (no Browser Folders needed).
using System;
using System.Collections.Generic;
using System.Linq;

namespace StudioCharaFilter
{
    internal sealed class FolderNode
    {
        public string Key;      // lowercase relative path, "" = root
        public string Name;     // last path part, original case
        public string Path;     // relative path, original case
        public int Depth;
        public int Direct;      // cards directly in this folder
        public int Total;       // cards here and in all subfolders
        public bool HasChildren;
    }

    internal sealed class FolderPicker
    {
        public readonly HashSet<string> Selected = new HashSet<string>();
        public readonly HashSet<string> Expanded = new HashSet<string>();
        public bool IncludeSubfolders = true;
        public int Version; // bumped when the selection changes
        public List<FolderNode> Nodes = new List<FolderNode>();
        private int _generation = -1;
        private int _sex = -1;

        public void Update(CardIndex idx)
        {
            if (_generation == idx.Generation && _sex == idx.Sex) return;
            if (_sex != idx.Sex)
            {
                Selected.Clear();
                Expanded.Clear();
                Version++;
            }
            _generation = idx.Generation;
            _sex = idx.Sex;

            Dictionary<string, FolderNode> map = new Dictionary<string, FolderNode>();
            FolderNode root = new FolderNode();
            root.Key = "";
            root.Name = idx.RootName;
            root.Path = "";
            root.Depth = 0;
            map[""] = root;

            foreach (CardInfo ci in idx.Snapshot())
            {
                if (!ci.Ok) continue;
                FolderNode node = GetOrAdd(map, ci.FolderKey ?? "", ci.Folder ?? "");
                node.Direct++;
                // Add to this folder and every parent.
                string k = ci.FolderKey ?? "";
                while (true)
                {
                    map[k].Total++;
                    if (k.Length == 0) break;
                    int slash = k.LastIndexOf('/');
                    k = slash >= 0 ? k.Substring(0, slash) : "";
                }
            }

            foreach (FolderNode n in map.Values)
            {
                if (n.Key.Length == 0) continue;
                int slash = n.Key.LastIndexOf('/');
                string parent = slash >= 0 ? n.Key.Substring(0, slash) : "";
                FolderNode p;
                if (map.TryGetValue(parent, out p)) p.HasChildren = true;
            }

            Nodes = map.Values.OrderBy(n => n.Key, StringComparer.Ordinal).ToList();
            // Root stays first; order() above already sorts "" first.
        }

        // Makes sure a folder and all its parents exist in the map.
        private static FolderNode GetOrAdd(Dictionary<string, FolderNode> map, string key, string path)
        {
            FolderNode n;
            if (map.TryGetValue(key, out n)) return n;
            int slash = key.LastIndexOf('/');
            string parentKey = slash >= 0 ? key.Substring(0, slash) : "";
            int pslash = path.LastIndexOf('/');
            string parentPath = pslash >= 0 ? path.Substring(0, pslash) : "";
            FolderNode parent = GetOrAdd(map, parentKey, parentPath);
            n = new FolderNode();
            n.Key = key;
            n.Path = path;
            n.Name = pslash >= 0 ? path.Substring(pslash + 1) : path;
            if (n.Name.Length == 0) n.Name = key;
            n.Depth = parent.Depth + 1;
            map[key] = n;
            return n;
        }

        public bool IsVisible(FolderNode n)
        {
            // A folder shows when all of its parents are expanded.
            string k = n.Key;
            if (k.Length == 0) return true;
            while (true)
            {
                int slash = k.LastIndexOf('/');
                string parent = slash >= 0 ? k.Substring(0, slash) : "";
                if (!Expanded.Contains(parent)) return false;
                if (parent.Length == 0) return true;
                k = parent;
            }
        }

        public bool Matches(CardInfo ci)
        {
            if (Selected.Count == 0) return false;
            string k = ci.FolderKey ?? "";
            if (Selected.Contains(k)) return true;
            if (!IncludeSubfolders) return false;
            foreach (string s in Selected)
            {
                if (s.Length == 0) return true;
                if (k.Length > s.Length && k[s.Length] == '/' && k.StartsWith(s, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public string Summary()
        {
            if (Selected.Count == 0) return "none selected";
            if (Selected.Count == 1)
            {
                string only = Selected.First();
                FolderNode n = Nodes.FirstOrDefault(x => x.Key == only);
                return n == null ? "1 folder" : (n.Key.Length == 0 ? n.Name : n.Path);
            }
            return Selected.Count + " folders";
        }
    }
}
