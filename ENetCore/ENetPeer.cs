using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ENetCore
{
    public unsafe class ENetPeer : ENetListNode<ENetPeer>
    {
        public ENetHost host;
        public ushort outgoingPeerID;
        public ushort incomingPeerID;
        public uint connectID;
        public byte outgoingSessionID;
        public byte incomingSessionID;
        public IPEndPoint address; /**< Internet address of the peer */
        public void* data;    /**< Application private data, may be freely modified */
        public ENetPeerState state;
        public List<ENetChannel> channels;
        public int channelCount;      /**< Number of channels allocated for communication with peer */
        public uint incomingBandwidth; /**< Downstream bandwidth of the client in bytes/second */
        public uint outgoingBandwidth; /**< Upstream bandwidth of the client in bytes/second */
        public uint incomingBandwidthThrottleEpoch;
        public uint outgoingBandwidthThrottleEpoch;
        public uint incomingDataTotal;
        public ulong totalDataReceived;
        public uint outgoingDataTotal;
        public ulong totalDataSent;
        public uint lastSendTime;
        public uint lastReceiveTime;
        public uint nextTimeout;
        public uint earliestTimeout;
        public uint packetLossEpoch;
        public uint packetsSent;
        public ulong totalPacketsSent; /**< total number of packets sent during a session */
        public uint packetsLost;
        public uint totalPacketsLost;     /**< total number of packets lost during a session */
        public uint packetLoss; /**< mean packet loss of reliable packets as a ratio with respect to the constant ENET_PEER_PACKET_LOSS_SCALE */
        public uint packetLossVariance;
        public uint packetThrottle;
        public uint packetThrottleLimit;
        public uint packetThrottleCounter;
        public uint packetThrottleEpoch;
        public uint packetThrottleAcceleration;
        public uint packetThrottleDeceleration;
        public uint packetThrottleInterval;
        public uint pingInterval;
        public uint timeoutLimit;
        public uint timeoutMinimum;
        public uint timeoutMaximum;
        public uint lastRoundTripTime;
        public uint lowestRoundTripTime;
        public uint lastRoundTripTimeVariance;
        public uint highestRoundTripTimeVariance;
        public uint roundTripTime; /**< mean round trip time (RTT), in milliseconds, between sending a reliable packet and receiving its acknowledgement */
        public uint roundTripTimeVariance;
        public uint mtu;
        public uint windowSize;
        public uint reliableDataInTransit;
        public ushort outgoingReliableSequenceNumber;
        public ENetList<ENetAcknowledgement> acknowledgements;
        public ENetList<ENetOutgoingCommand> sentReliableCommands;
        public ENetList<ENetOutgoingCommand> sentUnreliableCommands;
        public ENetList<ENetOutgoingCommand> outgoingReliableCommands;
        public ENetList<ENetOutgoingCommand> outgoingUnreliableCommands;
        public ENetList<ENetIncomingCommand> dispatchedCommands;
        public bool needsDispatch;
        public ushort incomingUnsequencedGroup;
        public ushort outgoingUnsequencedGroup;
        static uint[] zeroUnsequencedWindow = new uint[(int)ENetConstant.ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32];
        public uint[] unsequencedWindow = new uint[(int)ENetConstant.ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32];
        public uint eventData;
        public int totalWaitingData;

        public bool IsConnected { get { return state == ENetPeerState.ENET_PEER_STATE_CONNECTED || state == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER; } }

        public void OnConnect()
        {

        }

        public void OnDisconnect()
        {

        }

        public void Reset()
        {
            // We don't want to reset connectID here, otherwise, we can't get it in the Disconnect event
            // peer->connectID                     = 0;
            outgoingPeerID = (ushort)ENetProtocolConstant.MAXIMUM_PEER_ID;
            state = ENetPeerState.ENET_PEER_STATE_DISCONNECTED;
            incomingBandwidth = 0;
            outgoingBandwidth = 0;
            incomingBandwidthThrottleEpoch = 0;
            outgoingBandwidthThrottleEpoch = 0;
            incomingDataTotal = 0;
            totalDataReceived = 0;
            outgoingDataTotal = 0;
            totalDataSent = 0;
            lastSendTime = 0;
            lastReceiveTime = 0;
            nextTimeout = 0;
            earliestTimeout = 0;
            packetLossEpoch = 0;
            packetsSent = 0;
            totalPacketsSent = 0;
            packetsLost = 0;
            totalPacketsLost = 0;
            packetLoss = 0;
            packetLossVariance = 0;
            packetThrottle = (uint)ENetConstant.ENET_PEER_DEFAULT_PACKET_THROTTLE;
            packetThrottleLimit = (uint)ENetConstant.ENET_PEER_DEFAULT_PACKET_THROTTLE;
            packetThrottleCounter = 0;
            packetThrottleEpoch = 0;
            packetThrottleAcceleration = (uint)ENetConstant.ENET_PEER_PACKET_THROTTLE_ACCELERATION;
            packetThrottleDeceleration = (uint)ENetConstant.ENET_PEER_PACKET_THROTTLE_DECELERATION;
            packetThrottleInterval = (uint)ENetConstant.ENET_PEER_PACKET_THROTTLE_INTERVAL;
            pingInterval = (uint)ENetConstant.ENET_PEER_PING_INTERVAL;
            timeoutLimit = (uint)ENetConstant.ENET_PEER_TIMEOUT_LIMIT;
            timeoutMinimum = (uint)ENetConstant.ENET_PEER_TIMEOUT_MINIMUM;
            timeoutMaximum = (uint)ENetConstant.ENET_PEER_TIMEOUT_MAXIMUM;
            lastRoundTripTime = (uint)ENetConstant.ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
            lowestRoundTripTime = (uint)ENetConstant.ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
            lastRoundTripTimeVariance = 0;
            highestRoundTripTimeVariance = 0;
            roundTripTime = (uint)ENetConstant.ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
            roundTripTimeVariance = 0;
            mtu = host.mtu;
            reliableDataInTransit = 0;
            outgoingReliableSequenceNumber = 0;
            windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;
            incomingUnsequencedGroup = 0;
            outgoingUnsequencedGroup = 0;
            eventData = 0;
            totalWaitingData = 0;

            Buffer.BlockCopy(zeroUnsequencedWindow, 0, unsequencedWindow, 0, unsequencedWindow.Length);
            ResetQueues();
        }


        public void ResetQueues()
        {

            if (needsDispatch)
            {
                ENetList<ENetPeer>.Remove(this);
                needsDispatch = false;
            }

            while (!acknowledgements.IsEmpty)
            {
                ENetAcknowledgement.Free(ENetList<ENetAcknowledgement>.Remove(acknowledgements.End));
            }

            ResetOutgoingCommands(sentReliableCommands);
            ResetOutgoingCommands(sentUnreliableCommands);
            ResetOutgoingCommands(outgoingReliableCommands);
            ResetOutgoingCommands(outgoingUnreliableCommands);
            ResetIncomingCommands(dispatchedCommands);

            if (channels != null && channelCount > 0)
            {
                for (int i = 0; i < channels.Count; ++i)
                {
                    ResetIncomingCommands(channels[i].incomingReliableCommands);
                    ResetIncomingCommands(channels[i].incomingUnreliableCommands);
                }

                //TODO: recycle channels?
                //enet_free(peer->channels);
            }

            channels = null;
            channelCount = 0;
        }


        /// <summary>
        /// reset/frees an outgoing command list, as well as the enetpacket inside of it.
        /// </summary>
        /// <param name="queue">list to reset</param>
        static void ResetOutgoingCommands(ENetList<ENetOutgoingCommand> queue)
        {
            ENetOutgoingCommand outgoingCommand;

            while (!queue.IsEmpty)
            {
                outgoingCommand = ENetList<ENetOutgoingCommand>.Remove(queue.Begin);

                if (outgoingCommand.packet != null)
                {
                    --outgoingCommand.packet.referenceCount;

                    if (outgoingCommand.packet.referenceCount == 0)
                    {
                        ENetPacket.Destroy(outgoingCommand.packet);
                    }
                }

                ENetOutgoingCommand.Free(outgoingCommand);
            }
        }

        static void ResetIncomingCommands(ENetList<ENetIncomingCommand> queue)
        {
            RemoveIncomingCommands(queue, queue.Begin, queue.End);
        }

        static void RemoveIncomingCommands(ENetList<ENetIncomingCommand> queue, ENetIncomingCommand startCommand, ENetIncomingCommand endCommand)
        {
            for (var currentCommand = startCommand; currentCommand != endCommand;)
            {
                var incomingCommand = currentCommand;
                currentCommand = currentCommand.Next;

                ENetList<ENetIncomingCommand>.Remove(incomingCommand);

                if (incomingCommand.packet != null)
                { 
                    --incomingCommand.packet.referenceCount;

                    if (incomingCommand.packet.referenceCount == 0)
                    {
                        ENetPacket.Destroy(incomingCommand.packet);
                    }
                }

                if (incomingCommand.fragments != null)
                {
                    //todo
                    //ENet.Free(incomingCommand.fragments);
                }

                ENetIncomingCommand.Free(incomingCommand);
            }
        }

        public ENetPacket Receive(ref int channelID)
        {
            if (dispatchedCommands.IsEmpty)
                return null;

            var incomingCommand = ENetList<ENetIncomingCommand>.Remove(dispatchedCommands.Begin);

            channelID = incomingCommand.command.header.channelID;

            var packet = incomingCommand.packet;
            packet.referenceCount--;

            if(incomingCommand.fragments != null)
            {
                //todo
                //ENet.Free(incomingCommand.fragments);
            }

            ENetIncomingCommand.Free(incomingCommand);
            totalWaitingData -= packet.dataLength;

            return packet;
        }

        public int Throttle(uint rtt)
        {
            if (lastRoundTripTime <= lastRoundTripTimeVariance)
                packetThrottle = packetThrottleLimit;
            else if(rtt < lastRoundTripTime)
            {
                packetThrottle += packetThrottleAcceleration;
                if (packetThrottle > packetThrottleLimit)
                    packetThrottle = packetThrottleLimit;

                return 1;
            }
            else if(rtt > lastRoundTripTime + 2 * lastRoundTripTimeVariance)
            {
                if (packetThrottle > packetThrottleDeceleration)
                    packetThrottle -= packetThrottleDeceleration;
                else
                    packetThrottle = 0;

                return -1;
            }

            return 0;
        }

        public void Disconnect(uint data)
        {
            if (state == ENetPeerState.ENET_PEER_STATE_DISCONNECTING ||
                state == ENetPeerState.ENET_PEER_STATE_DISCONNECTED ||
                state == ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_DISCONNECT ||
                state == ENetPeerState.ENET_PEER_STATE_ZOMBIE)
                return;

            ResetQueues();

            ENetProtocol command = new ENetProtocol();
            command.header.command = (byte)ENetProtocolCommand.DISCONNECT;
            command.header.channelID = 0xFF;
            command.disconnect.data = ENetUtil.HostToNet(data);

            if (IsConnected)
                command.header.command |= (byte)ENetProtocolFlag.COMMAND_FLAG_ACKNOWLEDGE;
            else
                command.header.command |= (byte)ENetProtocolFlag.COMMAND_FLAG_UNSEQUENCED;

            QueueOutgoingCommand(ref command, null, 0, 0);

            if(IsConnected)
            {
                OnDisconnect();
                state = ENetPeerState.ENET_PEER_STATE_DISCONNECTING;
            }
            else
            {
                host.Flush();
                Reset();
            }
        }

        public ENetOutgoingCommand QueueOutgoingCommand(ref ENetProtocol command, ENetPacket packet, uint offset, uint length)
        {
            return null;
        }


        public ENetIncomingCommand QueueIncomingCommand(ref ENetProtocol command, int currentOffset, int dataLength, uint flags, uint fragmentCount)
        {
            return null;
        }

        public void DispatchIncomingUnreliableCommands(ENetChannel channel)
        {

        }

        public void DispatchIncomingReliableCommands(ENetChannel channel)
        {

        }
    }
}
