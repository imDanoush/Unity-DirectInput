using System;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace DirectInputManager.Editor.Tests
{
    /// <summary>
    /// EditMode tests (Window > General > Test Runner). They need no device and do not load the
    /// native DLL. The same assertions run on any OS in src~/DirectInputManager.Tests~ (xunit).
    /// </summary>
    public class AbiLayoutTests
    {
        [Test]
        public void DeviceInfo_matches_native_size_and_offsets()
        {
            Assert.AreEqual(48, Marshal.SizeOf<DeviceInfo>());
            Assert.AreEqual(40, (int)Marshal.OffsetOf<DeviceInfo>(nameof(DeviceInfo.FFBCapable)));
        }

        [Test]
        public void DeviceInfo_FFBCapable_is_a_one_byte_bool()
        {
            int size = Marshal.SizeOf<DeviceInfo>();
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                for (int i = 0; i < size; i++) Marshal.WriteByte(p, i, 0);
                Marshal.WriteByte(p, 41, 0x7F); // padding after the bool must be ignored
                Assert.IsFalse(Marshal.PtrToStructure<DeviceInfo>(p).FFBCapable);
                Marshal.WriteByte(p, 40, 1);
                Assert.IsTrue(Marshal.PtrToStructure<DeviceInfo>(p).FFBCapable);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        [Test]
        public void State_structs_match_dinput_h()
        {
            Assert.AreEqual(88, Marshal.SizeOf<FlatJoyState2>());
            Assert.AreEqual(272, Marshal.SizeOf<DIJOYSTATE2>());
            Assert.AreEqual(44, Marshal.SizeOf<DIDEVCAPS>());
        }

        [Test]
        public void FlatJoyState2State_matches_FlatJoyState2_for_the_pointer_reinterpret()
        {
            // DirectInputDevice.DeviceStateChanged reinterprets one as the other instead of marshalling.
            Assert.AreEqual(Marshal.SizeOf<FlatJoyState2>(), Marshal.SizeOf<FlatJoyState2State>());
            Assert.AreEqual(Marshal.OffsetOf<FlatJoyState2>(nameof(FlatJoyState2.lX)), Marshal.OffsetOf<FlatJoyState2State>(nameof(FlatJoyState2State.lX)));
            Assert.AreEqual(Marshal.OffsetOf<FlatJoyState2>(nameof(FlatJoyState2.lFRz)), Marshal.OffsetOf<FlatJoyState2State>(nameof(FlatJoyState2State.lFRz)));
            Assert.AreEqual(Marshal.OffsetOf<FlatJoyState2>(nameof(FlatJoyState2.rgdwPOV)), Marshal.OffsetOf<FlatJoyState2State>(nameof(FlatJoyState2State.rgdwPOV)));
        }

        [Test]
        public void FlatJoyState2_equality_is_field_exact()
        {
            var a = new FlatJoyState2 { lX = 1, lFRz = 65535 };
            var b = a;
            Assert.IsTrue(a.Equals(b));
            b.lFRz = 0;
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Attach_rejects_unknown_guid()
        {
            Assert.IsFalse(DIManager.Attach("{00000000-0000-0000-0000-000000000000}"));
            StringAssert.Contains("EnumerateDevices", DIManager.LastError);
        }
    }
}
