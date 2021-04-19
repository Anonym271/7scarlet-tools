#define STB_IMAGE_IMPLEMENTATION
#define STB_IMAGE_WRITE_IMPLEMENTATION

#include <iostream>
#include <fstream>
#include <vector>

#include <cstdint>
#include <cstring>

#include "stb_image.h"
#include "stb_image_write.h"

#pragma pack(push, 1)
struct TbgHeader
{
    int magic;
    uint32_t data_offset;
    uint32_t data_length;
    uint32_t width;
    uint32_t height;
    uint32_t flags;
    uint32_t image_count; // probably
    uint32_t unkn1, unkn2; // 0
    float resx, resy; // probably
    // many 0s following, don't seem to have any use
};
#pragma pack(pop)

void tbg_to_png(const std::string& name)
{
        std::ifstream fin(name, std::ios::binary | std::ios::ate);
        if (!fin.is_open())
        {
            std::cerr << "Could not open file!\n";
            return;
        }

        std::vector<char> data(fin.tellg());
        fin.seekg(0);
        fin.read(data.data(), data.size());
        fin.close();

        TbgHeader& hdr = *(TbgHeader*)data.data();
        int channels = hdr.flags >> 24;
        if (channels == 0x98)
            channels = 3;
        else if (channels == 0x0C)
            channels = 4;
        else
        {
            std::cerr << "Unknown channel flag! "<< std::hex << hdr.flags <<"\n";
            return;
        }

        for (char* pos = data.data() + hdr.data_offset, *end = data.data() + data.size();
            pos < end; pos += channels)
        {
            std::swap(pos[0], pos[2]);   
        }

        std::string outname = name.substr(0, name.find_last_of('.')) + ".png";
        stbi_write_png(outname.c_str(), hdr.width, hdr.height, channels, data.data() + hdr.data_offset, hdr.width * channels);
}

void png_to_tbg(const std::string& filename)
{
    int w, h, c;
    uint8_t* data = stbi_load(filename.c_str(), &w, &h, &c, 0);
    if (!data)
    {
        std::cerr << "Could not load " << filename << " as image!";
        return;
    }

    std::vector<char> hdr_data(828, 0);
    TbgHeader& hdr = *(TbgHeader*)hdr_data.data();
    hdr.magic = '\0gbt';
    hdr.data_offset = 828; // it's always this big, don't ask me
    hdr.data_length = w * h * c;
    hdr.width = w;
    hdr.height = h;
    if (c == 3)
        hdr.flags = 0x98;
    else if (c == 4)
        hdr.flags = 0x0C;
    else 
    {
        std::cerr << "Unsupported channel count: " << c << "\n";
        return;
    }
    hdr.flags <<= 24;
    hdr.flags |= 0x1000;
    hdr.image_count = 1;
    hdr.resx = 1.f;
    hdr.resy = 1.f;

    for (uint8_t* pos = data + hdr.data_offset, *end = data + w * h * c;
        pos < end; pos += c)
    {
        std::swap(pos[0], pos[2]);   
    }

    std::string name = filename.substr(0, filename.find_last_of('.')) + ".tbg";
    std::ofstream fout(name, std::ios::binary);
    fout.write(hdr_data.data(), hdr_data.size());
    fout.write((char*)data, w * h * c);
}

int main(int argc, const char** argv)
{
    if (argc < 2)
    {
        std::cout << "Usage: <me> <input files...> (can batch convert many files at once)\n";
        return 0;
    }

    for (int i = 1; i < argc; i++)
    {
        std::string name(argv[i]);
        std::cout << name << "\n";
        auto dotpos = name.find_last_of('.');
        if (dotpos == -1)
        {
            std::cerr << "Unknown file type.\n";
            continue;
        }
        std::string ext = name.substr(dotpos);
        if (ext == ".png")
            png_to_tbg(name);
        else if (ext == ".tbg")
            tbg_to_png(argv[i]);
        else
            std::cerr << "Unknown file type.\n";
    }

    return 0;
}