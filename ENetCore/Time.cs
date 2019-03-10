using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ENetCore
{
    public static class ENetTime
    {
        //in MS max 1day
        public const uint Overflow = 86400000;

        public static bool Less(uint a, uint b)
        {
            return a - b >= Overflow;
        }

        public static bool Greater(uint a, uint b)
        {
            return b - a >= Overflow;
        }

        public static bool LessNotEqual(uint a, uint b)
        {
            return !Greater(a, b);
        }

        public static bool GreaterNotEqual(uint a, uint b)
        {
            return !Less(a, b);
        }

        public static uint Difference(uint a, uint b)
        {
            return (a - b >= Overflow) ? (b - a) : (a - b);
        }



        static long start_time_ns = 0;
        static double frequencyToMicroseconds = 0;
        public static uint GetTime()
        {
            //TODO: handling long/integer overflow

            const long ns_in_ms = 1000 * 1000;

            long offset_ns = Interlocked.Read(ref start_time_ns);
            long current_time_ns;
            //not initialized
            if (offset_ns == 0)
            {
                frequencyToMicroseconds = System.Diagnostics.Stopwatch.Frequency / 1000000d;
                current_time_ns = (long)(System.Diagnostics.Stopwatch.GetTimestamp() / frequencyToMicroseconds) * 1000;
            }
            else
            {
                current_time_ns = (long)(System.Diagnostics.Stopwatch.GetTimestamp() / frequencyToMicroseconds) * 1000;
                long want_value = current_time_ns - 1 * ns_in_ms;
                long old_value = Interlocked.CompareExchange(ref start_time_ns, 0, want_value);
                offset_ns = old_value == 0 ? want_value : old_value;
            }

            long result_in_ns = current_time_ns - offset_ns;
            return (uint)(result_in_ns / ns_in_ms);
        }
    }
}
