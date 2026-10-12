using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DirectInputManager;
using Xunit;

namespace DirectInputManager.Tests
{
    /// <summary>Pure managed logic that must hold without a device or the native DLL.</summary>
    public class ManagerLogicTests
    {
        [Fact]
        public void FlatJoyState2_equality_is_field_exact()
        {
            var a = new FlatJoyState2 { lX = 1, lFRz = 65535, buttonsB = 1UL << 63, rgdwPOV = 0x8001 };
            var b = a;
            Assert.True(a.Equals(b));
            Assert.True(a.Equals((object)b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            b.lFRz = 65534;             // a field the cheap hash does not cover
            Assert.False(a.Equals(b));  // equality must still see it

            b = a; b.buttonsB = 0;
            Assert.False(a.Equals(b));
            Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Attach_rejects_unknown_guid_without_touching_native()
        {
            // No device was enumerated in this process, so the guid cannot be known.
            bool attached = DIManager.Attach("{00000000-0000-0000-0000-000000000000}");
            Assert.False(attached);
            Assert.Equal(unchecked((int)0x80070490), DIManager.LastHResult);
            Assert.Contains("EnumerateDevices", DIManager.LastError);
        }

        [Fact]
        public void UpdateCustomForceNative_rejects_empty_sample_array()
        {
            Assert.False(DIManager.UpdateCustomForceNative("{00000000-0000-0000-0000-000000000000}", Array.Empty<int>(), 0));
            Assert.Equal(unchecked((int)0x80070057), DIManager.LastHResult);
        }

        [Fact]
        public void Describe_names_directinput_codes_and_falls_back_for_others()
        {
            Assert.Contains("DIERR_NOTEXCLUSIVEACQUIRED", WinErrors.Describe(unchecked((int)0x80040205)));
            Assert.Contains("DIERR_INPUTLOST", WinErrors.Describe(unchecked((int)0x8007001E)));
            Assert.StartsWith("0x80040205", WinErrors.Describe(unchecked((int)0x80040205)));
        }

        [Fact]
        public void Check_reports_each_state_change_once_and_keeps_LastError()
        {
            var reports = new List<(DirectInputNotificationSeverity severity, string title)>();
            var previous = DIManager.Reporter;
            DIManager.Reporter = (severity, title, details) => reports.Add((severity, title));
            try
            {
                DIManager.ResetReports();
                const string guid = "{11111111-0000-0000-0000-000000000000}";
                int inputLost = unchecked((int)0x8007001E);

                Assert.False(DIManager.Check("GetDeviceState", inputLost, guid));
                Assert.False(DIManager.Check("GetDeviceState", inputLost, guid)); // per-frame repeat: silent
                Assert.Single(reports);
                Assert.Equal(DirectInputNotificationSeverity.Warning, reports[0].severity); // a lost device is a state
                string lostError = DIManager.LastError;
                Assert.Contains("DIERR_INPUTLOST", lostError);

                Assert.True(DIManager.Check("GetDeviceState", 0, guid)); // recovery, reported once
                Assert.True(DIManager.Check("GetDeviceState", 0, guid));
                Assert.Equal(2, reports.Count);
                Assert.Equal(DirectInputNotificationSeverity.Info, reports[1].severity);
                Assert.Equal(lostError, DIManager.LastError); // a success does not erase the last failure's text
                Assert.Equal(0, DIManager.LastHResult);

                Assert.True(DIManager.Check("UpdateConstantForce", 3, guid, FFBEffects.ConstantForce)); // DI_DOWNLOADSKIPPED
                Assert.True(DIManager.Check("UpdateConstantForce", 3, guid, FFBEffects.ConstantForce));
                Assert.True(DIManager.Check("UpdateConstantForce", 0, guid, FFBEffects.ConstantForce)); // flapping: no "works again"
                Assert.True(DIManager.Check("UpdateConstantForce", 3, guid, FFBEffects.ConstantForce)); // and no second warning
                Assert.Equal(3, reports.Count);
                Assert.Equal(DirectInputNotificationSeverity.Warning, reports[2].severity);

                Assert.False(DIManager.Check("CreateFFBEffect", unchecked((int)0x80004001), guid, FFBEffects.Sine));
                Assert.False(DIManager.Check("CreateFFBEffect", unchecked((int)0x80040201), guid, FFBEffects.Sine)); // new code: new report
                Assert.False(DIManager.Check("CreateFFBEffect", unchecked((int)0x80004001), guid, FFBEffects.Square)); // other effect: own state
                Assert.Equal(6, reports.Count);
                Assert.All(reports.GetRange(3, 3), r => Assert.Equal(DirectInputNotificationSeverity.Error, r.severity));

                Assert.False(DIManager.Check("GetFFBEffectStatus", unchecked((int)0x80004004), guid, FFBEffects.Sine, log: false));
                Assert.Equal(6, reports.Count); // status queries record without reporting
            }
            finally
            {
                DIManager.Reporter = previous;
                DIManager.ResetReports();
            }
        }

        [Fact]
        public void Initialize_without_the_dll_sets_the_cause_and_reports_it_once()
        {
            var reports = new List<string>();
            var previous = DIManager.Reporter;
            DIManager.Reporter = (severity, title, details) => reports.Add(title);
            try
            {
                DIManager.ResetReports();
                Assert.False(DIManager.Initialize()); // no DirectInputForceFeedback.dll next to the test assembly
                Assert.False(DIManager.Initialize());
                Assert.NotEqual(0, DIManager.LastHResult);
                Assert.Contains("DirectInputForceFeedback.dll", DIManager.LastError, StringComparison.OrdinalIgnoreCase);
                Assert.Single(reports);
            }
            finally
            {
                DIManager.Reporter = previous;
                DIManager.ResetReports();
            }
        }

        [Fact]
        public async Task Debouncer_runs_once_after_quiet_period_without_a_synchronization_context()
        {
            // Native device-change callbacks may arrive on a thread with no SynchronizationContext.
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var debouncer = new Debouncer(50);
                int runs = 0;
                var done = new TaskCompletionSource<bool>();
                for (int i = 0; i < 5; i++) debouncer.Debounce(() => { Interlocked.Increment(ref runs); done.TrySetResult(true); });
                await Task.WhenAny(done.Task, Task.Delay(2000));
                await Task.Delay(100); // give any stray continuation a chance to run
                Assert.Equal(1, runs);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }
    }
}
