using System;
using System.Threading;
using System.Threading.Tasks;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;

namespace NetworkProfileManager.Tests;

public class IpScannerServiceTests
{
    [Fact]
    public async Task ScanRangeAsync_RaisesProgress_ForEachAddress()
    {
        var svc = new IpScannerService();
        int progressCount = 0;
        int? lastTotal = null;

        var progress = new Progress<ScanProgress>(p =>
        {
            Interlocked.Increment(ref progressCount);
            lastTotal = p.Total;
        });

        // 192.0.2.0/24 is TEST-NET-1 (RFC 5737) — guaranteed unrouted
        await svc.ScanRangeAsync("192.0.2.1", "192.0.2.3",
            timeoutMs: 100, maxParallel: 8, progress, CancellationToken.None);

        Assert.True(progressCount > 0, "no progress callbacks fired");
        Assert.Equal(3, lastTotal);
    }

    [Fact]
    public async Task ScanRangeAsync_RespectsCancellation()
    {
        var svc = new IpScannerService();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        var task = svc.ScanRangeAsync("192.0.2.1", "192.0.2.100",
            timeoutMs: 2000, maxParallel: 4,
            new Progress<ScanProgress>(_ => { }), cts.Token);

        var completed = await Task.WhenAny(task, Task.Delay(10_000));
        Assert.Same(task, completed);
    }
}
