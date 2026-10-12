// Unit tests for the platform-independent helpers of the native DLL.
// Build and run on any OS (no Windows SDK needed):
//   clang++ -std=c++17 -Wall -Wextra -I../DirectInputForceFeedback helpers_test.cpp -o helpers_test && ./helpers_test
// Exit code 0 means every check passed.
#include "DirectInputHelpers.h"
#include <cstdio>

static int failures = 0;
#define CHECK(cond) do { if (!(cond)) { std::printf("FAIL %s:%d %s\n", __FILE__, __LINE__, #cond); ++failures; } } while (0)

int main() {
	using namespace diffb;

	// GUID strings as produced by StringFromGUID2 (braces) and as users may type them (no braces).
	CHECK(IsValidGuidString("{6F1D2B60-D5A0-11CF-BFC7-444553540000}"));
	CHECK(IsValidGuidString("6F1D2B60-D5A0-11CF-BFC7-444553540000"));
	CHECK(!IsValidGuidString(nullptr));
	CHECK(!IsValidGuidString(""));
	CHECK(!IsValidGuidString("{6F1D2B60-D5A0-11CF-BFC7-444553540000"));   // 37 chars
	CHECK(!IsValidGuidString("6F1D2B60_D5A0-11CF-BFC7-444553540000"));    // wrong separator
	CHECK(!IsValidGuidString("{6F1D2B60-D5A0-11CF-BFC7-444553540000}x")); // 39 chars
	CHECK(!IsValidGuidString("{6F1D2B60-D5A0-11CF-BFC7444553540000-}"));  // 38 chars, separator shifted
	CHECK(!IsValidGuidString("[6F1D2B60-D5A0-11CF-BFC7-444553540000]"));  // 38 chars, wrong brackets

	// Axis flattening keeps the DirectInput default range and never wraps negative values.
	CHECK(ClampToUInt16(-5) == 0);
	CHECK(ClampToUInt16(0) == 0);
	CHECK(ClampToUInt16(32767) == 32767);
	CHECK(ClampToUInt16(65535) == 65535);
	CHECK(ClampToUInt16(70000) == 65535);

	// POV: centred, cardinal and diagonal directions.
	CHECK(PovToDpadBits(0xFFFFFFFFu) == 0);
	CHECK(PovToDpadBits(0xFFFFu) == 0);
	CHECK(PovToDpadBits(0) == 1);
	CHECK(PovToDpadBits(9000) == 8);
	CHECK(PovToDpadBits(18000) == 2);
	CHECK(PovToDpadBits(27000) == 4);
	CHECK(PovToDpadBits(4500) == (1 | 8));
	CHECK(PovToDpadBits(13500) == (8 | 2));
	CHECK(PovToDpadBits(22500) == (2 | 4));
	CHECK(PovToDpadBits(31500) == (4 | 1));
	CHECK(PovToDpadBits(35900) == 1);
	CHECK(PovToDpadBits(100) == 1);
	CHECK(PovToDpadBits(4499) == 1);
	CHECK(PovToDpadBits(4501) == 8);

	// Default effect direction per actuator count.
	long one[1] = { 123 };
	FillDefaultDirection(one, 1, 10000L);
	CHECK(one[0] == 10000);
	long two[2] = { 1, 1 };
	FillDefaultDirection(two, 2, 10000L);
	CHECK(two[0] == 10000 && two[1] == 0);
	long three[3] = { 1, 1, 1 };
	FillDefaultDirection(three, 3, 10000L);
	CHECK(three[0] == 10000 && three[1] == 0 && three[2] == 0);

	if (failures) {
		std::printf("%d check(s) failed\n", failures);
		return 1;
	}
	std::printf("all helper tests passed\n");
	return 0;
}
