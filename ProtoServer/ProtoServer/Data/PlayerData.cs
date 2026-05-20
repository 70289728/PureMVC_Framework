using System.Collections.Generic;

/// <summary>
/// Player game data model.
/// Stored per account in JSON format.
/// </summary>
public class PlayerData
{
    #region Base Fields (network_module.proto)

    public string PlayerName { get; set; } = "";
    public int Gender { get; set; } = 0;
    public int Job { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int Gold { get; set; } = 0;
    public int Diamond { get; set; } = 0;
    public int Exp { get; set; } = 0;
    public List<string> Equipment { get; set; } = new List<string>();
    public string LastLoginTime { get; set; }
    public string CreatedTime { get; set; }

    #endregion

    #region HotUpdate Fields (player_ext.proto)

    public int VipLevel { get; set; } = 0;
    public string Signature { get; set; } = "";
    public int BattlePassExp { get; set; } = 0;

    #endregion

    #region Friend Fields (friend.proto)

    public List<long> FriendIds { get; set; } = new List<long>();
    public Dictionary<long, string> FriendRemarks { get; set; } = new Dictionary<long, string>();
    public List<long> BlockedIds { get; set; } = new List<long>();
    public List<FriendApplyEntry> PendingApplies { get; set; } = new List<FriendApplyEntry>();

    #endregion

    #region Mail Fields (mail.proto)

    public List<MailEntry> Mails { get; set; } = new List<MailEntry>();

    #endregion

    #region SignIn Fields (signin.proto)

    public int SignDay { get; set; } = 1;
    public int ConsecutiveDays { get; set; } = 0;
    public int MaxConsecutiveDays { get; set; } = 0;
    public int TotalSignDays { get; set; } = 0;
    public long CycleStartTime { get; set; } // unix seconds
    public long LastSignTime { get; set; }

    #endregion

    #region Bag Fields (bag.proto)

    public List<BagItemEntry> BagItems { get; set; } = new List<BagItemEntry>();

    #endregion

    #region Shop Fields (shop.proto)

    public Dictionary<int, int> ShopBuyRecords { get; set; } = new Dictionary<int, int>();

    #endregion
}

public class FriendApplyEntry
{
    public long FromPlayerId { get; set; }
    public string FromPlayerName { get; set; }
    public int FromLevel { get; set; }
    public string ApplyTime { get; set; }
    public int Status { get; set; } // 0: pending, 1: accepted, 2: refused
}

public class MailEntry
{
    public long MailId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string SenderName { get; set; }
    public string SendTime { get; set; }
    public int Status { get; set; } // 0: unread, 1: read, 2: claimed
    public List<MailAttachmentEntry> Attachments { get; set; } = new List<MailAttachmentEntry>();
}

public class MailAttachmentEntry
{
    public int Type { get; set; }
    public int ItemId { get; set; }
    public int Count { get; set; }
}

public class BagItemEntry
{
    public int ItemId { get; set; }
    public int Count { get; set; }
}
