// Background index of all cards under chara/female or chara/male, cached to disk.
// Only new or changed files (by size + modified time) are re-read.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace StudioCharaFilter
{
    internal sealed class CardIndex
    {
        private const int CacheFormat = 10; // 9: face, colors, profile, details, hands/feet; 10: card marker flag

        public readonly int Sex;
        private readonly string _root;
        private readonly string _cacheFile;
        private readonly object _lock = new object();
        private Dictionary<string, CardInfo> _cards = new Dictionary<string, CardInfo>();
        private Dictionary<string, ModMeta> _meta = new Dictionary<string, ModMeta>();
        private bool _cacheLoaded;
        private volatile bool _rescanRequested;

        public volatile bool Busy;
        public volatile bool CacheReady;   // the cached index has been loaded (may still be scanning for changes)
        public volatile int Done;
        public volatile int Total;
        public volatile int Generation; // bumped whenever the contents change
        public volatile string LastResult = "";

        private readonly string _rootNorm;   // lowercase, "/" separators, no trailing slash
        private readonly int _rootLength;

        public CardIndex(int sex, string root, string cacheFile)
        {
            Sex = sex;
            _root = root;
            _cacheFile = cacheFile;
            _rootNorm = CardInfo.NormalizePath(root).TrimEnd('/');
            _rootLength = _rootNorm.Length;
        }

        public string RootName
        {
            get { return Sex == 1 ? "female" : "male"; }
        }

        // Works out the card's folder relative to the root from its path.
        private void AssignFolder(CardInfo ci)
        {
            string key = ci.Key ?? "";
            int slash = key.LastIndexOf('/');
            string dirKey = slash >= 0 ? key.Substring(0, slash) : "";
            if (dirKey.StartsWith(_rootNorm) && (dirKey.Length == _rootLength || dirKey[_rootLength] == '/'))
            {
                string relKey = dirKey.Length > _rootLength ? dirKey.Substring(_rootLength + 1) : "";
                ci.FolderKey = relKey;
                // Take the same characters from the original-case path for display.
                string full = CardInfo.DisplayPath(ci.FilePath);
                int fslash = full.LastIndexOf('/');
                string dir = fslash >= 0 ? full.Substring(0, fslash) : "";
                ci.Folder = (relKey.Length > 0 && dir.Length >= relKey.Length) ? dir.Substring(dir.Length - relKey.Length) : relKey;
            }
            else
            {
                ci.FolderKey = dirKey;
                ci.Folder = dirKey;
            }
        }

        public int Count
        {
            get { lock (_lock) return _cards.Count; }
        }

        public CardInfo Get(string anyPath)
        {
            string key = CardInfo.NormalizePath(anyPath);
            CardInfo ci;
            lock (_lock)
            {
                if (_cards.TryGetValue(key, out ci)) return ci;
            }
            return null;
        }

        // Drop a card right away (e.g. after it was moved out); the next scan confirms it.
        public void Remove(string anyPath)
        {
            string key = CardInfo.NormalizePath(anyPath);
            lock (_lock)
            {
                if (!_cards.ContainsKey(key)) return;
                Dictionary<string, CardInfo> copy = new Dictionary<string, CardInfo>(_cards);
                copy.Remove(key);
                _cards = copy;
            }
            Generation++;
        }

        public ModMeta GetMeta(string modId)
        {
            ModMeta m;
            lock (_lock)
            {
                if (_meta.TryGetValue(modId, out m)) return m;
            }
            return null;
        }

        private void MergeMeta(CardInfo ci)
        {
            if (ci.NewMeta == null) return;
            lock (_lock)
            {
                foreach (KeyValuePair<string, ModMeta> kv in ci.NewMeta)
                {
                    ModMeta old;
                    if (!_meta.TryGetValue(kv.Key, out old) || (old.Website.Length == 0 && kv.Value.Website.Length > 0))
                        _meta[kv.Key] = kv.Value;
                }
            }
            ci.NewMeta = null;
        }

        public List<CardInfo> Snapshot()
        {
            lock (_lock) return new List<CardInfo>(_cards.Values);
        }

        public void RequestScan()
        {
            if (Busy)
            {
                _rescanRequested = true;
                return;
            }
            Busy = true;
            Thread t = new Thread(Worker);
            t.IsBackground = true;
            t.Name = "StudioCharaFilter index";
            t.Start();
        }

        private void Worker()
        {
            try
            {
                do
                {
                    _rescanRequested = false;
                    Scan();
                } while (_rescanRequested);
            }
            catch (Exception e)
            {
                StudioCharaFilterPlugin.Log.LogError("[SCF] Index scan failed: " + e);
            }
            finally
            {
                Busy = false;
            }
        }

        private void Scan()
        {
            Stopwatch sw = Stopwatch.StartNew();
            if (!_cacheLoaded)
            {
                _cacheLoaded = true;
                Dictionary<string, ModMeta> loadedMeta;
                Dictionary<string, CardInfo> loaded = LoadCache(out loadedMeta);
                lock (_lock) _meta = loadedMeta;
                lock (_lock) _cards = loaded;
                Generation++;
                CacheReady = true;
                StudioCharaFilterPlugin.Log.LogInfo(string.Format("[SCF] {0} index: loaded {1} cached cards in {2} ms",
                    SexName, loaded.Count, sw.ElapsedMilliseconds));
            }

            if (!Directory.Exists(_root))
            {
                LastResult = "Folder not found: " + _root;
                return;
            }

            string[] files = Directory.GetFiles(_root, "*.png", SearchOption.AllDirectories);
            Total = files.Length;
            Done = 0;

            Dictionary<string, CardInfo> old;
            lock (_lock) old = _cards;
            Dictionary<string, CardInfo> fresh = new Dictionary<string, CardInfo>(files.Length);

            // Pass 1: keep unchanged cards (same size and date), collect the ones that need reading.
            List<CardInfo> toRead = new List<CardInfo>();
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string key = CardInfo.NormalizePath(file);
                try
                {
                    FileInfo fi = new FileInfo(file);
                    long size = fi.Length;
                    long mtime = fi.LastWriteTimeUtc.Ticks;
                    CardInfo ci;
                    if (old.TryGetValue(key, out ci) && ci.Size == size && ci.MTimeTicks == mtime)
                    {
                        if (ci.FilePath != file)
                        {
                            ci.FilePath = file;
                            AssignFolder(ci);
                        }
                        fresh[key] = ci;
                    }
                    else
                    {
                        ci = new CardInfo();
                        ci.Key = key;
                        ci.FilePath = file;
                        ci.Size = size;
                        ci.MTimeTicks = mtime;
                        AssignFolder(ci);
                        toRead.Add(ci);
                    }
                }
                catch (Exception e)
                {
                    StudioCharaFilterPlugin.Log.LogDebug("[SCF] Skipped " + file + ": " + e.Message);
                }
            }
            Done = files.Length - toRead.Count;

            // Pass 2: read new/changed cards on several threads (helps a lot on SSDs).
            int reread = toRead.Count, failed = 0;
            Stopwatch readSw = Stopwatch.StartNew();
            if (toRead.Count > 0)
            {
                int threads = Math.Max(1, Math.Min(Settings.IndexThreads.Value, toRead.Count / 20 + 1));
                int next = -1, finished = 0;
                Thread[] workers = new Thread[threads];
                for (int t = 0; t < threads; t++)
                {
                    workers[t] = new Thread(() =>
                    {
                        while (true)
                        {
                            int i = Interlocked.Increment(ref next);
                            if (i >= toRead.Count) break;
                            CardInfo ci = toRead[i];
                            try { CardReader.Read(ci); }
                            catch (Exception e)
                            {
                                ci.Ok = false;
                                StudioCharaFilterPlugin.Log.LogDebug("[SCF] Could not read " + ci.FilePath + ": " + e.Message);
                            }
                            Interlocked.Increment(ref finished);
                        }
                    });
                    workers[t].IsBackground = true;
                    workers[t].Name = "StudioCharaFilter index " + t;
                    workers[t].Start();
                }

                // Report progress, and publish what's done every couple of seconds so filters work during the first index.
                Stopwatch publish = Stopwatch.StartNew();
                int alive = threads;
                while (alive > 0)
                {
                    alive = 0;
                    foreach (Thread w in workers)
                        if (!w.Join(200)) alive++;
                    Done = files.Length - toRead.Count + Thread.VolatileRead(ref finished);
                    if (publish.ElapsedMilliseconds > 2000 && alive > 0)
                    {
                        publish.Reset();
                        publish.Start();
                        Dictionary<string, CardInfo> partial = new Dictionary<string, CardInfo>(old);
                        foreach (KeyValuePair<string, CardInfo> kv in fresh) partial[kv.Key] = kv.Value;
                        int upTo = Math.Min(toRead.Count, Thread.VolatileRead(ref next));
                        for (int i = 0; i < upTo; i++)
                            if (toRead[i].Ok) partial[toRead[i].Key] = toRead[i];
                        lock (_lock) _cards = partial;
                        Generation++;
                    }
                }

                foreach (CardInfo ci in toRead)
                {
                    if (!ci.Ok) failed++;
                    MergeMeta(ci);
                    fresh[ci.Key] = ci;
                }
            }
            long readMs = readSw.ElapsedMilliseconds;
            Done = files.Length;

            bool changed = reread > 0 || fresh.Count != old.Count;
            lock (_lock) _cards = fresh;
            Generation++;

            LastResult = string.Format("{0} cards, {1} re-read ({2} unreadable)", fresh.Count, reread, failed);
            StudioCharaFilterPlugin.Log.Log(reread > 0 ? BepInEx.Logging.LogLevel.Info : Settings.TimingLevel, string.Format(
                "[SCF] {0} index scan: {1} files, {2} re-read in {3} ms ({4} unreadable, {6} threads), total {5} ms",
                SexName, files.Length, reread, readMs, failed, sw.ElapsedMilliseconds, Settings.IndexThreads.Value));

            if (changed) SaveCache(fresh);
        }

        private string SexName
        {
            get { return Sex == 1 ? "Female" : "Male"; }
        }

        private Dictionary<string, CardInfo> LoadCache(out Dictionary<string, ModMeta> meta)
        {
            Dictionary<string, CardInfo> result = new Dictionary<string, CardInfo>();
            meta = new Dictionary<string, ModMeta>();
            if (!File.Exists(_cacheFile)) return result;
            try
            {
                using (FileStream fs = new FileStream(_cacheFile, FileMode.Open, FileAccess.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    if (br.ReadInt32() != CacheFormat) return result;
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        CardInfo ci = new CardInfo();
                        ci.Key = br.ReadString();
                        ci.FilePath = br.ReadString();
                        ci.Size = br.ReadInt64();
                        ci.MTimeTicks = br.ReadInt64();
                        ci.Ok = br.ReadBoolean();
                        ci.OddMarker = br.ReadBoolean();
                        ci.FirstName = br.ReadString();
                        ci.LastName = br.ReadString();
                        ci.Sex = br.ReadInt32();
                        ci.Personality = br.ReadInt32();
                        ci.BirthMonth = br.ReadInt32();
                        ci.BirthDay = br.ReadInt32();
                        int nb = br.ReadInt32();
                        ci.Body = new float[nb];
                        for (int j = 0; j < nb; j++) ci.Body[j] = br.ReadSingle();
                        ci.AreolaSize = br.ReadSingle();
                        ci.BustSoftness = br.ReadSingle();
                        ci.BustWeight = br.ReadSingle();
                        ci.AnusScale = br.ReadSingle();
                        ci.UncensorBody = string.Intern(br.ReadString());
                        ci.HandScale = br.ReadSingle();
                        ci.FootScale = br.ReadSingle();
                        ci.HasAbmx = br.ReadBoolean();
                        ci.Futa = br.ReadBoolean();
                        int nf = br.ReadInt32();
                        ci.Face = new float[nf];
                        for (int j = 0; j < nf; j++) ci.Face[j] = br.ReadSingle();
                        ci.IrisWidth = br.ReadSingle();
                        ci.IrisHeight = br.ReadSingle();
                        ci.Fangs = br.ReadBoolean();
                        ci.HairColor = ReadColor(br);
                        ci.EyeColor = ReadColor(br);
                        ci.SkinColor = ReadColor(br);
                        ci.UnderhairId = br.ReadInt32();
                        ci.SunburnId = br.ReadInt32();
                        ci.NipId = br.ReadInt32();
                        ci.HasPaint = br.ReadBoolean();
                        ci.NipModName = string.Intern(br.ReadString());
                        ci.BloodType = br.ReadInt32();
                        ci.Club = br.ReadInt32();
                        ci.Aggressive = br.ReadInt32();
                        ci.Diligence = br.ReadInt32();
                        ci.Kindness = br.ReadInt32();
                        ci.VoiceRate = br.ReadSingle();
                        ci.Likes = br.ReadInt32();
                        ci.Refusals = br.ReadInt32();
                        ci.Traits = br.ReadInt32();
                        int nk = br.ReadInt32();
                        ci.ExtKeys = new string[nk];
                        for (int j = 0; j < nk; j++) ci.ExtKeys[j] = string.Intern(br.ReadString());
                        int nm = br.ReadInt32();
                        ci.ModIds = new string[nm];
                        ci.ModCats = new int[nm];
                        ci.ModRegions = new byte[nm];
                        for (int j = 0; j < nm; j++)
                        {
                            ci.ModIds[j] = string.Intern(br.ReadString());
                            ci.ModCats[j] = br.ReadInt32();
                            ci.ModRegions[j] = br.ReadByte();
                        }
                        ci.RegionMask = br.ReadInt32();
                        int na = br.ReadInt32();
                        ci.Authors = new string[na];
                        for (int j = 0; j < na; j++) ci.Authors[j] = string.Intern(br.ReadString());
                        int no = br.ReadInt32();
                        ci.OutfitStates = br.ReadBytes(no);
                        ci.OutfitTops = new int[no];
                        for (int j = 0; j < no; j++) ci.OutfitTops[j] = br.ReadInt32();
                        int nn = br.ReadInt32();
                        ci.OutfitNames = new string[nn];
                        for (int j = 0; j < nn; j++)
                        {
                            string on = br.ReadString();
                            ci.OutfitNames[j] = on.Length == 0 ? null : string.Intern(on);
                        }
                        AssignFolder(ci);
                        result[ci.Key] = ci;
                    }
                    int metaCount = br.ReadInt32();
                    for (int i = 0; i < metaCount; i++)
                    {
                        string id = br.ReadString();
                        ModMeta m = new ModMeta();
                        m.Name = br.ReadString();
                        m.Author = br.ReadString();
                        m.Website = br.ReadString();
                        meta[id] = m;
                    }
                }
            }
            catch (Exception e)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Cache unreadable, rebuilding: " + e.Message);
                result.Clear();
                meta.Clear();
            }
            return result;
        }

        private static float[] ReadColor(BinaryReader br)
        {
            if (!br.ReadBoolean()) return null;
            return new[] { br.ReadSingle(), br.ReadSingle(), br.ReadSingle() };
        }

        private static void WriteColor(BinaryWriter bw, float[] c)
        {
            bw.Write(c != null && c.Length >= 3);
            if (c != null && c.Length >= 3) { bw.Write(c[0]); bw.Write(c[1]); bw.Write(c[2]); }
        }

        private void SaveCache(Dictionary<string, CardInfo> cards)
        {
            try
            {
                string dir = Path.GetDirectoryName(_cacheFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmp = _cacheFile + ".tmp";
                using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(CacheFormat);
                    bw.Write(cards.Count);
                    foreach (CardInfo ci in cards.Values)
                    {
                        bw.Write(ci.Key);
                        bw.Write(ci.FilePath);
                        bw.Write(ci.Size);
                        bw.Write(ci.MTimeTicks);
                        bw.Write(ci.Ok);
                        bw.Write(ci.OddMarker);
                        bw.Write(ci.FirstName ?? "");
                        bw.Write(ci.LastName ?? "");
                        bw.Write(ci.Sex);
                        bw.Write(ci.Personality);
                        bw.Write(ci.BirthMonth);
                        bw.Write(ci.BirthDay);
                        bw.Write(ci.Body.Length);
                        for (int j = 0; j < ci.Body.Length; j++) bw.Write(ci.Body[j]);
                        bw.Write(ci.AreolaSize);
                        bw.Write(ci.BustSoftness);
                        bw.Write(ci.BustWeight);
                        bw.Write(ci.AnusScale);
                        bw.Write(ci.UncensorBody ?? "");
                        bw.Write(ci.HandScale);
                        bw.Write(ci.FootScale);
                        bw.Write(ci.HasAbmx);
                        bw.Write(ci.Futa);
                        bw.Write(ci.Face.Length);
                        for (int j = 0; j < ci.Face.Length; j++) bw.Write(ci.Face[j]);
                        bw.Write(ci.IrisWidth);
                        bw.Write(ci.IrisHeight);
                        bw.Write(ci.Fangs);
                        WriteColor(bw, ci.HairColor);
                        WriteColor(bw, ci.EyeColor);
                        WriteColor(bw, ci.SkinColor);
                        bw.Write(ci.UnderhairId);
                        bw.Write(ci.SunburnId);
                        bw.Write(ci.NipId);
                        bw.Write(ci.HasPaint);
                        bw.Write(ci.NipModName ?? "");
                        bw.Write(ci.BloodType);
                        bw.Write(ci.Club);
                        bw.Write(ci.Aggressive);
                        bw.Write(ci.Diligence);
                        bw.Write(ci.Kindness);
                        bw.Write(ci.VoiceRate);
                        bw.Write(ci.Likes);
                        bw.Write(ci.Refusals);
                        bw.Write(ci.Traits);
                        bw.Write(ci.ExtKeys.Length);
                        for (int j = 0; j < ci.ExtKeys.Length; j++) bw.Write(ci.ExtKeys[j]);
                        bw.Write(ci.ModIds.Length);
                        for (int j = 0; j < ci.ModIds.Length; j++)
                        {
                            bw.Write(ci.ModIds[j]);
                            bw.Write(ci.ModCats[j]);
                            bw.Write(ci.ModRegions[j]);
                        }
                        bw.Write(ci.RegionMask);
                        bw.Write(ci.Authors.Length);
                        for (int j = 0; j < ci.Authors.Length; j++) bw.Write(ci.Authors[j]);
                        bw.Write(ci.OutfitStates.Length);
                        bw.Write(ci.OutfitStates);
                        for (int j = 0; j < ci.OutfitStates.Length; j++) bw.Write(j < ci.OutfitTops.Length ? ci.OutfitTops[j] : 0);
                        bw.Write(ci.OutfitNames.Length);
                        for (int j = 0; j < ci.OutfitNames.Length; j++) bw.Write(ci.OutfitNames[j] ?? "");
                    }
                    Dictionary<string, ModMeta> metaCopy;
                    lock (_lock) metaCopy = new Dictionary<string, ModMeta>(_meta);
                    bw.Write(metaCopy.Count);
                    foreach (KeyValuePair<string, ModMeta> kv in metaCopy)
                    {
                        bw.Write(kv.Key);
                        bw.Write(kv.Value.Name ?? "");
                        bw.Write(kv.Value.Author ?? "");
                        bw.Write(kv.Value.Website ?? "");
                    }
                }
                if (File.Exists(_cacheFile)) File.Delete(_cacheFile);
                File.Move(tmp, _cacheFile);
            }
            catch (Exception e)
            {
                StudioCharaFilterPlugin.Log.LogWarning("[SCF] Could not save index cache: " + e.Message);
            }
        }
    }
}
