using System.Threading.Tasks;

/// <summary>
/// Data storage abstraction.
/// Current implementation: JSON file (JsonDataStore).
/// Can be replaced with SQLite/MySQL later by implementing this interface.
/// </summary>
public interface IDataStore
{
    #region Account

    /// <summary>Register a new account. Returns false if account already exists.</summary>
    Task<bool> CreateAccountAsync(long accountId, string password);

    /// <summary>Validate login credentials. Returns true if valid.</summary>
    Task<bool> ValidateLoginAsync(long accountId, string password);

    /// <summary>Check if an account exists.</summary>
    Task<bool> AccountExistsAsync(long accountId);

    #endregion

    #region Player Data

    /// <summary>
    /// Create a player character for the account. Returns false if player already exists.
    /// </summary>
    Task<bool> CreatePlayerAsync(long accountId, string playerName, int gender, int job);

    /// <summary>
    /// Get player game data. Returns default (new) data if player doesn't exist yet.
    /// </summary>
    Task<PlayerData> GetPlayerDataAsync(long accountId);

    /// <summary>Save player game data.</summary>
    Task SavePlayerDataAsync(long accountId, PlayerData data);

    #endregion

    #region Friend

    /// <summary>Search for a player by name. Returns (accountId, PlayerData) or null.</summary>
    Task<(long accountId, PlayerData data)?> SearchPlayerByNameAsync(string playerName);

    #endregion
}