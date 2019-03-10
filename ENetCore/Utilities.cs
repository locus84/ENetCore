using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ENetCore
{
    public class ENetListNode
    {
        public ENetListNode next;
        public ENetListNode previous;
    }

    public class ENetList 
    {
        public ENetListNode sentinel;
    }

    public class ENetUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HostToNet(int value)
        {
            return System.Net.IPAddress.HostToNetworkOrder(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short HostToNet(short value)
        {
            return System.Net.IPAddress.HostToNetworkOrder(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HostToNet(uint value)
        {
            return (uint)System.Net.IPAddress.HostToNetworkOrder((int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort HostToNet(ushort value)
        {
            return (ushort)System.Net.IPAddress.HostToNetworkOrder((short)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NetToHost(int value)
        {
            return System.Net.IPAddress.NetworkToHostOrder(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short NetToHost(short value)
        {
            return System.Net.IPAddress.NetworkToHostOrder(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint NetToHost(uint value)
        {
            return (uint)System.Net.IPAddress.NetworkToHostOrder((int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort NetToHost(ushort value)
        {
            return (ushort)System.Net.IPAddress.NetworkToHostOrder((short)value);
        }

        //this works even byte does not have enough size.
        //don't know if there'll be exception
        //best is just know command size
        public static unsafe T ReadStructure<T>(byte[] bytes, int offset = 0) where T : struct
        {
            fixed (byte* ptr = bytes)
            {
                return Marshal.PtrToStructure<T>((IntPtr)(ptr + offset));
            }
        }

        public static unsafe void WriteStructure<T>(T input, byte[] bytes, int offset = 0) where T : struct
        {
            TypedReference resultRef = __makeref(input);
            var ptr = *((IntPtr*)&resultRef);
            Marshal.Copy(ptr, bytes, offset, SizeOf<T>.Value);
        }
    }

    public static class SizeOf<T> where T : struct
    {
        static SizeOf()
        {
            T[] m_Array = new T[2];

            unsafe
            {
                TypedReference elem1 = __makeref(m_Array[0]), elem2 = __makeref(m_Array[1]);
                Value = (int)((byte*)*(IntPtr*)(&elem2) - (byte*)*(IntPtr*)(&elem1));
            }
        }
        public static readonly int Value;
    }
}
