using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Bag system proxy — item inventory with server sync.
/// Bag data is pushed by server after login.
/// Registered at startup by RegisterProxyCommand in StartupMacroCommand.
/// </summary>
public class BagProxy : ProxyBase
{
    public new const string NAME = "BagProxy";

    public List<BagItem> Items { get; private set; } = new List<BagItem>();
    public int MaxSlots { get; private set; } = 100;

    public BagProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Register(MessageConst.BAG_LIST_S2C, OnBagListS2C);
        disp.Register(MessageConst.BAG_USE_S2C, OnBagUseS2C);
        disp.Register(MessageConst.BAG_SELL_S2C, OnBagSellS2C);

        // Bag data is pushed by server after login.
    }

    public override void OnRemove()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Unregister(MessageConst.BAG_LIST_S2C, OnBagListS2C);
        disp.Unregister(MessageConst.BAG_USE_S2C, OnBagUseS2C);
        disp.Unregister(MessageConst.BAG_SELL_S2C, OnBagSellS2C);
    }

    #region Helpers

    public BagItem GetItem(int itemId) => Items.FirstOrDefault(i => i.ItemId == itemId);

    public int GetItemCount(int itemId)
    {
        var item = GetItem(itemId);
        return item?.Count ?? 0;
    }

    public bool HasItem(int itemId, int count = 1)
    {
        return GetItemCount(itemId) >= count;
    }

    /// <summary>
    /// Add an item locally. Does NOT send a network request.
    /// Use this for offline previews or when server already knows.
    /// For direct network add, server pushes BAG_LIST_S2C.
    /// </summary>
    public void AddItem(int itemId, int count = 1)
    {
        var item = GetItem(itemId);
        if (item != null)
        {
            item.Count += count;
        }
        else
        {
            Items.Add(new BagItem { ItemId = itemId, Count = count });
        }
    }

    #endregion

    #region Callbacks

    private void OnBagListS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseBagListS2C(body);
        Items.Clear();
        Items.AddRange(resp.Items);
        MaxSlots = resp.MaxSlots;
        Log.d($"Bag list: {Items.Count} items", NAME);
        SendNotification(NotificationConst.BAG_LIST_UPDATED);
    }

    private void OnBagUseS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseBagUseS2C(body);
        Log.d($"Use result: {(resp.Rst.Result ? $"{resp.ItemId} remaining {resp.Remaining}" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (resp.Rst.Result)
        {
            if (resp.Remaining > 0)
            {
                var item = GetItem(resp.ItemId);
                if (item != null) item.Count = resp.Remaining;
                else Items.Add(new BagItem { ItemId = resp.ItemId, Count = resp.Remaining });
            }
            else
            {
                Items.RemoveAll(i => i.ItemId == resp.ItemId);
            }
            SendNotification(NotificationConst.BAG_ITEM_CHANGED, resp);
        }
    }

    private void OnBagSellS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseBagSellS2C(body);
        Log.d($"Sell result: {(resp.Rst.Result ? $"gained {resp.GoldGained} gold" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (resp.Rst.Result) NetworkMessageHelper.SendBagList();
        SendNotification(NotificationConst.BAG_ITEM_CHANGED);
    }

    #endregion
}
