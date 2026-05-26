using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Test ConfigManager thread-safety and cache behavior.
/// Uses Register/GetAll internally — no file I/O needed.
/// </summary>
public class ConfigManagerTests
{
    // Dummy config type for testing
    public class TestConfig
    {
        public int id;
        public string name;
    }

    [SetUp]
    public void Setup()
    {
        ConfigManager.ClearAll();
    }

    [TearDown]
    public void Teardown()
    {
        ConfigManager.ClearAll();
    }

    #region Register / GetAll / IsLoaded

    [Test]
    public void Register_ThenGetAll_ReturnsSameData()
    {
        var data = new List<TestConfig>
        {
            new TestConfig { id = 1, name = "A" },
            new TestConfig { id = 2, name = "B" }
        };

        ConfigManager.Register(data);
        var result = ConfigManager.GetAll<TestConfig>();

        Assert.NotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("A", result[0].name);
    }

    [Test]
    public void GetAll_NotLoaded_ReturnsNull()
    {
        var result = ConfigManager.GetAll<TestConfig>();
        Assert.IsNull(result);
    }

    [Test]
    public void IsLoaded_AfterRegister_ReturnsTrue()
    {
        Assert.IsFalse(ConfigManager.IsLoaded<TestConfig>());
        ConfigManager.Register(new List<TestConfig>());
        Assert.IsTrue(ConfigManager.IsLoaded<TestConfig>());
    }

    [Test]
    public void ClearAll_RemovesAllCache()
    {
        ConfigManager.Register(new List<TestConfig>());
        Assert.IsTrue(ConfigManager.IsLoaded<TestConfig>());

        ConfigManager.ClearAll();
        Assert.IsFalse(ConfigManager.IsLoaded<TestConfig>());
    }

    #endregion

    #region Thread Safety

    [Test]
    public void ConcurrentRegister_NoException()
    {
        int threadCount = 10;
        var barrier = new Barrier(threadCount);
        int exceptions = 0;
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int id = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    var data = new List<TestConfig>
                    {
                        new TestConfig { id = id, name = $"T{id}" }
                    };
                    // Each thread registers (last one wins, no crash)
                    ConfigManager.Register(data);
                }
                catch
                {
                    Interlocked.Increment(ref exceptions);
                }
            });
        }

        Task.WaitAll(tasks);
        Assert.AreEqual(0, exceptions, "Concurrent Register threw exceptions");
    }

    [Test]
    public void ConcurrentGetAll_DuringRegister_NoException()
    {
        int exceptions = 0;
        var ready = new ManualResetEventSlim(false);
        var done = new ManualResetEventSlim(false);

        // Writer: rapidly re-registers
        var writer = Task.Run(() =>
        {
            ready.Wait();
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    ConfigManager.Register(new List<TestConfig>
                    {
                        new TestConfig { id = i, name = $"W{i}" }
                    });
                }
                catch
                {
                    Interlocked.Increment(ref exceptions);
                }
            }
            done.Set();
        });

        // Reader: reads while writer is active
        var reader = Task.Run(() =>
        {
            ready.Wait();
            while (!done.IsSet)
            {
                try
                {
                    var result = ConfigManager.GetAll<TestConfig>();
                    // Just accessing, no crash is success
                }
                catch
                {
                    Interlocked.Increment(ref exceptions);
                }
                Thread.Sleep(1);
            }
        });

        ready.Set();
        writer.Wait();
        reader.Wait();

        Assert.AreEqual(0, exceptions, "Concurrent read/write threw exceptions");
    }

    #endregion
}
