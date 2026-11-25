using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ASTRO.Polswid.Core.Timing;

public static class Clock
{
    static readonly double UsPerTick = 1_000_000.0 / Stopwatch.Frequency;

    /// <summary>Monotonic microseconds.</summary>
    public static long NowUs => (long)(Stopwatch.GetTimestamp() * UsPerTick);

    public static long TicksToUs(long ticks) => (long)(ticks * UsPerTick);
}

/// <summary>
/// Sleep-free scheduler for the real-time loop: a high-resolution waitable timer covers the bulk of the wait
/// (Windows 10 1803+), then a short spin finishes it with microsecond accuracy. Never uses Thread.Sleep.
/// </summary>
public sealed class PrecisionWaiter : IDisposable
{
    const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x2;
    const uint TIMER_ALL_ACCESS = 0x1F0003;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateWaitableTimerExW(IntPtr attrs, string? name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period, IntPtr completion, IntPtr arg, bool resume);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr handle, uint ms);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
    [DllImport("winmm.dll")]
    static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")]
    static extern uint timeEndPeriod(uint ms);

    readonly IntPtr _timer;
    readonly bool _periodRaised;
    readonly long _spinWindowUs;

    /// <summary>True when the OS granted a high-resolution timer.</summary>
    public bool HighResolution { get; }

    public PrecisionWaiter(long spinWindowUs = 300)
    {
        _spinWindowUs = spinWindowUs;
        _timer = CreateWaitableTimerExW(IntPtr.Zero, null, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
        HighResolution = _timer != IntPtr.Zero;
        if (!HighResolution)
        {
            // Older Windows: fall back to a normal waitable timer with a 1 ms system timer period.
            _timer = CreateWaitableTimerExW(IntPtr.Zero, null, 0, TIMER_ALL_ACCESS);
            _periodRaised = timeBeginPeriod(1) == 0;
            _spinWindowUs = Math.Max(spinWindowUs, 1500);
        }
    }

    /// <summary>Block until the monotonic <see cref="Clock.NowUs"/> reaches <paramref name="targetUs"/>.</summary>
    public void WaitUntil(long targetUs)
    {
        long remaining = targetUs - Clock.NowUs;
        if (remaining > _spinWindowUs + 100 && _timer != IntPtr.Zero)
        {
            long due = -((remaining - _spinWindowUs) * 10); // 100 ns units, negative = relative
            if (SetWaitableTimer(_timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
                WaitForSingleObject(_timer, 100);
        }
        int spins = 0;
        while (Clock.NowUs < targetUs)
        {
            Thread.SpinWait(20);
            if (++spins > 2000) { Thread.Yield(); spins = 0; }
        }
    }

    public void Dispose()
    {
        if (_timer != IntPtr.Zero) CloseHandle(_timer);
        if (_periodRaised) timeEndPeriod(1);
    }
}

/// <summary>Single-writer rolling latency statistics (microseconds). Reads are approximate by design.</summary>
public sealed class LatencyStats
{
    readonly long[] _ring;
    int _idx;
    long _count, _max;

    public LatencyStats(int window = 2048) => _ring = new long[window];

    public void Record(long us)
    {
        _ring[_idx] = us;
        _idx = (_idx + 1) % _ring.Length;
        _count++;
        if (us > Volatile.Read(ref _max)) Volatile.Write(ref _max, us);
    }

    public long Count => Volatile.Read(ref _count);
    public long MaxEver => Volatile.Read(ref _max);

    public (double AvgUs, long MaxUs, long P99Us) Snapshot()
    {
        int n = (int)Math.Min(Volatile.Read(ref _count), _ring.Length);
        if (n == 0) return (0, 0, 0);
        var copy = new long[n];
        Array.Copy(_ring, copy, n);
        Array.Sort(copy);
        double sum = 0;
        foreach (var v in copy) sum += v;
        return (sum / n, copy[n - 1], copy[Math.Min(n - 1, (int)(n * 0.99))]);
    }
}
