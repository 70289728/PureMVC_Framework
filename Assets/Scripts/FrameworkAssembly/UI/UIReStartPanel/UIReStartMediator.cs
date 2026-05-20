using UnityEngine;
using UnityEngine.UI;

public class UIReStartMediator : UIMediatorBase
{
    public new const string NAME = UIConst.UIReStart;

    #region UI Components
    [SerializeField] private Button restartBtn;
    #endregion

    public UIReStartMediator(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false)
        : base(mediatorName, viewComponent, layer, isReuseView)
    {
    }

    protected override void InitUIComponents()
    {
        restartBtn = viewTrans.GetComponentInChildren<ButtonBind>(true).Component;
        InitClickEvents();
    }

    protected override void RegisterUIEvents()
    {
        base.RegisterUIEvents();
    }

    protected override void UnRegisterUIEvents()
    {
        base.UnRegisterUIEvents();
    }

    private void InitClickEvents()
    {
        if (restartBtn != null)
        {
            restartBtn.onClick.AddListener(OnRestartBtnClick);
        }
    }

    private void OnRestartBtnClick()
    {
        Log.d("User clicked restart — quitting app", "UIReStartMediator");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
