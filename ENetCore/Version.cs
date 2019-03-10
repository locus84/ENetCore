using System;
using System.Collections.Generic;
using System.Text;

namespace ENetCore
{
    public static class ENetVersion
    {
        public const byte Major = 2;
        public const byte Minor = 1;
        public const byte Patch = 2;

        public static int GetMajor(int version)
        {
            return (version >> 16) & 0xff;
        }

        public static int GetMiner(int version)
        {
            return (version >> 8) & 0xff;
        }

        public static int GetPatch(int version)
        {
            return version & 0xff;
        }

        public static int Create(byte major, byte minor, byte patch)
        {
            return major << 16 | minor << 8 | patch;
        }

        public static int Current
        {
            get
            {
                return Create(Major, Minor, Patch);
            }
        }
    }
}
