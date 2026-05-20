using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sub-mediator for a single shop good item.
/// Managed by UIShopMediator.
/// </summary>
public class UIShopGoodItemMediator : UIMediatorBase
{
    public const string NAME_PREFIX = "UIShopGoodItem_";

    #region UI Components
    [SerializeField] private Transform goodItem;
    [SerializeField] private Image goodImg;
    [SerializeField] private Text goodName;
    [SerializeField] private Button buyBtn;
    [SerializeField] private Text nameText;
    [SerializeField] private Text priceText;
    [SerializeField] private Text limitText;
    #endregion

    private ShopItemVO _data;
    private System.Action<ShopItemVO> _onBuyCallback;

    public UIShopGoodItemMediator(GameObject viewComponent, int layer)
        : base(NAME_PREFIX + viewComponent.GetInstanceID(), viewComponent, layer, false)
    {
    }

    protected override void InitUIComponents()
    {
        goodItem = viewTrans.GetComponentInChildren<TransformBind>(true).Component;
        goodImg = viewTrans.GetComponentInChildren<ImageBind>(true).Component;
        goodName = viewTrans.GetComponentInChildren<TextBind>(true).Component;
        buyBtn = viewTrans.GetComponentInChildren<Button>(true);
        nameText = viewTrans.Find("NameText")?.GetComponent<Text>();
        priceText = viewTrans.Find("PriceText")?.GetComponent<Text>();
        limitText = viewTrans.Find("LimitText")?.GetComponent<Text>();
        InitClickEvents();
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
        if (buyBtn != null)
        {
            buyBtn.onClick.AddListener(OnBuyClick);
        }
    }

    protected override void UnRegisterUIEvents()
    {
        if (buyBtn != null)
        {
            buyBtn.onClick.RemoveListener(OnBuyClick);
        }
        base.UnRegisterUIEvents();
    }

    private void InitClickEvents()
    {
    }

    /// <summary>
    /// Bind data and callback. Called by parent mediator after creation.
    /// </summary>
    public void SetData(ShopItemVO data, System.Action<ShopItemVO> onBuyCallback)
    {
        _data = data;
        _onBuyCallback = onBuyCallback;
        RefreshView();
    }

    private void RefreshView()
    {
        if (_data == null) return;

        if (nameText != null)
            nameText.text = $"Item {_data.ItemId}";

        if (priceText != null)
        {
            int finalPrice = _data.Price;
            if (_data.Discount > 0)
                finalPrice = (int)(_data.Price * (1 - _data.Discount));
            priceText.text = finalPrice.ToString();
        }

        if (limitText != null)
        {
            limitText.text = _data.LimitBuyNum > 0
                ? $"{_data.BoughtCount}/{_data.LimitBuyNum}"
                : "Unlimited";
        }

        if (buyBtn != null)
        {
            buyBtn.interactable = _data.CanBuy;
        }
    }

    private void OnBuyClick()
    {
        _onBuyCallback?.Invoke(_data);
    }
}
