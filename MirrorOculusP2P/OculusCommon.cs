using Oculus.Platform;
using UnityEngine;

namespace Mirror.OculusP2P
{
    public class OculusCommon
    {
        public static bool CanParseId(string address)
        {
            if (ulong.TryParse(address, out ulong _))
            {
                return true;
            }

            return false;
        }

        public const int ReliableMaxMessageSize = 65535;

        public const int UnreliableMaxMessageSize = 1200;

        protected bool SendPacket(ulong userId, byte[] data, int channelId)
        {
            switch (channelId)
            {
                case Channels.Reliable:
                    return Net.SendPacket(userId, data, SendPolicy.Reliable);
                case Channels.Unreliable:
                    return Net.SendPacket(userId, data, SendPolicy.Unreliable);
                default:
                    OculusLogError("Unknown send policy. Defaulting to reliable.");
                    return Net.SendPacket(userId, data, SendPolicy.Reliable);
            }
        }

        protected (byte[], int) ProcessPacket(Packet packet)
        {
            int channel;
            switch (packet.Policy)
            {
                case SendPolicy.Unreliable:
                    channel = Channels.Unreliable;
                    break;
                case SendPolicy.Reliable:
                    channel = Channels.Reliable;
                    break;
                default:
                    channel = Channels.Reliable;
                    OculusLogWarning("Unknown packet policy, defaulting to reliable");
                    break;
            }

            byte[] managedArray = new byte[packet.Size];
            packet.ReadBytes(managedArray);
            packet.Dispose();
            return (managedArray, channel);
        }

        public static void DisposeAllPackets()
        {
            Packet packet;
            while ((packet = Net.ReadPacket()) != null)
            {
                packet.Dispose();
            }
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
}