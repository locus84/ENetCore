using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ENetCore
{
    public delegate byte[] MallocDelegate(int size);
    public delegate void FreeDelegate(byte[] buffer);
    public delegate void NoMemoryDelegate();

    public struct ENetCallbacks
    {
        public static ENetCallbacks Default = new ENetCallbacks
        {
            Malloc = size => new byte[size],
            Free = buffer => { return; },
            NoMemeory = () => throw new InsufficientMemoryException()
        };

        public MallocDelegate Malloc;
        public FreeDelegate Free;
        //In my opinion, we should drop corresponding connection if this occur.
        //it's better than shut down entire system
        public NoMemoryDelegate NoMemeory;
    }
}
