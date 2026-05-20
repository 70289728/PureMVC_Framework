using System.Collections.Generic;

/// <summary>
/// Sign-in reward configuration: 7-day cycle, rewards increase per day.
/// </summary>
public static class SignInConfig
{
    public static readonly List<(int type, int itemId, int count)> Rewards = new List<(int, int, int)>
    {
        // Day 1
        (1, 0, 100),   // gold 100
        // Day 2
        (1, 0, 200),
        // Day 3
        (2, 0, 10),    // diamond 10
        // Day 4
        (1, 0, 300),
        // Day 5
        (2, 0, 20),
        // Day 6
        (1, 0, 500),
        // Day 7
        (2, 0, 50),    // diamond 50
    };
}
