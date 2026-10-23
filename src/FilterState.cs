// The user's current filter settings and the matching logic.
using System;
using System.Collections.Generic;

namespace StudioCharaFilter
{
    internal sealed class ValueRange
    {
        public float Min;
        public float Max;
        public string MinText;
        public string MaxText;

        public ValueRange(float min, float max)
        {
            Set(min, max);
        }

        public void Set(float min, float max)
        {
            Min = min;
            Max = max;
            MinText = min.ToString("0");
            MaxText = max.ToString("0");
        }
    }

    internal sealed class PropertyFilter
    {
        public const int Bins = 30;

        public readonly string Label;
        public readonly int BodyIndex; // index into ChaFileBody.shapeValueBody; -1 areola size, -2 bust softness, -3 bust weight
        public readonly bool DefaultShown;
        public readonly string Section;  // "Body", "Face" or "Profile"
        public bool HasToggle;         // set when F1 controls whether this slider is shown
        public bool ShownFlag = true;  // kept in sync with the F1 "Sliders shown" list

        public bool Visible
        {
            get { return !HasToggle || ShownFlag; }
        }
        public readonly List<ValueRange> Ranges = new List<ValueRange>();

        // Slider span, widened beyond 0-100 when cards use Slider Unlocker values.
        public float SpanMin = 0;
        public float SpanMax = 100;
        public int[] Histogram = new int[Bins];
        public int HistogramMax;

        public PropertyFilter(string label, int bodyIndex, bool defaultShown) : this(label, bodyIndex, defaultShown, "Body")
        {
        }

        // bodyIndex: 0..43 body sliders, 1000+ face sliders, negative = special values (see GetValue).
        public PropertyFilter(string label, int bodyIndex, bool defaultShown, string section)
        {
            Label = label;
            BodyIndex = bodyIndex;
            DefaultShown = defaultShown;
            Section = section;
            Reset();
        }

        public void Reset()
        {
            Ranges.Clear();
            Ranges.Add(new ValueRange(SpanMin, SpanMax));
        }

        // A range covering the whole slider means "no limit" and doesn't filter anything.
        public bool IsRangeActive(ValueRange r)
        {
            return r.Min > SpanMin + 0.01f || r.Max < SpanMax - 0.01f;
        }

        public bool Active
        {
            get
            {
                foreach (ValueRange r in Ranges) if (IsRangeActive(r)) return true;
                return false;
            }
        }

        public float? GetValue(CardInfo ci)
        {
            if (BodyIndex == -1) return ci.AreolaSize * 100f;
            if (BodyIndex == -2) return ci.BustSoftness * 100f;
            if (BodyIndex == -3) return ci.BustWeight * 100f;
            if (BodyIndex == -4) return ci.AnusScale * 100f;
            if (BodyIndex == -5) return ci.IrisWidth * 100f;
            if (BodyIndex == -6) return ci.IrisHeight * 100f;
            if (BodyIndex == -7) return ci.VoiceRate * 100f;
            if (BodyIndex == -8) return ci.HandScale * 100f;
            if (BodyIndex == -9) return ci.FootScale * 100f;
            if (BodyIndex == -10) return ci.Aggressive;
            if (BodyIndex == -11) return ci.Diligence;
            if (BodyIndex == -12) return ci.Kindness;
            if (BodyIndex >= 1000)
            {
                int fi = BodyIndex - 1000;
                if (ci.Face == null || fi >= ci.Face.Length) return null;
                return ci.Face[fi] * 100f;
            }
            if (ci.Body == null || BodyIndex >= ci.Body.Length) return null;
            return ci.Body[BodyIndex] * 100f;
        }

        public bool Matches(CardInfo ci)
        {
            if (!Visible || !Active) return true;
            float? v = GetValue(ci);
            if (!v.HasValue) return false;
            foreach (ValueRange r in Ranges)
            {
                if (!IsRangeActive(r)) continue;
                // A handle at the end of the slider means "and beyond", so outliers aren't lost.
                float lo = r.Min <= SpanMin + 0.01f ? float.MinValue : r.Min - 0.5f;
                float hi = r.Max >= SpanMax - 0.01f ? float.MaxValue : r.Max + 0.5f;
                if (v.Value >= lo && v.Value < hi) return true;
            }
            return false;
        }

