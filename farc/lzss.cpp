#include "lzss.hpp"

#include <string>
#include <cstring>
#include <cassert>


void lzss::compress(std::vector<uint8_t>& data) 
{
	throw std::runtime_error("LZSS compression is not yet implemented!");
}


void lzss::decompress(std::vector<uint8_t>& data)
{
	size_t flength = data.size();
	if (flength < sizeof(LZSSHeader))
		return;
	uint8_t* in_start = data.data();
	LZSSHeader& hdr = *reinterpret_cast<LZSSHeader*>(in_start);
	if (hdr.magic != 'SSZL')
		return;
	hdr.swapnd();
	if (hdr.type != 3)
		throw std::runtime_error("Unsupported LZSS type (" + std::to_string(hdr.type) + ")");

	uint8_t* in_pos = in_start + sizeof(LZSSHeader);
	uint8_t* in_end = in_start + flength;

	std::vector<uint8_t> output(hdr.original_length);
	uint8_t* out_pos = output.data();
	uint8_t* out_end = out_pos + output.size();

	uint8_t flags;
	uint8_t flag_pos = 0;
	int flag;
	uint32_t control;
	uint32_t length;
	uint32_t offset;
	while (in_pos < in_end)
	{
		assert(out_pos < out_end);
		if (flag_pos == 0)
		{
			flag_pos = 4;
			flags = *in_pos++;
		}
		flag = flags & 0b11;
		//size_t in_offs = in_pos - in_start;

		switch (flag)
		{
		case 0: // uncompressed
			*out_pos++ = *in_pos++;
			break;
		case 1: // 1 control byte
			control = *in_pos++;
			length = (control & 3) + 2;
			offset = (control >> 2) & 0x3F;
			std::memcpy(out_pos, out_pos - offset - 1, length);
			out_pos += length;
			break;
		case 2: // 2 control bytes
			control = *in_pos++;
			control <<= 8;
			control |= *in_pos++;

			length = (control & 0x3F) + 3;
			offset = (control >> 6) & 0x3FF;

			std::memcpy(out_pos, out_pos - offset - 1, length);
			out_pos += length;
			break;
		case 3: // 3 control bytes
			control = *in_pos++;
			control <<= 8;
			control |= *in_pos++;
			control <<= 8;
			control |= *in_pos++;

			length = (control & 0x1FF) + 4;
			offset = (control >> 9) & 0x7FFF;

			std::memcpy(out_pos, out_pos - offset - 1, length);
			out_pos += length;
			break;
		}

		flags >>= 2;
		flag_pos--;
		assert(out_pos <= out_end);
	}
	assert(out_pos == out_end && in_pos == in_end);
	data = std::move(output);
}