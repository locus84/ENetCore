using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ENetCore
{
    public unsafe struct ENetHost
    {
        public IENetSocket socket;
        public ENetAddress address;           /**< Internet address of the host */
        public uint incomingBandwidth; /**< downstream bandwidth of the host */
        public uint outgoingBandwidth; /**< upstream bandwidth of the host */
        public uint bandwidthThrottleEpoch;
        public uint mtu;
        public uint randomSeed;
        public bool recalculateBandwidthLimits;
        public ENetPeer[] peers;        /**< array of peers allocated for this host */
        public int peerCount;    /**< number of peers allocated for this host */
        public int channelLimit; /**< maximum number of channels allowed for connected peers */
        public uint serviceTime;
        public ENetList<ENetPeer> dispatchQueue;
        public int continueSending;
        public int packetSize;
        public ushort headerFlags;
        public ENetProtocol[] commands;// = new ENetProtocol[(int)ENetProtocolConstant.MAXIMUM_PACKET_COMMANDS];
        public int commandCount;
        public ENetBuffer[] buffers;//[(int)ENetProtocolConstant.ENET_BUFFER_MAXIMUM];
        public int bufferCount;
        public ENetChecksumCallback checksum; /**< callback the user can set to enable packet checksums for this host */
        public ENetCompressor compressor;
        public byte[][] packetData; //[2][ENET_PROTOCOL_MAXIMUM_MTU];
        public IPEndPoint receivedAddress;
        public byte[] receivedData;
        public int receivedDataLength;
        public uint totalSentData;        /**< total data sent, user should reset to 0 as needed to prevent overflow */
        public uint totalSentPackets;     /**< total UDP packets sent, user should reset to 0 as needed to prevent overflow */
        public uint totalReceivedData;    /**< total data received, user should reset to 0 as needed to prevent overflow */
        public uint totalReceivedPackets; /**< total UDP packets received, user should reset to 0 as needed to prevent overflow */
        public ENetInterceptCallback intercept;            /**< callback the user can set to intercept received raw UDP packets */
        public int connectedPeers;
        public int bandwidthLimitedPeers;
        public int duplicatePeers;     /**< optional number of allowed peers from duplicate IPs, defaults to ENET_PROTOCOL_MAXIMUM_PEER_ID */
        public int maximumPacketSize;  /**< the maximum allowable packet size that may be sent or received on a peer */
        public int maximumWaitingData; /**< the maximum aggregate amount of buffer space a peer may use waiting for packets to be delivered */



        public void ProtocolChangeState(ENetPeer peer, ENetPeerState state)
        {
            if(state == ENetPeerState.ENET_PEER_STATE_CONNECTED || state == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                peer.OnConnect();
            }
            else
            {
                peer.OnDisconnect();
            }

            peer.state = state;
        }

        public void ProtocolDispatchState(ENetPeer peer, ENetPeerState state)
        {
            ProtocolChangeState(peer, state);

            if(!peer.needsDispatch)
            {
                ENetList<ENetPeer>.Insert(dispatchQueue.End, peer);
                peer.needsDispatch = true;
            }
        }

        public int ProtocolDispatchIncomingCommands(ENetEvent netEvent)
        {
            while(!dispatchQueue.IsEmpty)
            {
                var peer = ENetList<ENetPeer>.Remove(dispatchQueue.Begin);
                peer.needsDispatch = false;

                switch (peer.state)
                {
                    case ENetPeerState.ENET_PEER_STATE_CONNECTION_PENDING:
                    case ENetPeerState.ENET_PEER_STATE_CONNECTION_SUCCEEDED:
                        ProtocolChangeState(peer, ENetPeerState.ENET_PEER_STATE_CONNECTED);

                        netEvent.type = ENetEventType.ENET_EVENT_TYPE_CONNECT;
                        netEvent.peer = peer;
                        netEvent.data = peer.eventData;

                        return 1;

                    case ENetPeerState.ENET_PEER_STATE_ZOMBIE:
                        recalculateBandwidthLimits = true;

                        netEvent.type = ENetEventType.ENET_EVENT_TYPE_DISCONNECT;
                        netEvent.peer = peer;
                        netEvent.data = peer.eventData;

                        peer.Reset();
                        return 1;
                    case ENetPeerState.ENET_PEER_STATE_CONNECTED:
                        if (peer.dispatchedCommands.IsEmpty)
                            continue;

                        netEvent.packet = peer.Receive(ref netEvent.channelID);
                        if (netEvent.packet == null)
                            continue;

                        netEvent.type = ENetEventType.ENET_EVENT_TYPE_RECEIVE;
                        netEvent.peer = peer;

                        if (!peer.dispatchedCommands.IsEmpty)
                        {
                            peer.needsDispatch = true;
                            ENetList<ENetPeer>.Insert(dispatchQueue.End, peer);
                        }

                        return 1;
                    default:
                        break;
                }
            }

            return 0;
        }

        public void ProtocolNotifyConnect(ENetPeer peer, ENetEvent netEvent)
        {
            recalculateBandwidthLimits = true;

            if(netEvent != null)
            {
                ProtocolChangeState(peer, ENetPeerState.ENET_PEER_STATE_CONNECTED);

                peer.totalDataSent =        0;
                peer.totalDataReceived =    0;
                peer.totalPacketsSent =     0;
                peer.totalPacketsLost =     0;

                netEvent.type = ENetEventType.ENET_EVENT_TYPE_CONNECT;
                netEvent.peer = peer;
                netEvent.data = peer.eventData;
            }
            else
            {
                ProtocolDispatchState(peer, peer.state == ENetPeerState.ENET_PEER_STATE_CONNECTING ? 
                    ENetPeerState.ENET_PEER_STATE_CONNECTION_SUCCEEDED : ENetPeerState.ENET_PEER_STATE_CONNECTION_PENDING);
            }
        }

        public void ProtocolNotifyDisconnect(ENetPeer peer, ENetEvent netEvent, bool timeOut = false)
        {
            if(peer.state >= ENetPeerState.ENET_PEER_STATE_CONNECTION_PENDING)
            {
                recalculateBandwidthLimits = true;
            }

            if(peer.state != ENetPeerState.ENET_PEER_STATE_CONNECTING && peer.state < ENetPeerState.ENET_PEER_STATE_CONNECTION_SUCCEEDED)
            {
                peer.Reset();
            }
            else if(netEvent != null)
            {
                netEvent.type = timeOut? ENetEventType.ENET_EVENT_TYPE_DISCONNECT_TIMEOUT : ENetEventType.ENET_EVENT_TYPE_DISCONNECT;
                netEvent.peer = peer;
                netEvent.data = 0;

                peer.Reset();
            }
            else
            {
                peer.eventData = 0;
                ProtocolDispatchState(peer, ENetPeerState.ENET_PEER_STATE_ZOMBIE);
            }
        }

        public void ProtocolRemoveSentUnreliableCommands(ENetPeer peer)
        {
            while(!peer.sentUnreliableCommands.IsEmpty)
            {
                var outgoingCommand = peer.sentUnreliableCommands.Front;
                ENetList<ENetOutgoingCommand>.Remove(outgoingCommand);


                if(outgoingCommand.packet != null)
                {
                    outgoingCommand.packet.referenceCount--;

                    if(outgoingCommand.packet.referenceCount == 0)
                    {
                        outgoingCommand.packet.flags |= ENetPacketFlag.ENET_PACKET_FLAG_SENT;
                        ENetPacket.Destroy(outgoingCommand.packet);
                    }
                }

                ENetOutgoingCommand.Free(outgoingCommand);
            }
        }

        public ENetProtocolCommand ProtocolRemoveSentReliableCommand(ENetPeer peer, ushort reliableSequenceNumber, byte channelID)
        {
            ENetOutgoingCommand outgoingCommand = null;
            bool wasSent = true;

            for(outgoingCommand = peer.sentReliableCommands.Begin; outgoingCommand != peer.sentReliableCommands.End; outgoingCommand = outgoingCommand.Next)
            {
                if (outgoingCommand.reliableSequenceNumber == reliableSequenceNumber && outgoingCommand.command.header.channelID == channelID) break;
            }

            if(outgoingCommand == peer.sentReliableCommands.End)
            {
                for(outgoingCommand = peer.outgoingReliableCommands.Begin; outgoingCommand != peer.outgoingReliableCommands.End; outgoingCommand = outgoingCommand.Next)
                {
                    if (outgoingCommand.sendAttempts < 1) return ENetProtocolCommand.NONE;

                    if (outgoingCommand.reliableSequenceNumber == reliableSequenceNumber && outgoingCommand.command.header.channelID == channelID) break;
                }

                if (outgoingCommand == peer.outgoingReliableCommands.End) return ENetProtocolCommand.NONE;

                wasSent = false;
            }

            if(outgoingCommand == null) return ENetProtocolCommand.NONE;

            if(channelID < peer.channelCount)
            {
                ENetChannel channel = peer.channels[channelID];
                ushort reliableWindow = (ushort)(reliableSequenceNumber / (int)ENetConstant.ENET_PEER_RELIABLE_WINDOW_SIZE);
                if(peer.channels[channelID].reliableWindows[reliableWindow] > 0)
                {
                    channel.reliableWindows[reliableWindow]--;
                    if(channel.reliableWindows[reliableWindow] == 0)
                    {
                        channel.usedReliableWindows &= (ushort)~(1 << reliableWindow);
                    }
                }
            }

            ENetProtocolCommand commandNumber = (ENetProtocolCommand)outgoingCommand.command.header.command & ENetProtocolCommand.MASK;
            ENetList<ENetOutgoingCommand>.Remove(outgoingCommand);

            if(outgoingCommand.packet != null)
            {
                if(wasSent)
                {
                    peer.reliableDataInTransit -= outgoingCommand.fragmentLength;
                }

                outgoingCommand.packet.referenceCount--;

                if(outgoingCommand.packet.referenceCount == 0)
                {
                    outgoingCommand.packet.flags |= ENetPacketFlag.ENET_PACKET_FLAG_SENT;
                    ENetPacket.Destroy(outgoingCommand.packet);
                }
            }

            ENetOutgoingCommand.Free(outgoingCommand);

            if (peer.sentReliableCommands.IsEmpty) return commandNumber;

            outgoingCommand = peer.sentReliableCommands.Front;
            peer.nextTimeout = outgoingCommand.sentTime + outgoingCommand.roundTripTimeout;

            return commandNumber;
        }


        public ENetPeer ProtocolHandleConnect(ENetProtocolHeader header, ENetProtocol command)
        {
            byte incomingSessionID, outgoingSessionID;
            uint mtu, windowSize;
            ENetChannel channel;
            int channelCount, curDuplicatedPeers = 0;
            ENetPeer currentPeer, peer = null;

            channelCount = System.Net.IPAddress.NetworkToHostOrder((int)command.connect.channelCount);

            if (channelCount < (int)ENetProtocolConstant.MINIMUM_CHANNEL_COUNT || channelCount > (int)ENetProtocolConstant.MAXIMUM_CHANNEL_COUNT)
            {
                return null;
            }

            for (int i = 0; i < peers.Length; i++)
            {
                currentPeer = peers[i];
                if (currentPeer.state == ENetPeerState.ENET_PEER_STATE_DISCONNECTED)
                {
                    if (peer == null) peer = currentPeer;
                }
                else if (currentPeer.state != ENetPeerState.ENET_PEER_STATE_CONNECTING && currentPeer.address.Address == receivedAddress.Address)
                {
                    if (currentPeer.address.Port == receivedAddress.Port && currentPeer.connectID == command.connect.connectID)
                    {
                        return null;
                    }

                    ++curDuplicatedPeers;
                }
            }

            if (peer == null || curDuplicatedPeers >= duplicatePeers)
                return null;

            if (channelCount > channelLimit)
                channelCount = channelLimit;

            peer.channels = ENetChannel.CreateChannels(channelCount, peer.channels);
            if (peer.channels == null)
                return null;

            peer.channelCount = channelCount;
            peer.state = ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_CONNECT;
            peer.connectID = command.connect.connectID;
            peer.address = receivedAddress;
            peer.outgoingPeerID = (ushort)IPAddress.NetworkToHostOrder((short)command.connect.outgoingPeerID);
            peer.incomingBandwidth = (uint)IPAddress.NetworkToHostOrder((int)command.connect.incomingBandwidth);
            peer.outgoingBandwidth = (uint)IPAddress.NetworkToHostOrder((int)command.connect.outgoingBandwidth);
            peer.packetThrottleInterval = (uint)IPAddress.NetworkToHostOrder((int)command.connect.packetThrottleInterval);
            peer.packetThrottleAcceleration = (uint)IPAddress.NetworkToHostOrder((int)command.connect.packetThrottleAcceleration);
            peer.packetThrottleDeceleration = (uint)IPAddress.NetworkToHostOrder((int)command.connect.packetThrottleDeceleration);
            peer.eventData = (uint)IPAddress.NetworkToHostOrder((int)command.connect.data);

            incomingSessionID = command.connect.incomingSessionID == 0xFF ? peer.outgoingSessionID : command.connect.incomingSessionID;
            incomingSessionID = (byte)((incomingSessionID + 1) & ((int)ENetProtocolFlag.HEADER_SESSION_MASK >> (int)ENetProtocolFlag.HEADER_SESSION_SHIFT));
            if (incomingSessionID == peer.outgoingSessionID)
            {
                incomingSessionID = (byte)((incomingSessionID + 1) & ((int)ENetProtocolFlag.HEADER_SESSION_MASK >> (int)ENetProtocolFlag.HEADER_SESSION_SHIFT));
            }
            peer.outgoingSessionID = incomingSessionID;

            outgoingSessionID = command.connect.outgoingSessionID == 0xFF ? peer.incomingSessionID : command.connect.outgoingSessionID;
            outgoingSessionID = (byte)((outgoingSessionID + 1) & ((int)ENetProtocolFlag.HEADER_SESSION_MASK >> (int)ENetProtocolFlag.HEADER_SESSION_SHIFT));
            if (outgoingSessionID == peer.incomingSessionID)
            {
                outgoingSessionID = (byte)((outgoingSessionID + 1) & ((int)ENetProtocolFlag.HEADER_SESSION_MASK >> (int)ENetProtocolFlag.HEADER_SESSION_SHIFT));
            }
            peer.incomingSessionID = outgoingSessionID;

            for (int i = 0; i < peer.channels.Count; i++)
            {
                channel = peer.channels[i];
                channel.outgoingReliableSequenceNumber = 0;
                channel.outgoingUnreliableSequenceNumber = 0;
                channel.incomingReliableSequenceNumber = 0;
                channel.incomingUnreliableSequenceNumber = 0;

                channel.incomingReliableCommands.Clear();
                channel.incomingUnreliableCommands.Clear();
                channel.usedReliableWindows = 0;

                for (int j = 0; j < channel.reliableWindows.Length; j++)
                {
                    channel.reliableWindows[j] = 0;
                }
            }

            mtu = (uint)IPAddress.NetworkToHostOrder((int)command.connect.mtu);

            if (outgoingBandwidth == 0 && incomingBandwidth == 0)
            {
                peer.windowSize = (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;
            }
            else if (outgoingBandwidth == 0 || peer.incomingBandwidth == 0)
            {
                peer.windowSize = (Math.Max(outgoingBandwidth, peer.incomingBandwidth) / (int)ENetConstant.ENET_PEER_WINDOW_SIZE_SCALE) * (int)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;
            }
            else
            {
                peer.windowSize = (Math.Max(outgoingBandwidth, peer.incomingBandwidth) / (int)ENetConstant.ENET_PEER_WINDOW_SIZE_SCALE) * (int)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;
            }

            if (peer.windowSize < (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE)
                peer.windowSize = (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;
            else if (peer.windowSize > (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE)
                peer.windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;

            if(incomingBandwidth == 0)
            {
                windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;
            }
            else
            {
                windowSize = (incomingBandwidth / (int)ENetConstant.ENET_PEER_WINDOW_SIZE_SCALE) * (int)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;
            }

            if (windowSize > (uint)IPAddress.NetworkToHostOrder((uint)command.connect.windowSize))
                windowSize = (uint)IPAddress.NetworkToHostOrder((uint)command.connect.windowSize);

            if(windowSize < (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE)
            {
                windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;
            }
            else if (windowSize > (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE)
            {
                windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;
            }

            ENetProtocol verifyCommand = default(ENetProtocol);
            verifyCommand.header.command = (byte)(ENetProtocolCommand.VERIFY_CONNECT | ENetProtocolCommand.ACKNOWLEDGE);
            verifyCommand.header.channelID = 0xFF;
            verifyCommand.verifyConnect.outgoingPeerID = (ushort)IPAddress.HostToNetworkOrder((short)peer.incomingPeerID);
            verifyCommand.verifyConnect.incomingSessionID = incomingSessionID;
            verifyCommand.verifyConnect.outgoingSessionID = outgoingSessionID;
            verifyCommand.verifyConnect.mtu = (uint)IPAddress.HostToNetworkOrder((int)peer.mtu);
            verifyCommand.verifyConnect.windowSize = (uint)IPAddress.HostToNetworkOrder((int)windowSize);
            verifyCommand.verifyConnect.channelCount = (uint)IPAddress.HostToNetworkOrder(channelCount);
            verifyCommand.verifyConnect.incomingBandwidth = (uint)IPAddress.HostToNetworkOrder((int)incomingBandwidth);
            verifyCommand.verifyConnect.outgoingBandwidth = (uint)IPAddress.HostToNetworkOrder((int)outgoingBandwidth);
            verifyCommand.verifyConnect.packetThrottleInterval = (uint)IPAddress.HostToNetworkOrder((int)peer.packetThrottleInterval);
            verifyCommand.verifyConnect.packetThrottleAcceleration = (uint)IPAddress.HostToNetworkOrder((int)peer.packetThrottleAcceleration);
            verifyCommand.verifyConnect.packetThrottleDeceleration = (uint)IPAddress.HostToNetworkOrder((int)peer.packetThrottleDeceleration);
            verifyCommand.verifyConnect.connectID = peer.connectID;

            peer.QueueOutgoingCommand(ref verifyCommand, null, 0, 0);
            return peer;
        }

        //reliable sequenced
        public bool ProtocolHandleSendReliable(ENetPeer peer, ENetProtocol command, ref int dataOffset)
        {
            if (command.header.channelID >= peer.channelCount || !peer.IsConnected)
                return false;

            var currentOffset = dataOffset;
            var dataLength = ENetUtil.NetToHost(command.sendReliable.dataLength);
            dataOffset += dataLength;

            //data length validation
            if (dataLength > maximumPacketSize || receivedDataLength < dataOffset)
                return false;

            if (peer.QueueIncomingCommand(ref command, currentOffset + ENet.GetProtocolCommandSize(ENetProtocolCommand.SEND_RELIABLE), dataLength, (int)ENetPacketFlag.ENET_PACKET_FLAG_RELIABLE, 0) == null)
                return false;

            return true;
        }

        //unreliable unsequenced - only receive not received values
        public bool ProtocolHandleSendUnsequenced(ENetPeer peer, ENetProtocol command, ref int dataOffset)
        {
            if (command.header.channelID >= peer.channelCount || !peer.IsConnected)
                return false;

            var currentOffset = dataOffset;
            var dataLength = ENetUtil.NetToHost(command.sendReliable.dataLength);
            dataOffset += dataLength;

            //data length validation
            if (dataLength > maximumPacketSize || receivedDataLength < dataOffset)
                return false;

            //unsequencedgroup is just incremental number of remote peer
            uint unsequencedGroup = ENetUtil.NetToHost(command.sendUnsequenced.unsequencedGroup);

            //index is under 1024 = 100 0000 0000 = 2^10
            //this is just index
            var index = unsequencedGroup % (int)ENetConstant.ENET_PEER_UNSEQUENCED_WINDOW_SIZE;

            //if message group is previous group means late message
            //or maybe it's overflown short value, add max short val
            if (unsequencedGroup < peer.incomingUnsequencedGroup)
                // 1 0000 0000 0000 0000 add 
                unsequencedGroup += 0x10000;

            //if previous group is received, it'll be dropped, (ex: 1024 , 1023 + short.max), if it's overflown value, it'll work
            if (unsequencedGroup > peer.incomingUnsequencedGroup + (uint)ENetConstant.ENET_PEER_FREE_UNSEQUENCED_WINDOWS * (uint)ENetConstant.ENET_PEER_UNSEQUENCED_WINDOW_SIZE)
                return true;

            //extract meaningful data as it's originally ushort
            unsequencedGroup &= 0xFFFF;

            //if sequence is advanced, sync to current group and clear windows
            if(unsequencedGroup - index != peer.incomingUnsequencedGroup)
            {
                peer.incomingUnsequencedGroup = (ushort)(unsequencedGroup - index);
                for (int i = 0; i < peer.unsequencedWindow.Length; i++)
                    peer.unsequencedWindow[i] = 0;
            }else if(peer.unsequencedWindow[index / 32] % (1 << (int)(index % 32)) > 0)
            {
                //this is already received message
                return true;
            }

            //try receive
            if (peer.QueueIncomingCommand(ref command, currentOffset + SizeOf<ENetProtocolSendUnsequenced>.Value, dataLength, (uint)ENetPacketFlag.ENET_PACKET_FLAG_UNSEQUENCED, 0) == null)
                return false;

            //record to following index
            peer.unsequencedWindow[index / 32] |= 1u << (int)(index % 32);

            return true;
        }

        public bool ProtocolHandleSendUnreliable(ENetPeer peer, ENetProtocol command, ref int dataOffset)
        {
            if (command.header.channelID >= peer.channelCount || !peer.IsConnected)
                return false;

            var currentOffset = dataOffset;
            var dataLength = ENetUtil.NetToHost(command.sendUnreliable.dataLength);
            dataOffset += dataLength;

            //data length validation
            if (dataLength > maximumPacketSize || receivedDataLength < dataOffset)
                return false;

            //try receive
            if (peer.QueueIncomingCommand(ref command, currentOffset + SizeOf<ENetProtocolSendUnreliable>.Value, dataLength, 0, 0) == null)
                return false;

            return true;
        }

        public bool ProtocolHandleSendFragment(ENetPeer peer, ENetProtocol command, ref int dataOffset)
        {
            if (command.header.channelID >= peer.channelCount || !peer.IsConnected)
                return false;

            var currentOffset = dataOffset;
            uint fragmentLength = ENetUtil.NetToHost(command.sendFragment.dataLength);
            dataOffset += (int)fragmentLength;

            //data length validation
            if (fragmentLength > maximumPacketSize || receivedDataLength < dataOffset)
                return false;

            var channel = peer.channels[command.header.channelID];
            //as uint 
            uint startSequenceNumber = ENetUtil.NetToHost(command.sendFragment.startSequenceNumber);
            var startWindow = (ushort)(startSequenceNumber / (ushort)ENetConstant.ENET_PEER_RELIABLE_WINDOW_SIZE);
            var currentWindow = (ushort)(channel.incomingReliableSequenceNumber / (ushort)ENetConstant.ENET_PEER_RELIABLE_WINDOW_SIZE);

            if (startSequenceNumber < channel.incomingReliableSequenceNumber)
                startWindow += (ushort)ENetConstant.ENET_PEER_RELIABLE_WINDOWS;

            if (startWindow < currentWindow || startWindow >= currentWindow + (ushort)ENetConstant.ENET_PEER_RELIABLE_WINDOWS - 1)
                return false;

            var fragmentNumber = ENetUtil.NetToHost(command.sendFragment.fragmentNumber);
            var fragmentCount = ENetUtil.NetToHost(command.sendFragment.fragmentCount);
            var fragmentOffset = ENetUtil.NetToHost(command.sendFragment.fragmentOffset);
            var totalLength = ENetUtil.NetToHost(command.sendFragment.totalLength);

            if (fragmentCount > (uint)ENetProtocolConstant.MAXIMUM_FRAGMENT_COUNT ||
                fragmentNumber >= fragmentCount ||
                totalLength > maximumPacketSize ||
                fragmentOffset >= totalLength ||
                fragmentLength > totalLength - fragmentOffset)
                return false;


            ENetIncomingCommand startCommand = null;

            for (var incomingCommand = channel.incomingReliableCommands.End.Previous; 
                incomingCommand != channel.incomingReliableCommands.End;
                incomingCommand = incomingCommand.Previous)
            {
                if (startSequenceNumber >= channel.incomingReliableSequenceNumber)
                {
                    if (incomingCommand.reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                        continue;
                }
                else if (incomingCommand.reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                    break;

                if(incomingCommand.reliableSequenceNumber <= startSequenceNumber)
                {
                    if (incomingCommand.reliableSequenceNumber < startSequenceNumber)
                        break;

                    if ((incomingCommand.command.header.command & (uint)ENetProtocolCommand.MASK) != (uint)ENetProtocolCommand.SEND_FRAGMENT ||
                        totalLength != incomingCommand.packet.dataLength ||
                        fragmentCount != incomingCommand.fragmentCount)
                        return false;

                    startCommand = incomingCommand;
                    break;
                }
            }

            if(startCommand == null)
            {
                ENetProtocol hostCommand = command;
                hostCommand.header.reliableSequenceNumber = (ushort)startSequenceNumber;
                startCommand = peer.QueueIncomingCommand(ref hostCommand, -1, (int)totalLength, (int)ENetPacketFlag.ENET_PACKET_FLAG_RELIABLE, fragmentCount);
                if (startCommand == null)
                    return false;
            }


            if((startCommand.fragments[fragmentNumber / 32] & (1 << ((int)fragmentNumber % 32))) == 0)
            {
                --startCommand.fragmentsRemaining;
                startCommand.fragments[fragmentNumber / 32] |= 1u << ((int)fragmentNumber % 32);

                if (fragmentOffset + fragmentLength > startCommand.packet.dataLength)
                    fragmentLength = (uint)startCommand.packet.dataLength - fragmentOffset;

                // copy received data to starting packet

                Buffer.BlockCopy(receivedData, currentOffset + SizeOf<ENetProtocolSendFragment>.Value, startCommand.packet.data, (int)fragmentOffset, (int)fragmentLength);

                if (startCommand.fragmentsRemaining <= 0)
                    peer.DispatchIncomingReliableCommands(channel);
            }

            return true;
        }

        public bool ProtocolHandleSendUnreliableFragment(ENetPeer peer, ENetProtocol command, ref int dataOffset)
        {
            if (command.header.channelID >= peer.channelCount || !peer.IsConnected)
                return false;

            var currentOffset = dataOffset;
            uint fragmentLength = ENetUtil.NetToHost(command.sendFragment.dataLength);
            dataOffset += (int)fragmentLength;

            //data length validation
            if (fragmentLength > maximumPacketSize || receivedDataLength < dataOffset)
                return false;

            var channel = peer.channels[command.header.channelID];
            //as uint 
            uint reliableSequenceNumber = command.header.reliableSequenceNumber;
            uint startSequenceNumber = ENetUtil.NetToHost(command.sendFragment.startSequenceNumber);

            var reliableWindow = (ushort)(reliableSequenceNumber / (ushort)ENetConstant.ENET_PEER_RELIABLE_WINDOW_SIZE);
            var currentWindow = (ushort)(channel.incomingReliableSequenceNumber / (ushort)ENetConstant.ENET_PEER_RELIABLE_WINDOW_SIZE);

            if (reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                reliableWindow += (int)ENetConstant.ENET_PEER_RELIABLE_WINDOWS;

            if (reliableWindow < currentWindow || reliableWindow >= currentWindow + (int)ENetConstant.ENET_PEER_RELIABLE_WINDOWS - 1)
                return true;

            if (reliableSequenceNumber == channel.incomingReliableSequenceNumber && startSequenceNumber <= channel.incomingUnreliableSequenceNumber)
                return true;

            uint fragmentNumber = ENetUtil.NetToHost(command.sendFragment.fragmentNumber);
            uint fragmentCount  = ENetUtil.NetToHost(command.sendFragment.fragmentCount);
            uint fragmentOffset = ENetUtil.NetToHost(command.sendFragment.fragmentOffset);
            uint totalLength    = ENetUtil.NetToHost(command.sendFragment.totalLength);

            if (fragmentCount > (uint)ENetProtocolConstant.MAXIMUM_FRAGMENT_COUNT ||
                fragmentNumber >= fragmentCount ||
                totalLength > maximumPacketSize ||
                fragmentOffset >= totalLength ||
                fragmentLength > totalLength - fragmentOffset)
                return false;

            ENetIncomingCommand startCommand = null;

            for (var incomingCommand = channel.incomingUnreliableCommands.End.Previous;
                incomingCommand != channel.incomingUnreliableCommands.End;
                incomingCommand = incomingCommand.Previous)
            {
                if (reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                {
                    if (incomingCommand.reliableSequenceNumber < channel.incomingReliableSequenceNumber)
                        continue;
                }
                else if (incomingCommand.reliableSequenceNumber >= channel.incomingReliableSequenceNumber)
                    break;

                if (incomingCommand.reliableSequenceNumber < reliableSequenceNumber)
                    break;

                if (incomingCommand.reliableSequenceNumber > reliableSequenceNumber)
                    continue;

                if (incomingCommand.unreliableSequenceNumber <= startSequenceNumber)
                {
                    if (incomingCommand.unreliableSequenceNumber < startSequenceNumber)
                        break;

                    if ((incomingCommand.command.header.command & (uint)ENetProtocolCommand.MASK) != (uint)ENetProtocolCommand.SEND_UNRELIABLE_FRAGMENT ||
                        totalLength != incomingCommand.packet.dataLength ||
                        fragmentCount != incomingCommand.fragmentCount)
                        return false;

                    startCommand = incomingCommand;
                    break;
                }

                if(startCommand == null)
                {
                    startCommand = peer.QueueIncomingCommand(ref command, -1, (int)totalLength, (int)ENetPacketFlag.ENET_PACKET_FLAG_RELIABLE, fragmentCount);
                    if (startCommand == null)
                        return false;
                }


                if ((startCommand.fragments[fragmentNumber / 32] & (1 << ((int)fragmentNumber % 32))) == 0)
                {
                    --startCommand.fragmentsRemaining;
                    startCommand.fragments[fragmentNumber / 32] |= 1u << ((int)fragmentNumber % 32);

                    if (fragmentOffset + fragmentLength > startCommand.packet.dataLength)
                        fragmentLength = (uint)startCommand.packet.dataLength - fragmentOffset;

                    // copy received data to starting packet
                    Buffer.BlockCopy(receivedData, currentOffset + SizeOf<ENetProtocolSendFragment>.Value, startCommand.packet.data, (int)fragmentOffset, (int)fragmentLength);

                    if (startCommand.fragmentsRemaining <= 0)
                        peer.DispatchIncomingReliableCommands(channel);
                }
            }
            return true;
        }

        public bool ProtocolHandlePing(ENetPeer peer, ENetProtocol command)
        {
            if (!peer.IsConnected) return false;
            return true;
        }

        public bool ProtocolHandleBandwidthLimit(ENetPeer peer, ENetProtocol command)
        {
            if (!peer.IsConnected) return false;

            if (peer.incomingBandwidth != 0)
                --bandwidthLimitedPeers;

            peer.incomingBandwidth = ENetUtil.NetToHost(command.bandwidthLimit.incomingBandwidth);
            peer.outgoingBandwidth = ENetUtil.NetToHost(command.bandwidthLimit.outgoingBandwidth);

            if (peer.incomingBandwidth != 0)
                ++bandwidthLimitedPeers;

            if (peer.incomingBandwidth == 0 && outgoingBandwidth == 0)
                peer.windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;
            else if (peer.incomingBandwidth == 0 || outgoingBandwidth == 0)
                peer.windowSize = (Math.Max(peer.incomingBandwidth, outgoingBandwidth) / (uint)ENetConstant.ENET_PEER_WINDOW_SIZE_SCALE) * 
                    (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;
            else
                peer.windowSize = (Math.Min(peer.incomingBandwidth, outgoingBandwidth) / (uint)ENetConstant.ENET_PEER_WINDOW_SIZE_SCALE) *
                    (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;

            if (peer.windowSize < (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE)
                peer.windowSize = (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;
            else if (peer.windowSize > (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE)
                peer.windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;

            return true;
        }

        public bool ProtocolHandleDisconnect(ENetPeer peer, ENetProtocol command)
        {
            //already in disconnect progress
            if (peer.state == ENetPeerState.ENET_PEER_STATE_DISCONNECTED || peer.state == ENetPeerState.ENET_PEER_STATE_ZOMBIE ||
                peer.state == ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_DISCONNECT)
                return true;

            peer.ResetQueues();

            if (peer.state == ENetPeerState.ENET_PEER_STATE_CONNECTION_SUCCEEDED || peer.state == ENetPeerState.ENET_PEER_STATE_DISCONNECTING ||
                peer.state == ENetPeerState.ENET_PEER_STATE_CONNECTING)
                peer.Reset();
            else if (!peer.IsConnected)
            {
                if (peer.state == ENetPeerState.ENET_PEER_STATE_CONNECTION_PENDING) recalculateBandwidthLimits = true;
                peer.Reset();
            }
            else if ((command.header.command & (byte)ENetProtocolFlag.COMMAND_FLAG_ACKNOWLEDGE) > 0)
                ProtocolChangeState(peer, ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_DISCONNECT);
            else
                ProtocolDispatchState(peer, ENetPeerState.ENET_PEER_STATE_ZOMBIE);

            if (peer.state != ENetPeerState.ENET_PEER_STATE_DISCONNECTED)
                peer.eventData = ENetUtil.NetToHost(command.disconnect.data);

            return true;
        }

        public bool ProtocolHandleAcknowledge(ENetEvent @event, ENetPeer peer, ENetProtocol command)
        {
            if (peer.state == ENetPeerState.ENET_PEER_STATE_DISCONNECTED || peer.state == ENetPeerState.ENET_PEER_STATE_ZOMBIE)
                return true;

            uint receivedSentTime = ENetUtil.NetToHost(command.acknowledge.receivedSentTime);
            receivedSentTime |= serviceTime & 0xFFFF0000;
            if ((receivedSentTime & 0x8000) > (serviceTime & 0x8000))
                receivedSentTime -= 0x10000;

            if (ENetTime.Less(serviceTime, receivedSentTime))
                return true;

            peer.lastReceiveTime = serviceTime;
            peer.earliestTimeout = 0;
            uint roundTripTime = ENetTime.Difference(serviceTime, receivedSentTime);

            peer.Throttle(roundTripTime);

            peer.roundTripTimeVariance -= peer.roundTripTimeVariance / 4;

            if(roundTripTime >= peer.roundTripTime)
            {
                peer.roundTripTime          += (roundTripTime - peer.roundTripTime) / 8;
                peer.roundTripTimeVariance  += (roundTripTime - peer.roundTripTime) / 4;
            }
            else
            {
                peer.roundTripTime          -= (peer.roundTripTime - roundTripTime) / 8;
                peer.roundTripTimeVariance  += (peer.roundTripTime - roundTripTime) / 4;
            }

            if (peer.roundTripTime < peer.lowestRoundTripTime)
                peer.lowestRoundTripTime = peer.roundTripTime;

            if (peer.roundTripTimeVariance < peer.highestRoundTripTimeVariance)
                peer.highestRoundTripTimeVariance = peer.roundTripTimeVariance;

            if(peer.packetThrottleEpoch == 0 || ENetTime.Difference(serviceTime, peer.packetThrottleEpoch) >= peer.packetThrottleInterval)
            {
                peer.lastRoundTripTime = peer.lowestRoundTripTime;
                peer.lastRoundTripTimeVariance = peer.highestRoundTripTimeVariance;
                peer.lowestRoundTripTime = peer.roundTripTime;
                peer.highestRoundTripTimeVariance = peer.roundTripTimeVariance;
                peer.packetThrottleEpoch = serviceTime;
            }

            ushort receivedReliableSequenceNumber = ENetUtil.NetToHost(command.acknowledge.receivedReliableSequenceNumber);
            ENetProtocolCommand commandNumber = ProtocolRemoveSentReliableCommand(peer, receivedReliableSequenceNumber, command.header.channelID);

            switch(peer.state)
            {
                case ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_CONNECT:
                    if (commandNumber != ENetProtocolCommand.VERIFY_CONNECT)
                        return false;

                    ProtocolNotifyConnect(peer, @event);
                    break;

                case ENetPeerState.ENET_PEER_STATE_DISCONNECTING:
                    if (commandNumber != ENetProtocolCommand.DISCONNECT)
                        return false;

                    ProtocolNotifyDisconnect(peer, @event);
                    break;

                case ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER:
                    if (peer.outgoingReliableCommands.IsEmpty && peer.outgoingUnreliableCommands.IsEmpty && peer.sentReliableCommands.IsEmpty)
                        peer.Disconnect(peer.eventData);
                    break;

                default:
                    break;
            }

            return true;
        }

        public bool ProtocolHandleVerifyConnect(ENetEvent @event, ENetPeer peer, ENetProtocol command)
        {
            if (peer.state != ENetPeerState.ENET_PEER_STATE_CONNECTING)
                return true;

            uint channelCount = ENetUtil.NetToHost(command.verifyConnect.channelCount);

            if (channelCount < (uint)ENetProtocolConstant.MINIMUM_CHANNEL_COUNT || channelCount > (uint)ENetProtocolConstant.MAXIMUM_CHANNEL_COUNT ||
                ENetUtil.NetToHost(command.verifyConnect.packetThrottleInterval) != peer.packetThrottleInterval ||
                ENetUtil.NetToHost(command.verifyConnect.packetThrottleAcceleration) != peer.packetThrottleAcceleration ||
                ENetUtil.NetToHost(command.verifyConnect.packetThrottleDeceleration) != peer.packetThrottleDeceleration ||
                command.verifyConnect.connectID != peer.connectID)
            {
                peer.eventData = 0;
                ProtocolDispatchState(peer, ENetPeerState.ENET_PEER_STATE_ZOMBIE);
                return false;
            }

            ProtocolRemoveSentReliableCommand(peer, 1, 0xFF);

            if (channelCount < peer.channelCount)
                peer.channelCount = (int)channelCount;

            peer.outgoingPeerID = ENetUtil.NetToHost(command.verifyConnect.outgoingPeerID);
            peer.incomingSessionID = command.verifyConnect.incomingSessionID;
            peer.outgoingSessionID = command.verifyConnect.outgoingSessionID;

            uint mtu = ENetUtil.NetToHost(command.verifyConnect.mtu);

            if (mtu < (uint)ENetProtocolConstant.MINIMUM_MTU)
                mtu = (uint)ENetProtocolConstant.MINIMUM_MTU;
            else if(mtu > (uint)ENetProtocolConstant.MAXIMUM_MTU)
                mtu = (uint)ENetProtocolConstant.MAXIMUM_MTU;

            if (mtu < peer.mtu)
                peer.mtu = mtu;

            uint windowSize = ENetUtil.NetToHost(command.verifyConnect.windowSize);
            if (windowSize < (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE)
                windowSize = (uint)ENetProtocolConstant.MINIMUM_WINDOW_SIZE;

            if (windowSize > (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE)
                windowSize = (uint)ENetProtocolConstant.MAXIMUM_WINDOW_SIZE;

            if (windowSize < peer.windowSize)
                peer.windowSize = windowSize;

            peer.incomingBandwidth = ENetUtil.NetToHost(command.verifyConnect.incomingBandwidth);
            peer.outgoingBandwidth = ENetUtil.NetToHost(command.verifyConnect.outgoingBandwidth);

            ProtocolNotifyConnect(peer, @event);
            return true;
        }

        public int ProtocolHandleIncomingCommands(ENetEvent @event)
        {
            if (receivedDataLength < 2)
                return 0;

            var header = ENetUtil.ReadStructure<ENetProtocolHeader>(receivedData);

            ushort peerID = ENetUtil.NetToHost(header.peerID);
            byte sessionID = (byte)((peerID & (int)ENetProtocolFlag.HEADER_SESSION_MASK) >> (int)ENetProtocolFlag.HEADER_SESSION_SHIFT);
            ushort flags = (ushort)(peerID & (uint)ENetProtocolFlag.HEADER_FLAG_MASK);
            peerID = (ushort)(peerID & (uint)ENetProtocolFlag.PEER_ID_MASK);

            int headerSize = (flags & (uint)ENetProtocolFlag.HEADER_FLAG_SENT_TIME) > 0 ? SizeOf<ENetProtocolHeader>.Value : 2;

            if(checksum != null)
                headerSize += sizeof(uint);

            ENetPeer peer;

            if (peerID == (ushort)ENetProtocolConstant.MAXIMUM_PEER_ID)
                peer = null;
            else if (peerID >= peerCount)
                return 0;
            else
            {
                peer = peers[peerID];

                if (peer.state == ENetPeerState.ENET_PEER_STATE_DISCONNECTED || peer.state == ENetPeerState.ENET_PEER_STATE_ZOMBIE ||
                    receivedAddress != peer.address || (peer.outgoingPeerID < (ushort)ENetProtocolConstant.MAXIMUM_PEER_ID && sessionID != peer.incomingSessionID))
                    return 0;
            }

            //continue...
        }

        public void Flush()
        {
            serviceTime = ENetTime.GetTime();
            //TODO
            //enet_protocol_send_outgoing_commands(host, NULL, 0);
        }


    }
}