        public string Summary()
        {
            List<string> parts = new List<string>();
            foreach (ValueRange r in Ranges)
            {
                if (!IsRangeActive(r)) continue;
                bool openLo = r.Min <= SpanMin + 0.01f;
                bool openHi = r.Max >= SpanMax - 0.01f;
                if (Math.Abs(r.Min - r.Max) < 0.01f) parts.Add(r.Min.ToString("0"));
                else if (openLo) parts.Add("up to " + r.Max.ToString("0"));
                else if (openHi) parts.Add(r.Min.ToString("0") + "+");
                else parts.Add(r.Min.ToString("0") + " to " + r.Max.ToString("0"));
            }
            return parts.Count == 0 ? "any" : string.Join(", ", parts.ToArray());
        }

        // Slider span comes from the whole collection (so handles don't jump between folders);
        // the histogram shows the cards in the current scope.
        public void UpdateStats(List<CardInfo> cards, float trimPercent)
        {
            UpdateSpan(cards, trimPercent);
            UpdateHistogram(cards);
        }

        public void UpdateSpan(List<CardInfo> cards, float trimPercent)
        {
            List<float> values = new List<float>(cards.Count);
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                float? v = GetValue(ci);
                if (v.HasValue && !float.IsNaN(v.Value) && !float.IsInfinity(v.Value)) values.Add(v.Value);
            }

            float oldMin = SpanMin, oldMax = SpanMax;
            float newMin = 0, newMax = 100;
            if (values.Count > 0)
            {
                values.Sort();
                // Ignore the most extreme few % on each side (configurable) so one odd card doesn't squash the scale.
                float trim = Math.Max(0f, Math.Min(5f, trimPercent)) / 100f;
                float pLo = values[Math.Min(values.Count - 1, (int)(values.Count * trim))];
                float pHi = values[Math.Min(values.Count - 1, (int)(values.Count * (1f - trim)))];
                newMin = Math.Max(-200f, (float)Math.Floor(Math.Min(0f, pLo) / 10f) * 10f);
                newMax = Math.Min(400f, (float)Math.Ceiling(Math.Max(100f, pHi) / 10f) * 10f);
            }
            SpanMin = newMin;
            SpanMax = newMax;

            // Keep "no limit" handles glued to the ends when the span changes.
            foreach (ValueRange r in Ranges)
            {
                float min = r.Min <= oldMin + 0.01f ? newMin : r.Min;
                float max = r.Max >= oldMax - 0.01f ? newMax : r.Max;
                if (min != r.Min || max != r.Max) r.Set(min, max);
            }

        }

