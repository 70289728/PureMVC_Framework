/// <summary>
/// PureMVC Proxy for hot update state management.
/// Holds the current state, version info, and progress data.
/// </summary>
public class HotUpdateProxy : ProxyBase
{
    public new const string NAME = "HotUpdateProxy";

    public HotUpdateState State { get; set; } = HotUpdateState.Idle;
    public string CurrentVersion { get; set; } = "";
    public string ServerVersion { get; set; } = "";
    public float Progress { get; set; } = 0f;
    public string StatusMessage { get; set; } = "";
    public bool IsUpdateNeeded { get; set; } = false;

    public HotUpdateProxy() : base(NAME, null)
    {
    }
}
