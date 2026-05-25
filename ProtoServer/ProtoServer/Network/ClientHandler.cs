using Config;
using Google.Protobuf;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class ClientHandler : IDisposable
{
    private readonly TcpClient _client;
    private readonly TCPServer _server;
    private readonly CancellationToken _cancellationToken;
    private NetworkStream _stream;

    // Fix4: use volatile to ensure multi-thread visibility，avoid race condition
    private volatile bool _isDisposed = false;

    // Fix3: use SemaphoreSlim to ensure send stream serialization，prevent concurrent write packet corruption
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

    // Fix7: heartbeat timeout detection，record last heartbeat time
    private DateTime _lastHeartbeatTime = DateTime.UtcNow;
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(60);

    // Rate limiting: sliding-window per-client message count
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _rateLimitMap =
        new ConcurrentDictionary<string, RateLimitEntry>();
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(1);
    private const int RateLimitMaxMessages = 100;

    public string ClientId { get; } = Guid.NewGuid().ToString();
    public long AccountId { get; set; } = 0;
    public string RemoteEndPoint => _client.Client.RemoteEndPoint?.ToString() ?? "unknown";
    public bool IsConnected => !_isDisposed && _client.Connected && _stream?.CanRead == true && _stream?.CanWrite == true;

    public ClientHandler(TcpClient client, TCPServer server, CancellationToken cancellationToken)
    {
        _client = client;
        _server = server;
        _cancellationToken = cancellationToken;
        _stream = client.GetStream();
        Console.WriteLine($"[ClientHandler] client initialized: {ClientId} | {RemoteEndPoint}");
    }

    public async Task StartListeningAsync()
    {
        if (_isDisposed)
        {
            Console.WriteLine($"[ClientHandler] client {ClientId} disposed, cannot start listen");
            return;
        }

        var connectAck = new ConnectS2C
        {
            Rst = new S2CResult { Result = true },
        };
        await SendMessageAsync(EMessageType.CONNECT_S2C, connectAck);

        // Fix7: start heartbeat timeout detection background task
        _ = HeartbeatTimeoutCheckAsync(_cancellationToken);

        while (!_cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                // step1：read4bytesmessage type（big-endian）
                var typeBuffer = new byte[4];
                int typeRead = await ReadExactlyAsync(_stream, typeBuffer, 0, 4, _cancellationToken);
                if (typeRead < 4)
                {
                    Console.WriteLine($"[ClientHandler] client {ClientId} disconnected (message type read failed)");
                    Disconnect();
                    break;
                }
                if (BitConverter.IsLittleEndian) Array.Reverse(typeBuffer);
                int typeInt = BitConverter.ToInt32(typeBuffer, 0);
                if (!Enum.IsDefined(typeof(EMessageType), typeInt))
                {
                    Console.WriteLine($"[ClientHandler] client {ClientId} invalid message type: {typeInt}");
                    Disconnect();
                    break;
                }
                EMessageType messageType = (EMessageType)typeInt;

                // step2：read4bytesmessage bodylength（big-endian）
                var lengthBuffer = new byte[4];
                int lengthRead = await ReadExactlyAsync(_stream, lengthBuffer, 0, 4, _cancellationToken);
                if (lengthRead < 4)
                {
                    Console.WriteLine($"[ClientHandler] client {ClientId} disconnected (message length read failed)");
                    Disconnect();
                    break;
                }
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBuffer);
                int msgLength = BitConverter.ToInt32(lengthBuffer, 0);

                // Anti-abuse: limit message body max 1MB, min 1 byte
                if (msgLength <= 0 || msgLength > 1024 * 1024)
                {
                    Console.WriteLine($"[ClientHandler] client {ClientId} invalid message length: {msgLength} (exceeds 1MB limit)");
                    Disconnect();
                    break;
                }

                // step3: read complete message body
                var msgBuffer = new byte[msgLength];
                int bodyRead = await ReadExactlyAsync(_stream, msgBuffer, 0, msgLength, _cancellationToken);
                if (bodyRead < msgLength)
                {
                    Console.WriteLine($"[ClientHandler] client {ClientId} disconnected (message body read incomplete)");
                    Disconnect();
                    break;
                }

                // step4: parse and process message
                await ProcessMessageAsync(messageType, msgBuffer);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[ClientHandler] client {ClientId} listen cancelled");
                break;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine($"[ClientHandler] client {ClientId} stream disposed, stop listening");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientHandler] Unity client {ClientId} message read error: {ex.Message}");
                Disconnect();
                break;
            }
        }
    }

    // Fix7: heartbeat timeout detection，periodic checkwhetherexceeds HeartbeatTimeout no heartbeat received
    private async Task HeartbeatTimeoutCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                if (DateTime.UtcNow - _lastHeartbeatTime > HeartbeatTimeout)
                {
                    Console.WriteLine($"[ClientHandler] client {ClientId} heartbeat timeout (> {HeartbeatTimeout.TotalSeconds}s), force disconnect");
                    Disconnect();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal cancel, ignored
        }
    }

    private async Task<int> ReadExactlyAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException("offset or length out of buffer range");
        if (count == 0) return 0;

        int totalRead = 0;
        while (totalRead < count && !cancellationToken.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken);
            if (read == 0) break;
            totalRead += read;
        }
        return totalRead;
    }

    /// <summary>
    /// send message to client (packed in new format)
    /// </summary>
    /// <param name="messageType">message type</param>
    /// <param name="message">protobufmessage body</param>
    public async Task SendMessageAsync(EMessageType messageType, IMessage message = null)
    {
        if (_isDisposed)
        {
            Console.WriteLine($"send failed: client {ClientId} disposed");
            return;
        }
        if (!IsConnected)
        {
            Console.WriteLine($"send failed: client {ClientId} disconnected");
            return;
        }
        if (message == null)
        {
            Console.WriteLine($"send failed: client {ClientId} message body is empty");
            return;
        }

        // Fix3: lock, ensure single coroutine writes to stream，prevent concurrent write disorder
        await _sendLock.WaitAsync(_cancellationToken);
        try
        {
            // 1. Protobufmessage bodytobytes
            byte[] bodyBytes = message.ToByteArray();
            if (bodyBytes.Length > 1024 * 1024)
            {
                Console.WriteLine($"send failed: client {ClientId} message body exceeds 1MB limit");
                return;
            }

            // 2. message typeto4bytes（big-endian）
            byte[] typeBytes = BitConverter.GetBytes((int)messageType);
            if (BitConverter.IsLittleEndian) Array.Reverse(typeBytes);

            // 3. messagelengthto4bytes（big-endian）
            byte[] lenBytes = BitConverter.GetBytes(bodyBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);

            // 4. assemble packet：type(4) + length(4) + message body
            byte[] sendData = new byte[4 + 4 + bodyBytes.Length];
            Buffer.BlockCopy(typeBytes, 0, sendData, 0, 4);
            Buffer.BlockCopy(lenBytes, 0, sendData, 4, 4);
            Buffer.BlockCopy(bodyBytes, 0, sendData, 8, bodyBytes.Length);

            // 5. async send
            await _stream.WriteAsync(sendData, 0, sendData.Length, _cancellationToken);
            await _stream.FlushAsync(_cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"send message to {ClientId} error: {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Process message by type
    /// </summary>
    /// <param name="messageType">message type</param>
    /// <param name="messageBody">message body bytes</param>
    public async Task ProcessMessageAsync(EMessageType messageType, byte[] messageBody)
    {
        if (messageBody == null || messageBody.Length == 0)
        {
            Console.WriteLine($"[ClientHandler] client {ClientId} message body is empty, skip process");
            return;
        }

        // Rate limit check — sliding window per client
        if (!CheckRateLimit())
        {
            Console.WriteLine($"[ClientHandler] client {ClientId} exceeded rate limit ({RateLimitMaxMessages}/s), disconnecting");
            Disconnect();
            return;
        }

        try
        {
            switch (messageType)
            {
                case EMessageType.CONNECT_S2C:
                    Console.WriteLine($"[ClientHandler] client {ClientId} connection confirmed");
                    break;
                case EMessageType.CHAT_S2C:
                    // Client should not send S2C messages; reject silently
                    Console.WriteLine($"[ClientHandler] Unexpected CHAT_S2C from client {ClientId}, ignored");
                    break;
                case EMessageType.HEARTBEAT_C2S:
                    var heartbeatMsg = HeartbeatC2S.Parser.ParseFrom(messageBody);
                    _lastHeartbeatTime = DateTime.UtcNow;
                    // Console.WriteLine($"[ClientHandler] client {heartbeatMsg.AccountId} {ClientId} received heartbeat");
                    var heartbeatAck = new HeartbeatS2C
                    {
                        AccountId = heartbeatMsg.AccountId,
                        Rst = new S2CResult { Result = true }
                    };
                    await SendMessageAsync(EMessageType.HEARTBEAT_S2C, heartbeatAck);
                    break;
                case EMessageType.Disconnect:
                    Disconnect();
                    break;
                case EMessageType.LOGIN_C2S:
                    var loginMsg = LoginMessageC2S.Parser.ParseFrom(messageBody);
                    Console.WriteLine($"[ClientHandler] Client {ClientId} login request, account: {loginMsg.AccountId}");
                    var decryptedLoginPassword = AesHelper.DecryptString(loginMsg.Password) ?? loginMsg.Password;
                    var loginValid = await _server.DataStore.ValidateLoginAsync(loginMsg.AccountId, decryptedLoginPassword);
                    PlayerInfo playerData = null;
                    if (loginValid)
                    {
                        AccountId = loginMsg.AccountId;
                        _server.ClientManager.OnClientLoggedIn(ClientId, AccountId);
                        var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
                        if (!string.IsNullOrEmpty(pd.PlayerName))
                        {
                            playerData = new PlayerInfo
                            {
                                PlayerName = pd.PlayerName,
                                Gender = pd.Gender,
                                Job = pd.Job,
                                Level = pd.Level,
                                Exp = pd.Exp,
                                Gold = pd.Gold,
                                Diamond = pd.Diamond
                            };
                        }
                        Console.WriteLine($"[ClientHandler] Client {ClientId} login success, account: {AccountId}, hasPlayer: {playerData != null}");

                        // Push friend data after login
                        await PushFriendDataAsync();
                        // Push mail data after login
                        await PushMailDataAsync();
                        // Push announce data after login
                        await _server.PushAnnounceListAsync(this);
                        // Push sign-in data after login
                        await PushSignInInfoAsync();
                        // Push bag data after login
                        await PushBagListAsync();
                        // Push shop data after login
                        await PushShopListAsync();
                        // Init achievement progress and push after login
                        AchievementChecker.InitProgress(pd);
                        await CheckAndPushAchievements(pd, "PlayerLogin", 1);
                        await _server.DataStore.SavePlayerDataAsync(AccountId, pd);
                        await PushAchievementListAsync();
                    }
                    else
                    {
                        Console.WriteLine($"[ClientHandler] Client {ClientId} login failed, account: {loginMsg.AccountId}");
                    }
                    var loginAck = new LoginMessageS2C
                    {
                        AccountId = loginMsg.AccountId,
                        Rst = new S2CResult
                        {
                            Result = loginValid,
                            ErrCode = loginValid ? 0 : 1
                        },
                        PlayerData = playerData
                    };
                    await SendMessageAsync(EMessageType.LOGIN_S2C, loginAck);
                    break;
                case EMessageType.REGISTER_C2S:
                    var registerMsg = RegisterC2S.Parser.ParseFrom(messageBody);
                    Console.WriteLine($"[ClientHandler] Client {ClientId} register request, account: {registerMsg.AccountId}");
                    var decryptedRegPassword = AesHelper.DecryptString(registerMsg.Password) ?? registerMsg.Password;
                    var registerResult = await _server.DataStore.CreateAccountAsync(registerMsg.AccountId, decryptedRegPassword);
                    var registerAck = new RegisterS2C
                    {
                        AccountId = registerMsg.AccountId,
                        Rst = new S2CResult
                        {
                            Result = registerResult,
                            ErrCode = registerResult ? 0 : 1
                        }
                    };
                    await SendMessageAsync(EMessageType.REGISTER_S2C, registerAck);
                    break;
                case EMessageType.CREATE_PLAYER_C2S:
                    var createPlayerMsg = CreatePlayerC2S.Parser.ParseFrom(messageBody);
                    Console.WriteLine($"[ClientHandler] Client {ClientId} create player request, name: {createPlayerMsg.PlayerName}");
                    var createResult = await _server.DataStore.CreatePlayerAsync(AccountId, createPlayerMsg.PlayerName, createPlayerMsg.Gender, createPlayerMsg.Job);
                    var createPlayerAck = new CreatePlayerS2C
                    {
                        Rst = new S2CResult
                        {
                            Result = createResult,
                            ErrCode = createResult ? 0 : 1
                        },
                        PlayerName = createPlayerMsg.PlayerName,
                        Level = 1,
                        Exp = 0
                    };
                    await SendMessageAsync(EMessageType.CREATE_PLAYER_S2C, createPlayerAck);
                    break;
                case EMessageType.PLAYER_EXT_C2S:
                    var playerExtMsg = PlayerExtC2S.Parser.ParseFrom(messageBody);
                    Console.WriteLine($"[ClientHandler] Client {ClientId} player ext request, account: {playerExtMsg.AccountId}");
                    var pdExt = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var playerExtAck = new PlayerExtS2C
                    {
                        Rst = new S2CResult { Result = true },
                        VipLevel = pdExt.VipLevel,
                        Signature = pdExt.Signature ?? "",
                        BattlePassExp = pdExt.BattlePassExp
                    };
                    await SendMessageAsync(EMessageType.PLAYER_EXT_S2C, playerExtAck);
                    break;

                // ── Friend ──

                case EMessageType.FRIEND_SEARCH_C2S:
                    var searchMsg = FriendSearchC2S.Parser.ParseFrom(messageBody);
                    var searchResult = await _server.DataStore.SearchPlayerByNameAsync(searchMsg.Keyword);
                    var searchAck = new FriendSearchS2C { Rst = new S2CResult { Result = true } };
                    if (searchResult != null && searchResult.Value.accountId != AccountId)
                    {
                        searchAck.PlayerId = searchResult.Value.accountId;
                        searchAck.PlayerName = searchResult.Value.data.PlayerName;
                        searchAck.Level = searchResult.Value.data.Level;
                    }
                    else
                    {
                        searchAck.Rst = new S2CResult { Result = false, ErrCode = 1 }; // not found
                    }
                    await SendMessageAsync(EMessageType.FRIEND_SEARCH_S2C, searchAck);
                    break;

                case EMessageType.FRIEND_APPLY_C2S:
                    var applyMsg = FriendApplyC2S.Parser.ParseFrom(messageBody);
                    var applyAck = new FriendApplyS2C { Rst = new S2CResult { Result = true } };
                    if (applyMsg.TargetPlayerId == AccountId)
                    {
                        applyAck.Rst = new S2CResult { Result = false, ErrCode = 10 }; // cannot self
                    }
                    else
                    {
                        var myData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                        if (myData.BlockedIds.Contains(applyMsg.TargetPlayerId))
                        {
                            applyAck.Rst = new S2CResult { Result = false, ErrCode = 11 }; // blocked
                        }
                        else if (myData.FriendIds.Count >= 50)
                        {
                            applyAck.Rst = new S2CResult { Result = false, ErrCode = 12 }; // full
                        }
                        else
                        {
                            var targetData = await _server.DataStore.GetPlayerDataAsync(applyMsg.TargetPlayerId);
                            if (targetData.BlockedIds.Contains(AccountId))
                            {
                                applyAck.Rst = new S2CResult { Result = false, ErrCode = 13 }; // blocked by target
                            }
                            else
                            {
                                var exists = targetData.PendingApplies.Exists(a => a.FromPlayerId == AccountId && a.Status == 0);
                                if (exists)
                                {
                                    applyAck.Rst = new S2CResult { Result = false, ErrCode = 14 }; // already applied
                                }
                                else
                                {
                                    var myPlayer = await _server.DataStore.GetPlayerDataAsync(AccountId);
                                    targetData.PendingApplies.Add(new FriendApplyEntry
                                    {
                                        FromPlayerId = AccountId,
                                        FromPlayerName = myPlayer.PlayerName,
                                        FromLevel = myPlayer.Level,
                                        ApplyTime = DateTime.UtcNow.ToString("o"),
                                        Status = 0
                                    });
                                    await _server.DataStore.SavePlayerDataAsync(applyMsg.TargetPlayerId, targetData);
                                }
                            }
                        }
                    }
                    await SendMessageAsync(EMessageType.FRIEND_APPLY_S2C, applyAck);
                    break;

                case EMessageType.FRIEND_APPLY_LIST_C2S:
                    var myApplyData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var applyListAck = new FriendApplyListS2C();
                    foreach (var a in myApplyData.PendingApplies)
                    {
                        applyListAck.Applies.Add(new FriendApplyInfo
                        {
                            FromPlayerId = a.FromPlayerId,
                            FromPlayerName = a.FromPlayerName,
                            FromLevel = a.FromLevel,
                            ApplyTime = a.ApplyTime,
                            Status = a.Status
                        });
                    }
                    await SendMessageAsync(EMessageType.FRIEND_APPLY_LIST_S2C, applyListAck);
                    break;

                case EMessageType.FRIEND_REPLY_C2S:
                    var replyMsg = FriendReplyC2S.Parser.ParseFrom(messageBody);
                    var replyData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var entry = replyData.PendingApplies.Find(a => a.FromPlayerId == replyMsg.FromPlayerId && a.Status == 0);
                    var replyAck = new FriendReplyS2C { Rst = new S2CResult { Result = true } };
                    if (entry != null)
                    {
                        entry.Status = replyMsg.Agree ? 1 : 2;
                        if (replyMsg.Agree)
                        {
                            if (!replyData.FriendIds.Contains(replyMsg.FromPlayerId))
                                replyData.FriendIds.Add(replyMsg.FromPlayerId);
                            var fromPlayerData = await _server.DataStore.GetPlayerDataAsync(replyMsg.FromPlayerId);
                            if (!fromPlayerData.FriendIds.Contains(AccountId))
                                fromPlayerData.FriendIds.Add(AccountId);
                            await _server.DataStore.SavePlayerDataAsync(replyMsg.FromPlayerId, fromPlayerData);
                        }
                    }
                    await _server.DataStore.SavePlayerDataAsync(AccountId, replyData);
                    await SendMessageAsync(EMessageType.FRIEND_REPLY_S2C, replyAck);

                    if (replyMsg.Agree)
                        await CheckAndPushAchievements(replyData, "FriendAdd", 1);
                    break;

                case EMessageType.FRIEND_LIST_C2S:
                    var listData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var listAck = new FriendListS2C();
                    foreach (var fid in listData.FriendIds)
                    {
                        var fdata = await _server.DataStore.GetPlayerDataAsync(fid);
                        listAck.Friends.Add(new FriendInfo
                        {
                            PlayerId = fid,
                            PlayerName = fdata.PlayerName,
                            Level = fdata.Level,
                            IsOnline = _server.ClientManager.IsAccountOnline(fid),
                            LastLoginTime = fdata.LastLoginTime ?? "",
                            Remark = listData.FriendRemarks.ContainsKey(fid) ? listData.FriendRemarks[fid] : ""
                        });
                    }
                    await SendMessageAsync(EMessageType.FRIEND_LIST_S2C, listAck);
                    break;

                case EMessageType.FRIEND_DELETE_C2S:
                    var delMsg = FriendDeleteC2S.Parser.ParseFrom(messageBody);
                    var delMyData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    delMyData.FriendIds.Remove(delMsg.FriendPlayerId);
                    delMyData.FriendRemarks.Remove(delMsg.FriendPlayerId);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, delMyData);
                    var delOtherData = await _server.DataStore.GetPlayerDataAsync(delMsg.FriendPlayerId);
                    delOtherData.FriendIds.Remove(AccountId);
                    delOtherData.FriendRemarks.Remove(AccountId);
                    await _server.DataStore.SavePlayerDataAsync(delMsg.FriendPlayerId, delOtherData);
                    await SendMessageAsync(EMessageType.FRIEND_DELETE_S2C, new FriendDeleteS2C { Rst = new S2CResult { Result = true } });
                    break;

                case EMessageType.FRIEND_REMARK_C2S:
                    var remarkMsg = FriendRemarkC2S.Parser.ParseFrom(messageBody);
                    var remarkData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    if (!string.IsNullOrEmpty(remarkMsg.Remark))
                        remarkData.FriendRemarks[remarkMsg.FriendPlayerId] = remarkMsg.Remark;
                    else
                        remarkData.FriendRemarks.Remove(remarkMsg.FriendPlayerId);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, remarkData);
                    await SendMessageAsync(EMessageType.FRIEND_REMARK_S2C, new FriendRemarkS2C { Rst = new S2CResult { Result = true } });
                    break;

                case EMessageType.FRIEND_BLOCK_C2S:
                    var blockMsg = FriendBlockC2S.Parser.ParseFrom(messageBody);
                    var blockData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    if (blockMsg.IsBlock)
                    {
                        if (!blockData.BlockedIds.Contains(blockMsg.PlayerId))
                            blockData.BlockedIds.Add(blockMsg.PlayerId);
                        blockData.FriendIds.Remove(blockMsg.PlayerId);
                        var otherBlock = await _server.DataStore.GetPlayerDataAsync(blockMsg.PlayerId);
                        otherBlock.FriendIds.Remove(AccountId);
                        await _server.DataStore.SavePlayerDataAsync(blockMsg.PlayerId, otherBlock);
                    }
                    else
                    {
                        blockData.BlockedIds.Remove(blockMsg.PlayerId);
                    }
                    await _server.DataStore.SavePlayerDataAsync(AccountId, blockData);
                    await SendMessageAsync(EMessageType.FRIEND_BLOCK_S2C, new FriendBlockS2C { Rst = new S2CResult { Result = true } });
                    break;

                case EMessageType.FRIEND_BLOCK_LIST_C2S:
                    var blistData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var blistAck = new FriendBlockListS2C();
                    foreach (var bid in blistData.BlockedIds)
                    {
                        var bdata = await _server.DataStore.GetPlayerDataAsync(bid);
                        blistAck.Blocks.Add(new BlockInfo
                        {
                            PlayerId = bid,
                            PlayerName = bdata.PlayerName,
                            Level = bdata.Level,
                            BlockTime = ""
                        });
                    }
                    await SendMessageAsync(EMessageType.FRIEND_BLOCK_LIST_S2C, blistAck);
                    break;

                // ── Mail ──

                case EMessageType.MAIL_LIST_C2S:
                    var mailData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var mailListAck = new MailListS2C();
                    foreach (var m in mailData.Mails)
                        mailListAck.Mails.Add(MapMailEntry(m));
                    await SendMessageAsync(EMessageType.MAIL_LIST_S2C, mailListAck);
                    break;

                case EMessageType.MAIL_READ_C2S:
                    var readMsg = MailReadC2S.Parser.ParseFrom(messageBody);
                    var readData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var readEntry = readData.Mails.Find(m => m.MailId == readMsg.MailId);
                    if (readEntry != null && readEntry.Status == 0) readEntry.Status = 1;
                    await _server.DataStore.SavePlayerDataAsync(AccountId, readData);
                    await SendMessageAsync(EMessageType.MAIL_READ_S2C, new MailReadS2C { Rst = new S2CResult { Result = true } });
                    break;

                case EMessageType.MAIL_CLAIM_C2S:
                    var claimMsg = MailClaimC2S.Parser.ParseFrom(messageBody);
                    var claimData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var claimEntry = claimData.Mails.Find(m => m.MailId == claimMsg.MailId);
                    var claimAck = new MailClaimS2C { Rst = new S2CResult { Result = true } };
                    if (claimEntry != null && claimEntry.Status != 2 && claimEntry.Attachments.Count > 0)
                    {
                        claimEntry.Status = 2;
                        foreach (var att in claimEntry.Attachments)
                        {
                            claimAck.Claimed.Add(new MailAttachment { Type = att.Type, ItemId = att.ItemId, Count = att.Count });
                            // TODO: apply attachment to player inventory when bag system is ready
                            if (att.Type == 1) claimData.Gold += att.Count;
                            else if (att.Type == 2) claimData.Diamond += att.Count;
                        }
                    }
                    await _server.DataStore.SavePlayerDataAsync(AccountId, claimData);
                    await SendMessageAsync(EMessageType.MAIL_CLAIM_S2C, claimAck);
                    break;

                case EMessageType.MAIL_DELETE_C2S:
                    var mailDelMsg = MailDeleteC2S.Parser.ParseFrom(messageBody);
                    var mailDelData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    mailDelData.Mails.RemoveAll(m => mailDelMsg.MailIds.Contains(m.MailId));
                    await _server.DataStore.SavePlayerDataAsync(AccountId, mailDelData);
                    await SendMessageAsync(EMessageType.MAIL_DELETE_S2C, new MailDeleteS2C { Rst = new S2CResult { Result = true } });
                    break;

                // ── Sign In ──

                case EMessageType.SIGNIN_INFO_C2S:
                    await PushSignInInfoAsync();
                    break;

                case EMessageType.SIGNIN_DO_C2S:
                    var signData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var signAck = ProcessSignIn(signData);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, signData);
                    await SendMessageAsync(EMessageType.SIGNIN_DO_S2C, signAck);

                    if (signAck.Rst.Result)
                        await CheckAndPushAchievements(signData, "SignIn", 1);
                    break;

                case EMessageType.SIGNIN_MAKEUP_C2S:
                    var muData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var muAck = ProcessSignInMakeUp(muData);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, muData);
                    await SendMessageAsync(EMessageType.SIGNIN_MAKEUP_S2C, muAck);
                    break;

                // ── Bag ──

                case EMessageType.BAG_LIST_C2S:
                    await PushBagListAsync();
                    break;

                case EMessageType.BAG_USE_C2S:
                    var useMsg = BagUseC2S.Parser.ParseFrom(messageBody);
                    var useData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var useAck = ProcessBagUse(useData, useMsg.ItemId, useMsg.Count);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, useData);
                    await SendMessageAsync(EMessageType.BAG_USE_S2C, useAck);
                    break;

                case EMessageType.BAG_SELL_C2S:
                    var sellMsg = BagSellC2S.Parser.ParseFrom(messageBody);
                    var sellData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var sellAck = ProcessBagSell(sellData, sellMsg.ItemId, sellMsg.Count);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, sellData);
                    await SendMessageAsync(EMessageType.BAG_SELL_S2C, sellAck);

                    if (sellAck.Rst.Result)
                        await CheckAndPushAchievements(sellData, "GoldChange", sellData.Gold);
                    break;

                // ── Shop ──

                case EMessageType.SHOP_LIST_C2S:
                    await PushShopListAsync();
                    break;

                case EMessageType.SHOP_BUY_C2S:
                    var shopBuyMsg = ShopBuyC2S.Parser.ParseFrom(messageBody);
                    var shopBuyData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var shopBuyAck = ProcessShopBuy(shopBuyData, shopBuyMsg.ShopItemId);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, shopBuyData);
                    await SendMessageAsync(EMessageType.SHOP_BUY_S2C, shopBuyAck);

                    // Trigger achievement checks after shop buy
                    if (shopBuyAck.Rst.Result)
                    {
                        await CheckAndPushAchievements(shopBuyData, "ShopBuy", 1);
                        await CheckAndPushAchievements(shopBuyData, "GoldChange", shopBuyData.Gold);
                        await CheckAndPushAchievements(shopBuyData, "ItemGet", 1);
                    }
                    break;

                // ── Achievement ──

                case EMessageType.ACHIEVEMENT_LIST_C2S:
                    await PushAchievementListAsync();
                    break;

                case EMessageType.ACHIEVEMENT_CLAIM_C2S:
                    var achClaimMsg = AchievementClaimC2S.Parser.ParseFrom(messageBody);
                    var achClaimData = await _server.DataStore.GetPlayerDataAsync(AccountId);
                    var achClaimAck = ProcessAchievementClaim(achClaimData, achClaimMsg.Id);
                    await _server.DataStore.SavePlayerDataAsync(AccountId, achClaimData);
                    await SendMessageAsync(EMessageType.ACHIEVEMENT_CLAIM_S2C, achClaimAck);
                    break;

                default:
                    Console.WriteLine($"[ClientHandler] client {ClientId} unknown message type: {messageType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientHandler] client {ClientId} message parse error: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task PushFriendDataAsync()
    {
        var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
        Console.WriteLine($"[ClientHandler] Pushing friend data to account {AccountId}: {pd.FriendIds.Count} friends, {pd.PendingApplies.Count} applies, {pd.BlockedIds.Count} blocked");

        // Push friend list
        var flist = new FriendListS2C();
        foreach (var fid in pd.FriendIds)
        {
            var fdata = await _server.DataStore.GetPlayerDataAsync(fid);
            flist.Friends.Add(new FriendInfo
            {
                PlayerId = fid,
                PlayerName = fdata.PlayerName,
                Level = fdata.Level,
                IsOnline = _server.ClientManager.IsAccountOnline(fid),
                LastLoginTime = fdata.LastLoginTime ?? "",
                Remark = pd.FriendRemarks.ContainsKey(fid) ? pd.FriendRemarks[fid] : ""
            });
        }
        await SendMessageAsync(EMessageType.FRIEND_LIST_S2C, flist);

        // Push apply list
        var alist = new FriendApplyListS2C();
        foreach (var a in pd.PendingApplies)
        {
            alist.Applies.Add(new FriendApplyInfo
            {
                FromPlayerId = a.FromPlayerId,
                FromPlayerName = a.FromPlayerName,
                FromLevel = a.FromLevel,
                ApplyTime = a.ApplyTime,
                Status = a.Status
            });
        }
        await SendMessageAsync(EMessageType.FRIEND_APPLY_LIST_S2C, alist);

        // Push block list
        var blist = new FriendBlockListS2C();
        foreach (var bid in pd.BlockedIds)
        {
            var bdata = await _server.DataStore.GetPlayerDataAsync(bid);
            blist.Blocks.Add(new BlockInfo
            {
                PlayerId = bid,
                PlayerName = bdata.PlayerName,
                Level = bdata.Level,
                BlockTime = ""
            });
        }
        await SendMessageAsync(EMessageType.FRIEND_BLOCK_LIST_S2C, blist);

        // Notify online friends that this player is now online
        foreach (var fid in pd.FriendIds)
        {
            if (_server.ClientManager.IsAccountOnline(fid))
            {
                var friendClient = _server.ClientManager.GetByAccountId(fid);
                if (friendClient != null)
                {
                    var notify = new FriendOnlineNotifyS2C { PlayerId = AccountId, IsOnline = true };
                    await friendClient.SendMessageAsync(EMessageType.FRIEND_ONLINE_NOTIFY_S2C, notify);
                }
            }
        }
    }

    private async Task PushMailDataAsync()
    {
        var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
        var ack = new MailListS2C();
        foreach (var m in pd.Mails) ack.Mails.Add(MapMailEntry(m));
        Console.WriteLine($"[ClientHandler] Pushing mail data to account {AccountId}: {pd.Mails.Count} mails");
        await SendMessageAsync(EMessageType.MAIL_LIST_S2C, ack);
    }

    private static MailInfo MapMailEntry(MailEntry m)
    {
        var info = new MailInfo
        {
            MailId = m.MailId,
            Title = m.Title ?? "",
            Content = m.Content ?? "",
            SenderName = m.SenderName ?? "",
            SendTime = m.SendTime ?? "",
            Status = m.Status
        };
        foreach (var a in m.Attachments)
            info.Attachments.Add(new MailAttachment { Type = a.Type, ItemId = a.ItemId, Count = a.Count });
        return info;
    }

    private async Task NotifyFriendsOfflineAsync()
    {
        if (AccountId == 0) return;
        try
        {
            var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
            foreach (var fid in pd.FriendIds)
            {
                var friendClient = _server.ClientManager.GetByAccountId(fid);
                if (friendClient != null)
                {
                    var notify = new FriendOnlineNotifyS2C { PlayerId = AccountId, IsOnline = false };
                    await friendClient.SendMessageAsync(EMessageType.FRIEND_ONLINE_NOTIFY_S2C, notify);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientHandler] NotifyFriendsOfflineAsync error: {ex.Message}");
        }
    }

    private async Task PushSignInInfoAsync()
    {
        var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
        var ack = BuildSignInInfo(pd);
        Console.WriteLine($"[ClientHandler] Pushing sign-in data to account {AccountId}: day={pd.SignDay}, streak={pd.ConsecutiveDays}");
        await SendMessageAsync(EMessageType.SIGNIN_INFO_S2C, ack);
    }

    private static SignInInfoS2C BuildSignInInfo(PlayerData pd)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var today = DateTime.UtcNow.Date;
        var lastSignDate = DateTimeOffset.FromUnixTimeSeconds(pd.LastSignTime).UtcDateTime.Date;
        bool canSign = pd.LastSignTime == 0 || lastSignDate < today;
        bool canMakeUp = lastSignDate == today.AddDays(-1); // yesterday

        var ack = new SignInInfoS2C
        {
            SignDay = pd.SignDay,
            TotalSignDays = pd.TotalSignDays,
            ConsecutiveDays = pd.ConsecutiveDays,
            MaxConsecutiveDays = pd.MaxConsecutiveDays,
            CycleStartTime = pd.CycleStartTime,
            LastSignTime = pd.LastSignTime,
            CanSignToday = canSign
        };

        for (int d = 1; d <= 7; d++)
        {
            var day = new SignDay { Day = d };
            if (d < pd.SignDay) day.Status = 1;      // claimed
            else if (d == pd.SignDay && !canSign) day.Status = 1; // already signed today
            else if (d == pd.SignDay && canSign) day.Status = 2; // available (or can makeup if day==1)
            else day.Status = 0; // future / missed
            ack.Days.Add(day);
        }

        for (int d = 1; d <= 7; d++)
        {
            var reward = SignInConfig.Rewards[d - 1];
            ack.Rewards.Add(new SignReward { Day = d, Type = reward.type, ItemId = reward.itemId, Count = reward.count });
        }

        return ack;
    }

    private static SignInDoS2C ProcessSignIn(PlayerData pd)
    {
        var today = DateTime.UtcNow.Date;
        var lastSignDate = DateTimeOffset.FromUnixTimeSeconds(pd.LastSignTime).UtcDateTime.Date;

        if (pd.LastSignTime > 0 && lastSignDate >= today)
            return new SignInDoS2C { Rst = new S2CResult { Result = false, ErrCode = 1 } }; // already signed

        if (pd.LastSignTime > 0 && lastSignDate < today.AddDays(-1))
        {
            // Missed yesterday or more: reset streak, reset cycle
            pd.SignDay = 1;
            pd.ConsecutiveDays = 0;
            pd.CycleStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        int day = pd.SignDay;
        if (day < 1 || day > SignInConfig.Rewards.Count)
        {
            Console.WriteLine($"[ClientHandler] SignIn: invalid SignDay={day}, resetting to 1");
            day = 1;
            pd.SignDay = 1;
        }
        var reward = SignInConfig.Rewards[day - 1];
        int type = reward.type, itemId = reward.itemId, count = reward.count;

        pd.SignDay = day < 7 ? day + 1 : 1;
        pd.ConsecutiveDays++;
        pd.TotalSignDays++;
        pd.LastSignTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (pd.ConsecutiveDays > pd.MaxConsecutiveDays) pd.MaxConsecutiveDays = pd.ConsecutiveDays;
        if (pd.CycleStartTime == 0) pd.CycleStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Apply reward
        if (type == 1) pd.Gold += count;
        else if (type == 2) pd.Diamond += count;

        var ack = new SignInDoS2C
        {
            Rst = new S2CResult { Result = true },
            Day = day,
            ConsecutiveDays = pd.ConsecutiveDays
        };
        ack.Claimed.Add(new SignReward { Day = day, Type = type, ItemId = itemId, Count = count });
        return ack;
    }

    private static SignInMakeUpS2C ProcessSignInMakeUp(PlayerData pd)
    {
        var today = DateTime.UtcNow.Date;
        var lastSignDate = DateTimeOffset.FromUnixTimeSeconds(pd.LastSignTime).UtcDateTime.Date;

        if (lastSignDate != today.AddDays(-1))
            return new SignInMakeUpS2C { Rst = new S2CResult { Result = false, ErrCode = 2 } }; // only yesterday

        var ack = new SignInMakeUpS2C { Rst = new S2CResult { Result = true }, Day = pd.SignDay };
        pd.LastSignTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return ack;
    }

    private async Task PushBagListAsync()
    {
        var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
        var ack = new BagListS2C { MaxSlots = 100 };
        foreach (var bi in pd.BagItems)
            ack.Items.Add(new BagItem { ItemId = bi.ItemId, Count = bi.Count });
        Console.WriteLine($"[ClientHandler] Pushing bag data to account {AccountId}: {pd.BagItems.Count} item types");
        await SendMessageAsync(EMessageType.BAG_LIST_S2C, ack);
    }

    private static BagUseS2C ProcessBagUse(PlayerData pd, int itemId, int count)
    {
        var entry = pd.BagItems.Find(i => i.ItemId == itemId);
        if (entry == null || entry.Count < count)
            return new BagUseS2C { Rst = new S2CResult { Result = false, ErrCode = 1 } }; // insufficient

        entry.Count -= count;
        int remaining = entry.Count;
        if (remaining <= 0) pd.BagItems.Remove(entry);

        return new BagUseS2C { Rst = new S2CResult { Result = true }, ItemId = itemId, Remaining = remaining };
    }

    private static BagSellS2C ProcessBagSell(PlayerData pd, int itemId, int count)
    {
        var entry = pd.BagItems.Find(i => i.ItemId == itemId);
        if (entry == null || entry.Count < count)
            return new BagSellS2C { Rst = new S2CResult { Result = false, ErrCode = 1 } };

        entry.Count -= count;
        if (entry.Count <= 0) pd.BagItems.Remove(entry);

        // 10 gold per item (simple formula)
        int goldGained = count * 10;
        pd.Gold += goldGained;

        return new BagSellS2C { Rst = new S2CResult { Result = true }, GoldGained = goldGained };
    }

    private async Task PushShopListAsync()
    {
        var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
        var ack = new ShopListS2C { Rst = new S2CResult { Result = true } };
        foreach (var kv in pd.ShopBuyRecords)
        {
            ack.Records.Add(new ShopBuyRecord { ShopItemId = kv.Key, BoughtCount = kv.Value });
        }
        Console.WriteLine($"[ClientHandler] Pushing shop data to account {AccountId}: {pd.ShopBuyRecords.Count} records");
        await SendMessageAsync(EMessageType.SHOP_LIST_S2C, ack);
    }

    private static ShopBuyS2C ProcessShopBuy(PlayerData pd, int shopItemId)
    {
        // Load shop config from JSON
        var shopItems = LoadShopConfig();
        var cfg = shopItems.FirstOrDefault(x => x.id == shopItemId);
        if (cfg == null)
            return new ShopBuyS2C { Rst = new S2CResult { Result = false, ErrCode = 1 } }; // item not found

        // Check limit
        int bought = pd.ShopBuyRecords.TryGetValue(shopItemId, out int c) ? c : 0;
        if (cfg.limitBuyNum > 0 && bought >= cfg.limitBuyNum)
            return new ShopBuyS2C { Rst = new S2CResult { Result = false, ErrCode = 2 } }; // limit reached

        // Calculate final price
        int finalPrice = cfg.price;
        if (cfg.discount > 0)
            finalPrice = (int)(cfg.price * (1 - cfg.discount));

        // Check gold
        if (pd.Gold < finalPrice)
            return new ShopBuyS2C { Rst = new S2CResult { Result = false, ErrCode = 3 } }; // not enough gold

        // Deduct gold
        pd.Gold -= finalPrice;

        // Add item to bag
        var bagEntry = pd.BagItems.FirstOrDefault(i => i.ItemId == cfg.itemId);
        if (bagEntry != null)
            bagEntry.Count += 1;
        else
            pd.BagItems.Add(new BagItemEntry { ItemId = cfg.itemId, Count = 1 });

        // Update buy record
        pd.ShopBuyRecords[shopItemId] = bought + 1;

        return new ShopBuyS2C
        {
            Rst = new S2CResult { Result = true },
            ShopItemId = shopItemId,
            BoughtCount = bought + 1,
            GoldRemaining = pd.Gold
        };
    }

    /// <summary>
    /// Root directory for DesignConfig JSON files.
    /// Set via TCPServer.DesignConfigRoot before starting the server.
    /// Falls back to auto-detection (relative to executable).
    /// </summary>
    public static string DesignConfigRoot { get; set; }

    /// <summary>
    /// Resolve the DesignConfig root directory.
    /// Priority: CONFIG_ROOT env var > explicitly set > auto-detect from project layout > base dir
    /// </summary>
    private static string ResolveDesignConfigRoot()
    {
        // Priority 1: environment variable CONFIG_ROOT
        string envRoot = Environment.GetEnvironmentVariable("CONFIG_ROOT");
        if (!string.IsNullOrEmpty(envRoot) && Directory.Exists(envRoot))
            return envRoot;

        // Priority 2: explicitly set by Startup via TCPServer.DesignConfigRoot
        if (!string.IsNullOrEmpty(DesignConfigRoot) && Directory.Exists(DesignConfigRoot))
            return DesignConfigRoot;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Auto-detect: try 4 levels up from bin/Debug|Release → workspace root (PureMVC_Framework/)
        string workspaceRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        string designPath = Path.Combine(workspaceRoot, "DesignConfig");
        if (Directory.Exists(designPath))
            return designPath;

        // Fallback: try DesignConfig in baseDir (production deployment layout)
        string baseDesignPath = Path.Combine(baseDir, "DesignConfig");
        if (Directory.Exists(baseDesignPath))
            return baseDesignPath;

        return designPath; // Return best guess, let caller handle missing files
    }

    private static List<ShopItem> _cachedShopConfig;
    private static List<ShopItem> LoadShopConfig()
    {
        if (_cachedShopConfig != null) return _cachedShopConfig;
        try
        {
            string configPath = Path.Combine(ResolveDesignConfigRoot(), "Json", "ShopItem.json");
            string json = File.ReadAllText(configPath);
            var wrapper = JsonConvert.DeserializeObject<ShopConfigWrapper>(json);
            _cachedShopConfig = wrapper?.items ?? new List<ShopItem>();
            Console.WriteLine($"[ShopConfig] Loaded {_cachedShopConfig.Count} items from {configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShopConfig] Load error: {ex.Message}");
            _cachedShopConfig = new List<ShopItem>();
        }
        return _cachedShopConfig;
    }

    [Serializable]
    private class ShopConfigWrapper
    {
        public List<ShopItem> items;
    }

    // ── Achievement ──

    private async Task PushAchievementListAsync()
    {
        var pd = await _server.DataStore.GetPlayerDataAsync(AccountId);
        var list = AchievementChecker.BuildInfoList(pd);
        var ack = new AchievementListS2C();
        ack.Achievements.AddRange(list);
        Console.WriteLine($"[ClientHandler] Pushing achievement data to account {AccountId}: {list.Count} achievements");
        await SendMessageAsync(EMessageType.ACHIEVEMENT_LIST_S2C, ack);
    }

    /// <summary>
    /// Check achievements for a trigger event and push updates to client.
    /// Caller must save PlayerData after calling this.
    /// </summary>
    private async Task CheckAndPushAchievements(PlayerData pd, string triggerEvent, int value)
    {
        var changed = AchievementChecker.Check(pd, triggerEvent, value);
        foreach (var (id, progress, target, status) in changed)
        {
            var update = new AchievementProgressS2C
            {
                Id = id,
                Progress = progress,
                Target = target,
                Status = status
            };
            await SendMessageAsync(EMessageType.ACHIEVEMENT_PROGRESS_S2C, update);

            if (status == 1) // newly unlocked
            {
                var unlock = new AchievementUnlockS2C { Id = id };
                await SendMessageAsync(EMessageType.ACHIEVEMENT_UNLOCK_S2C, unlock);
                Console.WriteLine($"[ClientHandler] Achievement unlocked: {id} for account {AccountId}");
            }
        }
    }

    private static AchievementClaimS2C ProcessAchievementClaim(PlayerData pd, int achievementId)
    {
        // Validate: must be in unlocked list
        if (!pd.UnlockedAchievements.Contains(achievementId))
            return new AchievementClaimS2C
            {
                Rst = new S2CResult { Result = false, ErrCode = 1 },
                Id = achievementId
            };

        // Find config
        var cfg = AchievementChecker.Configs.Find(c => c.id == achievementId);
        if (cfg == null)
            return new AchievementClaimS2C
            {
                Rst = new S2CResult { Result = false, ErrCode = 2 },
                Id = achievementId
            };

        // Move from unlocked to claimed
        pd.UnlockedAchievements.Remove(achievementId);
        if (!pd.ClaimedAchievements.Contains(achievementId))
            pd.ClaimedAchievements.Add(achievementId);

        // Grant reward
        if (cfg.rewardType == 1) pd.Gold += cfg.rewardNum;
        else if (cfg.rewardType == 2) pd.Diamond += cfg.rewardNum;

        Console.WriteLine($"[ClientHandler] Achievement claimed: {achievementId}, reward: type={cfg.rewardType} count={cfg.rewardNum}");

        return new AchievementClaimS2C
        {
            Rst = new S2CResult { Result = true },
            Id = achievementId,
            RewardType = cfg.rewardType,
            RewardCount = cfg.rewardNum
        };
    }

    /// <summary>
    /// Sliding-window rate limit check per client.
    /// Returns true if within limit, false if exceeded.
    /// Thread-safe via Interlocked operations on the count.
    /// </summary>
    private bool CheckRateLimit()
    {
        var now = DateTime.UtcNow;
        var entry = _rateLimitMap.GetOrAdd(ClientId, _ => new RateLimitEntry { WindowStart = now });

        lock (entry)
        {
            if (now - entry.WindowStart > RateLimitWindow)
            {
                // Window expired, reset
                entry.WindowStart = now;
                Interlocked.Exchange(ref entry.Count, 1);
                return true;
            }

            int newCount = Interlocked.Increment(ref entry.Count);
            return newCount <= RateLimitMaxMessages;
        }
    }

    private class RateLimitEntry
    {
        public DateTime WindowStart;
        public int Count = 1;
    }

    internal void Disconnect()
    {
        if (Interlocked.Exchange(ref _isDisposedInt, 1) == 1) return;
        _isDisposed = true;

        // Notify online friends that this player is offline (fire-and-forget)
        _ = NotifyFriendsOfflineAsync();

        try
        {
            if (_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
                _stream = null;
            }
            if (_client.Connected)
            {
                _client.Client.Shutdown(SocketShutdown.Both);
            }
            _client.Dispose();
            Console.WriteLine($"[ClientHandler] client {ClientId} disconnected actively");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClientHandler] disconnect Unity client {ClientId} error: {ex.Message}");
        }
    }

    // Interlocked atomic int flag: 0=not freed, 1=disposed
    private int _isDisposedInt = 0;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disconnect();
            _sendLock.Dispose();
        }
    }

    ~ClientHandler()
    {
        Dispose(false);
    }
}
