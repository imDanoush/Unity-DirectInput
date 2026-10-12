#pragma once
// Pure helpers used by DirectInputForceFeedback.cpp. No Windows headers, so they compile and run
// on any platform: see tests/helpers_test.cpp.
#include <cstddef>
#include <cstdint>
#include <cstring>

namespace diffb {

// Accepts "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" (36 chars) or the same wrapped in braces (38 chars).
// Only the separator positions are checked; CLSIDFromString validates the hex digits.
inline bool IsValidGuidString(const char* guid) {
	if (!guid) return false;
	size_t len = std::strlen(guid);
	if (len == 38) {
		if (guid[0] != '{' || guid[37] != '}') return false;
		guid += 1;
	}
	else if (len != 36) {
		return false;
	}
	return guid[8] == '-' && guid[13] == '-' && guid[18] == '-' && guid[23] == '-';
}

// DirectInput reports absolute axes as 0..65535 unless DIPROP_RANGE is changed (this DLL never changes it).
inline uint16_t ClampToUInt16(int32_t value) {
	return value < 0 ? 0 : value > 65535 ? 65535 : static_cast<uint16_t>(value);
}

// POV hat value (hundredths of a degree clockwise from north; low word 0xFFFF = centred) to the 4-bit
// layout the Input System device expects: up = 1, down = 2, left = 4, right = 8. An exact diagonal such
// as 4500 (north-east) sets both neighbouring bits.
inline uint16_t PovToDpadBits(uint32_t pov) {
	if ((pov & 0xFFFFu) == 0xFFFFu) return 0;
	uint16_t bits = 0;
	if (pov >= 31500 || pov <= 4500) bits |= 1;
	if (pov >= 4500 && pov <= 13500) bits |= 8;
	if (pov >= 13500 && pov <= 22500) bits |= 2;
	if (pov >= 22500 && pov <= 31500) bits |= 4;
	return bits;
}

// Default DIEFF_CARTESIAN direction for an effect spanning `count` actuators: a unit vector along the
// first actuator, so the whole magnitude reaches it and the sign of the magnitude selects the side.
// Microsoft's single-axis sample and SDL pass 0 here; Wine ignores the value for one axis, but no Microsoft text
// says Windows does. A non-zero
// first element is kept because commit 8a4e3d7 reports a wheel that misread 0 (unverified), and it is
// equally valid. The previous "10000 on every element" was a 45 degree diagonal on two actuators.
template <typename T>
inline void FillDefaultDirection(T* out, int count, T nominalMax) {
	for (int i = 0; i < count; i++) {
		out[i] = (i == 0) ? nominalMax : static_cast<T>(0);
	}
}

} // namespace diffb
