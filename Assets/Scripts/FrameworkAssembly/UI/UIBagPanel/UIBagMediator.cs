using UnityEngine;
using UnityEngine.UI;

public class UIBagMediator : UIMediatorBase
{
    public new const string NAME = UIConst.UIBag;

    #region UI Components
    // TODO: Add UI component references here
    // Example:
    // private Button closeBtn;
    // private Text titleText;
    #endregion

    public UIBagMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false) 
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    protected override void InitUIComponents()
    {
        // TODO: Initialize UI components here
        // Example:
        // closeBtn = viewTrans.Find("CloseBtn").GetComponent<Button>();
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
        // TODO: Register UI event listeners here
    }

    protected override void UnRegisterUIEvents()
    {
        base.UnRegisterUIEvents();
        // TODO: Unregister UI event listeners here
    }
}
