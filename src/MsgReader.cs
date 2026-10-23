// Minimal MessagePack reader - just enough to pull a few values out of card blocks
// without running the game's full card loader.
using System;
using System.Text;

namespace StudioCharaFilter
{
    internal sealed class MsgReader
    {
        private readonly byte[] _b;
        private int _p;

        public MsgReader(byte[] buffer, int pos)
        {
            _b = buffer;
            _p = pos;
        }

        public int Pos
        {
            get { return _p; }
            set { _p = value; }
        }

        private ulong ReadBE(int n)
        {
            ulong v = 0;
            for (int i = 0; i < n; i++) v = (v << 8) | _b[_p++];
            return v;
        }

        public byte[] Buffer
        {
            get { return _b; }
        }

        public bool IsNil()
        {
            return _b[_p] == 0xc0;
        }

        public bool IsBin()
        {
            byte t = _b[_p];
            return t == 0xc4 || t == 0xc5 || t == 0xc6;
        }

        // Reads a bin header and leaves the reader at the start of the bytes.
        public int ReadBinLength()
        {
            byte t = _b[_p++];
            if (t == 0xc4) return (int)ReadBE(1);
            if (t == 0xc5) return (int)ReadBE(2);
            if (t == 0xc6) return (int)ReadBE(4);
            throw new FormatException("Expected bin, got 0x" + t.ToString("x2"));
        }

        public int ReadMapHeader()
        {
            byte t = _b[_p++];
            if (t >= 0x80 && t <= 0x8f) return t & 0x0f;
            if (t == 0xde) return (int)ReadBE(2);
            if (t == 0xdf) return (int)ReadBE(4);
            if (t == 0xc0) return 0;
            throw new FormatException("Expected map, got 0x" + t.ToString("x2"));
        }

        public int ReadArrayHeader()
        {
            byte t = _b[_p++];
            if (t >= 0x90 && t <= 0x9f) return t & 0x0f;
            if (t == 0xdc) return (int)ReadBE(2);
            if (t == 0xdd) return (int)ReadBE(4);
            if (t == 0xc0) return 0;
            throw new FormatException("Expected array, got 0x" + t.ToString("x2"));
        }

        public string ReadString()
        {
            byte t = _b[_p++];
            int n;
            if (t >= 0xa0 && t <= 0xbf) n = t & 0x1f;
            else if (t == 0xd9 || t == 0xc4) n = (int)ReadBE(1);
            else if (t == 0xda || t == 0xc5) n = (int)ReadBE(2);
            else if (t == 0xdb || t == 0xc6) n = (int)ReadBE(4);
            else if (t == 0xc0) return null;
            else throw new FormatException("Expected string, got 0x" + t.ToString("x2"));
            string s = Encoding.UTF8.GetString(_b, _p, n);
            _p += n;
            return s;
        }

        public double ReadNumber()
        {
            byte t = _b[_p++];
            if (t <= 0x7f) return t;
            if (t >= 0xe0) return (sbyte)t;
            switch (t)
            {
                case 0xcc: return ReadBE(1);
                case 0xcd: return ReadBE(2);
                case 0xce: return ReadBE(4);
                case 0xcf: return ReadBE(8);
                case 0xd0: return (sbyte)ReadBE(1);
                case 0xd1: return (short)ReadBE(2);
                case 0xd2: return (int)ReadBE(4);
                case 0xd3: return (long)ReadBE(8);
                case 0xca:
                {
                    byte[] f = new byte[4];
                    for (int i = 0; i < 4; i++) f[i] = _b[_p + 3 - i];
                    _p += 4;
                    return BitConverter.ToSingle(f, 0);
                }
                case 0xcb:
                {
                    byte[] f = new byte[8];
                    for (int i = 0; i < 8; i++) f[i] = _b[_p + 7 - i];
                    _p += 8;
                    return BitConverter.ToDouble(f, 0);
                }
                case 0xc2: return 0;
                case 0xc3: return 1;
                case 0xc0: return 0;
            }
            throw new FormatException("Expected number, got 0x" + t.ToString("x2"));
        }

        // Map keys can be numbers or (for some serializers) numeric strings.
        public double ReadNumberOrNumericString()
        {
            byte t = _b[_p];
            if ((t >= 0xa0 && t <= 0xbf) || t == 0xd9 || t == 0xda || t == 0xdb)
            {
                double v;
                return double.TryParse(ReadString(), out v) ? v : -1;
            }
            return ReadNumber();
        }

        public float[] ReadFloatArray()
        {
            int n = ReadArrayHeader();
            float[] r = new float[n];
            for (int i = 0; i < n; i++) r[i] = (float)ReadNumber();
            return r;
        }

        public void Skip()
        {
            byte t = _b[_p++];
            if (t <= 0x7f || t >= 0xe0) return;
            if (t >= 0x80 && t <= 0x8f) { SkipItems((t & 0x0f) * 2); return; }
            if (t >= 0x90 && t <= 0x9f) { SkipItems(t & 0x0f); return; }
            if (t >= 0xa0 && t <= 0xbf) { _p += t & 0x1f; return; }
            switch (t)
            {
                case 0xc0: case 0xc2: case 0xc3: return;
                case 0xcc: case 0xd0: _p += 1; return;
                case 0xcd: case 0xd1: _p += 2; return;
                case 0xce: case 0xd2: case 0xca: _p += 4; return;
                case 0xcf: case 0xd3: case 0xcb: _p += 8; return;
                // Read the length into a local first: "_p += ReadBE(n)" would use _p from before the length bytes.
                case 0xc4: case 0xd9: { int n = (int)ReadBE(1); _p += n; return; }
                case 0xc5: case 0xda: { int n = (int)ReadBE(2); _p += n; return; }
                case 0xc6: case 0xdb: { int n = (int)ReadBE(4); _p += n; return; }
                case 0xdc: SkipItems((int)ReadBE(2)); return;
                case 0xdd: SkipItems((int)ReadBE(4)); return;
                case 0xde: SkipItems((int)ReadBE(2) * 2); return;
                case 0xdf: SkipItems((int)ReadBE(4) * 2); return;
                case 0xd4: _p += 2; return;
                case 0xd5: _p += 3; return;
                case 0xd6: _p += 5; return;
                case 0xd7: _p += 9; return;
                case 0xd8: _p += 17; return;
                case 0xc7: { int n = (int)ReadBE(1); _p += n + 1; return; }
                case 0xc8: { int n = (int)ReadBE(2); _p += n + 1; return; }
                case 0xc9: { int n = (int)ReadBE(4); _p += n + 1; return; }
            }
            throw new FormatException("Unknown msgpack type 0x" + t.ToString("x2"));
        }

        private void SkipItems(int count)
        {
            for (int i = 0; i < count; i++) Skip();
        }
    }
}
