using System;
using System.Runtime.InteropServices;

namespace ENetCore
{
    public enum ENetProtocolConstant
    {
        MINIMUM_MTU = 576,
        MAXIMUM_MTU = 4096,
        MAXIMUM_PACKET_COMMANDS = 32,
        MINIMUM_WINDOW_SIZE = 4096,
        MAXIMUM_WINDOW_SIZE = 65536,
        MINIMUM_CHANNEL_COUNT = 1,
        MAXIMUM_CHANNEL_COUNT = 255,
        MAXIMUM_PEER_ID = 0xFFF,
        MAXIMUM_FRAGMENT_COUNT = 1024 * 1024
    }

    //encoded into command value only 4 bit required
    public enum ENetProtocolCommand
    {
        NONE = 0,
        ACKNOWLEDGE = 1,
        CONNECT = 2,
        VERIFY_CONNECT = 3,
        DISCONNECT = 4,
        PING = 5,
        SEND_RELIABLE = 6,
        SEND_UNRELIABLE = 7,
        SEND_FRAGMENT = 8,
        SEND_UNSEQUENCED = 9,
        BANDWIDTH_LIMIT = 10,
        THROTTLE_CONFIGURE = 11,
        SEND_UNRELIABLE_FRAGMENT = 12,
        COUNT = 13,

        MASK = 0x0F
    }

    [Flags]
    public enum ENetProtocolFlag : uint
    {
        //4 bits for protocol command value
        //rest is for command flag
        COMMAND_FLAG_ACKNOWLEDGE = (1 << 7),
        COMMAND_FLAG_UNSEQUENCED = (1 << 6),

        //encoded into peerid 1111 1111 1111[PeerID 2^12 = 4096 maximum]  11[SessionID] 11[header flag]
        HEADER_FLAG_COMPRESSED = (1 << 14),
        HEADER_FLAG_SENT_TIME = (1 << 15),
        HEADER_FLAG_MASK = HEADER_FLAG_COMPRESSED | HEADER_FLAG_SENT_TIME,

        //encoded into peerid only 2 bits 0~3
        HEADER_SESSION_MASK = (3 << 12),
        HEADER_SESSION_SHIFT = 12,

        PEER_ID_MASK = ~(HEADER_FLAG_MASK | HEADER_SESSION_MASK)
    }

    public class ENetAcknowledgement : ENetListNode<ENetAcknowledgement>
    {
        uint sentTime;
        ENetProtocol command;
    }

    public class ENetOutgoingCommand : ENetListNode<ENetOutgoingCommand>
    {
        public ushort reliableSequenceNumber;
        public ushort unreliableSequenceNumber;
        public uint sentTime;
        public uint roundTripTimeout;
        public uint roundTripTimeoutLimit;
        public uint fragmentOffset;
        public ushort fragmentLength;
        public ushort sendAttempts;
        public ENetProtocol command;
        public ENetPacket packet;
    }

    public class ENetIncomingCommand : ENetListNode<ENetIncomingCommand>
    {
        public ushort reliableSequenceNumber;
        public ushort unreliableSequenceNumber;
        public ENetProtocol command;
        public uint fragmentCount;
        public uint fragmentsRemaining;
        public uint[] fragments;
        public ENetPacket packet;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolHeader
    {
        public ushort peerID;
        public ushort sentTime;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolCommandHeader
    {
        public byte command;
        public byte channelID;
        public ushort reliableSequenceNumber;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolAcknowledge
    {
        public ENetProtocolCommandHeader header;
        public ushort receivedReliableSequenceNumber;
        public ushort receivedSentTime;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolConnect
    {
        public ENetProtocolCommandHeader header;
        public ushort outgoingPeerID;
        public byte incomingSessionID;
        public byte outgoingSessionID;
        public uint mtu;
        public uint windowSize;
        public uint channelCount;
        public uint incomingBandwidth;
        public uint outgoingBandwidth;
        public uint packetThrottleInterval;
        public uint packetThrottleAcceleration;
        public uint packetThrottleDeceleration;
        public uint connectID;
        public uint data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolVerifyConnect
    {
        public ENetProtocolCommandHeader header;
        public ushort outgoingPeerID;
        public byte incomingSessionID;
        public byte outgoingSessionID;
        public uint mtu;
        public uint windowSize;
        public uint channelCount;
        public uint incomingBandwidth;
        public uint outgoingBandwidth;
        public uint packetThrottleInterval;
        public uint packetThrottleAcceleration;
        public uint packetThrottleDeceleration;
        public uint connectID;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolBandwidthLimit
    {
        public ENetProtocolCommandHeader header;
        public uint incomingBandwidth;
        public uint outgoingBandwidth;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolThrottleConfigure
    {
        public ENetProtocolCommandHeader header;
        public uint packetThrottleInterval;
        public uint packetThrottleAcceleration;
        public uint packetThrottleDeceleration;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolDisconnect
    {
        public ENetProtocolCommandHeader header;
        public uint data;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolPing
    {
        public ENetProtocolCommandHeader header;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolSendReliable
    {
        public ENetProtocolCommandHeader header;
        public ushort dataLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolSendUnreliable
    {
        public ENetProtocolCommandHeader header;
        public ushort unreliableSequenceNumber;
        public ushort dataLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolSendUnsequenced
    {
        public ENetProtocolCommandHeader header;
        public ushort unsequencedGroup;
        public ushort dataLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ENetProtocolSendFragment
    {
        public ENetProtocolCommandHeader header;
        public ushort startSequenceNumber;
        public ushort dataLength;
        public uint fragmentCount;
        public uint fragmentNumber;
        public uint totalLength;
        public uint fragmentOffset;
    }

    //this was originally a union, in c# union should have all same 0 position at struct layout
    //size does not matter as all it's member already packed and this struct only works as a warpper
    [StructLayout(LayoutKind.Explicit)]
    public struct ENetProtocol {
        [FieldOffset(0)] public ENetProtocolCommandHeader header;
        [FieldOffset(0)] public ENetProtocolAcknowledge acknowledge;
        [FieldOffset(0)] public ENetProtocolConnect connect;
        [FieldOffset(0)] public ENetProtocolVerifyConnect verifyConnect;
        [FieldOffset(0)] public ENetProtocolDisconnect disconnect;
        [FieldOffset(0)] public ENetProtocolPing ping;
        [FieldOffset(0)] public ENetProtocolSendReliable sendReliable;
        [FieldOffset(0)] public ENetProtocolSendUnreliable sendUnreliable;
        [FieldOffset(0)] public ENetProtocolSendUnsequenced sendUnsequenced;
        [FieldOffset(0)] public ENetProtocolSendFragment sendFragment;
        [FieldOffset(0)] public ENetProtocolBandwidthLimit bandwidthLimit;
        [FieldOffset(0)] public ENetProtocolThrottleConfigure throttleConfigure;
    }

}
