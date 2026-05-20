using Google.Protobuf;

/// <summary>
/// Hot-update network message helpers.
/// Add Send/Parse methods for new hot-update protocols here.
/// 
/// Usage:
///   1. Add message IDs in HotUpdateMessageConst.RegisterTo()
///   2. Add SendXxx() methods here to build and send C2S messages
///   3. Add ParseXxx() methods here to decode S2C body bytes
///   4. In the relevant hot-update Proxy, register the dispatcher in OnRegister():
///        NetworkManager.Instance.Dispatcher.Register(MessageConst.CHAT_S2C, OnChatS2C);
/// </summary>
public static class HotNetworkMessageHelper
{
    // -------------------------------------------------------------------------
    // Player Extension (hot-update)
    // -------------------------------------------------------------------------

    public static void SendPlayerExt(long accountId)
    {
        var msg = new PlayerExtC2S { AccountId = accountId };
        NetworkManager.Instance.Send(HotUpdateMessageConst.PLAYER_EXT_C2S, msg);
    }

    public static PlayerExtS2C ParsePlayerExtS2C(byte[] body)
        => PlayerExtS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Example: Chat
    // -------------------------------------------------------------------------

    /*
    public static void SendChat(int accountId, string channel, string message)
    {
        var msg = new ChatC2S
        {
            AccountId = accountId,
            Channel = channel,
            Message = message
        };
        NetworkManager.Instance.Send(MessageConst.CHAT_C2S, msg);
    }

    public static ChatS2C ParseChatS2C(byte[] body)
        => ChatS2C.Parser.ParseFrom(body);
    */

    /// <summary>
    /// Parse bytes into a protobuf message of type T.
    /// </summary>
    public static T Parse<T>(byte[] body) where T : IMessage<T>, new()
    {
        return new MessageParser<T>(() => new T()).ParseFrom(body);
    }
}
