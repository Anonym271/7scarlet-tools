#pragma once
#include <fstream>
#include <vector>

uint32_t swap_endian_cpy(uint32_t x);
uint64_t swap_endian_cpy(uint64_t x);
void swap_endian(uint32_t& x);
void swap_endian(uint64_t& x);

namespace fs = std::filesystem;

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