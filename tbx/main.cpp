#include <iostream>
#include <fstream>
#include <vector>
#include <filesystem>

#include <cstdint>
#include <cstring>

namespace fs = std::filesystem;

constexpr int TBX_MAGIC = '\0xbt';

#pragma pack(push, 1)
struct TbxHeader
{
	int magic;
	uint32_t data_offset; // always file_count * 8 + 16
	uint32_t file_count;
	uint32_t table_offset; // probably, always 0x10
};

struct TbxEntry
{
	uint32_t offset;
	uint32_t length;
};
#pragma pack(pop)

int main(int argc, const char** argv)
{
	if (argc < 2)
	{
		std::cout << "Usage: <me> <input> [output]\n";
		return 0;
	}
	std::string input, output;
	if (argc == 2)
	{
		input = argv[1];
		auto dotpos = input.find_last_of('.');
		if (dotpos == -1)
		{
			std::cerr << "Invalid tbx file name: " << input << std::endl;
			return 1;
		}
		output = input.substr(0, dotpos);
	}
	else if (argc == 3)
	{
		input = argv[1];
		output = argv[2];
	}
	else
	{
		std::cerr << "Usage: <me> <input> [output]\n";
		return 1;
	}

	std::ifstream fin(input, std::ios::binary);
	if (!fin.is_open())
	{
		std::cerr << "Could not open file " << input << std::endl;
		return 1;
	}
	try
	{
		fin.exceptions(std::ios::failbit | std::ios::badbit);
		TbxHeader hdr;
		fin.read((char*)&hdr, sizeof(TbxHeader));
		if (hdr.magic != TBX_MAGIC)
		{
			std::cerr << "Invalid file format!\n";
			return 1;
		}
		std::cout << "Extracting " << hdr.file_count << " files...\n";
		std::vector<TbxEntry> toc(hdr.file_count);
		fin.seekg(hdr.table_offset);
		fin.read((char*)toc.data(), hdr.file_count * sizeof(TbxEntry));
		
		std::vector<char> file;
		std::ofstream fout;
		fs::path outpath(output);
		fs::create_directories(outpath);
		fout.exceptions(std::ios::failbit | std::ios::badbit);
		for (int i = 0; i < hdr.file_count; i++)
		{
			fin.seekg(toc[i].offset);
			file.resize(toc[i].length);
			fin.read(file.data(), toc[i].length);
			fout.open(outpath / (std::to_string(i) + ".tbg"), std::ios::binary);
			fout.write(file.data(), toc[i].length);
			fout.close();
		}
	}
	catch (const std::ios::failure& fail)
	{
		std::cerr << "IO Error: " << fail.what();
		return 1;
	}
	catch (const std::exception& exc)
	{
		std::cerr << "Error: " << exc.what();
		return 1;
	}


	return 0;
}