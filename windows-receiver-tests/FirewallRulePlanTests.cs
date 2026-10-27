using PocketMicReceiver;
using Xunit;

namespace PocketMicReceiver.Tests;

public class FirewallRulePlanTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(65535)]
    public void InvalidPortCannotGeneratePrivilegedPlan(int port) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FirewallRulePlan.Build(@"C:\Apps\Receiver.exe", port));

    [Fact]
    public void PlanScopesExactExecutableCustomPortsSubnetAndPrivateProfile()
    {
        var script = FirewallRulePlan.Build(@"C:\Apps\Ryan's Receiver.exe", 49520);
        Assert.Contains("$program = 'C:\\Apps\\Ryan''s Receiver.exe'", script);
        Assert.Contains("-LocalPort 49520,49521,49501", script);
        Assert.Contains("-RemoteAddress LocalSubnet", script);
        Assert.Contains("-Profile Private", script);
        Assert.Contains("-Program $program", script);
        Assert.Contains("-Protocol UDP", script);
        Assert.DoesNotContain("Remove-NetFirewallRule", script);
        Assert.DoesNotContain("Set-NetConnectionProfile", script);
        Assert.Contains("if ($blocked.Count)", script);
        Assert.Contains("$existing.Group -ne 'PocketMic LAN'", script);
    }

    [Fact]
    public void RuleIdentityIsStablePerExecutableAndSeparateAcrossInstallations()
    {
        var first = FirewallRulePlan.Build(@"C:\Apps\Receiver.exe", 49500);
        var again = FirewallRulePlan.Build(@"C:\Apps\Receiver.exe", 49500);
        var other = FirewallRulePlan.Build(@"C:\Other\Receiver.exe", 49500);
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }
}
