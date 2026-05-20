using System.Collections.Generic;

/// <summary>
/// Shop system proxy — shop data with server sync.
/// Shop list is pushed by server after login (like BagProxy).
/// Buy actions go through server for validation and persistence.
/// Registered at startup by RegisterProxyCommand in StartupMacroCommand.
/// </summary>
public class ShopProxy : ProxyBase
{
    public new const string NAME = "ShopProxy";

    private List<ShopItem> _shopConfig;
    private Dictionary<int, int> _buyRecords = new Dictionary<int, int>();

    public ShopProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Register(MessageConst.SHOP_LIST_S2C, OnShopListS2C);
        disp.Register(MessageConst.SHOP_BUY_S2C, OnShopBuyS2C);

        LoadShopConfig();
    }

    public override void OnRemove()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Unregister(MessageConst.SHOP_LIST_S2C, OnShopListS2C);
        disp.Unregister(MessageConst.SHOP_BUY_S2C, OnShopBuyS2C);
    }

    #region Config

    private void LoadShopConfig()
    {
        ConfigManager.Load<ShopItem>();
        _shopConfig = ConfigManager.GetAll<ShopItem>();
        Log.d($"ShopProxy loaded {_shopConfig.Count} shop items from config", NAME);
    }

    #endregion

    #region Queries

    /// <summary>
    /// Get all shop items with their current buy count from server data.
    /// </summary>
    public List<ShopItemVO> GetShopItems()
    {
        var result = new List<ShopItemVO>();
        foreach (var cfg in _shopConfig)
        {
            int bought = _buyRecords.TryGetValue(cfg.id, out int count) ? count : 0;
            result.Add(new ShopItemVO
            {
                Id = cfg.id,
                ItemId = cfg.itemId,
                Price = cfg.price,
                LimitBuyNum = cfg.limitBuyNum,
                Discount = cfg.discount,
                BoughtCount = bought,
                CanBuy = cfg.limitBuyNum == 0 || bought < cfg.limitBuyNum
            });
        }
        return result;
    }

    #endregion

    #region Network Actions

    /// <summary>
    /// Request shop list from server.
    /// </summary>
    public void RequestShopList()
    {
        NetworkMessageHelper.SendShopList();
    }

    /// <summary>
    /// Buy a shop item. Sends request to server for validation and persistence.
    /// Server responds with SHOP_BUY_S2C — OnShopBuyS2C handles the result.
    /// </summary>
    public void BuyItem(int shopItemId)
    {
        NetworkMessageHelper.SendShopBuy(shopItemId);
    }

    #endregion

    #region Callbacks

    private void OnShopListS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseShopListS2C(body);
        _buyRecords.Clear();
        foreach (var record in resp.Records)
        {
            _buyRecords[record.ShopItemId] = record.BoughtCount;
        }
        Log.d($"Shop list received: {_buyRecords.Count} records", NAME);
        SendNotification(NotificationConst.UPDATE_SHOP);
    }

    private void OnShopBuyS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseShopBuyS2C(body);
        if (resp.Rst.Result)
        {
            _buyRecords[resp.ShopItemId] = resp.BoughtCount;
            Log.d($"Buy success: shopItemId={resp.ShopItemId}, bought={resp.BoughtCount}, gold={resp.GoldRemaining}", NAME);

            // Sync gold to UserProxy
            var userProxy = GetProxy<UserProxy>(ProxyConst.USER_PROXY);
            if (userProxy?.userData != null)
            {
                userProxy.userData.Gold = resp.GoldRemaining;
            }

            // Request updated bag list since server added item
            NetworkMessageHelper.SendBagList();

            SendNotification(NotificationConst.BUY_ITEM, resp.ShopItemId);
            SendNotification(NotificationConst.UPDATE_SHOP);
            SendNotification(NotificationConst.SHOW_TIP, "Purchase successful");
        }
        else
        {
            string reason = resp.Rst.ErrCode switch
            {
                1 => "Item not found",
                2 => "Purchase limit reached",
                3 => "Not enough gold",
                _ => "Purchase failed"
            };
            Log.w($"Buy failed: shopItemId={resp.ShopItemId}, errCode={resp.Rst.ErrCode}, reason={reason}", NAME);
            SendNotification(NotificationConst.SHOW_TIP, reason);
        }
    }

    #endregion
}

/// <summary>
/// Shop item view object for UI display.
/// </summary>
public class ShopItemVO
{
    public int Id;
    public int ItemId;
    public int Price;
    public int LimitBuyNum;
    public float Discount;
    public int BoughtCount;
    public bool CanBuy;
}
