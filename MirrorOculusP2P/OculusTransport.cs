﻿using System;
using Mirror;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

namespace Mirror.OculusP2P
{
    public class OculusTransport : Transport
    {
        private User _user;
        private const string STEAM_SCHEME = "steam";

        private static IClient client;
        private static IServer server;

        private float _lastPing;

        public void Update()
        {
            if (ClientActive())
            {
                if (Time.realtimeSinceStartup - _lastPing > 0.3)
                {
                    _lastPing = Time.realtimeSinceStartup;
                    client.Ping();
                }
            }
        }

        public void LoggedIn(User user)
        {
            _user = user;
        }

        public override void ClientEarlyUpdate()
        {
            if (enabled)
            {
                client?.ReceiveData();
            }
        }

        public override void ServerEarlyUpdate()
        {
            if (enabled)
            {
                server?.ReceiveData();
            }
        }

        public override void ClientLateUpdate()
        {
            if (enabled)
            {
                client?.FlushData();
            }
        }

        public override void ServerLateUpdate()
        {
            if (enabled)
            {
                server?.FlushData();
            }
        }

        public override bool ClientConnected() => ClientActive() && client.Connected;

        public override void ClientConnect(string address)
        {
            if (!Core.IsInitialized())
            {
                Debug.LogError("SteamWorks not initialized. Client could not be started.");
                OnClientDisconnected.Invoke();
                return;
            }

            if (ServerActive())
            {
                Debug.LogError("Transport already running as server!");
                return;
            }

            if (!ClientActive() || client.Error)
            {
                Debug.Log($"Starting client [SteamSockets], target address {address}.");
                client = OculusClient.CreateClient(this, address);
            }
            else
            {
                Debug.LogError("Client already running!");
            }
        }

        public override void ClientConnect(Uri uri)
        {
            if (uri.Scheme != STEAM_SCHEME)
                throw new ArgumentException($"Invalid url {uri}, use {STEAM_SCHEME}://SteamID instead", nameof(uri));

            ClientConnect(uri.Host);
        }

        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            byte[] data = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
            client.Send(data, channelId);
        }

        public override void ClientDisconnect()
        {
            if (ClientActive())
            {
                Shutdown();
            }
        }

        public bool ClientActive() => client != null;

        public override bool ServerActive() => server != null;

        public override void ServerStart()
        {
            if (!Core.IsInitialized())
            {
                Debug.LogError("SteamWorks not initialized. Server could not be started.");
                return;
            }

            if (ClientActive())
            {
                Debug.LogError("Transport already running as client!");
                return;
            }

            if (!ServerActive())
            {
                Debug.Log($"Starting server [SteamSockets].");
                server = OculusServer.CreateServer(this, NetworkManager.singleton.maxConnections);
            }
            else
            {
                Debug.LogError("Server already started!");
            }
        }

        public override Uri ServerUri()
        {
            return new Uri(_user.ID.ToString());
        }

        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            if (ServerActive())
            {
                byte[] data = new byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
                server.Send(connectionId, data, channelId);
            }
        }

        public override bool ServerDisconnect(int connectionId) => ServerActive() && server.Disconnect(connectionId);
        public override string ServerGetClientAddress(int connectionId) => ServerActive() ? server.ServerGetClientAddress(connectionId) : string.Empty;

        public override void ServerStop()
        {
            if (ServerActive())
            {
                Shutdown();
            }
        }

        public override void Shutdown()
        {
            if (server != null)
            {
                server.Shutdown();
                server = null;
                Debug.Log("Transport shut down - was server.");
            }

            if (client != null)
            {
                client.Disconnect();
                client = null;
                Debug.Log("Transport shut down - was client.");
            }
        }

        public override int GetMaxPacketSize(int channelId)
        {
            switch (channelId)
            {
                case Mirror.Channels.Reliable:
                    return OculusCommon.ReliableMaxMessageSize;
                case Mirror.Channels.Unreliable:
                    return OculusCommon.UnreliableMaxMessageSize;
                default:
                    OculusLogWarning("Unknown channel");
                    return OculusCommon.UnreliableMaxMessageSize;
            }
        }

        public override bool Available()
        {
            try
            {
                return (Core.IsInitialized() && _user != null);
            }
            catch
            {
                return false;
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        #region Logging

        private void OculusLog(string msg)
        {
            Debug.Log("<color=orange>OculusTransport: </color>: " + msg);
        }

        private void OculusLogWarning(string msg)
        {
            Debug.LogWarning("<color=orange>OculusTransport: </color>: " + msg);
        }

        private void OculusLogError(string msg)
        {
            Debug.LogError("<color=orange>OculusTransport: </color>: " + msg);
        }

        #endregion
    }
}