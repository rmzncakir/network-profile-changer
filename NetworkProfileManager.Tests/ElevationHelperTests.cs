using System;
using System.Collections.Generic;
using System.IO;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Tests;

/// <summary>
/// Tests the security boundary of the elevated netsh worker. ValidateCommand is
/// the whitelist that keeps the runas'd worker from becoming a generic elevated
/// command runner, so its accept/reject behavior is safety-critical.
/// </summary>
public class ElevationHelperTests
{
    private static List<string> Cmd(params string[] args) => new(args);

    // ── Accepted: the exact command shapes NetworkService issues ──────

    [Fact]
    public void ValidateCommand_AllowsSetAddressStatic()
    {
        ElevationHelper.ValidateCommand(
            Cmd("interface", "ip", "set", "address", "name=Ethernet", "static",
                "192.168.1.10", "255.255.255.0", "192.168.1.1"));
    }

    [Fact]
    public void ValidateCommand_AllowsSetAddressDhcp()
    {
        ElevationHelper.ValidateCommand(
            Cmd("interface", "ip", "set", "address", "name=Wi-Fi", "source=dhcp"));
    }

    [Fact]
    public void ValidateCommand_AllowsSetAndAddDns()
    {
        ElevationHelper.ValidateCommand(
            Cmd("interface", "ip", "set", "dns", "name=Ethernet", "static", "8.8.8.8", "primary"));
        ElevationHelper.ValidateCommand(
            Cmd("interface", "ip", "add", "dns", "name=Ethernet", "8.8.4.4", "index=2"));
    }

    [Fact]
    public void ValidateCommand_AllowsAdapterNameWithSpaces()
    {
        ElevationHelper.ValidateCommand(
            Cmd("interface", "ip", "set", "address", "name=Ethernet 2", "source=dhcp"));
    }

    // ── Rejected: anything outside the interface-ip surface ───────────

    [Theory]
    [InlineData("advfirewall", "set", "allprofiles", "state", "off")]
    [InlineData("interface", "portproxy", "add", "v4tov4")]      // ip != portproxy
    [InlineData("wlan", "show", "profiles")]
    [InlineData("interface")]                                     // too short
    [InlineData("interface", "ip")]                               // too short
    [InlineData("interface", "ip", "delete", "address", "name=Eth")]  // delete not allowed
    [InlineData("interface", "ip", "set", "route", "name=Eth")]       // route not allowed
    [InlineData("interface", "ip", "add", "address", "name=Eth")]     // add address not allowed
    public void ValidateCommand_RejectsNonInterfaceIp(params string[] args)
    {
        Assert.Throws<Exception>(() => ElevationHelper.ValidateCommand(Cmd(args)));
    }

    [Theory]
    [InlineData("192.168.1.1\" && calc")]   // embedded quote → shell-ish injection attempt
    [InlineData("name=Eth\r\nmalicious")]   // CRLF
    [InlineData("value\0null")]             // null byte
    [InlineData("with\x01control")]         // control char
    public void ValidateCommand_RejectsDirtyArguments(string dirtyArg)
    {
        var cmd = Cmd("interface", "ip", "set", "address", "name=Ethernet", dirtyArg);
        Assert.Throws<Exception>(() => ElevationHelper.ValidateCommand(cmd));
    }

    [Fact]
    public void ValidateCommand_RejectsEmptyArgument()
    {
        // Valid verb/object + length, but a blank trailing arg must still be refused.
        var cmd = Cmd("interface", "ip", "set", "dns", "");
        Assert.Throws<Exception>(() => ElevationHelper.ValidateCommand(cmd));
    }

    // ── RunWorker: file-based entry point, testable without elevation ─

    [Fact]
    public void RunWorker_ReturnsError_ForRejectedCommand()
    {
        string req = Path.Combine(Path.GetTempPath(), $"npm_test_req_{Guid.NewGuid():N}.json");
        string res = Path.Combine(Path.GetTempPath(), $"npm_test_res_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(req, "[[\"advfirewall\",\"set\",\"allprofiles\",\"state\",\"off\"]]");

            int code = ElevationHelper.RunWorker(req, res);

            Assert.Equal(1, code);
            Assert.True(File.Exists(res));
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(res)));
        }
        finally
        {
            if (File.Exists(req)) File.Delete(req);
            if (File.Exists(res)) File.Delete(res);
        }
    }

    [Fact]
    public void RunWorker_ReturnsError_ForEmptyRequest()
    {
        string req = Path.Combine(Path.GetTempPath(), $"npm_test_req_{Guid.NewGuid():N}.json");
        string res = Path.Combine(Path.GetTempPath(), $"npm_test_res_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(req, "[]");
            Assert.Equal(1, ElevationHelper.RunWorker(req, res));
        }
        finally
        {
            if (File.Exists(req)) File.Delete(req);
            if (File.Exists(res)) File.Delete(res);
        }
    }
}
