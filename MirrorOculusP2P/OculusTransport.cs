using System;
using Mirror;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

namespace Mirror.OculusP2P
{
    public class OculusTransport : Transport
    {
        private User _user;

        private static IClient client;
        private static IServer server;

        public void LoggedIn(User user)
        {
            _user = user;
        }

        public void LateUpdate()
        {
            if (!enabled)
            {
                return;
            }
            client?.ReceiveData();
            server?.ReceiveData();
        }

        public override bool ClientConnected() => ClientActive() && client.Connected;

        public override void ClientConnect(string address)
        {
            if (!Core.IsInitialized())
            {
                Debug.LogError("Oculus not initialized. Client could not be started.");
                OnClientDisconnected.Invoke();
                return;
            }

            if (ServerActive())
            {
                Debug.LogError("Transport already running as server!");
                return;
            }

            if (client == null && (!ClientActive() || client.Error))
            {
                Debug.Log($"Starting client [Oculus], target address {address}.");
                client = OculusClient.CreateClient(this, address);
            }
            else
            {
                Debug.LogError("Client already running!");
            }
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            byte[] data = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
            
            // todo: can this be null?
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
                Debug.LogError("Oculus not initialized. Server could not be started.");
                return;
            }

            if (ClientActive())
            {
                Debug.LogError("Transport already running as client!");
                return;
            }

            if (!ServerActive())
            {
                Debug.Log($"Starting server [Oculus].");
                server = OculusServer.CreateServer(this, NetworkManager.singleton.maxConnections);
            }
            else
            {
                Debug.LogError("Server already started!");
            }
        }

        public override Uri ServerUri()
        {
            return new Uri(_user.ID.ToString
                ());
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (ServerActive())
            {
                byte[] data = new byte[segment.Count];
                Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
                server.Send(connectionId, data, channelId);
            }
        }

        //public override bool ServerDisconnect(int connectionId) => ServerActive() && server.Disconnect(connectionId);
        public override void ServerDisconnect(int connectionId)  => server.Disconnect(connectionId);


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