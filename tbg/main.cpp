#include <iostream>
#include <fstream>
#include <vector>
#include <deque>
#include <thread>
#include <mutex>

#include <lodepng.h>

namespace ch = std::chrono;

constexpr int TBG_HEADER_SIZE = 828;
constexpr unsigned int THREADS = 8;

std::deque<std::string> todo;
std::mutex mutex;

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

template<typename P>
void swap_colors(P* pixbuf, size_t length, int c)
{
    for (P *end = pixbuf + length; pixbuf < end; pixbuf += c)
        std::swap(pixbuf[0], pixbuf[2]);
}

int tbg_to_png(const std::string& name)
{
    std::ifstream fin(name, std::ios::binary | std::ios::ate);
    if (!fin.is_open())
    {
        std::cerr << "Could not open file!\n";
        return 1;
    }

    TbgHeader hdr;
    fin.seekg(0);
    fin.read((char*)&hdr, sizeof(TbgHeader));
    fin.seekg(hdr.data_offset);
    
    std::vector<uint8_t> data(hdr.data_length);
    fin.read((char*)data.data(), hdr.data_length);
    fin.close();

    
    lodepng::State state;
    int channels = hdr.flags >> 24;
    if (channels == 0x98)
        channels = 3;
    else if (channels == 0x0C)
        channels = 4;
    else
    {
        std::cerr << "Unknown channel flag! " << std::hex << hdr.flags << "\n";
        return 1;
    }

    swap_colors(data.data(), data.size(), channels); // 2-4 ms

    state.info_raw.colortype = channels == 3 ? LodePNGColorType::LCT_RGB : LodePNGColorType::LCT_RGBA;
    std::vector<uint8_t> png;
    auto err = lodepng::encode(png, data, hdr.width, hdr.height, state);
    if (err)
    {
        std::cerr << "Failed to encode PNG image!\n";
        return 1;
    }
    
    std::string outname = name.substr(0, name.find_last_of('.')) + ".png";
    lodepng::save_file(png, outname);
    return 0;
}

int png_to_tbg(const std::string& filename)
{
    uint8_t file_header[TBG_HEADER_SIZE];
    std::memset(file_header, 0, TBG_HEADER_SIZE);
    TbgHeader& hdr = *reinterpret_cast<TbgHeader*>(file_header);
    uint32_t c;
    std::vector<uint8_t> data;
    std::vector<uint8_t> png;
    lodepng::State state;
    state.decoder.color_convert = false;
    lodepng::load_file(png, filename);
    lodepng::decode(data, hdr.width, hdr.height, state, png);

    if (state.info_raw.colortype == LodePNGColorType::LCT_RGB)
        c = 3;
    else if (state.info_raw.colortype == LodePNGColorType::LCT_RGBA)
        c = 4;
    else
    {
        std::cerr << "Unsupported color type!\n";
        return 1;
    }

    swap_colors(data.data(), data.size(), c);

    hdr.magic = '\0gbt';
    hdr.data_offset = TBG_HEADER_SIZE;
    hdr.data_length = data.size();
    hdr.flags = ((c == 3 ? 0x98 : 0x0C) << 24u) | 0x1000;
    hdr.resx = 1.0;
    hdr.resy = 1.0;
    hdr.image_count = 1;
    
    std::string name = filename.substr(0, filename.find_last_of('.')) + ".tbg";
    std::ofstream fout(name, std::ios::binary);
    fout.write((char*)file_header, TBG_HEADER_SIZE);
    fout.write((char*)data.data(), data.size());
    fout.close();
    return 0;
}

void convert(const std::string& name)
{
    std::cout << name + '\n';
    auto dotpos = name.find_last_of('.');
    if (dotpos == -1)
    {
        std::cerr << "Unknown file type.\n";
        return;
    }
    std::string ext = name.substr(dotpos);
    if (ext == ".png")
        png_to_tbg(name);
    else if (ext == ".tbg")
        tbg_to_png(name);
    else
        std::cerr << "Unknown file type.\n";
}

void thread_main()
{
    std::string filename;
    while (true)
    {
        mutex.lock();
        if (todo.empty())
        {
            mutex.unlock();
            return;
        }
        filename = todo.front();
        todo.pop_front();
        mutex.unlock();
        convert(filename);
    }
}

int main(const int argc, const char** argv)
{
    const int argn = argc - 1;
    if (argn == 0)
    {
        std::cout << "Usage: <me> <input files...> (can batch convert many files at once)\n";
        return 0;
    }
    if (argn == 1) // only 1 image, no need for multi threading
        convert(argv[1]);
    std::cout << "Using " << THREADS << " threads for conversion...\n";
    std::vector<std::thread> threads(argn - 1);
    if (argn <= THREADS) // enough threads available, no need for thread pooling
    {
        for (int i = 0; i < argn - 1; i++)
            threads[i] = std::thread(convert, argv[i + 1]);
        convert(argv[argn]);
    }
    else // many images, use thread pool
    {
        for (int i = 1; i < argc; i++)
            todo.emplace_back(argv[i]);
        for (int i = 0; i < argn - 1; i++)
            threads[i] = std::thread(thread_main);
        thread_main();
    }

    for (auto& t : threads)
        t.join();
    return 0;
}