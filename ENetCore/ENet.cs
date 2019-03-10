using System;
using System.Collections.Generic;
using System.Text;

namespace ENetCore
{
    //static functions
    public static class ENet
    {
        static ENetCallbacks s_Callbacks = ENetCallbacks.Default;

        public static bool Initialize(int version, ENetCallbacks callbacks)
        {
            if(version < ENetVersion.Create(1,3,0))
                return false;

            if(callbacks.Malloc != null || callbacks.Free != null)
            {
                if (callbacks.Malloc == null || callbacks.Free == null)
                    return false;

                s_Callbacks.Malloc = callbacks.Malloc;
            }

            if(callbacks.NoMemeory != null)
            {
                s_Callbacks.NoMemeory = callbacks.NoMemeory;
            }

            return true;
        }

        public static byte[] Malloc(int size)
        {
            return s_Callbacks.Malloc(size);
        }

        public static void Free(byte[] buffer)
        {
            s_Callbacks.Free(buffer);
        }

        static int[] commandSizes = new int[]{
                0,
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolAcknowledge>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolVerifyConnect>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolDisconnect>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolPing>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolSendReliable>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolSendUnreliable>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolSendFragment>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolSendUnsequenced>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolBandwidthLimit>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolThrottleConfigure>(),
                System.Runtime.InteropServices.Marshal.SizeOf<ENetProtocolSendFragment>()
            };

        public static int GetProtocolCommandSize(ENetProtocolCommand command)
        {
            return commandSizes[(int)command & (int)ENetProtocolCommand.MASK];
        }
    }


    public unsafe struct ENetBuffer
    {
        public byte[] data;
    }

    public class ENetChannel
    {
        public ushort outgoingReliableSequenceNumber;
        public ushort outgoingUnreliableSequenceNumber;
        public ushort usedReliableWindows;
        public ushort[] reliableWindows = new ushort[(int)ENetConstant.ENET_PEER_RELIABLE_WINDOWS];
        public ushort incomingReliableSequenceNumber;
        public ushort incomingUnreliableSequenceNumber;
        public ENetList<ENetIncomingCommand> incomingReliableCommands;
        public ENetList<ENetIncomingCommand> incomingUnreliableCommands;

        static Queue<ENetChannel> s_ChannelPool = new Queue<ENetChannel>();

        public static List<ENetChannel> CreateChannels(int count, List<ENetChannel> previous)
        {
            if(previous == null)
                previous = new List<ENetChannel>(count);

            for (int i = 0; i < count; i++)
            {
                previous.Add(s_ChannelPool.Count > 0? s_ChannelPool.Dequeue() : new ENetChannel());
            }

            return previous;
        }

        public static void FreeChannels(List<ENetChannel> toFree)
        {
            for (int i = 0; i < toFree.Count; i++)
                s_ChannelPool.Enqueue(toFree[i]);
            toFree.Clear();
        }

    }

    /**
     * An ENet peer which data packets may be sent or received from.
     *
     * No fields should be modified unless otherwise specified.
     */
    


    /** An ENet packet compressor for compressing UDP packets before socket sends or receives. */
    public unsafe struct ENetCompressor
    {
        /** Context data for the compressor. Must be non-NULL. */
        void* context;

        /** Compresses from inBuffers[0:inBufferCount-1], containing inLimit bytes, to outData, outputting at most outLimit bytes. Should return 0 on failure. */
        delegate int compress(void* context, ENetBuffer inBuffers, int inBufferCount, int inLimit, byte[] outData, int outLimit);

        /** Decompresses from inData, containing inLimit bytes, to outData, outputting at most outLimit bytes. Should return 0 on failure. */
        delegate int decompress(void* context, byte[] inData, int inLimit, byte[] outData, int outLimit);

        /** Destroys the context when compression is disabled or the host is destroyed. May be NULL. */
        delegate void destroy(void* context);
    }

    ///** Callback that computes the checksum of the data held in buffers[0:bufferCount-1] */
    public delegate uint ENetChecksumCallback(ENetBuffer buffers, int bufferCount);

    ///** Callback for intercepting received raw UDP packets. Should return 1 to intercept, 0 to ignore, or -1 to propagate an error. */
    public delegate int ENetInterceptCallback(ENetHost host, ENetEvent eNetEvent);


    /**
     * An ENet event type, as specified in @ref ENetEvent.
     */
    public enum ENetEventType
    {
        /** no event occurred within the specified time limit */
        ENET_EVENT_TYPE_NONE = 0,

        /** a connection request initiated by enet_host_connect has completed.
         * The peer field contains the peer which successfully connected.
         */
        ENET_EVENT_TYPE_CONNECT = 1,

        /** a peer has disconnected.  This event is generated on a successful
         * completion of a disconnect initiated by enet_peer_disconnect, if
         * a peer has timed out.  The peer field contains the peer
         * which disconnected. The data field contains user supplied data
         * describing the disconnection, or 0, if none is available.
         */
        ENET_EVENT_TYPE_DISCONNECT = 2,

        /** a packet has been received from a peer.  The peer field specifies the
         * peer which sent the packet.  The channelID field specifies the channel
         * number upon which the packet was received.  The packet field contains
         * the packet that was received; this packet must be destroyed with
         * enet_packet_destroy after use.
         */
        ENET_EVENT_TYPE_RECEIVE = 3,

        /** a peer is disconnected because the host didn't receive the acknowledgment
         * packet within certain maximum time out. The reason could be because of bad
         * network connection or  host crashed.
         */
        ENET_EVENT_TYPE_DISCONNECT_TIMEOUT = 4,
    }


    /**
     * An ENet event as returned by enet_host_service().
     *
     * @sa enet_host_service
     */
    public class ENetEvent
    {
        public ENetEventType type;      /**< type of the event */
        public ENetPeer peer;      /**< peer that generated a connect, disconnect or receive event */
        public int channelID; /**< channel on the peer that generated the event, if appropriate */
        public uint data;      /**< data associated with the event, if appropriate */
        public ENetPacket packet;    /**< packet associated with the event, if appropriate */
    }
}