/// <summary>
/// Daily sign-in system proxy.
/// Sign-in info is pushed by server after login.
/// Supports 7-day cycle with consecutive streak and makeup (yesterday only).
/// Registered at startup by RegisterProxyCommand in StartupMacroCommand.
/// </summary>
public class SignInProxy : ProxyBase
{
    public new const string NAME = "SignInProxy";

    public SignInInfoS2C Info { get; private set; }

    public SignInProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Register(MessageConst.SIGNIN_INFO_S2C, OnSignInInfoS2C);
        disp.Register(MessageConst.SIGNIN_DO_S2C, OnSignInDoS2C);
        disp.Register(MessageConst.SIGNIN_MAKEUP_S2C, OnSignInMakeUpS2C);

        // Sign-in info is pushed by server after login.
    }

    public override void OnRemove()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Unregister(MessageConst.SIGNIN_INFO_S2C, OnSignInInfoS2C);
        disp.Unregister(MessageConst.SIGNIN_DO_S2C, OnSignInDoS2C);
        disp.Unregister(MessageConst.SIGNIN_MAKEUP_S2C, OnSignInMakeUpS2C);
    }

    #region Callbacks

    private void OnSignInInfoS2C(byte[] body)
    {
        Info = NetworkMessageHelper.ParseSignInInfoS2C(body);
        Log.d($"Sign-in info: day={Info.SignDay}, streak={Info.ConsecutiveDays}, canSign={Info.CanSignToday}", NAME);
        SendNotification(NotificationConst.SIGNIN_INFO_UPDATED);
    }

    private void OnSignInDoS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseSignInDoS2C(body);
        Log.d($"Sign-in result: {(resp.Rst.Result ? $"day={resp.Day}, streak={resp.ConsecutiveDays}" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (resp.Rst.Result) NetworkMessageHelper.SendSignInInfo();
        SendNotification(NotificationConst.SIGNIN_DO_RESULT, resp);
    }

    private void OnSignInMakeUpS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseSignInMakeUpS2C(body);
        Log.d($"Make-up result: {(resp.Rst.Result ? $"day={resp.Day}" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (resp.Rst.Result) NetworkMessageHelper.SendSignInInfo();
        SendNotification(NotificationConst.SIGNIN_DO_RESULT, resp);
    }

    #endregion
}
