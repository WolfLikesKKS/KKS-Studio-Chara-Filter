// Metadata kept per card in the index, plus the fast card reader that fills it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StudioCharaFilter
{
    internal sealed class CardInfo
    {
        public string Key;        // normalized path (lowercase, forward slashes) used for lookups
        public string FilePath;   // path as found on disk
        public long Size;
        public long MTimeTicks;   // last write time, UTC ticks
        public bool Ok;           // false = not a readable character card
        public bool OddMarker;    // KK: a Party ("SP") card - the game decides whether it loads
        public string FirstName = "";
        public string LastName = "";
        public int Sex;
        public int Personality;
        public int BirthMonth;   // 1-12 (the Maker's default is May 17)
        public int BirthDay;
        public float[] Body = new float[0];
        public float AreolaSize = 0.5f;
        public float BustSoftness = 0.5f;
        public float BustWeight = 0.5f;
        public float AnusScale = 1f;          // ABMX cf_j_ana scale (1 = not edited)
        public float HandScale = 1f;          // ABMX cf_j_hand_L/R average
        public float FootScale = 1f;          // ABMX cf_j_foot_L/R average
        public bool HasAbmx;                  // any ABMX bone edits
        public bool Futa;                     // Uncensor Selector shows a penis on this card

        // Face
        public float[] Face = new float[0];   // ChaFileFace.shapeValueFace (52 Maker face sliders)
        public float IrisWidth = 1f, IrisHeight = 1f;
        public bool Fangs;                    // doubleTooth (yaeba)

        // Colors (r, g, b), null when not read
        public float[] HairColor, EyeColor, SkinColor;

        // Body details
        public int UnderhairId, SunburnId, NipId;
        public bool HasPaint;
        public string NipModName = "";        // zipmod name when the nipple texture is a zipmod item

        // Profile
        public int BloodType, Club, Aggressive, Diligence, Kindness;
        public float VoiceRate = 0.5f;
        public int Likes, Refusals, Traits;   // bit masks, see ProfileData
        public string UncensorBody = "";      // Uncensor Selector body GUID ("" = default uncensor)
        public string[] ExtKeys = new string[0];

        // Zipmod items used by the card (unique mod + category pairs) and their body region.
        public string[] ModIds = new string[0];
        public int[] ModCats = new int[0];
        public byte[] ModRegions = new byte[0];
        public int RegionMask;            // regions that use any zipmod
        public string[] Authors = new string[0];

        // Outfits: one state per outfit slot (0 = nothing worn, 1 = underwear only, 2 = dressed, 3 = top only, no bottom),
        // the top's item id (-1 = zipmod top, -2 = zipmod towel) for towel detection,
        // and More Outfits names (index = slot, null where none).
        public byte[] OutfitStates = new byte[0];
        public int[] OutfitTops = new int[0];
        public string[] OutfitNames = new string[0];

        // Not cached:
        public int MissingMask;
        public bool MissingComputed;
        public Dictionary<string, ModMeta> NewMeta; // mod info found while reading; merged into the index
        public Dictionary<int, string> ModdedTops;  // outfit index -> zipmod name, while reading only

        // Folder relative to chara/female (or male), "" for the root. Not cached; set by CardIndex.
        public string Folder = "";      // display form, original case, "/" separators
        public string FolderKey = "";   // lowercase form for comparisons

        public static string NormalizePath(string path)
        {
            return DisplayPath(path).ToLowerInvariant();
        }

        // Full path with "/" separators, original case.
        public static string DisplayPath(string path)
        {
            if (path == null) return "";
            try { path = Path.GetFullPath(path); }
            catch { }
            return path.Replace('\\', '/');
        }
    }

    internal static class CardReader
    {
        private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private sealed class BlockInfo
        {
            public string Name;
            public long Pos;
            public long Size;
        }

        // Reads only the parts of the card we need: Parameter, Custom (body) and the list of KKEx keys.
        public static void Read(CardInfo info)
        {
            info.Ok = false;
            using (FileStream fs = new FileStream(info.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
            {
                byte[] sig = br.ReadBytes(8);
                for (int i = 0; i < 8; i++)
                    if (sig.Length < 8 || sig[i] != PngSig[i]) return;

                // Walk PNG chunks without reading image data.
                while (true)
                {
                    byte[] lenBytes = br.ReadBytes(4);
                    if (lenBytes.Length < 4) return;
                    long len = ((long)lenBytes[0] << 24) | ((long)lenBytes[1] << 16) | ((long)lenBytes[2] << 8) | lenBytes[3];
                    byte[] type = br.ReadBytes(4);
                    fs.Seek(len + 4, SeekOrigin.Current); // data + crc
                    if (type.Length == 4 && type[0] == 'I' && type[1] == 'E' && type[2] == 'N' && type[3] == 'D') break;
                }

                int productNo = br.ReadInt32();
                string marker = br.ReadString();
                bool oddMarker;
                if (marker == null || !GameDefs.AcceptCard(productNo, marker, out oddMarker)) return;
                info.OddMarker = oddMarker;
                br.ReadString(); // version
                int faceLen = br.ReadInt32();
                fs.Seek(faceLen, SeekOrigin.Current);
                int hdrLen = br.ReadInt32();
                byte[] hdr = br.ReadBytes(hdrLen);
                br.ReadInt64(); // total data length
                long baseOffset = fs.Position;

                List<BlockInfo> blocks = ParseHeader(hdr);
                foreach (BlockInfo bi in blocks)
                {
                    if (bi.Name != "Parameter" && bi.Name != "Custom" && bi.Name != "KKEx" && bi.Name != "Coordinate") continue;
                    if (bi.Size <= 0 || bi.Size > 64 * 1024 * 1024) continue;
                    fs.Seek(baseOffset + bi.Pos, SeekOrigin.Begin);
                    byte[] data = br.ReadBytes((int)bi.Size);
                    if (data.Length < bi.Size) continue;
                    if (bi.Name == "Parameter") ReadParameter(info, data);
                    else if (bi.Name == "Custom") ReadCustom(info, data);
                    else if (bi.Name == "Coordinate")
                    {
                        try { ReadCoordinates(info, data); }
                        catch (Exception) { info.OutfitStates = new byte[0]; }
                    }
                    else ReadExtKeys(info, data);
                }
                // Zipmod tops: the card stores the mod's own item number, so use the mod name instead of an id lookup.
                if (info.ModdedTops != null)
                {
                    foreach (KeyValuePair<int, string> kv in info.ModdedTops)
                        if (kv.Key < info.OutfitTops.Length)
                            info.OutfitTops[kv.Key] = TowelNames.IsTowelName(kv.Value) ? -2 : -1;
                    info.ModdedTops = null;
                }
                info.Ok = true;
            }
        }

        private static List<BlockInfo> ParseHeader(byte[] hdr)
        {
            List<BlockInfo> result = new List<BlockInfo>();
            MsgReader r = new MsgReader(hdr, 0);
            int n = r.ReadMapHeader();
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                if (key != "lstInfo") { r.Skip(); continue; }
                int count = r.ReadArrayHeader();
                for (int j = 0; j < count; j++)
                {
                    BlockInfo bi = new BlockInfo();
                    int m = r.ReadMapHeader();
                    for (int k = 0; k < m; k++)
                    {
                        string f = r.ReadString();
                        if (f == "name") bi.Name = r.ReadString();
                        else if (f == "pos") bi.Pos = (long)r.ReadNumber();
                        else if (f == "size") bi.Size = (long)r.ReadNumber();
                        else r.Skip();
                    }
                    result.Add(bi);
                }
            }
            return result;
        }

        private static void ReadParameter(CardInfo info, byte[] data)
        {
            MsgReader r = new MsgReader(data, 0);
            int n = r.ReadMapHeader();
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                switch (key)
                {
                    case "firstname": info.FirstName = r.ReadString() ?? ""; break;
                    case "lastname": info.LastName = r.ReadString() ?? ""; break;
                    case "sex": info.Sex = (int)r.ReadNumber(); break;
                    case "personality": info.Personality = (int)r.ReadNumber(); break;
                    case "birthMonth": info.BirthMonth = (int)r.ReadNumber(); break;
                    case "birthDay": info.BirthDay = (int)r.ReadNumber(); break;
                    case "bloodType": info.BloodType = (int)r.ReadNumber(); break;
                    case "clubActivities": info.Club = (int)r.ReadNumber(); break;
                    case "voiceRate": info.VoiceRate = (float)r.ReadNumber(); break;
                    case "aggressive": info.Aggressive = (int)r.ReadNumber(); break;
                    case "diligence": info.Diligence = (int)r.ReadNumber(); break;
                    case "kindness": info.Kindness = (int)r.ReadNumber(); break;
                    case "awnser": info.Likes = ReadBoolMask(r, ProfileData.LikeKeys); break;
                    case "denial": info.Refusals = ReadBoolMask(r, ProfileData.RefusalKeys); break;
                    case "attribute": info.Traits = ReadBoolMask(r, ProfileData.TraitKeys); break;
                    default: r.Skip(); break;
                }
            }
        }

        // Map of booleans -> bit mask, using the order of the given key list.
        private static int ReadBoolMask(MsgReader r, string[] keys)
        {
            if (r.IsNil()) { r.Skip(); return 0; }
            int mask = 0;
            int n = r.ReadMapHeader();
            for (int i = 0; i < n; i++)
            {
                string k = r.ReadString();
                int bit = Array.IndexOf(keys, k);
                if (bit >= 0) { if (r.ReadNumber() != 0) mask |= 1 << bit; }
                else r.Skip();
            }
            return mask;
        }

        private static float[] ReadColor(MsgReader r)
        {
            float[] c = r.ReadFloatArray();
            return c.Length >= 3 ? new[] { c[0], c[1], c[2] } : null;
        }

        private static void ReadFace(CardInfo info, byte[] data, int start)
        {
            MsgReader r = new MsgReader(data, start);
            int n = r.ReadMapHeader();
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                switch (key)
                {
                    case "shapeValueFace": info.Face = r.ReadFloatArray(); break;
                    case "pupilWidth": info.IrisWidth = (float)r.ReadNumber(); break;
                    case "pupilHeight": info.IrisHeight = (float)r.ReadNumber(); break;
                    case "doubleTooth": info.Fangs = r.ReadNumber() != 0; break;
                    case "pupil":
                    {
                        int count = r.ReadArrayHeader();
                        for (int p = 0; p < count; p++)
                        {
                            if (p > 0) { r.Skip(); continue; }
                            int m = r.ReadMapHeader();
                            for (int f = 0; f < m; f++)
                            {
                                string fk = r.ReadString();
                                if (fk == "baseColor") info.EyeColor = ReadColor(r);
                                else r.Skip();
                            }
                        }
                        break;
                    }
                    default: r.Skip(); break;
                }
            }
        }

        private static void ReadHair(CardInfo info, byte[] data, int start)
        {
            MsgReader r = new MsgReader(data, start);
            int n = r.ReadMapHeader();
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                if (key != "parts") { r.Skip(); continue; }
                int count = r.ReadArrayHeader();
                for (int p = 0; p < count; p++)
                {
                    if (p > 0) { r.Skip(); continue; } // parts[0] = back hair
                    int m = r.ReadMapHeader();
                    for (int f = 0; f < m; f++)
                    {
                        string fk = r.ReadString();
                        if (fk == "baseColor") info.HairColor = ReadColor(r);
                        else r.Skip();
                    }
                }
            }
        }

        // Custom block = [int32 len][face msgpack][int32 len][body msgpack][int32 len][hair msgpack]
        private static void ReadCustom(CardInfo info, byte[] data)
        {
            int faceLen = BitConverter.ToInt32(data, 0);
            try { ReadFace(info, data, 4); }
            catch (Exception) { }
            int bodyStart = 4 + faceLen;
            if (bodyStart + 4 > data.Length) return;
            int bodyLen = BitConverter.ToInt32(data, bodyStart);
            if (bodyLen <= 0 || bodyStart + 4 + bodyLen > data.Length) return;
            int hairStart = bodyStart + 4 + bodyLen;
            if (hairStart + 4 <= data.Length)
            {
                try { ReadHair(info, data, hairStart + 4); }
                catch (Exception) { }
            }
            MsgReader r = new MsgReader(data, bodyStart + 4);
            int n = r.ReadMapHeader();
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                if (key == "shapeValueBody") info.Body = r.ReadFloatArray();
                else if (key == "areolaSize") info.AreolaSize = (float)r.ReadNumber();
                else if (key == "bustSoftness") info.BustSoftness = (float)r.ReadNumber();
                else if (key == "bustWeight") info.BustWeight = (float)r.ReadNumber();
                else if (key == "skinMainColor") info.SkinColor = ReadColor(r);
                else if (key == "underhairId") info.UnderhairId = (int)r.ReadNumber();
                else if (key == "sunburnId") info.SunburnId = (int)r.ReadNumber();
                else if (key == "nipId") info.NipId = (int)r.ReadNumber();
                else if (key == "paintId")
                {
                    int pc = r.ReadArrayHeader();
                    for (int p = 0; p < pc; p++) if (r.ReadNumber() != 0) info.HasPaint = true;
                }
                else r.Skip();
            }
        }

        public const byte OutfitNothing = 0, OutfitUnderwear = 1, OutfitDressed = 2, OutfitTopOnly = 3;

        // Coordinate block = array of outfits; each outfit = [int32 len][ChaFileClothes][int32 len][ChaFileAccessory]...
        // ChaFileClothes.parts: top, bot, bra, shorts, gloves, panst, socks, shoes_inner, shoes_outer (id 0 = nothing).
        private static void ReadCoordinates(CardInfo info, byte[] data)
        {
            MsgReader r = new MsgReader(data, 0);
            int count = r.ReadArrayHeader();
            byte[] states = new byte[count];
            int[] tops = new int[count];
            for (int i = 0; i < count; i++)
            {
                int len = r.ReadBinLength();
                int start = r.Pos;
                r.Pos += len;
                states[i] = OutfitState(data, start, out tops[i]);
            }
            info.OutfitStates = states;
            info.OutfitTops = tops;
        }

        private static byte OutfitState(byte[] data, int start, out int topId)
        {
            MsgReader c = new MsgReader(data, start + 4);
            int n = c.ReadMapHeader();
            int[] ids = new int[9];
            for (int i = 0; i < n; i++)
            {
                string key = c.ReadString();
                if (key != "parts") { c.Skip(); continue; }
                int parts = c.ReadArrayHeader();
                for (int p = 0; p < parts; p++)
                {
                    int fields = c.ReadMapHeader();
                    for (int f = 0; f < fields; f++)
                    {
                        string fk = c.ReadString();
                        if (fk == "id" && p < ids.Length) ids[p] = (int)c.ReadNumber();
                        else c.Skip();
                    }
                }
            }
            topId = ids[0];
            if (ids[1] != 0) return OutfitDressed;                              // has a bottom
            if (ids[0] != 0) return OutfitTopOnly;                              // top only (could be a towel)
            if (ids[2] != 0 || ids[3] != 0 || ids[5] != 0) return OutfitUnderwear; // bra, underwear, pantyhose
            return OutfitNothing;
        }

        private const string AbmxKey = "KKABMPlugin.ABMData";
        private const string UncensorKey = "com.deathweasel.bepinex.uncensorselector";

        // ABMX bone data: [version, { boneData: LZ4(msgpack [ [boneName, [[scale xyz, length, pos xyz, rot xyz], ...per outfit], mode], ... ]) }]
        private static void ReadAbmx(CardInfo info, MsgReader r)
        {
            if (!SeekPluginValue(r, "boneData")) return;
            if (!r.IsBin()) return;
            int len = r.ReadBinLength();
            byte[] raw = Lz4.TryUnwrap(r.Buffer, r.Pos);
            MsgReader m = raw != null ? new MsgReader(raw, 0) : new MsgReader(r.Buffer, r.Pos);
            int count = m.ReadArrayHeader();
            float hand = 0, foot = 0;
            int handN = 0, footN = 0;
            for (int i = 0; i < count; i++)
            {
                int fields = m.ReadArrayHeader();
                if (fields < 2) { for (int f = 0; f < fields; f++) m.Skip(); continue; }
                string bone = m.ReadString();
                info.HasAbmx = true;
                bool wanted = bone == "cf_j_ana" || bone == "cf_j_hand_L" || bone == "cf_j_hand_R" || bone == "cf_j_foot_L" || bone == "cf_j_foot_R";
                if (!wanted) { for (int f = 1; f < fields; f++) m.Skip(); continue; }

                float avg = 1f;
                int coords = m.ReadArrayHeader();
                if (coords > 0)
                {
                    int parts = m.ReadArrayHeader();
                    float[] scale = parts > 0 ? m.ReadFloatArray() : new float[0];
                    for (int p = 1; p < parts; p++) m.Skip();
                    for (int c = 1; c < coords; c++) m.Skip();
                    if (scale.Length == 3) avg = (scale[0] + scale[1] + scale[2]) / 3f;
                }
                for (int f = 2; f < fields; f++) m.Skip();

                if (bone == "cf_j_ana") info.AnusScale = avg;
                else if (bone.StartsWith("cf_j_hand")) { hand += avg; handN++; }
                else { foot += avg; footN++; }
            }
            if (handN > 0) info.HandScale = hand / handN;
            if (footN > 0) info.FootScale = foot / footN;
        }

        private static void ReadUncensor(CardInfo info, MsgReader r)
        {
            int n = r.ReadArrayHeader();
            if (n < 2) return;
            r.Skip(); // version
            int m = r.ReadMapHeader();
            for (int i = 0; i < m; i++)
            {
                string key = r.ReadString();
                if (key == "BodyGUID")
                {
                    string guid = r.ReadString();
                    info.UncensorBody = string.IsNullOrEmpty(guid) ? "" : string.Intern(guid);
                }
                else if (key == "DisplayPenis") info.Futa = r.ReadNumber() != 0;
                else r.Skip();
            }
        }

        private const string MoreOutfitsKey = "com.deathweasel.bepinex.moreoutfits";

        private static void ReadOutfitNames(CardInfo info, MsgReader r)
        {
            if (!SeekPluginValue(r, "CoordinateNames")) return;
            MsgReader m = r;
            if (r.IsBin())
            {
                r.ReadBinLength();
                m = new MsgReader(r.Buffer, r.Pos);
            }
            int n = m.ReadMapHeader();
            Dictionary<int, string> names = new Dictionary<int, string>();
            int max = -1;
            for (int i = 0; i < n; i++)
            {
                int slot = (int)m.ReadNumberOrNumericString();
                string name = m.ReadString();
                if (slot >= 0 && slot < 64 && !string.IsNullOrEmpty(name))
                {
                    names[slot] = name.Trim();
                    if (slot > max) max = slot;
                }
            }
            string[] arr = new string[max + 1];
            foreach (KeyValuePair<int, string> kv in names) arr[kv.Key] = string.Intern(kv.Value);
            info.OutfitNames = arr;
        }

        private const string UarKey = "com.bepis.sideloader.universalautoresolver";
        private const string AuthorKey = "marco.authordata";

        private static void ReadExtKeys(CardInfo info, byte[] data)
        {
            MsgReader r = new MsgReader(data, 0);
            int n = r.ReadMapHeader();
            List<string> keys = new List<string>(n);
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                bool hasData = !r.IsNil();
                int start = r.Pos;
                if (hasData && key == UarKey)
                {
                    try { ReadUar(info, new MsgReader(data, start)); }
                    catch (Exception) { }
                }
                else if (hasData && key == AuthorKey)
                {
                    try { ReadAuthors(info, new MsgReader(data, start)); }
                    catch (Exception) { }
                }
                else if (hasData && key == AbmxKey)
                {
                    try { ReadAbmx(info, new MsgReader(data, start)); }
                    catch (Exception) { }
                }
                else if (hasData && key == UncensorKey)
                {
                    try { ReadUncensor(info, new MsgReader(data, start)); }
                    catch (Exception) { }
                }
                else if (hasData && key == MoreOutfitsKey)
                {
                    try { ReadOutfitNames(info, new MsgReader(data, start)); }
                    catch (Exception) { }
                }
                r.Skip();
                if (hasData && ModNames.IsSaneKey(key)) keys.Add(key);
            }
            info.ExtKeys = keys.ToArray();
        }

        // Plugin data is saved as [version, { name: value, ... }]. Moves the reader to the value of "name".
        private static bool SeekPluginValue(MsgReader r, string name)
        {
            int n = r.ReadArrayHeader();
            if (n < 2) return false;
            r.Skip(); // version
            int m = r.ReadMapHeader();
            for (int i = 0; i < m; i++)
            {
                string key = r.ReadString();
                if (key == name) return true;
                r.Skip();
            }
            return false;
        }

        private static void ReadUar(CardInfo info, MsgReader r)
        {
            if (!SeekPluginValue(r, "info")) return;
            int count = r.ReadArrayHeader();
            List<string> ids = new List<string>();
            List<int> cats = new List<int>();
            List<byte> regions = new List<byte>();
            HashSet<string> seen = new HashSet<string>();
            Dictionary<string, ModMeta> meta = new Dictionary<string, ModMeta>();
            int mask = 0;
            for (int i = 0; i < count; i++)
            {
                // Each entry is a separately serialized ResolveInfo (bin), or a plain map in some versions.
                MsgReader e;
                if (r.IsBin())
                {
                    int len = r.ReadBinLength();
                    e = new MsgReader(r.Buffer, r.Pos);
                    r.Pos += len;
                }
                else
                {
                    e = new MsgReader(r.Buffer, r.Pos);
                    r.Skip();
                }

                string modId = null, name = null, author = null, website = null, property = null;
                int cat = -1;
                int fields = e.ReadMapHeader();
                for (int f = 0; f < fields; f++)
                {
                    string k = e.ReadString();
                    switch (k)
                    {
                        case "ModID": modId = e.ReadString(); break;
                        case "CategoryNo": cat = (int)e.ReadNumber(); break;
                        case "Name": name = e.ReadString(); break;
                        case "Author": author = e.ReadString(); break;
                        case "Website": website = e.ReadString(); break;
                        case "Property": property = e.ReadString(); break;
                        default: e.Skip(); break;
                    }
                }
                if (string.IsNullOrEmpty(modId)) continue;
                if (property == "ChaFileBody.nipId") info.NipModName = string.Intern(name ?? modId);
                // "outfit3.ChaFileClothes.ClothesTop" -> outfit 3's top is a zipmod item
                if (property != null && property.StartsWith("outfit") && property.EndsWith(".ChaFileClothes.ClothesTop"))
                {
                    int dot = property.IndexOf('.');
                    int slot;
                    if (dot > 6 && int.TryParse(property.Substring(6, dot - 6), out slot))
                    {
                        if (info.ModdedTops == null) info.ModdedTops = new Dictionary<int, string>();
                        info.ModdedTops[slot] = (name ?? "") + " " + modId;
                    }
                }
                if (!meta.ContainsKey(modId))
                {
                    ModMeta mm = new ModMeta();
                    mm.Name = name ?? "";
                    mm.Author = author ?? "";
                    mm.Website = website ?? "";
                    meta[modId] = mm;
                }
                if (!seen.Add(modId + "|" + cat)) continue;
                int region = Regions.Classify(cat, modId, name);
                ids.Add(string.Intern(modId));
                cats.Add(cat);
                regions.Add((byte)region);
                mask |= 1 << region;
            }
            info.ModIds = ids.ToArray();
            info.ModCats = cats.ToArray();
            info.ModRegions = regions.ToArray();
            info.RegionMask = mask;
            info.NewMeta = meta;
        }

        private static void ReadAuthors(CardInfo info, MsgReader r)
        {
            if (!SeekPluginValue(r, "Authors")) return;
            // Saved as a separately serialized string array (bin), or directly as an array.
            MsgReader a = r;
            if (r.IsBin())
            {
                r.ReadBinLength();
                a = new MsgReader(r.Buffer, r.Pos);
            }
            int n = a.ReadArrayHeader();
            List<string> authors = new List<string>(n);
            for (int i = 0; i < n; i++)
            {
                string s = a.ReadString();
                if (!string.IsNullOrEmpty(s) && s.Trim().Length > 0) authors.Add(string.Intern(s.Trim()));
            }
            info.Authors = authors.ToArray();
        }
    }
}
