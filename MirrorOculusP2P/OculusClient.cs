﻿using System;
using System.Collections.Generic;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

namespace Mirror.OculusP2P
{
    public class OculusClient : OculusCommon, IClient
    {
        public bool Connected { get; private set; }
        public bool Error { get; private set; }

        private TimeSpan ConnectionTimeout;

        private event Action<byte[], int> OnReceivedData;
        private event Action OnConnected;
        private event Action OnDisconnected;

        private ulong HostID;
        private List<Action> BufferedData;

        public void Ping()
        {
            Net.Ping(HostID).OnComplete(a =>
            {
                if (a.IsError)
                {
                    OculusLogWarning(a.GetError().Message);
                }
                else
                {
                    if (a.Data.IsTimeout)
                    {
                        OculusLogWarning("Timeout");
                    }
                    else
                    {
                        OculusLog($"Ping {a.Data.PingTimeUsec}");
                    }
                }
            });
        }

        private OculusClient()
        {
            BufferedData = new List<Action>();
        }

        public static OculusClient CreateClient(OculusTransport transport, string host)
        {
            var c = new OculusClient();

            c.OnConnected += () => transport.OnClientConnected.Invoke();
            c.OnDisconnected += () => transport.OnClientDisconnected.Invoke();
            c.OnReceivedData += (data, ch) => transport.OnClientDataReceived.Invoke(new ArraySegment<byte>(data), ch);

            if (ulong.TryParse(host, out var id))
            {
                if (Core.IsInitialized())
                {
                    c.Connect(host);
                }
                else
                {
                    OculusLogError("Oculus platform not initialized");
                    c.OnConnectionFailed();
                }
            }
            else
            {
                OculusLogError($"Can't parse ({host}) to ulong");
                c.OnConnectionFailed();
            }

            return c;
        }

        private void Connect(string host)
        {
            if (ulong.TryParse(host, out ulong userId))
            {
                HostID = userId;
                Net.SetConnectionStateChangedCallback(OnConnectionStatusChanged);
                Net.Connect(HostID);
            }
            else
            {
                OculusLogError($"Could not parse {host} to ulong");
                Error = true;
                OnConnectionFailed();
            }
        }

        private void OnConnectionStatusChanged(Message<NetworkingPeer> message)
        {
            // OnConnectionFailed();

            Debug.Log($"Connection state changed: {message.Data.State}");
            switch (message.Data.State)
            {
                case PeerConnectionState.Unknown:
                    break;
                case PeerConnectionState.Connected:
                    Connected = true;
                    OnConnected.Invoke();
                    Debug.Log("Connection established.");

                    if (BufferedData.Count > 0)
                    {
                        Debug.Log($"{BufferedData.Count} received before connection was established. Processing now.");
                        {
                            foreach (Action a in BufferedData)
                            {
                                a();
                            }
                        }
                    }

                    break;
                case PeerConnectionState.Timeout:
                    break;
                case PeerConnectionState.Closed:
                    Disconnect();
                    break;
            }
        }

        public void Disconnect()
        {
            Dispose();

            if (Net.IsConnected(HostID))
            {
                Debug.Log("Sending Disconnect message");
                Net.Close(HostID);
            }
        }

        protected void Dispose()
        {
            Net.SetConnectionStateChangedCallback(_ => { });
            DisposeAllPackets();
        }

        private void InternalDisconnect()
        {
            Connected = false;
            OnDisconnected.Invoke();
            Debug.Log("Disconnected.");
            Net.Close(HostID);
        }

        public void ReceiveData()
        {
            Packet packet;
            while ((packet = Net.ReadPacket()) != null)
            {
                (byte[] data, int ch) = ProcessPacket(packet);
                if (Connected)
                {
                    OnReceivedData(data, ch);
                }
                else
                {
                    BufferedData.Add(() => OnReceivedData(data, ch));
                }
            }
        }

        public void Send(byte[] data, int channelId)
        {
            var sent = SendPacket(HostID, data, channelId);

            if (!sent)
            {
                OculusLogError($"Could not send");
                InternalDisconnect();
            }
        }

        private void OnConnectionFailed() => OnDisconnected.Invoke();

        public void FlushData() { }

        #region Logging

        private static void OculusLog(string msg)
        {
            Debug.Log("<color=green>OculusClient: </color>: " + msg);
        }

        private static void OculusLogWarning(string msg)
        {
            Debug.LogWarning("<color=green>OculusClient: </color>: " + msg);
        }

        private static void OculusLogError(string msg)
        {
            Debug.LogError("<color=green>OculusClient: </color>: " + msg);
        }

        #endregion
    }
}