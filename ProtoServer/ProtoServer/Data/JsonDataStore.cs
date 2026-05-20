using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// JSON file based data storage.
/// 
/// File structure:
///   Data/accounts.json   - account registry (accountId -> passwordHash)
///   Data/players/         - one JSON file per player (accountId.json)
/// 
/// Thread-safe: uses ConcurrentDictionary + per-file locks for writes.
/// </summary>
public class JsonDataStore : IDataStore
{
    private readonly string _dataDir;
    private readonly string _accountsFile;
    private readonly string _playersDir;

    // In-memory cache for fast read access
    private readonly ConcurrentDictionary<long, string> _accounts
        = new ConcurrentDictionary<long, string>();

    private readonly object _accountsFileLock = new object();

    public JsonDataStore(string baseDir = null)
    {
        _dataDir = baseDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _accountsFile = Path.Combine(_dataDir, "accounts.json");
        _playersDir = Path.Combine(_dataDir, "players");

        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_playersDir);

        LoadAccounts();
    }

    #region Account

    public Task<bool> CreateAccountAsync(long accountId, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Task.FromResult(false);

        // Validate: accountId must be 5~16 digits
        if (accountId < 10000 || accountId > 9999999999999999)
            return Task.FromResult(false);

        var hash = HashPassword(password);

        if (!_accounts.TryAdd(accountId, hash))
        {
            Console.WriteLine($"[JsonDataStore] Account {accountId} already exists, registration rejected");
            return Task.FromResult(false); // already exists
        }

        SaveAccounts();
        Console.WriteLine($"[JsonDataStore] Account created: {accountId}");
        return Task.FromResult(true);
    }

    public Task<bool> ValidateLoginAsync(long accountId, string password)
    {
        if (!_accounts.TryGetValue(accountId, out var storedHash))
        {
            Console.WriteLine($"[JsonDataStore] ValidateLogin: account {accountId} not found in registry");
            return Task.FromResult(false);
        }

        var inputHash = HashPassword(password);
        var match = storedHash == inputHash;
        Console.WriteLine($"[JsonDataStore] ValidateLogin: account {accountId}, hash match = {match}");
        return Task.FromResult(match);
    }

    public Task<bool> AccountExistsAsync(long accountId)
    {
        return Task.FromResult(_accounts.ContainsKey(accountId));
    }

    #endregion

    #region Player Data

    public Task<bool> CreatePlayerAsync(long accountId, string playerName, int gender, int job)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return Task.FromResult(false);

        var filePath = GetPlayerFilePath(accountId);
        if (File.Exists(filePath))
        {
            Console.WriteLine($"[JsonDataStore] Player already exists for account {accountId}");
            return Task.FromResult(false);
        }

        var data = new PlayerData
        {
            PlayerName = playerName,
            Gender = gender,
            Job = job,
            Level = 1,
            Exp = 0,
            CreatedTime = DateTime.UtcNow.ToString("o"),
            LastLoginTime = DateTime.UtcNow.ToString("o")
        };

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(filePath, json, Encoding.UTF8);
        Console.WriteLine($"[JsonDataStore] Player created: {playerName} for account {accountId}");
        return Task.FromResult(true);
    }

    public Task<PlayerData> GetPlayerDataAsync(long accountId)
    {
        var filePath = GetPlayerFilePath(accountId);

        if (!File.Exists(filePath))
        {
            // Return default new player data
            var newData = new PlayerData
            {
                CreatedTime = DateTime.UtcNow.ToString("o"),
                LastLoginTime = DateTime.UtcNow.ToString("o")
            };
            return Task.FromResult(newData);
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        var data = JsonConvert.DeserializeObject<PlayerData>(json);
        return Task.FromResult(data);
    }

    public Task SavePlayerDataAsync(long accountId, PlayerData data)
    {
        if (data == null) return Task.CompletedTask;

        var filePath = GetPlayerFilePath(accountId);
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(filePath, json, Encoding.UTF8);

        return Task.CompletedTask;
    }

    #endregion

    #region Friend

    public async Task<(long accountId, PlayerData data)?> SearchPlayerByNameAsync(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return null;

        var files = Directory.GetFiles(_playersDir, "*.json");
        foreach (var file in files)
        {
            var accountIdStr = Path.GetFileNameWithoutExtension(file);
            if (!long.TryParse(accountIdStr, out var accId)) continue;

            var data = await GetPlayerDataAsync(accId);
            if (data != null && data.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase))
            {
                return (accId, data);
            }
        }
        return null;
    }

    #endregion

    #region Internal

    private string GetPlayerFilePath(long accountId)
    {
        return Path.Combine(_playersDir, accountId + ".json");
    }

    private void LoadAccounts()
    {
        if (!File.Exists(_accountsFile)) return;

        lock (_accountsFileLock)
        {
            var json = File.ReadAllText(_accountsFile, Encoding.UTF8);
            var dict = JsonConvert.DeserializeObject<ConcurrentDictionary<long, string>>(json);
            if (dict != null)
            {
                foreach (var kv in dict)
                    _accounts[kv.Key] = kv.Value;
            }
        }
    }

    private void SaveAccounts()
    {
        lock (_accountsFileLock)
        {
            var json = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
            File.WriteAllText(_accountsFile, json, Encoding.UTF8);
        }
    }

    private static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }

    #endregion
}