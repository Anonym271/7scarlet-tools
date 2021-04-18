#include <cstdint>
#include <iostream>
#include <fstream>
#include <vector>
#include <filesystem>
#include <cstring>

#ifdef __GNUC__
uint32_t swap_endian_cpy(uint32_t x) { return __builtin_bswap32(x); }
uint64_t swap_endian_cpy(uint64_t x) { return __builtin_bswap64(x); }
void swap_endian(uint32_t& x) { x = __builtin_bswap32(x); }
void swap_endian(uint64_t& x) { x = __builtin_bswap64(x); }
#else
#include <intrin.h>
uint32_t swap_endian_cpy(uint32_t x) { return _byteswap_ulong(x); }
uint64_t swap_endian_cpy(uint64_t x) { return _byteswap_uint64(x); }
void swap_endian(uint32_t& x) { x = _byteswap_ulong(x); }
void swap_endian(uint64_t& x) { x = _byteswap_uint64(x); }
#endif
#include <cassert>

namespace fs = std::filesystem;

#pragma pack(push, 1)
struct Header
{
	uint32_t magic;
	uint32_t file_count;
	uint32_t data_offs;
	uint32_t unkn2;

	void swapnd()
	{
		swap_endian(file_count);
		swap_endian(data_offs);
		swap_endian(unkn2);
	}
};

struct FTEntry
{
	uint32_t name_offs;
	uint32_t id;
	uint64_t offset;
	uint64_t length;
	uint64_t orig_length;

	void swapnd()
	{
		swap_endian(name_offs);
		swap_endian(id);
		swap_endian(offset);
		swap_endian(length);
		swap_endian(orig_length);
	}
};

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

template<typename T> 
T read(std::istream& f)
{
	T t;
	f.read((char*)&t, sizeof(T));
	return t;
}
template<typename T>
void read(std::ifstream& f, T& t)
{
	f.read((char*)&t, sizeof(T));
}

template<typename T>
std::vector<T> readn(std::istream& f, size_t count)
{
	std::vector<T> v(count);
	f.read((char*)v.data(), sizeof(T) * count);
	return v;
}
template<typename T>
void readn(std::istream& f, std::vector<T>& v)
{
	f.read((char*)v.data(), sizeof(T) * v.size());
}

void unlzss(std::vector<uint8_t>& data)
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
		size_t in_offs = in_pos - in_start;

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

void usage_and_exit(int code = 0)
{
	std::cout <<
		"Usage: <me> <input-file> [output-dir]\n";
	exit(code);
}

int main(int argc, const char** argv)
{
	if (argc < 2 || argc > 4)
		usage_and_exit();
	fs::path filename = argv[1];
	fs::path outpath;
	if (argc == 2)
	{
		outpath = filename;
		outpath.replace_extension();
	}
	else outpath = argv[2];
	
	std::ifstream fin(filename, std::ios::binary);
	if (!fin.is_open())
	{
		std::cerr << "Could not open file " << filename << std::endl;
		return 1;
	}

	auto hdr = read<Header>(fin);
	hdr.swapnd();
	auto toc = readn<FTEntry>(fin, hdr.file_count);
	for (auto& e : toc)
		e.swapnd();

	uint64_t names_start = fin.tellg();
	uint64_t names_end = hdr.data_offs;
	uint64_t names_len = names_end - names_start;

	auto name_table = readn<char>(fin, names_len);
	std::string fname;
	fs::path fpath;
	std::ofstream fout;
	fout.exceptions(std::ios::failbit | std::ios::badbit);

	for (auto& entry : toc)
	{
		fname = name_table.data() + (entry.name_offs - names_start);
		fpath = outpath / fname;
		std::cout << fname << '\n';

		try
		{
			fs::create_directories(fpath.parent_path());
			fout.open(fpath);

			fin.seekg(entry.offset);
			auto data = readn<uint8_t>(fin, entry.length);

			unlzss(data);

			fout.write((char*)data.data(), data.size());
			fout.close();
		}
		catch (const std::ios::failure& fail)
		{
			std::cerr << "IO Error: " << fail.what() << "\nCould not extract file " << fname << ".\n";
		}
		catch (const std::exception& exc)
		{
			std::cerr << "Error: " << exc.what() << "\nCould not extract file " << fname << ".\n";
		}
	}
	

	return 0;
}