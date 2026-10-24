/*
 * OGL FastMap 2.0: shared, bounded startup map renderer.
 * Independent from Scene so contention/cancellation can be regression tested.
 */
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSim.Region.Framework.Scenes
{
    internal static class OglFastMapStartupQueue
    {
        // One CPU-heavy Warp3D renderer per simulator process. The permit is
        // held until the complete render + grid re-registration has finished.
        private static readonly SemaphoreSlim s_renderSlot = new(1, 1);

        internal static async Task RunAsync(Action generateAndPublish,
            int delaySeconds, CancellationToken cancellation)
        {
            if (generateAndPublish is null)
                throw new ArgumentNullException(nameof(generateAndPublish));
            if (delaySeconds < 0 || delaySeconds > 3600)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));

            // Never run the expensive synchronous renderer on the simulator
            // startup/registration thread, including when the delay is zero.
            await Task.Yield();
            if (delaySeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellation);

            await s_renderSlot.WaitAsync(cancellation);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                generateAndPublish();
            }
            finally
            {
                s_renderSlot.Release();
            }
        }
    }
}
