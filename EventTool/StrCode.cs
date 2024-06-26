using System;

namespace EventTool
{
    public class StrCode
    {
        public static ulong StrCode64(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            const ulong seed0 = 0x9ae16a3b2f90404f;
            ulong seed1 = text.Length > 0 ? (uint)((text[0]) << 16) + (uint)text.Length : 0;
            return CityHash.CityHash.CityHash64WithSeeds(text + "\0", seed0, seed1) & 0xFFFFFFFFFFFF;
        }
        public static uint StrCode32(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            return (uint)StrCode64(text);
        }
    }
}
