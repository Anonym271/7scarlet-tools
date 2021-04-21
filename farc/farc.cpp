#include "farc.hpp"

#include <iostream>
#include <vector>

#include <cstdint>
#include <cstring>
#include <cassert>

#include "lzss.hpp"

void farc::extract(const fs::path& filename, const fs::path& outpath)
{
	std::ifstream fin(filename, std::ios::binary);
	if (!fin.is_open())
		throw std::ios::failure("Could not open input file");
	fin.exceptions(std::ios::failbit | std::ios::badbit);
	auto hdr = read<Header>(fin);
	hdr.swapnd();
	if (hdr.magic != 'CRAF')
		throw std::runtime_error("Inavlid file format: invalid magic number!");
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

			fin.seekg(entry.offset);
			auto data = readn<uint8_t>(fin, entry.length);

			lzss::decompress(data);

			fout.open(fpath, std::ios::binary);
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
}

void farc::pack(const fs::path& inpath, const fs::path& outfile)
{
	file_collection_t files;
	size_t data_offs = sizeof(Header);
	size_t file_count = collect_files(inpath, "", files, data_offs);

	std::vector<FTEntry> toc(file_count);
	size_t names_offs = sizeof(Header) + file_count * sizeof(FTEntry);
	std::vector<char> names(data_offs - names_offs);

	std::ofstream fout(outfile, std::ios::binary);
	if (!fout.is_open())
		throw std::ios::failure("Could not open output file!");
	fout.exceptions(std::ios::failbit | std::ios::badbit);
	std::ifstream fin;
	fin.exceptions(std::ios::failbit | std::ios::badbit);

	Header hdr;
	hdr.magic = 'CRAF';
	hdr.file_count = file_count;
	hdr.data_offs = data_offs;
	hdr.unknown = 0;
	hdr.swapnd();
	fout.write((char*)&hdr, sizeof(Header));
	fout.seekp(data_offs);

	uint32_t id = 0;
	uint32_t name_pos = 0;
	size_t name_length;
	uint64_t fsize;
	for (auto& [path, name] : files)
	{
		std::cout << name << '\n';
		fin.open(path, std::ios::binary | std::ios::ate);
		fsize = fin.tellg();
		fin.seekg(0);

		FTEntry& entry = toc[id];
		entry.id = id;
		entry.length = fsize;
		entry.orig_length = fsize;
		entry.offset = fout.tellp();
		entry.name_offs = name_pos + names_offs;
		entry.swapnd();

		name_length = name.size() + 1;
		std::memcpy(&names[name_pos], name.c_str(), name_length);
		name_pos += name_length;

		fout << fin.rdbuf();
		fin.close();

		id++;
	}

	fout.seekp(sizeof(Header));
	fout.write((char*)toc.data(), toc.size() * sizeof(FTEntry));
	fout.write(names.data(), names.size());
}

size_t farc::collect_files(
	const fs::path& dir,
	const std::string& parent_path,
	file_collection_t& files,
	size_t& data_offs)
{
	size_t count = 0;
	fs::path p;
	std::string fn;
	for (auto& entry : fs::directory_iterator(dir))
	{
		p = entry.path();
		if (entry.is_directory())
			count += collect_files(p, parent_path + p.filename().string() + '/', files, data_offs);
		else if (entry.is_regular_file())
		{
			fn = parent_path + p.filename().string();
			data_offs += fn.size() + 1 + sizeof(FTEntry);
			files.emplace_back(std::make_pair(std::move(p), std::move(fn)));
			count++;
		}
		else std::cout << "Warning: '" << p << " does not seem to be a regular file and is omitted.\n";
	}
	return count;
}