// MessagePack-CSharp "LZ4" extension (ext type 99) as used by ABMX for its bone data.
using System;

namespace StudioCharaFilter
{
    internal static class Lz4
    {
        // If the reader is at an LZ4 ext (type 99), returns the decompressed MessagePack bytes; otherwise null.
        public static byte[] TryUnwrap(byte[] b, int pos)
        {
            byte t = b[pos];
            int len, typePos;
            if (t == 0xc7) { len = b[pos + 1]; typePos = pos + 2; }
            else if (t == 0xc8) { len = (b[pos + 1] << 8) | b[pos + 2]; typePos = pos + 3; }
            else if (t == 0xc9) { len = (b[pos + 1] << 24) | (b[pos + 2] << 16) | (b[pos + 3] << 8) | b[pos + 4]; typePos = pos + 5; }
            else return null;
            if ((sbyte)b[typePos] != 99) return null;
            int p = typePos + 1;
            int end = p + len;
            // Payload: msgpack int32 (uncompressed length) followed by an LZ4 block.
            if (b[p] != 0xd2) return null;
            int outLen = (b[p + 1] << 24) | (b[p + 2] << 16) | (b[p + 3] << 8) | b[p + 4];
            if (outLen <= 0 || outLen > 64 * 1024 * 1024) return null;
            return DecodeBlock(b, p + 5, end, outLen);
        }

        private static byte[] DecodeBlock(byte[] src, int i, int end, int outLen)
        {
            byte[] dst = new byte[outLen];
            int o = 0;
            while (i < end)
            {
                int token = src[i++];
                int lit = token >> 4;
                if (lit == 15)
                {
                    int b;
                    do { b = src[i++]; lit += b; } while (b == 255);
                }
                Buffer.BlockCopy(src, i, dst, o, lit);
                i += lit;
                o += lit;
                if (i >= end || o >= outLen) break;
                int offset = src[i] | (src[i + 1] << 8);
                i += 2;
                int match = token & 15;
                if (match == 15)
                {
                    int b;
                    do { b = src[i++]; match += b; } while (b == 255);
                }
                match += 4;
                int from = o - offset;
                if (from < 0) throw new FormatException("Bad LZ4 offset");
                for (int k = 0; k < match; k++) dst[o + k] = dst[from + k]; // may overlap, so copy byte by byte
                o += match;
            }
            return dst;
        }
    }
}
