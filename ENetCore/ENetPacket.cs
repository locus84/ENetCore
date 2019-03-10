using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ENetCore
{
    [Flags]
    public enum ENetPacketFlag
    {
        ENET_PACKET_FLAG_RELIABLE = (1 << 0), /** packet must be received by the target peer and resend attempts should be made until the packet is delivered */
        ENET_PACKET_FLAG_UNSEQUENCED = (1 << 1), /** packet will not be sequenced with other packets not supported for reliable packets */
        ENET_PACKET_FLAG_NO_ALLOCATE = (1 << 2), /** packet will not allocate data, and user must supply it instead */
        ENET_PACKET_FLAG_UNRELIABLE_FRAGMENT = (1 << 3), /** packet will be fragmented using unreliable (instead of reliable) sends if it exceeds the MTU */
        ENET_PACKET_FLAG_SENT = (1 << 8), /** whether the packet has been sent from all queues it has been entered into */
    }

    public delegate void ENetPacketFreeCallbackDelegate(ENetPacket packet);

    public class ENetPacket
    {
        private static Queue<ENetPacket> s_PacketPool = new Queue<ENetPacket>();
        private static ENetPacket Create()
        {
            return s_PacketPool.Count > 0 ? s_PacketPool.Dequeue() : new ENetPacket();
        }
        private static void Free(ENetPacket packet)
        {
            s_PacketPool.Enqueue(packet);
        }

        public int referenceCount; 
        public ENetPacketFlag flags;          // bitwise-or of ENetPacketFlag constants 
        public byte[] data;           // allocated data for packet 
        public int dataLength;     // length of data 
        public ENetPacketFreeCallbackDelegate freeCallback;   // function to be called when the packet is no longer in use 
        public object userData;       // application private data, may be freely modified 

        
        public static ENetPacket Create(byte[] data, int dataLength, ENetPacketFlag flags)
        {
            ENetPacket packet;
            if ((flags & ENetPacketFlag.ENET_PACKET_FLAG_NO_ALLOCATE) > 0)
            {
                packet = Create();
                packet.data = data;
            }
            else
            {
                packet = Create();
                packet.data = ENet.Malloc(dataLength);
                Buffer.BlockCopy(data, 0, packet.data, 0, dataLength);
            }

            packet.referenceCount = 0;
            packet.flags = flags;
            packet.dataLength = dataLength;
            packet.freeCallback = null;
            packet.userData = null;

            return packet;
        }


        public static ENetPacket Create_Offset(byte[] data, int dataLength, int dataOffset, ENetPacketFlag flags)
        {
            ENetPacket packet;
            if ((flags & ENetPacketFlag.ENET_PACKET_FLAG_NO_ALLOCATE) > 0)
            {
                packet = Create();
                packet.data = data;
            }
            else
            {
                packet = Create();
                packet.data = ENet.Malloc(dataLength + dataOffset);

                Buffer.BlockCopy(data, 0, packet.data, dataOffset, dataLength);
            }

            packet.referenceCount = 0;
            packet.flags = flags;
            packet.dataLength = dataLength + dataOffset;
            packet.freeCallback = null;
            packet.userData = null;

            return packet;
        }

        public static void Destroy(ENetPacket packet)
        {
            if (packet == null) return;

            packet.freeCallback?.Invoke(packet);

            ENet.Free(packet.data);
            Free(packet);
        }


        static bool initializedCRC32 = false;
        static uint[] crcTable = new uint[256];

        //this is reverse function
        static uint Reflect_CRC(int val, int bits)
        {
            int result = 0;
            
            for(var bit = 0; bit < bits; bit++)
            {
                //val is over 1
                if((val & 1) > 0)
                {
                    //last from current index
                    result |= 1 << (bits - 1 - bit);
                }
                //remove current
                val >>= 1;
            }

            return (uint)result;
        }

        static void Initialize_crc32()
        {
            for(int @byte = 0; @byte < 256; ++@byte)
            {
                uint crc = Reflect_CRC(@byte, 8) << 24;
                for(int offset = 0; offset < 8; ++offset)
                {
                    //exceed overflow?
                    if ((crc & 0x80000000) > 0)
                        //xor
                        crc = (crc << 1) ^ 0x04c11db7;
                    else
                        crc <<= 1;
                }
                crcTable[@byte] = Reflect_CRC((int)crc, 32);
            }

            initializedCRC32 = true;
        }


        public static uint enet_crc32(IEnumerable<ENetBuffer> buffers) {
            uint crc = 0xFFFFFFFF;

            if (!initializedCRC32)
            {
                Initialize_crc32();
            }

            foreach(var buffer in buffers)
            {
                enet_crc32(buffer, ref crc);
            }

            return (uint)System.Net.IPAddress.HostToNetworkOrder((int)~crc);
        }

        public static uint enet_crc32(ENetBuffer buffer)
        {
            uint crc = 0xFFFFFFFF;

            if (!initializedCRC32)
            {
                Initialize_crc32();
            }

            enet_crc32(buffer, ref crc);

            return (uint)System.Net.IPAddress.HostToNetworkOrder((int)~crc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void enet_crc32(ENetBuffer buffer,ref uint crc)
        {
            for (int i = 0; i < buffer.data.Length; ++i)
            {
                crc = (crc >> 8) ^ crcTable[(crc & 0xff) ^ buffer.data[i]];
            }
        }
    }
}