        public void UpdateHistogram(List<CardInfo> cards)
        {
            int[] hist = new int[Bins];
            float width = (SpanMax - SpanMin) / Bins;
            foreach (CardInfo ci in cards)
            {
                if (!ci.Ok) continue;
                float? fv = GetValue(ci);
                if (!fv.HasValue || float.IsNaN(fv.Value) || float.IsInfinity(fv.Value)) continue;
                float v = fv.Value;
                int b = (int)((v - SpanMin) / width);
                if (b < 0) b = 0;
                if (b >= Bins) b = Bins - 1;
                hist[b]++;
            }
            int max2 = 0;
            foreach (int h in hist) if (h > max2) max2 = h;
            Histogram = hist;
            HistogramMax = max2;
        }
    }

    internal enum ModFilterMode
    {
        Ignore = 0,
        Require = 1,
        Exclude = 2
    }

    internal sealed class FilterState
    {
        // Every body slider from the Maker's body tab, in Maker order. Slot numbers come from
        // ChaFileDefine.cf_bodyshapename. true = shown by default (can be changed in F1).
        public readonly List<PropertyFilter> Properties = new List<PropertyFilter>
        {
            new PropertyFilter("Height", 0, true),
            new PropertyFilter("Head size", 1, false),
            new PropertyFilter("Neck width", 2, false),
            new PropertyFilter("Neck thickness", 3, false),
            new PropertyFilter("Bust size", 4, true),
            new PropertyFilter("Bust height", 5, false),
            new PropertyFilter("Bust spacing", 6, false),
            new PropertyFilter("Bust horizontal position", 7, false),
            new PropertyFilter("Bust vertical angle", 8, false),
            new PropertyFilter("Bust pointiness", 9, false),
            new PropertyFilter("Bust shape", 10, false),
            new PropertyFilter("Bust softness", -2, false),
            new PropertyFilter("Bust weight", -3, false),
            new PropertyFilter("Areola size", -1, true),
            new PropertyFilter("Areola puffiness", 11, true),
            new PropertyFilter("Nipple thickness", 12, true),
            new PropertyFilter("Nipple stand", 13, true),
            new PropertyFilter("Anus size (ABMX)", -4, true),
            new PropertyFilter("Hand size (ABMX)", -8, true),
            new PropertyFilter("Foot size (ABMX)", -9, true),
            new PropertyFilter("Torso shoulder width", 14, false),
            new PropertyFilter("Torso shoulder thickness", 15, false),
            new PropertyFilter("Upper torso width", 16, false),
            new PropertyFilter("Upper torso thickness", 17, false),
            new PropertyFilter("Lower torso width", 18, false),
            new PropertyFilter("Lower torso thickness", 19, false),
            new PropertyFilter("Waist position", 20, false),
            new PropertyFilter("Belly", 21, false),
            new PropertyFilter("Upper hip width", 22, true),
            new PropertyFilter("Upper hip depth", 23, true),
            new PropertyFilter("Lower hip width", 24, true),
            new PropertyFilter("Lower hip depth", 25, true),
            new PropertyFilter("Butt", 26, true),
            new PropertyFilter("Butt angle", 27, false),
            new PropertyFilter("Upper thigh width", 28, false),
            new PropertyFilter("Upper thigh thickness", 29, false),
            new PropertyFilter("Lower thigh width", 30, false),
            new PropertyFilter("Lower thigh thickness", 31, false),
            new PropertyFilter("Knee width", 32, false),
            new PropertyFilter("Knee thickness", 33, false),
            new PropertyFilter("Calves", 34, false),
            new PropertyFilter("Ankle width", 35, false),
            new PropertyFilter("Ankle thickness", 36, false),
            new PropertyFilter("Shoulder width", 37, false),
            new PropertyFilter("Shoulder thickness", 38, false),
            new PropertyFilter("Upper arm width", 39, false),
            new PropertyFilter("Upper arm thickness", 40, false),
            new PropertyFilter("Elbow width", 41, false),
            new PropertyFilter("Elbow thickness", 42, false),
            new PropertyFilter("Forearm", 43, false),
            new PropertyFilter("Face width", 1000, true, "Face"),
            new PropertyFilter("Upper face depth", 1001, false, "Face"),
            new PropertyFilter("Upper face height", 1002, false, "Face"),
            new PropertyFilter("Upper face size", 1003, false, "Face"),
            new PropertyFilter("Lower face depth", 1004, false, "Face"),
            new PropertyFilter("Lower face width", 1005, false, "Face"),
            new PropertyFilter("Lower jaw height", 1006, false, "Face"),
            new PropertyFilter("Lower jaw depth", 1007, false, "Face"),
            new PropertyFilter("Jaw height", 1008, false, "Face"),
            new PropertyFilter("Jaw width", 1009, false, "Face"),
            new PropertyFilter("Jaw depth", 1010, false, "Face"),
            new PropertyFilter("Chin height", 1011, false, "Face"),
            new PropertyFilter("Chin depth", 1012, false, "Face"),
            new PropertyFilter("Chin width", 1013, false, "Face"),
            new PropertyFilter("Cheekbone width", 1014, false, "Face"),
            new PropertyFilter("Cheekbone depth", 1015, false, "Face"),
            new PropertyFilter("Cheek width", 1016, false, "Face"),
            new PropertyFilter("Cheek depth", 1017, false, "Face"),
            new PropertyFilter("Cheek height", 1018, false, "Face"),
            new PropertyFilter("Eyebrow height", 1019, false, "Face"),
            new PropertyFilter("Eyebrow position", 1020, false, "Face"),
            new PropertyFilter("Eyebrow angle", 1021, false, "Face"),
            new PropertyFilter("Eyebrow inner shape", 1022, false, "Face"),
            new PropertyFilter("Eyebrow outer shape", 1023, false, "Face"),
            new PropertyFilter("Upper eyelid shape 1", 1024, false, "Face"),
            new PropertyFilter("Upper eyelid shape 2", 1025, false, "Face"),
            new PropertyFilter("Upper eyelid shape 3", 1026, false, "Face"),
            new PropertyFilter("Lower eyelid shape 1", 1027, false, "Face"),
            new PropertyFilter("Lower eyelid shape 2", 1028, false, "Face"),
            new PropertyFilter("Lower eyelid shape 3", 1029, false, "Face"),
            new PropertyFilter("Eye vertical position", 1030, false, "Face"),
            new PropertyFilter("Eye spacing", 1031, false, "Face"),
            new PropertyFilter("Eye depth", 1032, false, "Face"),
            new PropertyFilter("Eye angle", 1033, false, "Face"),
            new PropertyFilter("Eye height", 1034, true, "Face"),
            new PropertyFilter("Eye width", 1035, true, "Face"),
            new PropertyFilter("Iris width", -5, true, "Face"),
            new PropertyFilter("Iris height", -6, true, "Face"),
            new PropertyFilter("Inner eye corner", 1036, false, "Face"),
            new PropertyFilter("Outer eye corner height", 1037, false, "Face"),
            new PropertyFilter("Nose tip height", 1038, false, "Face"),
            new PropertyFilter("Nose vertical position", 1039, false, "Face"),
            new PropertyFilter("Nose bridge height", 1040, true, "Face"),
            new PropertyFilter("Mouth vertical position", 1041, false, "Face"),
            new PropertyFilter("Mouth width", 1042, true, "Face"),
            new PropertyFilter("Mouth depth", 1043, false, "Face"),
            new PropertyFilter("Upper lip shape", 1044, false, "Face"),
            new PropertyFilter("Lower lip shape", 1045, false, "Face"),
            new PropertyFilter("Mouth corners", 1046, false, "Face"),
            new PropertyFilter("Ear size", 1047, false, "Face"),
            new PropertyFilter("Ear angle (Y)", 1048, false, "Face"),
            new PropertyFilter("Ear angle (Z)", 1049, false, "Face"),
            new PropertyFilter("Upper ear shape", 1050, false, "Face"),
            new PropertyFilter("Lower ear shape", 1051, false, "Face"),
            new PropertyFilter("Voice pitch", -7, true, "Profile"),
            new PropertyFilter("Aggressive", -10, true, "Profile"),
            new PropertyFilter("Diligence", -11, true, "Profile"),
            new PropertyFilter("Kindness", -12, true, "Profile"),
        };

        // Sections that can be turned off in F1; their filters only apply while shown.
        public bool ProfileEnabled, ColorsEnabled, DetailsEnabled, FileEnabled;

        public bool PropertyApplies(PropertyFilter p)
        {
            return p.Visible && (p.Section != "Profile" || ProfileEnabled);
        }

        // Colors: bucket indexes (ColorBuckets); a card matches if its bucket is ticked.
        public readonly HashSet<int> HairColors = new HashSet<int>();
        public readonly HashSet<int> EyeColors = new HashSet<int>();
        public readonly HashSet<int> SkinTones = new HashSet<int>();

        // Profile
        public readonly HashSet<int> Personalities = new HashSet<int>();
        public readonly HashSet<int> BloodTypes = new HashSet<int>();
        public readonly HashSet<int> Clubs = new HashSet<int>();
        public readonly ModFilterMode[] TraitModes = new ModFilterMode[ProfileData.TraitKeys.Length];
        public readonly ModFilterMode[] LikeModes = new ModFilterMode[ProfileData.LikeKeys.Length];
        public readonly ModFilterMode[] RefusalModes = new ModFilterMode[ProfileData.RefusalKeys.Length];

        // Body details: Require = has it, Exclude = doesn't
        public const int DetailPubicHair = 0, DetailTanLines = 1, DetailBodyPaint = 2, DetailFangs = 3, DetailFuta = 4, DetailAbmx = 5;
        public static readonly string[] DetailNames = { "Pubic hair", "Tan lines", "Body paint / tattoos", "Fangs (yaeba)", "Futa (penis shown)", "Any ABMX bone edits" };
        public readonly ModFilterMode[] DetailModes = new ModFilterMode[6];
        public readonly HashSet<string> NipTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // File date: 0 any, else index into FileAgeDays (negative = older than)
        public int FileAgeMode;
        public static readonly int[] FileAgeDays = { 0, 1, 7, 30, 90, 365, -365 };
        public static readonly string[] FileAgeNames = { "any", "last day", "last 7 days", "last 30 days", "last 90 days", "last year", "older than a year" };

        public static bool DetailValue(CardInfo ci, int d)
        {
            switch (d)
            {
                case DetailPubicHair: return ci.UnderhairId != 0;
                case DetailTanLines: return ci.SunburnId != 0;
                case DetailBodyPaint: return ci.HasPaint;
                case DetailFangs: return ci.Fangs;
                case DetailFuta: return ci.Futa;
                default: return ci.HasAbmx;
            }
        }

        private static bool MaskMatches(ModFilterMode[] modes, int mask)
        {
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == ModFilterMode.Ignore) continue;
                bool has = (mask & (1 << i)) != 0;
                if (modes[i] == ModFilterMode.Require && !has) return false;
                if (modes[i] == ModFilterMode.Exclude && has) return false;
            }
            return true;
        }

        private static bool AnySet(ModFilterMode[] modes)
        {
            foreach (ModFilterMode m in modes) if (m != ModFilterMode.Ignore) return true;
            return false;
        }

        private bool MatchesExtras(CardInfo ci)
        {
            if (ColorsEnabled)
            {
                if (HairColors.Count > 0 && !HairColors.Contains(ColorBuckets.Hue(ci.HairColor))) return false;
                if (EyeColors.Count > 0 && !EyeColors.Contains(ColorBuckets.Hue(ci.EyeColor))) return false;
                if (SkinTones.Count > 0 && !SkinTones.Contains(ColorBuckets.Skin(ci.SkinColor))) return false;
            }
            if (ProfileEnabled)
            {
                if (Personalities.Count > 0 && !Personalities.Contains(ci.Personality)) return false;
                if (BloodTypes.Count > 0 && !BloodTypes.Contains(ci.BloodType)) return false;
                if (Clubs.Count > 0 && !Clubs.Contains(ci.Club)) return false;
                if (!MaskMatches(TraitModes, ci.Traits)) return false;
                if (!MaskMatches(LikeModes, ci.Likes)) return false;
                if (!MaskMatches(RefusalModes, ci.Refusals)) return false;
            }
            if (DetailsEnabled)
            {
                for (int d = 0; d < DetailModes.Length; d++)
                {
                    if (DetailModes[d] == ModFilterMode.Ignore) continue;
                    bool has = DetailValue(ci, d);
                    if (DetailModes[d] == ModFilterMode.Require && !has) return false;
                    if (DetailModes[d] == ModFilterMode.Exclude && has) return false;
                }
                if (NipTypes.Count > 0 && !NipTypes.Contains(ProfileData.NipName(ci))) return false;
            }
            if (FileEnabled && FileAgeMode > 0)
            {
                int days = FileAgeDays[FileAgeMode];
                double age = (DateTime.UtcNow - new DateTime(ci.MTimeTicks, DateTimeKind.Utc)).TotalDays;
                if (days > 0 && age > days) return false;
                if (days < 0 && age <= -days) return false;
            }
            return true;
        }

        private bool ExtrasActive
        {
            get
            {
                if (ColorsEnabled && (HairColors.Count > 0 || EyeColors.Count > 0 || SkinTones.Count > 0)) return true;
                if (ProfileEnabled && (Personalities.Count > 0 || BloodTypes.Count > 0 || Clubs.Count > 0 ||
                    AnySet(TraitModes) || AnySet(LikeModes) || AnySet(RefusalModes))) return true;
                if (DetailsEnabled && (AnySet(DetailModes) || NipTypes.Count > 0)) return true;
                if (FileEnabled && FileAgeMode > 0) return true;
                return false;
            }
        }

        // Legacy: plugin data present / absent.
        public readonly Dictionary<string, ModFilterMode> Mods = new Dictionary<string, ModFilterMode>();

        // Zipmod content per region. Index 0 = any region, 1.. = Regions (offset by one).
        //   Uses:    Require = uses zipmods there,   Exclude = no zipmods there
        //   Missing: Require = has missing zipmods,  Exclude = nothing missing
        public readonly ModFilterMode[] RegionUses = new ModFilterMode[Regions.Count + 1];
        public readonly ModFilterMode[] RegionMissing = new ModFilterMode[Regions.Count + 1];

        // Card authors (from the author info saved by the Maker); a card matches if any of its authors is selected.
        public readonly HashSet<string> Authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool LegacyEnabled;

        // Genital type (Uncensor Selector body). "" = default uncensor (none chosen on the card).
        public bool UncensorEnabled;
        public readonly HashSet<string> UncensorBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Birthday (only used while the Birthday section is enabled in F1).
        public bool BirthdayEnabled;
        public readonly HashSet<int> BirthMonths = new HashSet<int>();  // 1-12; a card matches if its month is ticked
        public ModFilterMode BirthdaySet;                               // Require = set, Exclude = not set (Maker default)

        public static bool IsBirthdayUnset(CardInfo ci)
        {
            if (ci.BirthMonth < 1 || ci.BirthMonth > 12) return true;
            return Settings.MaySeventeenIsUnset.Value && ci.BirthMonth == 5 && ci.BirthDay == 17;
        }

        private bool MatchesBirthday(CardInfo ci)
        {
            if (!BirthdayEnabled) return true;
            bool unset = IsBirthdayUnset(ci);
            if (BirthdaySet == ModFilterMode.Require && unset) return false;
            if (BirthdaySet == ModFilterMode.Exclude && !unset) return false;
            if (BirthMonths.Count > 0 && (unset || !BirthMonths.Contains(ci.BirthMonth))) return false;
            return true;
        }

        // Name search: every word must appear in the character's name or the card's file name.
        private string[] _nameTerms = new string[0];

        public void SetNameQuery(string query)
        {
            List<string> terms = new List<string>();
            foreach (string t in (query ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                terms.Add(t);
            _nameTerms = terms.ToArray();
        }

        private bool MatchesName(CardInfo ci)
        {
            if (_nameTerms.Length == 0) return true;
            string file = System.IO.Path.GetFileNameWithoutExtension(ci.FilePath ?? "");
            foreach (string t in _nameTerms)
            {
                if ((ci.FirstName ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if ((ci.LastName ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (file.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return false;
            }
            return true;
        }

        // Outfits. Per slot: 0 any, 1 dressed, 2 towel, 3 underwear only, 4 nothing worn.
        public static readonly int MaxOutfitSlots = GameDefs.BaseOutfits + 8;
        public readonly int[] OutfitModes = new int[MaxOutfitSlots];
        public ModFilterMode ExtraOutfits;

        // "Has an outfit that is ..." across all outfit slots, indexed by TowelNames state
        // (Nothing, Underwear, Dressed, Towel). Require = at least one outfit, Exclude = no outfit.
        public readonly ModFilterMode[] AnyOutfit = new ModFilterMode[4];      // Require = has More Outfits slots, Exclude = only the standard ones
        public string OutfitNameQuery = "";     // matches More Outfits names

        public const int OutfitModeCount = 5;
        private static readonly int[] OutfitModeState = { -1, TowelNames.Dressed, TowelNames.Towel, TowelNames.Underwear, TowelNames.Nothing };

        private bool MatchesOutfits(CardInfo ci)
        {
            for (int i = 0; i < MaxOutfitSlots; i++)
            {
                int m = OutfitModes[i];
                if (m == 0) continue;
                if (i >= ci.OutfitStates.Length) return false;
                if (TowelNames.EffectiveState(ci, i) != OutfitModeState[m]) return false;
            }
            for (int st = 0; st < AnyOutfit.Length; st++)
            {
                if (AnyOutfit[st] == ModFilterMode.Ignore) continue;
                bool has = false;
                for (int i = 0; i < ci.OutfitStates.Length; i++)
                    if (TowelNames.EffectiveState(ci, i) == st) { has = true; break; }
                if (AnyOutfit[st] == ModFilterMode.Require && !has) return false;
                if (AnyOutfit[st] == ModFilterMode.Exclude && has) return false;
            }
            if (ExtraOutfits != ModFilterMode.Ignore)
            {
                bool extra = ci.OutfitStates.Length > GameDefs.BaseOutfits;
                if (ExtraOutfits == ModFilterMode.Require && !extra) return false;
                if (ExtraOutfits == ModFilterMode.Exclude && extra) return false;
            }
            if (!string.IsNullOrEmpty(OutfitNameQuery))
            {
                bool any = false;
                foreach (string n in ci.OutfitNames)
                    if (n != null && n.IndexOf(OutfitNameQuery, StringComparison.OrdinalIgnoreCase) >= 0) { any = true; break; }
                if (!any) return false;
            }
            return true;
        }

        public bool IsActive
        {
            get
            {
                foreach (PropertyFilter p in Properties) if (PropertyApplies(p) && p.Active) return true;
                if (ExtrasActive) return true;
                if (LegacyEnabled)
                    foreach (ModFilterMode m in Mods.Values) if (m != ModFilterMode.Ignore) return true;
                foreach (ModFilterMode m in RegionUses) if (m != ModFilterMode.Ignore) return true;
                foreach (ModFilterMode m in RegionMissing) if (m != ModFilterMode.Ignore) return true;
                if (_nameTerms.Length > 0) return true;
                if (BirthdayEnabled && (BirthMonths.Count > 0 || BirthdaySet != ModFilterMode.Ignore)) return true;
                if (UncensorEnabled && UncensorBodies.Count > 0) return true;
                foreach (int m in OutfitModes) if (m != 0) return true;
                foreach (ModFilterMode m in AnyOutfit) if (m != ModFilterMode.Ignore) return true;
                if (ExtraOutfits != ModFilterMode.Ignore || !string.IsNullOrEmpty(OutfitNameQuery)) return true;
                return Authors.Count > 0;
            }
        }

        public bool Matches(CardInfo ci)
        {
            if (ci == null || !ci.Ok) return false;
            foreach (PropertyFilter p in Properties)
                if (PropertyApplies(p) && !p.Matches(ci)) return false;
            if (!MatchesExtras(ci)) return false;
            if (!MatchesName(ci)) return false;
            if (!MatchesBirthday(ci)) return false;
            if (UncensorEnabled && UncensorBodies.Count > 0 && !UncensorBodies.Contains(ci.UncensorBody ?? "")) return false;
            if (!MatchesRegions(ci)) return false;
            if (!MatchesOutfits(ci)) return false;
            if (Authors.Count > 0)
            {
                bool any = false;
                foreach (string a in ci.Authors)
                    if (Authors.Contains(a)) { any = true; break; }
                if (!any) return false;
            }
            if (!LegacyEnabled) return true;
            foreach (KeyValuePair<string, ModFilterMode> kv in Mods)
            {
                if (kv.Value == ModFilterMode.Ignore) continue;
                bool has = Array.IndexOf(ci.ExtKeys, kv.Key) >= 0;
                if (kv.Value == ModFilterMode.Require && !has) return false;
                if (kv.Value == ModFilterMode.Exclude && has) return false;
            }
            return true;
        }

        private bool MatchesRegions(CardInfo ci)
        {
            int missing = -1;
            for (int i = 0; i <= Regions.Count; i++)
            {
                ModFilterMode u = RegionUses[i], m = RegionMissing[i];
                if (u == ModFilterMode.Ignore && m == ModFilterMode.Ignore) continue;
                int bit = i == 0 ? ~0 : 1 << (i - 1);
                if (u != ModFilterMode.Ignore)
                {
                    bool uses = (ci.RegionMask & bit) != 0;
                    if (u == ModFilterMode.Require && !uses) return false;
                    if (u == ModFilterMode.Exclude && uses) return false;
                }
                if (m != ModFilterMode.Ignore)
                {
                    if (missing < 0) missing = SideloaderBridge.MissingMask(ci);
                    bool has = (missing & bit) != 0;
                    if (m == ModFilterMode.Require && !has) return false;
                    if (m == ModFilterMode.Exclude && has) return false;
                }
            }
            return true;
        }

        public void ResetAll()
        {
            for (int i = 0; i < RegionUses.Length; i++)
            {
                RegionUses[i] = ModFilterMode.Ignore;
                RegionMissing[i] = ModFilterMode.Ignore;
            }
            Authors.Clear();
            _nameTerms = new string[0];
            BirthMonths.Clear();
            UncensorBodies.Clear();
            HairColors.Clear(); EyeColors.Clear(); SkinTones.Clear();
            Personalities.Clear(); BloodTypes.Clear(); Clubs.Clear(); NipTypes.Clear();
            Array.Clear(TraitModes, 0, TraitModes.Length);
            Array.Clear(LikeModes, 0, LikeModes.Length);
            Array.Clear(RefusalModes, 0, RefusalModes.Length);
            Array.Clear(DetailModes, 0, DetailModes.Length);
            FileAgeMode = 0;
            BirthdaySet = ModFilterMode.Ignore;
            Array.Clear(OutfitModes, 0, OutfitModes.Length);
            ExtraOutfits = ModFilterMode.Ignore;
            for (int i = 0; i < AnyOutfit.Length; i++) AnyOutfit[i] = ModFilterMode.Ignore;
            OutfitNameQuery = "";
            foreach (PropertyFilter p in Properties) p.Reset();
            Mods.Clear();
        }
    }
}
