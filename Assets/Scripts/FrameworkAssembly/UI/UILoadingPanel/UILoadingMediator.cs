using UnityEngine;
using UnityEngine.UI;

public class UILoadingMediator : UIMediatorBase
{
    public new const string NAME = UIConst.UILoading;

    #region UI Components
    // No Bind components found on prefab
    #endregion

    public UILoadingMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    protected override void InitUIComponents()
    {
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
    }

    protected override void UnRegisterUIEvents()
    {
        base.UnRegisterUIEvents();
    }

    #region Update (Optional)
    // Uncomment if this UI needs Update functionality
    // protected override bool NeedsUpdate() => true;
    // protected override UpdateFrequency GetUpdateFrequency() => UpdateFrequency.Low;
    // protected override UpdateType[] GetUpdateTypes() => new UpdateType[] { UpdateType.Update };
    // protected override void OnUpdate(float deltaTime) { }
    // protected override void OnFixedUpdate(float fixedDeltaTime) { }
    // protected override void OnLateUpdate(float deltaTime) { }
    #endregion

}
