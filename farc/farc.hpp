#pragma once
#include <filesystem>
#include <deque>

#include "utils.hpp"

class farc
{
private:
#pragma pack(push, 1)
	struct Header
	{
		uint32_t magic;
		uint32_t file_count;
		uint32_t data_offs;
		uint32_t unknown; // usually 0

		void swapnd()
		{
			swap_endian(file_count);
			swap_endian(data_offs);
			swap_endian(unknown);
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
#pragma pack(pop)

public:

	static void extract(const fs::path& filename, const fs::path& outpath);
	static void pack(const fs::path& inpath, const fs::path& outfile);

private:
	typedef std::deque<std::pair<fs::path, std::string>> file_collection_t;
	static size_t collect_files(
		const fs::path& dir,
		const std::string& parent_path,
		file_collection_t& files,
		size_t& data_offs);
};