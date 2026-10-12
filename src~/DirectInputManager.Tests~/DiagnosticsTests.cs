using DirectInputManager;
using Xunit;
using static DirectInputManager.DIDiagnostics;

namespace DirectInputManager.Tests
{
    /// <summary>Pure mappings behind the notifications: HRESULT names, load-failure diagnosis, wheel hints.</summary>
    public class DiagnosticsTests
    {
        [Theory]
        [InlineData(0x8007047Eu, "DIERR_OLDDIRECTINPUTVERSION")]
        [InlineData(0x80070481u, "DIERR_BETADIRECTINPUTVERSION")]
        [InlineData(0x80070077u, "DIERR_BADDRIVERVER")]
        [InlineData(0x80040154u, "DIERR_DEVICENOTREG")]
        [InlineData(0x8007000Eu, "E_OUTOFMEMORY")]
        [InlineData(0x8000000Au, "E_PENDING")]
        [InlineData(0x2u, "DI_POLLEDDEVICE")]
        [InlineData(0x3u, "DI_DOWNLOADSKIPPED")]
        [InlineData(0x8u, "DI_TRUNCATED")]
        [InlineData(0xCu, "DI_TRUNCATEDANDRESTARTED")]
        [InlineData(0x80040301u, "DIERR_DRIVERFIRST+1 (defined by the device's driver)")]
        [InlineData(0xC0110004u, "HID error 0x0004")]
        public void Describe_names_added_codes_and_ranges(uint hr, string expected)
        {
            Assert.Contains(expected, WinErrors.Describe(unchecked((int)hr)));
        }

        [Fact]
        public void Access_denied_is_named_by_operation()
        {
            int accessDenied = unchecked((int)0x80070005);
            Assert.Contains("DIERR_OTHERAPPHASPRIO (E_ACCESSDENIED; another application has priority)", WinErrors.Describe(accessDenied, "CreateDevice"));
            Assert.Contains("DIERR_READONLY", WinErrors.Describe(accessDenied, "SetAutocenter"));
            Assert.Contains("Another program is using this device exclusively", WinErrors.Hint(accessDenied, "CreateDevice"));
            Assert.Contains("unplugged since it was listed", WinErrors.Hint(unchecked((int)0x80004005), "CreateDevice"));
            Assert.Contains("defined by the device's driver", WinErrors.Hint(unchecked((int)0x800403FF), "UpdateConstantForce"));
            Assert.Null(WinErrors.Hint(0, "Poll"));
        }

        [Fact]
        public void Only_not_fully_applied_success_codes_are_partial()
        {
            Assert.True(DIManager.IsPartialSuccess(3));
            Assert.True(DIManager.IsPartialSuccess(8));
            Assert.True(DIManager.IsPartialSuccess(0xC));
            Assert.False(DIManager.IsPartialSuccess(0));
            Assert.False(DIManager.IsPartialSuccess(1));
            Assert.False(DIManager.IsPartialSuccess(4)); // DI_EFFECTRESTARTED: applied
        }

        [Fact]
        public void Load_failure_classification_follows_the_probe()
        {
            Assert.Equal(LoadFailure.NotWindows, ClassifyLoadFailure(false, true, false, false, false, 0));
            Assert.Equal(LoadFailure.Not64Bit, ClassifyLoadFailure(true, false, false, false, true, 0));
            Assert.Equal(LoadFailure.Arm64, ClassifyLoadFailure(true, true, true, false, true, 0));
            Assert.Equal(LoadFailure.OldDll, ClassifyLoadFailure(true, true, false, true, true, 0));
            Assert.Equal(LoadFailure.FileMissing, ClassifyLoadFailure(true, true, false, false, false, 0));
            Assert.Equal(LoadFailure.NotLoadedByEngine, ClassifyLoadFailure(true, true, false, false, true, 0));
            Assert.Equal(LoadFailure.DependencyMissing, ClassifyLoadFailure(true, true, false, false, true, 126));
            Assert.Equal(LoadFailure.WrongFormat, ClassifyLoadFailure(true, true, false, false, true, 193));
            Assert.Equal(LoadFailure.Other, ClassifyLoadFailure(true, true, false, false, true, 5));
        }

