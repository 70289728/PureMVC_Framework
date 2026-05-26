using NUnit.Framework;
using PureMVC.Interfaces;
using PureMVC.Patterns.Facade;
using PureMVC.Patterns.Observer;
using System;
using System.Collections;
using UnityEngine.TestTools;

/// <summary>
/// Test CommandBase and MacroCommandBase global exception handling.
/// Verifies that OnExecute exceptions are caught and SYS_ERROR notification is sent.
/// </summary>
public class CommandExceptionTests
{
    private Facade facade;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        facade = Facade.GetInstance(() => new Facade()) as Facade;
    }

    #region Test Commands

    public class ThrowingCommand : CommandBase
    {
        public bool WasExecuted = false;

        protected override void OnExecute(INotification notification)
        {
            WasExecuted = true;
            throw new InvalidOperationException("test exception from ThrowingCommand");
        }
    }

    public class NormalCommand : CommandBase
    {
        public bool WasExecuted = false;

        protected override void OnExecute(INotification notification)
        {
            WasExecuted = true;
        }
    }

    public class ThrowingMacroCommand : MacroCommandBase
    {
        public bool WasExecuted = false;

        protected override void InitializeMacroCommand()
        {
            WasExecuted = true;
            AddSubCommand(() => new ThrowingCommand());
        }
    }

    /// <summary>
    /// Captures a flag when executed — used to verify SYS_ERROR dispatch.
    /// </summary>
    public class FlagCaptureCommand : CommandBase
    {
        public bool WasCalled = false;

        protected override void OnExecute(INotification notification)
        {
            WasCalled = true;
        }
    }

    #endregion

    #region CommandBase Tests

    [UnityTest]
    public IEnumerator CommandBase_ThrowingOnExecute_SendsSysError()
    {
        LogAssert.ignoreFailingMessages = true;

        var captureCmd = new FlagCaptureCommand();
        facade.RegisterCommand(NotificationConst.SYS_ERROR, () => captureCmd);

        var cmd = new ThrowingCommand();
        var notif = new Notification("TEST_NOTIF", null, null);

        bool threw = false;
        try { cmd.Execute(notif); }
        catch { threw = true; }

        Assert.IsFalse(threw, "Exception should be caught, not propagated");
        Assert.IsTrue(cmd.WasExecuted, "OnExecute should have been called");
        Assert.IsTrue(captureCmd.WasCalled, "SYS_ERROR notification should be dispatched");

        facade.RemoveCommand(NotificationConst.SYS_ERROR);
        LogAssert.ignoreFailingMessages = false;
        yield return null;
    }

    [Test]
    public void CommandBase_NormalExecute_CompletesCleanly()
    {
        var cmd = new NormalCommand();
        var notif = new Notification("TEST_NOTIF", null, null);

        bool threw = false;
        try { cmd.Execute(notif); }
        catch { threw = true; }

        Assert.IsFalse(threw);
        Assert.IsTrue(cmd.WasExecuted);
    }

    #endregion

    #region MacroCommandBase Tests

    [UnityTest]
    public IEnumerator MacroCommandBase_ThrowingSubCommand_SendsSysError()
    {
        LogAssert.ignoreFailingMessages = true;

        var captureCmd = new FlagCaptureCommand();
        facade.RegisterCommand(NotificationConst.SYS_ERROR, () => captureCmd);

        var cmd = new ThrowingMacroCommand();
        var notif = new Notification("TEST_MACRO", null, null);

        bool threw = false;
        try { cmd.Execute(notif); }
        catch { threw = true; }

        Assert.IsFalse(threw, "MacroCommand should not crash on sub-command exception");
        Assert.IsTrue(cmd.WasExecuted, "InitializeMacroCommand should have been called");
        Assert.IsTrue(captureCmd.WasCalled, "SYS_ERROR notification should be dispatched from sub-command");

        facade.RemoveCommand(NotificationConst.SYS_ERROR);
        LogAssert.ignoreFailingMessages = false;
        yield return null;
    }

    #endregion
}
