using System;
using Mirror;
using Oculus.Platform.Samples.VrVoiceChat;
using UnityEngine;

public class OculusTransport : Transport
{
    public NetworkManagerGame manager;
    private OculusClient _client;
    private bool _ready;
    private OculusServer _server;

    private void Awake()
    {
        manager.LoggedIn += user =>
        {
            _client = new OculusClient
            {
                OnConnected = () => OnClientConnected.Invoke(),
                OnData = (message, channelId) => { OnClientDataReceived.Invoke(message, channelId); },
                OnDisconnected = () => OnClientDisconnected.Invoke()
            };
            _server = new OculusServer
            {
                OnConnected = connectionId => { OnServerConnected.Invoke(connectionId); },
                OnData = (connectionId, message, channelId) => { OnServerDataReceived.Invoke(connectionId, message, channelId); },
                OnDisconnected = connectionId => { OnServerDisconnected.Invoke(connectionId); }
            };

            _ready = true;
        };
    }

    private void OnEnable()
    {
        _client?.Unpause();
        _server?.Unpause();
    }

    private void OnDisable()
    {
        _client?.Pause();
        _server?.Pause();
    }

    public override bool Available()
    {
        return Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.WindowsEditor ||
               Application.platform == RuntimePlatform.WindowsPlayer;
    }

    public override bool ClientConnected()
    {
        return _client.Connected;
    }

    public override void ClientConnect(string address)
    {
        _client.Connect(address);
    }

    public override void ClientSend(int channelId, ArraySegment<byte> segment)
    {
        _client.Send(channelId, segment);
    }

    public override void ClientDisconnect()
    {
        _client.Disconnect();
    }

    public override void ClientEarlyUpdate()
    {
        if (enabled && _ready)
        {
            _client.TickIncoming();
        }
    }

    public override void ClientLateUpdate()
    {
        if (_ready)
        {
            _client.TickOutgoing();
        }
    }

    public override Uri ServerUri()
    {
        return new Uri(PlatformManager.MyID.ToString());
    }

    public override bool ServerActive()
    {
        return _server.IsActive();
    }

    public override void ServerStart()
    {
        _server.Start();
    }

    public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
    {
        _server.Send(connectionId, channelId, segment);
    }

    public override bool ServerDisconnect(int connectionId)
    {
        _server.Disconnect(connectionId);
        return true;
    }

    public override string ServerGetClientAddress(int connectionId)
    {
        return _server.GetClientAddress(connectionId);
    }

    public override void ServerStop()
    {
        _server.Stop();
    }

    public override void ServerEarlyUpdate()
    {
        if (enabled && _ready)
        {
            _server.TickIncoming();
        }
    }

    public override void ServerLateUpdate()
    {
        if (_ready)
        {
            _server.TickOutgoing();
        }
    }

    public override int GetMaxPacketSize(int channelId = Channels.Reliable)
    {
        switch (channelId)
        {
            case Channels.Unreliable:
                return OculusPeer.UnreliableMaxMessageSize;
            default:
                return OculusPeer.ReliableMaxMessageSize;
        }
    }

    //public override int GetMaxBatchSize(int channelId)

    public override void Shutdown() { }

    #region Logging

    private void OculusLog(string msg)
    {
        Debug.Log("<color=green>OculusTransport: </color>: " + msg);
    }

    private void OculusLogWarning(string msg)
    {
        Debug.LogWarning("<color=green>OculusTransport: </color>: " + msg);
    }

    private void OculusLogError(string msg)
    {
        Debug.LogError("<color=green>OculusTransport: </color>: " + msg);
    }

    #endregion
}