#include <iostream>
#include <fstream>
#include <string>
#include <vector>

#include <cstdint>
#include <cstring>

typedef uint8_t byte;
typedef uint16_t ushort;
typedef uint32_t uint;
typedef uint64_t ulong;

template<typename T>
inline T& as(void* ptr)
{
	return *reinterpret_cast<T*>(ptr);
}
template<typename T, typename U>
inline T& read(U*& ptr)
{
	T& t = *reinterpret_cast<T*>(ptr);
	ptr = (U*)((size_t)ptr + sizeof(T));
}

int main(int argc, const char** argv)
{
	if (argc < 2)
	{
		std::cout << "Please offer a file name!\n";
		return 0;
	}

	std::string filename(argv[1]);
	
	std::ifstream fin(filename, std::ios::binary | std::ios::ate);
	if (!fin.is_open())
	{
		std::cerr << "Could not open " << filename << std::endl;
		return 1;
	}
	size_t fsize = fin.tellg();
	fin.seekg(0);
	
	std::vector<char> data(fsize);
	fin.read(data.data(), fsize);

	char* in_pos = data.data();
	char* in_end = in_pos + fsize;

	std::ofstream fout("test.txt");

	while (in_pos < in_end)
	{
		auto length = as<ushort>(in_pos);
		auto opcode = as<ushort>(in_pos + 2);
		if (opcode == 0x10)
		{
			int id = as<int>(in_pos + 4);
			fout << id << ": " << in_pos + 8 << '\n';
		}
		in_pos += length;
	}


	return 0;
}