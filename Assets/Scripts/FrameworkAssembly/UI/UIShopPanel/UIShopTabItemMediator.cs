using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sub-mediator for TabItem item.
/// Managed by parent panel mediator.
/// </summary>
public class UIShopTabItemMediator : UIMediatorBase
{
    public const string NAME_PREFIX = "UIShopTabItemMediator_";

    #region UI Components
    [SerializeField] private Transform tabItem;
    #endregion

    private System.Action _onClickCallback;

    public UIShopTabItemMediator(GameObject viewComponent, int layer)
        : base(NAME_PREFIX + viewComponent.GetInstanceID(), viewComponent, layer, false)
    {
    }

    protected override void InitUIComponents()
    {
        tabItem = viewTrans.GetComponentInChildren<TransformBind>(true).Component;
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
    }

    protected override void UnRegisterUIEvents()
    {
        base.UnRegisterUIEvents();
    }

    /// <summary>
    /// Bind data and callback. Called by parent mediator after creation.
    /// </summary>
    public void SetData(System.Action onClickCallback)
    {
        _onClickCallback = onClickCallback;
        RefreshView();
    }

    private void RefreshView()
    {
        // TODO: Bind data to UI components here
    }

    private void OnItemClick()
    {
        _onClickCallback?.Invoke();
    }

}
