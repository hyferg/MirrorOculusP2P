using System;
using Oculus.Platform;
using Oculus.Platform.Models;
using Debug = UnityEngine.Debug;

namespace Mirror.OculusP2P
{
    public class OculusServer : OculusCommon, IServer
    {
        private event Action<int> OnConnected;
        private event Action<int, byte[], int> OnReceivedData;
        private event Action<int> OnDisconnected;
        private event Action<int, Exception> OnReceivedError;

        private readonly BidirectionalDictionary<ulong, int> _oculusIDToMirrorID;
        private readonly int _maxConnections;
        private int _nextConnectionID;

        private OculusServer(int maxConnections)
        {
            _maxConnections = maxConnections;
            _oculusIDToMirrorID = new BidirectionalDictionary<ulong, int>();
            _nextConnectionID = 1;
            Net.SetPeerConnectRequestCallback(OnPeerConnectRequest);
            Net.SetConnectionStateChangedCallback(OnConnectionStatusChanged);
        }

        public static OculusServer CreateServer(OculusTransport transport, int maxConnections)
        {
            OculusServer s = new OculusServer(maxConnections);

            s.OnConnected += (id) => transport.OnServerConnected.Invoke(id);
            s.OnDisconnected += (id) => transport.OnServerDisconnected.Invoke(id);
            s.OnReceivedData += (id, data, ch) => transport.OnServerDataReceived.Invoke(id, new ArraySegment<byte>(data), ch);
            s.OnReceivedError += (id, exception) => transport.OnServerError.Invoke(id, exception);

            if (!Core.IsInitialized())
            {
                OculusLogError("Oculus platform not initialized.");
            }

            return s;
        }

        private void OnPeerConnectRequest(Message<NetworkingPeer> message)
        {
            var oculusId = message.Data.ID;
            if (_oculusIDToMirrorID.TryGetValue(oculusId, out int _))
            {
                OculusLogError($"Incoming connection {oculusId} already exists");
            }
            else
            {
                if (_oculusIDToMirrorID.Count >= _maxConnections)
                {
                    OculusLog($"Incoming connection {oculusId} would exceed max connection count. Rejecting.");
                }
                else
                {
                    OculusLog($"Accept connection {oculusId}");
                    Net.Accept(oculusId);
                }
            }

            Net.Accept(oculusId);
        }

        private void OnConnectionStatusChanged(Message<NetworkingPeer> message)
        {
            var oculusId = message.Data.ID;

            switch (message.Data.State)
            {
                case PeerConnectionState.Unknown:
                    break;
                case PeerConnectionState.Connected:
                    int connectionId = _nextConnectionID++;
                    _oculusIDToMirrorID.Add(oculusId, connectionId);
                    OnConnected.Invoke(connectionId);
                    OculusLog($"Client with OculusID {oculusId} connected. Assigning connection id {connectionId}");

                    break;
                case PeerConnectionState.Timeout:
                    Net.Connect(oculusId);
                    break;
                case PeerConnectionState.Closed:
                    if (_oculusIDToMirrorID.TryGetValue(oculusId, out int connId))
                    {
                        InternalDisconnect(connId, oculusId);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void InternalDisconnect(int connId, ulong userId)
        {
            if (_oculusIDToMirrorID.TryGetValue(userId, out int _))
            {
                _oculusIDToMirrorID.Remove(connId);
                OnDisconnected.Invoke(connId);
            }
            else
            {
                OculusLogWarning($"Nothing to disconnect");
            }
        }

        public bool Disconnect(int connectionId)
        {
            if (_oculusIDToMirrorID.TryGetValue(connectionId, out ulong userId))
            {
                OculusLog($"Closing connection {connectionId}");
                Net.Close(userId);
                //_oculusIDToMirrorID.Remove(connectionId);
                return true;
            }
            else
            {
                OculusLogWarning("Trying to disconnect unknown connection id: " + connectionId);
                return false;
            }
        }

        public void FlushData() { }

        public void ReceiveData()
        {
            Packet packet;
            while ((packet = Net.ReadPacket()) != null)
            {
                if (_oculusIDToMirrorID.TryGetValue(packet.SenderID, out int connId))
                {
                    (byte[] data, int ch) = ProcessPacket(packet);
                    OnReceivedData(connId, data, ch);
                }
                else
                {
                    Debug.LogWarning("Ignoring packet from sender not in dictionary");
                }
                
            }
        }

        public void Send(int connectionId, byte[] data, int channelId)
        {
            if (_oculusIDToMirrorID.TryGetValue(connectionId, out ulong userId))
            {
                var sent = SendPacket(userId, data, channelId);

                if (!sent)
                {
                    OculusLogError($"Could not send");
                }
            }
            else
            {
                OculusLogError("Trying to send on unknown connection: " + connectionId);
                OnReceivedError.Invoke(connectionId, new Exception("ERROR Unknown Connection"));
            }
        }

        public string ServerGetClientAddress(int connectionId)
        {
            if (_oculusIDToMirrorID.TryGetValue(connectionId, out ulong userId))
            {
                return userId.ToString();
            }
            else
            {
                OculusLogError("Trying to get info on unknown connection: " + connectionId);
                OnReceivedError.Invoke(connectionId, new Exception("ERROR Unknown Connection"));
                return string.Empty;
            }
        }

        public void Shutdown()
        {
            Net.SetPeerConnectRequestCallback(_ => { });
            Net.SetConnectionStateChangedCallback(_ => { });
            DisposeAllPackets();
        }

        #region Logging

        private static void OculusLog(string msg)
        {
            Debug.Log("<color=orange>OculusServer: </color>: " + msg);
        }

        private static void OculusLogWarning(string msg)
        {
            Debug.LogWarning("<color=orange>OculusServer: </color>: " + msg);
        }

        private static void OculusLogError(string msg)
        {
            Debug.LogError("<color=orange>OculusServer: </color>: " + msg);
        }

        #endregion
    }
}