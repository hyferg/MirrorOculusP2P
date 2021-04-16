using System;
using System.Collections.Generic;
using MirrorOculusP2P;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

public class OculusServer
{
    private readonly Dictionary<int, OculusPeer> Connections = new Dictionary<int, OculusPeer>();
    private bool _active;
    private bool _paused;
    private List<Packet> _reliablePackets = new List<Packet>();

    public Action<int> OnConnected;
    public Action<int, ArraySegment<byte>> OnData;
    public Action<int> OnDisconnected;

    public OculusServer()
    {
        Net.SetPeerConnectRequestCallback(PeerConnectRequestCallback);
    }

    public bool IsActive()
    {
        return true;
    }

    public void Start()
    {
        _active = true;
        _reliablePackets.Clear();
        OculusPeer.DisposeAllPackets();
        Net.SetPeerConnectRequestCallback(PeerConnectRequestCallback);
        Net.SetConnectionStateChangedCallback(ConnectionStateChangedCallback);
    }

    private int GetConnectionID(ulong id)
    {
        return id.GetHashCode();
    }

    private void PeerConnectRequestCallback(Message<NetworkingPeer> msg)
    {
        OculusLog($"Connection state to {msg.Data.ID} changed to {msg.Data.State}");

        var connectionId = GetConnectionID(msg.Data.ID);
        if (!Connections.TryGetValue(connectionId, out var connection))
        {
            connection = new OculusPeer();
            OculusLog($"Server added connection ({connectionId}): {msg.Data.ID}");
            connection.OnConnected = () => { OnConnected.Invoke(connectionId); };
            connection.OnData = message => { OnData.Invoke(connectionId, message); };
            connection.OnDisconnected = () =>
            {
                Connections.Remove(connectionId);
                OculusLog($"OnServerDisconnected ({connectionId})");
                OnDisconnected.Invoke(connectionId);
            };
            connection.Accept(msg.Data.ID);
            Connections.Add(connectionId, connection);
        }
        else
        {
            OculusLogWarning($"Already a connection for this id ({connectionId})");
        }
    }

    private void ConnectionStateChangedCallback(Message<NetworkingPeer> msg)
    {
        var connectionId = GetConnectionID(msg.Data.ID);
        if (Connections.TryGetValue(connectionId, out var connection))
        {
            connection.ConnectionStateChange(msg);
        }
        else if (msg.Data.State == PeerConnectionState.Closed)
        {
            // Happens for example if you shut down a connection as a client and then start hosting
            // quickly before the callback reports that it closed down.
            OculusLog($"Unknown connection ({msg.Data.ID}) is now closed. This is most likely fine.");
        }
        else
        {
            OculusLogWarning($"Received a state change for connection not in connections ({connectionId}) ({msg.Data.ID}) ({msg.Data.State})");
        }
    }

    public void Send(int connectionId, OculusChannel channelId, ArraySegment<byte> segment)
    {
        if (Connections.TryGetValue(connectionId, out var connection))
        {
            connection.Send(segment, channelId);
        }
        else
        {
            OculusLogWarning("Could not find connection to send message to");
        }
    }

    public void Disconnect(int connectionId)
    {
        if (Connections.TryGetValue(connectionId, out var connection))
        {
            connection.Disconnect();
        }
    }

    public string GetClientAddress(int connectionId)
    {
        if (Connections.TryGetValue(connectionId, out var connection))
        {
            return connection.GetID();
        }

        return "";
    }

    public void Stop()
    {
        _active = false;
        if (Connections.Count > 0)
        {
            OculusLogWarning($"Did not disconnect ({Connections.Count}) connections before stopping");
            Connections.Clear();
        }
    }

    private void ProcessQueue()
    {
        if (_reliablePackets.Count > 0)
        {
            var oldPackets = new List<Packet>();
            foreach (var reliablePacket in _reliablePackets)
            {
                var connectionId = GetConnectionID(reliablePacket.SenderID);
                var processed = false;

                if (Connections.TryGetValue(connectionId, out var connection))
                {
                    if (connection.Connected)
                    {
                        connection.ProcessPacket(reliablePacket);
                        processed = true;
                    }
                }
                else
                {
                    OculusLogWarning("Dropping packet for unknown connection");
                    processed = true;
                }

                if (!processed)
                {
                    oldPackets.Add(reliablePacket);
                }
            }

            _reliablePackets = oldPackets;
        }
    }

    public void TickIncoming()
    {
        if (_active)
        {
            if (!_paused)
            {
                ProcessQueue();
            }

            RawInput();
        }
    }

    public void TickOutgoing()
    {
        if (_active && !_paused)
        {
            foreach (var connection in Connections.Values)
            {
                connection.TickOutgoing();
            }
        }
    }

    public void Pause()
    {
        _paused = true;
    }

    public void Unpause()
    {
        _paused = false;
    }

    private void ProcessPacket(Packet packet)
    {
        var connectionId = GetConnectionID(packet.SenderID);
        if (Connections.TryGetValue(connectionId, out var connection))
        {
            connection.ProcessPacket(packet);
        }
        else
        {
            OculusLogWarning("Could not find connection to process packet");
            packet.Dispose();
        }
    }

    private bool ConnectedAndReady(ulong senderId)
    {
        var connectionId = GetConnectionID(senderId);
        if (Connections.TryGetValue(connectionId, out var connection))
        {
            if (connection.Connected)
            {
                return true;
            }
        }

        return false;
    }

    private void RawInput()
    {
        Packet packet;
        while ((packet = Net.ReadPacket()) != null)
        {
            switch (packet.Policy)
            {
                case SendPolicy.Unreliable:
                    if (!_paused && ConnectedAndReady(packet.SenderID))
                    {
                        ProcessPacket(packet);
                    }
                    else
                    {
                        packet.Dispose();
                    }

                    break;
                case SendPolicy.Reliable:
                    if (!_paused && ConnectedAndReady(packet.SenderID))
                    {
                        ProcessPacket(packet);
                    }
                    else
                    {
                        _reliablePackets.Add(packet);
                    }

                    break;
                default:
                    OculusLogWarning("Packet policy unknown, disposing");
                    packet.Dispose();
                    break;
            }
        }
    }

    #region Logging

    private void OculusLog(string msg)
    {
        Debug.Log("<color=orange>OculusServer: </color>: " + msg);
    }

    private void OculusLogWarning(string msg)
    {
        Debug.LogWarning("<color=orange>OculusServer: </color>: " + msg);
    }

    private void OculusLogError(string msg)
    {
        Debug.LogError("<color=orange>OculusServer: </color>: " + msg);
    }

    #endregion
}