        [Fact]
        public void Load_failure_text_names_the_cause_and_what_to_do()
        {
            var vc = ExplainLoadFailure(LoadFailure.DependencyMissing, new[] { "p" }, "p", 126, "VCRUNTIME140_1.dll", "DllNotFoundException");
            Assert.Equal(DirectInputNotificationSeverity.Error, vc.severity);
            Assert.Contains("VCRUNTIME140_1.dll", vc.title);
            Assert.Contains("Visual C++ 2015-2022 Redistributable (x64)", vc.details);

            Assert.Contains("repair the Windows installation", ExplainLoadFailure(LoadFailure.DependencyMissing, new[] { "p" }, "p", 126, "dinput8.dll", "").details);
            Assert.Contains("C:/game/x.dll", ExplainLoadFailure(LoadFailure.FileMissing, new[] { "C:/game/x.dll" }, null, 0, null, "").details);
            Assert.Equal(DirectInputNotificationSeverity.Warning, ExplainLoadFailure(LoadFailure.NotWindows, new string[0], null, 0, null, "").severity);
            Assert.Contains("x86_64", ExplainLoadFailure(LoadFailure.Not64Bit, new string[0], null, 0, null, "").details);

            // An exception thrown inside a loaded DLL's call is not diagnosed as a load failure.
            var inside = ExplainLoadFailure(new System.Runtime.InteropServices.SEHException("boom"), new[] { "p" });
            Assert.Equal("DirectInput could not be started", inside.title);
            Assert.Contains("SEHException: boom", inside.details);
        }

        [Fact]
        public void Vid_and_pid_come_from_a_PIDVID_product_guid_only()
        {
            Assert.True(TryGetVidPid("{C24F046D-0000-0000-0000-504944564944}", out ushort vid, out ushort pid));
            Assert.Equal(0x046D, vid);
            Assert.Equal(0xC24F, pid);
            Assert.False(TryGetVidPid("{6F1D2B60-D5A0-11CF-BFC7-444553540000}", out _, out _));
            Assert.False(TryGetVidPid(null, out _, out _));
        }

        static DeviceInfo Device(uint type, string guidProduct, bool ffb) =>
            new DeviceInfo { deviceType = type, guidProduct = guidProduct, guidInstance = "{G}", productName = "Wheel", FFBCapable = ffb };

        [Fact]
        public void No_force_feedback_hint_is_heuristic_and_vendor_specific()
        {
            const string logitech = "{C24F046D-0000-0000-0000-504944564944}";
            const string unknown = "{12341234-0000-0000-0000-504944564944}";
            string note = NoForceFeedbackNote(Device(0x0116, logitech, false)); // DI8DEVTYPE_DRIVING
            Assert.Contains("G HUB or Logitech Gaming Software", note);
            Assert.Contains("VID 046D PID C24F", note);
            Assert.Contains("likely causes, not a confirmed diagnosis", note);

            Assert.Null(NoForceFeedbackNote(Device(0x0116, logitech, true)));   // has FFB
            Assert.Null(NoForceFeedbackNote(Device(0x0115, logitech, false)));   // gamepad
            Assert.Null(NoForceFeedbackNote(Device(0x0114, unknown, false)));    // ordinary joystick
            Assert.NotNull(NoForceFeedbackNote(Device(0x0114, logitech, false))); // joystick type from a wheel vendor
            Assert.DoesNotContain("For ", NoForceFeedbackNote(Device(0x0116, unknown, false))); // generic wording only
            Assert.Contains("Moza", VendorHint(0x346E));
            Assert.Null(VendorHint(0x1234));
        }
    }
}
