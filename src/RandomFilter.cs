// Random filters: a random range on one slider (the dice next to its Reset), and "Random filters" - a random mix of a
// few slider ranges (and sometimes a hair or eye color) for a surprise selection.
//
// Ranges are picked from the cards themselves, never from empty parts of a slider: a random card's value is the middle
// and the range is widened until it holds a good share of the cards that still match. How far it narrows things down
// follows the size of the collection - the result aims for about 3 x sqrt(cards in scope) matches (100 cards -> ~30,
// 1,000 -> ~95, 5,000 -> ~210, at most 300) and never fewer than a minimum, so a small collection isn't filtered away.
using System;
using System.Collections.Generic;

namespace StudioCharaFilter
{
    internal static class RandomFilter
    {
        private static readonly Random Rng = new Random();

        // How many matches a random filter aims for, for a scope of n cards.
        public static int Target(int n)
        {
            int t = (int)Math.Round(3.0 * Math.Sqrt(Math.Max(n, 0)));
            return Math.Max(Math.Min(n, 8), Math.Min(t, 300));
        }

        // One slider: a random range (replacing its ranges, or added as another range when add = true), picked from the
        // cards that match every other filter. Returns false if there aren't enough cards with that value.
        public static bool RandomRange(FilterState f, PropertyFilter p, List<CardInfo> scope, bool add)
        {
            List<ValueRange> keep = new List<ValueRange>(p.Ranges);
            p.Reset();
            List<CardInfo> others = new List<CardInfo>();
            foreach (CardInfo ci in scope) if (f.Matches(ci)) others.Add(ci);
            p.Ranges.Clear();
            if (add) p.Ranges.AddRange(keep);
            int target = Target(scope.Count);
            float q = (float)(0.25 + Rng.NextDouble() * 0.35);
            ValueRange r = Pick(p, others, q, target);
            if (r == null)
            {
                p.Ranges.Clear();
                p.Ranges.AddRange(keep);
                return false;
            }
            if (!add) p.Ranges.Clear();
            else if (p.Ranges.Count == 1 && !p.IsRangeActive(p.Ranges[0])) p.Ranges.Clear();   // the "any" range would hide the new one
            p.Ranges.Add(r);
            return true;
        }

        // Everything at random: all filters cleared, then 2-5 random sliders narrowed in turn (each keeping 35-70% of
        // what's left) until about Target matches remain; half the time also one hair or eye color that the remaining
        // cards have. A step that would leave too few cards is skipped.
        public static int RandomAll(FilterState f, List<PropertyFilter> sliders, List<CardInfo> scope)
        {
            f.ResetAll();
            List<CardInfo> current = new List<CardInfo>();
            foreach (CardInfo ci in scope) if (ci != null && ci.Ok) current.Add(ci);
            int target = Target(current.Count);
            int floor = Math.Max(Math.Min(current.Count, 5), (int)(target * 0.6));
            List<PropertyFilter> order = new List<PropertyFilter>(sliders);
            for (int i = order.Count - 1; i > 0; i--) { int j = Rng.Next(i + 1); PropertyFilter t = order[i]; order[i] = order[j]; order[j] = t; }
            int steps = 2 + Rng.Next(4), done = 0;
            foreach (PropertyFilter p in order)
            {
                if (done >= steps || current.Count <= target * 1.3f) break;
                float q = (float)(0.35 + Rng.NextDouble() * 0.35);
                q = Math.Max(q, Math.Min(1f, target * 0.8f / Math.Max(1, current.Count)));
                ValueRange r = Pick(p, current, q, target);
                if (r == null) continue;
                p.Ranges.Clear();
                p.Ranges.Add(r);
                List<CardInfo> next = current.FindAll(p.Matches);
                if (next.Count < floor) { p.Reset(); continue; }
                current = next;
                done++;
            }
            if (f.ColorsEnabled && current.Count > target && Rng.Next(2) == 0)
            {
                bool hair = Rng.Next(2) == 0;
                CardInfo pick = current[Rng.Next(current.Count)];
                float[] col = hair ? pick.HairColor : pick.EyeColor;
                if (col != null)
                {
                    int bucket = ColorBuckets.Hue(col);
                    HashSet<int> set = hair ? f.HairColors : f.EyeColors;
                    set.Add(bucket);
                    List<CardInfo> next = current.FindAll(f.Matches);
                    if (next.Count < floor) set.Remove(bucket); else current = next;
                }
            }
            return current.Count;
        }

        // A range around a random card's value holding about q of `cards` (at least `target` of them when possible).
        private static ValueRange Pick(PropertyFilter p, List<CardInfo> cards, float q, int target)
        {
            List<float> values = new List<float>(cards.Count);
            foreach (CardInfo ci in cards)
            {
                float? v = p.GetValue(ci);
                if (v.HasValue && !float.IsNaN(v.Value) && !float.IsInfinity(v.Value)) values.Add(v.Value);
            }
            if (values.Count < 4) return null;
            values.Sort();
            if (values[values.Count - 1] - values[0] < 1f) return null;   // everyone has the same value: nothing to choose
            int n = values.Count;
            int w = Math.Max(2, Math.Min(n, Math.Max((int)Math.Round(n * q), Math.Min(n, target))));
            int center = Rng.Next(n);
            int start = Math.Max(0, Math.Min(n - w, center - w / 2));
            int end = start + w - 1;
            float lo = (float)Math.Floor(values[start]), hi = (float)Math.Ceiling(values[end]);
            if (start == 0) lo = p.SpanMin;           // reaching the end of the slider: "and beyond"
            if (end == n - 1) hi = p.SpanMax;
            if (lo <= p.SpanMin + 0.01f && hi >= p.SpanMax - 0.01f) return null;   // the whole slider: no filter at all
            return new ValueRange(lo, hi);
        }
    }
}
