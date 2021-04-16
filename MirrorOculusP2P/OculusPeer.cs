using System;
using System.Collections.Generic;
using MirrorOculusP2P;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

public class OculusPeer
{
    public const int ReliableMaxMessageSize = 65535;
    public const int UnreliableMaxMessageSize = 1200;
    private readonly byte[] _receiveBuffer = new byte[ReliableMaxMessageSize];
    private readonly byte[] _sendBuffer = new byte[ReliableMaxMessageSize];
    private readonly Queue<Packet> _reliablePackets = new Queue<Packet>();
    private bool _paused;
    private ulong _remoteID;
    private PeerConnectionState _state = PeerConnectionState.Unknown;
    public Action OnConnected;
    public Action<ArraySegment<byte>> OnData;
    public Action OnDisconnected;

    public bool Connected => _state == PeerConnectionState.Connected;

    public void ConnectTo(ulong userID)
    {
        // By connecting to a user we are in implicitly in client mode so this class will be
        // the only thing that cares about state changes and the callback will not be overwritten
        _reliablePackets.Clear();
        _remoteID = userID;
        OculusLog($"Connect to ({_remoteID})");
        Net.SetConnectionStateChangedCallback(ConnectionStateChangedCallback);
        Net.Connect(userID);
    }

    public void Accept(ulong userID)
    {
        // Whatever calls this should be in charge of feeding updates into ConnectionStateChange.
        // This is because we can only have one callback for multiple connections so it can't be
        // handled in a callback inside of this class because there might be other OculusPeers when
        // acting as a server
        _reliablePackets.Clear();
        _remoteID = userID;
        Net.Accept(userID);
    }

    public void Disconnect()
    {
        if (_remoteID != 0)
        {
            OculusLog($"Disconnect from ({_remoteID})");
            OnDisconnected.Invoke();
            _state = PeerConnectionState.Unknown;
            Net.Close(_remoteID);
            _reliablePackets.Clear();
            _remoteID = 0;
        }
        else
        {
            // This is a problem with mirror
            // StopClient() calls both
            // NetworkClient.Disconnect();
            // NetworkClient.Shutdown();
            // which each call this method (Mirror 35.1.0)
            OculusLog("Disconnect called with no remote peer");
        }
    }

    private void ConnectionStateChangedCallback(Message<NetworkingPeer> msg)
    {
        ConnectionStateChange(msg);
    }

    public void ConnectionStateChange(Message<NetworkingPeer> msg)
    {
        OculusLog($"Connection state to ({msg.Data.ID}) changed to {msg.Data.State}");
        if (msg.Data.ID == _remoteID)
        {
            _state = msg.Data.State;
            switch (_state)
            {
                case PeerConnectionState.Unknown:
                    break;
                case PeerConnectionState.Connected:
                    OnConnected.Invoke();
                    break;
                case PeerConnectionState.Timeout:
                    Net.Connect(_remoteID);
                    break;
                case PeerConnectionState.Closed:
                    OnDisconnected.Invoke();
                    break;
            }
        }
    }

    public void Send(ArraySegment<byte> data, OculusChannel channel)
    {
        if (data.Count == 0)
        {
            OculusLogWarning("Tried to send empty message. This should never happen. Disconnecting.");
            Disconnect();
            return;
        }

        if (_state == PeerConnectionState.Connected)
        {
            switch (channel)
            {
                case OculusChannel.Reliable:
                    SendPacket(_remoteID, data, SendPolicy.Reliable);
                    break;
                case OculusChannel.Unreliable:
                    SendPacket(_remoteID, data, SendPolicy.Unreliable);
                    break;
            }
        }
        else
        {
            OculusLogWarning("Can't send because client not connected");
        }
    }

    private void ProcessQueue()
    {
        {
            while (_reliablePackets.Count > 0)
            {
                ProcessPacket(_reliablePackets.Dequeue());
            }
        }
    }

    public void TickIncoming()
    {
        if (_state == PeerConnectionState.Connected)
        {
            if (!_paused)
            {
                ProcessQueue();
            }

            RawInput();
        }
    }

    public void TickOutgoing() { }

    public void Pause()
    {
        _paused = true;
    }

    public void Unpause()
    {
        _paused = false;
    }

    public void ProcessPacket(Packet packet)
    {
        var msgLength = (int) packet.Size;
        if (msgLength <= ReliableMaxMessageSize)
        {
            packet.ReadBytes(_receiveBuffer);
            var message = new ArraySegment<byte>(_receiveBuffer, 0, msgLength);
            OnData.Invoke(message);
        }
        else
        {
            OculusLogError(
                $"ClientConnection: message of size {msgLength} does not fit into buffer of size {_receiveBuffer.Length}. The excess was silently dropped. Disconnecting.");
            Disconnect();
        }

        packet.Dispose();
    }

    private void RawInput()
    {
        Packet packet;
        while ((packet = Net.ReadPacket()) != null)
        {
            // todo check the packet id
            switch (packet.Policy)
            {
                case SendPolicy.Unreliable:
                    if (!_paused)
                    {
                        ProcessPacket(packet);
                    }
                    else
                    {
                        packet.Dispose();
                    }

                    break;
                case SendPolicy.Reliable:
                    if (!_paused)
                    {
                        ProcessPacket(packet);
                    }
                    else
                    {
                        _reliablePackets.Enqueue(packet);
                    }

                    break;
                default:
                    OculusLogWarning("Packet policy unknown, disposing");
                    packet.Dispose();
                    break;
            }
        }
    }

    public string GetID()
    {
        return _remoteID.ToString();
    }

    public static void DisposeAllPackets()
    {
        Packet packet;
        while ((packet = Net.ReadPacket()) != null)
        {
            packet.Dispose();
        }
    }

    private bool SendPacket(ulong userID, ArraySegment<byte> bytes, SendPolicy policy)
    {
        // modified from Platform.SendPacket to handle ArraySegment
        if (Core.IsInitialized())
        {
            for (var i = 0; i < bytes.Count; i++)
            {
                _sendBuffer[i] = bytes.Array[bytes.Offset + i];
            }

            return CAPI.ovr_Net_SendPacket(userID, (UIntPtr) bytes.Count, _sendBuffer, policy);
        }

        return false;
    }

    #region Logging

    private void OculusLog(string msg)
    {
        Debug.Log("<color=orange>OculusPeer: </color>: " + msg);
    }

    private void OculusLogWarning(string msg)
    {
        Debug.LogWarning("<color=orange>OculusPeer: </color>: " + msg);
    }

    private void OculusLogError(string msg)
    {
        Debug.LogError("<color=orange>OculusPeer: </color>: " + msg);
    }

    #endregion
}