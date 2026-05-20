/// <summary>
/// Proxy for hot-update player extension data (player_ext.proto).
/// Registered at startup by RegisterHotUpdateProxiesCommand.
/// Triggers a request on login success via LoginSuccessCommand.
/// </summary>
public class PlayerExtProxy : ProxyBase
{
    public new const string NAME = "PlayerExtProxy";

    public PlayerExtProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        NetworkManager.Instance.Dispatcher.Register(
            HotUpdateMessageConst.PLAYER_EXT_S2C, OnPlayerExtS2C);
    }

    public override void OnRemove()
    {
        NetworkManager.Instance.Dispatcher.Unregister(
            HotUpdateMessageConst.PLAYER_EXT_S2C, OnPlayerExtS2C);
    }

    private void OnPlayerExtS2C(byte[] body)
    {
        var resp = HotNetworkMessageHelper.ParsePlayerExtS2C(body);
        if (!resp.Rst.Result)
        {
            Log.w($"PlayerExt failed, errCode={resp.Rst.ErrCode}", NAME);
            return;
        }

        var user = Facade.RetrieveProxy(UserProxy.NAME) as UserProxy;
        if (user != null)
        {
            user.SetExt("vipLevel", resp.VipLevel);
            user.SetExt("signature", resp.Signature);
            user.SetExt("battlePassExp", resp.BattlePassExp);
        }

        Log.d("Player extension data loaded.", NAME);
    }
}
