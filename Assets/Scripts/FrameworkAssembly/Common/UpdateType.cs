public enum UpdateType
{
    Update,
    FixedUpdate,
    LateUpdate
}

public enum UpdateFrequency
{
    EveryFrame,      // Every frame (default)
    Low,             // Every 0.1s (10fps)
    Medium,          // Every 0.05s (20fps)  
    High             // Every 0.033s (30fps)
}
