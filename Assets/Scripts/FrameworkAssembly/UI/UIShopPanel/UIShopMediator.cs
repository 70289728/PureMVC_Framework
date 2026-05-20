using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PureMVC.Interfaces;

public class UIShopMediator : UIMediatorBase
{
    public new const string NAME = UIConst.UIShop;

    #region UI Components
    [SerializeField] private Button closeBtn;
    [SerializeField] private Transform tabNode;
    [SerializeField] private Transform tabItem;
    [SerializeField] private Transform content;
    [SerializeField] private Transform goodItem;
    #endregion

    private ShopProxy _shopProxy;
    private List<ShopItemVO> _shopItems;
    private List<UIShopGoodItemMediator> _itemMediators = new List<UIShopGoodItemMediator>();

    public UIShopMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    #region PureMVC Lifecycle
    public override string[] ListNotificationInterests()
    {
        return new string[]
        {
            NotificationConst.UPDATE_SHOP,
        };
    }

    public override void HandleNotification(INotification notification)
    {
        if (notification.Name == NotificationConst.UPDATE_SHOP)
            RefreshShopItems();
    }
    #endregion

    protected override void InitUIComponents()
    {
        closeBtn = viewTrans.GetComponentInChildren<ButtonBind>(true).Component;
        var allTransformBinds = viewTrans.GetComponentsInChildren<TransformBind>(true);
        foreach (var bind in allTransformBinds)
        {
            switch (bind.gameObject.name)
            {
                case "TabNode": tabNode = bind.Component; break;
                case "TabItem": tabItem = bind.Component; break;
                case "Content": content = bind.Component; break;
                case "GoodItem": goodItem = bind.Component; break;
            }
        }
        InitClickEvents();
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
        _shopProxy = GetProxy<ShopProxy>(ProxyConst.SHOP_PROXY);
        RefreshShopItems();
    }

    protected override void UnRegisterUIEvents()
    {
        ClearItemMediators();
        base.UnRegisterUIEvents();
    }

    private void InitClickEvents()
    {
        if (closeBtn != null)
        {
            closeBtn.onClick.AddListener(OnCloseBtnClick);
        }
    }

    private void OnCloseBtnClick()
    {
        UIManager.Instance.CloseUI(NAME);
    }

    private void RefreshShopItems()
    {
        ClearItemMediators();
        _shopItems = _shopProxy.GetShopItems();

        if (goodItem == null || content == null)
        {
            Log.w("GoodItem or Content transform not bound", "UIShopMediator");
            return;
        }

        goodItem.gameObject.SetActive(false);

        foreach (var item in _shopItems)
        {
            var go = GameObject.Instantiate(goodItem.gameObject, content);
            go.SetActive(true);

            var itemMediator = new UIShopGoodItemMediator(go, viewLayer);
            itemMediator.SetData(item, OnBuyClick);
            itemMediator.Show();
            _itemMediators.Add(itemMediator);
        }
    }

    private void OnBuyClick(ShopItemVO item)
    {
        if (!item.CanBuy) return;
        _shopProxy.BuyItem(item.Id);
    }

    private void ClearItemMediators()
    {
        foreach (var mediator in _itemMediators)
        {
            mediator.Close();
        }
        _itemMediators.Clear();
    }

    #region Update (Optional)
    // protected override bool NeedsUpdate() => true;
    // protected override UpdateFrequency GetUpdateFrequency() => UpdateFrequency.Low;
    // protected override UpdateType[] GetUpdateTypes() => new UpdateType[] { UpdateType.Update };
    // protected override void OnUpdate(float deltaTime) { }
    // protected override void OnFixedUpdate(float fixedDeltaTime) { }
    // protected override void OnLateUpdate(float deltaTime) { }
    #endregion

}
