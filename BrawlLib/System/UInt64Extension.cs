﻿namespace System
{
    public static class UInt64Extension
    {
        public static BrawlLib.SSBBTypes.Endian Endian = BrawlLib.SSBBTypes.Endian.Big;
        public static unsafe UInt64 Reverse(this UInt64 value)
        {
            if (Endian == BrawlLib.SSBBTypes.Endian.Little)
                return value;
            return
                ((value >> 56) & 0xFF) | ((value & 0xFF) << 56) |
                ((value >> 40) & 0xFF00) | ((value & 0xFF00) << 40) |
                ((value >> 24) & 0xFF0000) | ((value & 0xFF0000) << 24) |
                ((value >> 8) & 0xFF000000) | ((value & 0xFF000000) << 8);
        }

        public static UInt64 Align(this UInt64 value, uint align)
        {
            if (value < 0) return 0;
            if (align <= 1) return value;
            ulong temp = value % align;
            if (temp != 0) value += align - temp;
            return value;
        }
        public static UInt64 Clamp(this UInt64 value, ulong min, ulong max)
        {
            if (value <= min) return min;
            if (value >= max) return max;
            return value;
        }
    }
}
