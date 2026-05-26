using NUnit.Framework;

/// <summary>
/// Test UserProxy data integrity: SetGold/SetDiamond validation.
/// </summary>
public class UserProxyTests
{
    private UserProxy proxy;

    [SetUp]
    public void Setup()
    {
        proxy = new UserProxy();
        // Set a baseline so we can detect changes
        proxy.userData.Gold = 500;
        proxy.userData.Diamond = 100;
    }

    [TearDown]
    public void Teardown()
    {
        proxy = null;
    }

    #region SetGold / AddGold

    [Test]
    public void SetGold_PositiveValue_UpdatesCorrectly()
    {
        proxy.SetGold(300);
        Assert.AreEqual(300, proxy.userData.Gold);
    }

    [Test]
    public void SetGold_Zero_UpdatesCorrectly()
    {
        proxy.SetGold(0);
        Assert.AreEqual(0, proxy.userData.Gold);
    }

    [Test]
    public void SetGold_Negative_ClampsToZero()
    {
        proxy.SetGold(-100);
        Assert.AreEqual(0, proxy.userData.Gold);
    }

    [Test]
    public void SetGold_LargeValue_NoOverflow()
    {
        proxy.SetGold(int.MaxValue);
        Assert.AreEqual(int.MaxValue, proxy.userData.Gold);
        proxy.SetGold(1);
        Assert.AreEqual(1, proxy.userData.Gold);
    }

    [Test]
    public void AddGold_Positive_Accumulates()
    {
        int before = proxy.userData.Gold;
        proxy.AddGold(50);
        Assert.AreEqual(before + 50, proxy.userData.Gold);
    }

    [Test]
    public void AddGold_Zero_NoChange()
    {
        int before = proxy.userData.Gold;
        proxy.AddGold(0);
        Assert.AreEqual(before, proxy.userData.Gold);
    }

    [Test]
    public void AddGold_Negative_DeductsThenClamps()
    {
        // 500 + (-200) = 300
        proxy.AddGold(-200);
        Assert.AreEqual(300, proxy.userData.Gold);

        // 300 + (-500) = -200 → clamped to 0
        proxy.AddGold(-500);
        Assert.AreEqual(0, proxy.userData.Gold);
    }

    #endregion

    #region SetDiamond / AddDiamond

    [Test]
    public void SetDiamond_PositiveValue_UpdatesCorrectly()
    {
        proxy.SetDiamond(200);
        Assert.AreEqual(200, proxy.userData.Diamond);
    }

    [Test]
    public void SetDiamond_Negative_ClampsToZero()
    {
        proxy.SetDiamond(-50);
        Assert.AreEqual(0, proxy.userData.Diamond);
    }

    [Test]
    public void AddDiamond_Positive_Accumulates()
    {
        int before = proxy.userData.Diamond;
        proxy.AddDiamond(30);
        Assert.AreEqual(before + 30, proxy.userData.Diamond);
    }

    [Test]
    public void AddDiamond_Negative_DeductsThenClamps()
    {
        proxy.AddDiamond(-200);
        Assert.AreEqual(0, proxy.userData.Diamond);
    }

    #endregion
}
