using System;
using System.Runtime.InteropServices;
using DirectInputManager;
using Xunit;

namespace DirectInputManager.Tests
{
    /// <summary>
    /// The managed structs must match the native layouts declared in
    /// src~/DirectInputForceFeedback~/DirectInputForceFeedback/DirectInputForceFeedback.h
    /// and dinput.h (x64). Sizes and offsets below are the native values.
    /// </summary>
    public class AbiLayoutTests
    {
        [Fact]
        public void DeviceInfo_matches_native_size_and_offsets()
        {
            // DWORD(4) + pad(4) + 4 pointers(32) + bool(1) + pad(7) = 48
            Assert.Equal(48, Marshal.SizeOf<DeviceInfo>());
            Assert.Equal(0, (int)Marshal.OffsetOf<DeviceInfo>(nameof(DeviceInfo.deviceType)));
            Assert.Equal(8, (int)Marshal.OffsetOf<DeviceInfo>(nameof(DeviceInfo.guidInstance)));
            Assert.Equal(40, (int)Marshal.OffsetOf<DeviceInfo>(nameof(DeviceInfo.FFBCapable)));
        }

        [Fact]
        public void DeviceInfo_FFBCapable_reads_only_the_native_one_byte_bool()
        {
            // The native field is a C++ bool (1 byte). The three bytes after it are
            // padding whose content is not defined. A false flag must stay false
            // whatever the padding holds.
            int size = Marshal.SizeOf<DeviceInfo>();
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                for (int i = 0; i < size; i++) Marshal.WriteByte(p, i, 0);
                for (int off = 8; off <= 32; off += 8) Marshal.WriteIntPtr(p, off, IntPtr.Zero);
                Marshal.WriteByte(p, 40, 0);     // bool = false
                Marshal.WriteByte(p, 41, 0x7F);  // padding garbage
                Marshal.WriteByte(p, 42, 0x7F);
                Marshal.WriteByte(p, 43, 0x7F);
                var info = Marshal.PtrToStructure<DeviceInfo>(p);
                Assert.False(info.FFBCapable);

                Marshal.WriteByte(p, 40, 1);     // bool = true
                info = Marshal.PtrToStructure<DeviceInfo>(p);
                Assert.True(info.FFBCapable);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        [Fact]
        public void FlatJoyState2_matches_native_layout()
        {
            // 2 x uint64 (16) + 33 x uint16 (66) = 82, padded to 8 -> 88
            Assert.Equal(88, Marshal.SizeOf<FlatJoyState2>());
            Assert.Equal(16, (int)Marshal.OffsetOf<FlatJoyState2>(nameof(FlatJoyState2.lX)));
            Assert.Equal(80, (int)Marshal.OffsetOf<FlatJoyState2>(nameof(FlatJoyState2.rgdwPOV)));
        }

        [Fact]
        public void DIJOYSTATE2_matches_dinput_h()
        {
            // 8 LONG/DWORD (32) + 4 DWORD POV (16) + 128 BYTE + 24 LONG (96) = 272
            Assert.Equal(272, Marshal.SizeOf<DIJOYSTATE2>());
            Assert.Equal(48, (int)Marshal.OffsetOf<DIJOYSTATE2>(nameof(DIJOYSTATE2.rgbButtons)));
            Assert.Equal(176, (int)Marshal.OffsetOf<DIJOYSTATE2>(nameof(DIJOYSTATE2.lVX)));
        }

        [Fact]
        public void DIDEVCAPS_matches_dinput_h()
        {
            Assert.Equal(44, Marshal.SizeOf<DIDEVCAPS>()); // 11 x DWORD
        }
    }
}
