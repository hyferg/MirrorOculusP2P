namespace Mirror.OculusP2P
{
  public interface IClient
  {
    bool Connected { get; }
    bool Error { get; }


    void ReceiveData();
    void Disconnect();
    void FlushData();
    void Send(byte[] data, int channelId);
    
  }
}