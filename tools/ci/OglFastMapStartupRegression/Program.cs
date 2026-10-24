using System;
using System.Threading;
using System.Threading.Tasks;
using OpenSim.Region.Framework.Scenes;

internal static class Program
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("OGL FastMap regression: " + message);
    }

    private static async Task Main()
    {
        int running = 0;
        int peak = 0;
        int completed = 0;
        Task[] jobs = new Task[8];
        for (int i = 0; i < jobs.Length; i++)
        {
            jobs[i] = OglFastMapStartupQueue.RunAsync(() =>
            {
                int current = Interlocked.Increment(ref running);
                if (current > peak) Interlocked.Exchange(ref peak, current);
                Thread.Sleep(35);
                Interlocked.Increment(ref completed);
                Interlocked.Decrement(ref running);
            }, 0, CancellationToken.None);
        }
        await Task.WhenAll(jobs);
        Check(peak == 1 && completed == jobs.Length,
            "multiple Warp3D workers ran concurrently");

        bool ranCancelled = false;
        using (var cts = new CancellationTokenSource())
        {
            Task pending = OglFastMapStartupQueue.RunAsync(
                () => ranCancelled = true, 1, cts.Token);
            cts.Cancel();
            try { await pending; throw new Exception("cancelled job completed"); }
            catch (OperationCanceledException) { }
            Check(!ranCancelled, "cancelled job rendered map");
        }

        bool failed = false;
        try
        {
            await OglFastMapStartupQueue.RunAsync(
                () => throw new InvalidOperationException("renderer failed"),
                0, CancellationToken.None);
        }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "render failure not propagated");

        bool recovered = false;
        await OglFastMapStartupQueue.RunAsync(
            () => recovered = true, 0, CancellationToken.None);
        Check(recovered, "failed render permanently held global slot");

        var delayWatch = System.Diagnostics.Stopwatch.StartNew();
        await OglFastMapStartupQueue.RunAsync(() => { }, 1, CancellationToken.None);
        Check(delayWatch.ElapsedMilliseconds >= 750,
            "configured post-registration delay was bypassed");

        bool rejected = false;
        try
        {
            await OglFastMapStartupQueue.RunAsync(() => { }, -1, CancellationToken.None);
        }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "negative startup delay was accepted");

        Console.WriteLine("OGL FastMap 2.0 startup queue: serialization, cancellation, failure recovery and delay OK");
    }
}
