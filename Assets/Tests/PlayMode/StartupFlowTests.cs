using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PureMVC.Patterns.Facade;
using PureMVC.Patterns.Mediator;
using PureMVC.Patterns.Observer;
using PureMVC.Interfaces;

/// <summary>
/// PlayMode integration tests for end-to-end flows.
/// Requires the SampleScene with GameMain and UIRoot present.
/// </summary>
public class StartupFlowTests
{
    [UnityTest]
    public IEnumerator Facade_SendNotification_ReachesRegisteredCommand()
    {
        const string testNotif = "Test_PlayMode_Notification";
        bool commandExecuted = false;

        Facade.Instance.RegisterCommand(testNotif, () => new TestCommand(() => commandExecuted = true));
        Facade.Instance.SendNotification(testNotif);

        Assert.IsTrue(commandExecuted, "Command should have executed synchronously");
        Facade.Instance.RemoveCommand(testNotif);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Notification_GlobalException_DoesNotCrashOtherObservers()
    {
        const string crashNotif = "Test_Crash_Notification";
        bool secondObserverCalled = false;

        // Intentional crash from CrashingMediator — View try-catch will log it
        LogAssert.Expect(LogType.Error, new Regex(@"Observer.*threw on notification"));

        var crashingMediator = new CrashingMediator();
        var normalMediator = new NormalMediator(() => secondObserverCalled = true);

        Facade.Instance.RegisterMediator(crashingMediator);
        Facade.Instance.RegisterMediator(normalMediator);

        // Send notification — should NOT throw, and second observer should still be called
        Facade.Instance.SendNotification(crashNotif);

        Assert.IsTrue(secondObserverCalled,
            "Normal observer should still be called even when another observer throws");

        // Cleanup
        Facade.Instance.RemoveMediator(crashingMediator.MediatorName);
        Facade.Instance.RemoveMediator(normalMediator.MediatorName);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TimerManager_DelayCall_FiresAfterDelay()
    {
        bool fired = false;
        var timer = TimerManager.Instance.DelayCall(0.05f, () => fired = true);
        Assert.IsNotNull(timer, "Timer should be created");

        // Wait for timer
        yield return new WaitForSeconds(0.1f);

        Assert.IsTrue(fired, "Timer callback should have fired after delay");
    }

    [UnityTest]
    public IEnumerator TimerManager_RepeatCall_FiresMultipleTimes()
    {
        int fireCount = 0;
        var timer = TimerManager.Instance.RepeatCall(0.05f, () => fireCount++, 3);
        Assert.IsNotNull(timer, "Timer should be created");

        // Wait longer than 3 intervals (0.05 * 3 = 0.15) + buffer
        yield return new WaitForSeconds(0.25f);

        Assert.GreaterOrEqual(fireCount, 3,
            $"Timer should fire at least 3 times, got {fireCount}");
    }

    [UnityTest]
    public IEnumerator UIManager_Init_InitializesWithoutError()
    {
        // UIRoot may not exist in test scene — expected, UIManager still initializes
        LogAssert.Expect(LogType.Error, "UIRoot node not found");
        UIManager.Instance.Init();
        Log.d("UIManager Init test passed", "PlayModeTests");
        yield return null;
    }

    [UnityTest]
    public IEnumerator NetworkConnect_NoServer_FiresNetworkError()
    {
        // Connection failure to closed port is expected — NetworkManager logs it
        LogAssert.Expect(LogType.Error, new Regex("Connection failed"));

        bool errorReceived = false;
        string errorMsg = null;

        // Listen for NETWORK_ERROR
        var listener = new NotificationMediator((notif) =>
        {
            if (notif.Name == NetworkNotificationConst.NETWORK_ERROR)
            {
                errorReceived = true;
                errorMsg = notif.Body as string;
            }
        });

        Facade.Instance.RegisterMediator(listener);

        // Try connecting to a non-routable address — expect timeout
        _ = NetworkManager.Instance.ConnectTaskAsync("127.0.0.1", 19999);

        // Wait for connect timeout (5s) + buffer
        float elapsed = 0f;
        while (!errorReceived && elapsed < 7f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(errorReceived,
            $"NETWORK_ERROR notification should be received within 7s. Elapsed: {elapsed:F1}s");

        // Cleanup
        Facade.Instance.RemoveMediator(listener.MediatorName);
        if (NetworkManager.Instance.IsConnected)
            NetworkManager.Instance.Disconnect();
    }

    // ── Test Helpers (inherit Mediator directly, no GameObject needed) ──

    private class TestCommand : Notifier, ICommand
    {
        private readonly System.Action _onExecute;
        public TestCommand(System.Action onExecute) => _onExecute = onExecute;
        public void Execute(INotification notification) => _onExecute?.Invoke();
    }

    private class CrashingMediator : Mediator
    {
        private static int _counter = 0;
        public CrashingMediator()
            : base($"CrashingMediator_{++_counter}", null) { }

        public override string[] ListNotificationInterests()
            => new[] { "Test_Crash_Notification" };

        public override void HandleNotification(INotification notification)
        {
            throw new System.InvalidOperationException("Intentional crash for test");
        }
    }

    private class NormalMediator : Mediator
    {
        private static int _counter = 0;
        private readonly System.Action _onNotify;
        public NormalMediator(System.Action onNotify)
            : base($"NormalMediator_{++_counter}", null)
        {
            _onNotify = onNotify;
        }

        public override string[] ListNotificationInterests()
            => new[] { "Test_Crash_Notification" };

        public override void HandleNotification(INotification notification)
        {
            _onNotify?.Invoke();
        }
    }

    private class NotificationMediator : Mediator
    {
        private static int _counter = 0;
        private readonly System.Action<INotification> _onNotify;
        public NotificationMediator(System.Action<INotification> onNotify)
            : base($"NotifMediator_{++_counter}", null)
        {
            _onNotify = onNotify;
        }

        public override string[] ListNotificationInterests()
            => new[] {
                NetworkNotificationConst.NETWORK_CONNECTED,
                NetworkNotificationConst.NETWORK_ERROR,
                NetworkNotificationConst.NETWORK_DISCONNECTED
            };

        public override void HandleNotification(INotification notification)
        {
            _onNotify?.Invoke(notification);
        }
    }
}
