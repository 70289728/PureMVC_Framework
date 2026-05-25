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

    // Name → accountId index for fast player search
    private readonly ConcurrentDictionary<string, long> _nameIndex
        = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);

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

        var match = VerifyAndUpgradePassword(password, accountId, ref storedHash);
        Console.WriteLine($"[JsonDataStore] ValidateLogin: account {accountId}, password match = {match}");
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
        _nameIndex.TryAdd(playerName, accountId);
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

        // O(1) index lookup instead of O(n) directory scan
        if (!_nameIndex.TryGetValue(playerName.Trim(), out var accId))
            return null;

        var data = await GetPlayerDataAsync(accId);
        if (data != null && data.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase))
            return (accId, data);

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

    // PBKDF2 parameters
    private const int Pbkdf2Iterations = 100000;
    private const int Pbkdf2SaltSize = 16; // 128-bit salt
    private const int Pbkdf2HashSize = 32; // 256-bit output

    /// <summary>
    /// Hash password using PBKDF2-SHA256 with random salt.
    /// Format: base64(salt + hash)
    /// </summary>
    private static string HashPassword(string password)
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] salt = new byte[Pbkdf2SaltSize];
            rng.GetBytes(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(Pbkdf2HashSize);
                byte[] combined = new byte[Pbkdf2SaltSize + Pbkdf2HashSize];
                Buffer.BlockCopy(salt, 0, combined, 0, Pbkdf2SaltSize);
                Buffer.BlockCopy(hash, 0, combined, Pbkdf2SaltSize, Pbkdf2HashSize);
                return Convert.ToBase64String(combined);
            }
        }
    }

    /// <summary>
    /// Verify a password against a PBKDF2 hash (or legacy SHA256 hash, auto-upgrades).
    /// </summary>
    private bool VerifyAndUpgradePassword(string password, long accountId, ref string storedHash)
    {
        // Try PBKDF2 verification first
        if (VerifyPassword(password, storedHash))
            return true;

        // Fallback: legacy SHA256 (pre-2026-05-25), auto-upgrade on success
        if (TryLegacySha256Verify(password, storedHash))
        {
            var newHash = HashPassword(password);
            _accounts[accountId] = newHash;
            SaveAccounts();
            Console.WriteLine($"[JsonDataStore] Account {accountId}: password hash auto-upgraded to PBKDF2");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Legacy SHA256 verification for migration support.
    /// Returns true if the stored hash is a 64-char hex string matching SHA256(password).
    /// </summary>
    private static bool TryLegacySha256Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash) || storedHash.Length != 64)
            return false;

        // Check if storedHash is a hex string (all hex characters)
        foreach (char c in storedHash)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;

        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return storedHash.Equals(hex, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Verify password against PBKDF2 hash.
    /// </summary>
    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            byte[] combined = Convert.FromBase64String(storedHash);
            if (combined.Length != Pbkdf2SaltSize + Pbkdf2HashSize)
                return false;

            byte[] salt = new byte[Pbkdf2SaltSize];
            byte[] expectedHash = new byte[Pbkdf2HashSize];
            Buffer.BlockCopy(combined, 0, salt, 0, Pbkdf2SaltSize);
            Buffer.BlockCopy(combined, Pbkdf2SaltSize, expectedHash, 0, Pbkdf2HashSize);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256))
            {
                byte[] actualHash = pbkdf2.GetBytes(Pbkdf2HashSize);
                return ConstantTimeEquals(actualHash, expectedHash);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Constant-time byte array comparison to prevent timing attacks.
    /// </summary>
    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;
        int result = 0;
        for (int i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }

    #endregion
}