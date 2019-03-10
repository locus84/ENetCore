using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace ENetCore
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ENetAddress
    {
        public In6_Addr Host;
        public ushort Port;
        public ushort Sin6_Scope_id;

        public ENetAddress(string toParse, ushort port)
        {
            var ip = IPAddress.Parse(toParse);
            Port = port;
            Sin6_Scope_id = (ushort)ip.ScopeId;

            unsafe
            {
                var bytes = ip.GetAddressBytes();
                var tr = new TypedReference();
                int length = Marshal.SizeOf<In6_Addr>();
                //Marshal.PtrToStructure<In6_Addr>((IntPtr)bytes)
            }
            Host = new In6_Addr();
        }


        public IPEndPoint ToIPEndPoint()
        {
            unsafe
            {
                int length = Marshal.SizeOf<In6_Addr>();
                byte* tmp = stackalloc byte[length];
                var span = new Span<byte>(tmp, length);
                MemoryMarshal.TryWrite(span, ref Host);
                var ip = new IPAddress(span, Sin6_Scope_id);
                return new IPEndPoint(ip, Port);
            }
        }

        public override string ToString()
        {
            return ToIPEndPoint().ToString();
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct In6_Addr : IEquatable<In6_Addr>
    {
        public char Char0;
        public char Char1;
        public char Char2;
        public char Char3;
        public char Char4;
        public char Char5;
        public char Char6;
        public char Char7;

        public bool Equals(In6_Addr other)
        {
            return Char0 == other.Char0 && Char1 == other.Char1 && Char2 == other.Char2 && Char3 == other.Char3 && 
                Char4 == other.Char4 && Char5 == other.Char5 && Char6 == other.Char6 && Char7 == other.Char7;
        }
    }
}
