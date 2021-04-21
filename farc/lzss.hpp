#pragma once
#include <vector>
#include <cstdint>

#include "utils.hpp"

class lzss
{
private:
#pragma pack(push, 1)
	struct LZSSHeader
	{
		uint32_t magic;
		uint32_t type; // < 4
		uint64_t original_length;

		void swapnd()
		{
			swap_endian(type);
			swap_endian(original_length);
		}
	};
#pragma pack(pop)

public:
	static void compress(std::vector<uint8_t>& data);
	static void decompress(std::vector<uint8_t>& data);
};