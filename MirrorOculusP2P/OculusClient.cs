using System;
using MirrorOculusP2P;
using UnityEngine;

public class OculusClient
{
    private OculusPeer _connection;
    public bool Connected;

    public Action OnConnected;
    public Action<ArraySegment<byte>> OnData;
    public Action OnDisconnected;

    public static bool CanParseId(string userIDStr)
    {
        try
        {
            var _ = ulong.Parse(userIDStr);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Connect(string userIDStr)
    {
        if (Connected)
        {
            OculusLogWarning("Client already connected");
            return;
        }

        OculusPeer.DisposeAllPackets();
        _connection = new OculusPeer
        {
            OnConnected = () =>
            {
                OculusLog("OnClientConnected");
                Connected = true;
                OnConnected.Invoke();
            },
            OnData = message => { OnData.Invoke(message); },
            OnDisconnected = () =>
            {
                OculusLog("OnClientDisconnected");
                Connected = false;
                _connection = null;
                OnDisconnected.Invoke();
            }
        };

        try
        {
            var userID = ulong.Parse(userIDStr);
            _connection.ConnectTo(userID);
        }
        catch (Exception e)
        {
            OculusLogError($"Error connecting to peer {e}");
        }
    }

    public void Send(ArraySegment<byte> segment, OculusChannel channel)
    {
        if (Connected)
        {
            _connection.Send(segment, channel);
        }
        else
        {
            OculusLogWarning("Can't send because client is not connected");
        }
    }

    public void Pause()
    {
        _connection?.Pause();
    }

    public void Unpause()
    {
        _connection?.Unpause();
    }

    public void Disconnect()
    {
        if (Connected)
        {
            _connection.Disconnect();
            Connected = false;
        }

        OculusPeer.DisposeAllPackets();
    }

    public void TickIncoming()
    {
        _connection?.TickIncoming();
    }

    public void TickOutgoing()
    {
        _connection?.TickOutgoing();
    }

    #region Logging

    private void OculusLog(string msg)
    {
        Debug.Log("<color=green>OculusClient: </color>: " + msg);
    }

    private void OculusLogWarning(string msg)
    {
        Debug.LogWarning("<color=green>OculusClient: </color>: " + msg);
    }

    private void OculusLogError(string msg)
    {
        Debug.LogError("<color=green>OculusClient: </color>: " + msg);
    }

    #endregion
}