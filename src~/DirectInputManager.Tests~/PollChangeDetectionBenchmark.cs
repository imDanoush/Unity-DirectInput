using System;
using System.Diagnostics;
using DirectInputManager;
using Xunit;
using Xunit.Abstractions;

namespace DirectInputManager.Tests
{
    /// <summary>
    /// Baseline measurement for the per-poll change detection in DIManager.Poll,
    /// which hashes the whole FlatJoyState2 with MD5 on every poll of every device.
    /// Not a correctness test: it prints ns per operation so a change can be compared
    /// against this baseline with the same method. Runtime: .NET 8 on the test host,
    /// which is not the Unity Mono or IL2CPP runtime; treat the ratio, not the absolute
    /// numbers, as the transferable result.
    /// </summary>
    public class PollChangeDetectionBenchmark
    {
        private readonly ITestOutputHelper _out;
        public PollChangeDetectionBenchmark(ITestOutputHelper output) { _out = output; }

        private static FlatJoyState2 SampleState(ushort x) => new FlatJoyState2 { lX = x, lY = 32768, buttonsA = 0x5 };

        // Candidate replacement for the MD5 hash: explicit field comparison, no allocation.
        private static bool FieldwiseEquals(in FlatJoyState2 a, in FlatJoyState2 b) =>
            a.buttonsA == b.buttonsA && a.buttonsB == b.buttonsB &&
            a.lX == b.lX && a.lY == b.lY && a.lZ == b.lZ && a.lU == b.lU && a.lV == b.lV &&
            a.lRx == b.lRx && a.lRy == b.lRy && a.lRz == b.lRz &&
            a.lVX == b.lVX && a.lVY == b.lVY && a.lVZ == b.lVZ && a.lVU == b.lVU && a.lVV == b.lVV &&
            a.lVRx == b.lVRx && a.lVRy == b.lVRy && a.lVRz == b.lVRz &&
            a.lAX == b.lAX && a.lAY == b.lAY && a.lAZ == b.lAZ && a.lAU == b.lAU && a.lAV == b.lAV &&
            a.lARx == b.lARx && a.lARy == b.lARy && a.lARz == b.lARz &&
            a.lFX == b.lFX && a.lFY == b.lFY && a.lFZ == b.lFZ && a.lFU == b.lFU && a.lFV == b.lFV &&
            a.lFRx == b.lFRx && a.lFRy == b.lFRy && a.lFRz == b.lFRz &&
            a.rgdwPOV == b.rgdwPOV;

        [Fact]
        [Trait("Category", "Benchmark")]
        public void Measure_md5_hash_versus_struct_equality()
        {
            const int iterations = 200_000;
            var a = SampleState(100);
            var b = SampleState(100);

            // Warm up both paths.
            for (int i = 0; i < 2_000; i++) { _ = a.GetHashCode(); _ = a.Equals(b); }

            var sw = Stopwatch.StartNew();
            int acc = 0;
            for (int i = 0; i < iterations; i++) acc ^= a.GetHashCode();
            sw.Stop();
            double md5Ns = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;

            sw.Restart();
            bool eq = true;
            for (int i = 0; i < iterations; i++) eq &= a.Equals(b);
            sw.Stop();
            double equalsNs = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;

            sw.Restart();
            bool feq = true;
            for (int i = 0; i < iterations; i++) feq &= FieldwiseEquals(a, b);
            sw.Stop();
            double fieldNs = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;

            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1_000; i++) acc ^= a.GetHashCode();
            long md5Bytes = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / 1_000;
            allocBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1_000; i++) feq &= FieldwiseEquals(a, b);
            long fieldBytes = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / 1_000;

            _out.WriteLine($"MD5 GetHashCode: {md5Ns:F0} ns/op, {md5Bytes} B/op allocated");
            _out.WriteLine($"ValueType.Equals (reflection): {equalsNs:F0} ns/op");
            _out.WriteLine($"Field-wise equality: {fieldNs:F1} ns/op, {fieldBytes} B/op allocated");
            _out.WriteLine($"ratio md5/fieldwise: {md5Ns / Math.Max(fieldNs, 0.1):F0}x  (acc={acc}, eq={eq}, feq={feq})");
            Assert.True(eq && feq);
            Assert.False(FieldwiseEquals(a, SampleState(101)));
        }
    }
}
