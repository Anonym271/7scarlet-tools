#include <iostream>
#include <fstream>
#include <vector>
#include <deque>
#include <thread>
#include <mutex>
#include <filesystem>
#include <sstream>
#include <set>
#include <regex>

#include <lodepng.h>

namespace fs = std::filesystem;

constexpr int TBX_MAGIC = '\0xbt';
constexpr int TBG_MAGIC = '\0gbt';
constexpr int TBG_HEADER_SIZE = 828;

const int HARWARE_THREADS = std::thread::hardware_concurrency();
const int THREADS = HARWARE_THREADS ? HARWARE_THREADS : 4;

std::mutex todo_mutex;
std::mutex file_mutex;
int universal_counter = 0;

enum class Mode
{
    none, convert, extract, pack
};
Mode _mode = Mode::none;

/* STRUCTS */
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

/* UTILITY FUNCTIONS */
template<typename P>
void swap_colors(P* pixbuf, size_t length, int c)
{
    for (P *end = pixbuf + length; pixbuf < end; pixbuf += c)
        std::swap(pixbuf[0], pixbuf[2]);
}

int error_msg(const std::string& msg)
{
    std::cerr << msg << std::endl;
    return 1;
}

void usage_and_exit(int code = 0)
{
    std::cout <<
        "Usage:   <me> [mode] <input_files...>\n"
        "Modes:\n"
        "  -c                 Convert TBG/PNG (can batch convert many files at once)\n"
        "  -x                 Extract TBX\n"
        "  -p <output_file>   Pack input files into one .tbx\n\n"
        "If no mode is offered one of the modes -c and -x will be chosen based on the first element.\n"
        //"Supports wildcard (*) but no subdirectories.\n"
        ;
    exit(code);
}

void process_args(int argc, const char** argv)
{
    if (argc < 2)
        usage_and_exit();
    std::string arg1(argv[1]);
    if (arg1 == "-c")
        _mode = Mode::convert;
    else if (arg1 == "-x")
        _mode = Mode::extract;
    else if (arg1 == "-p")
        _mode = Mode::pack;
    else if (arg1 == "-h")
        usage_and_exit();
    else
    {
        fs::path p1(arg1);
        std::string ext = p1.extension().string();
        std::transform(ext.begin(), ext.end(), ext.begin(), ::tolower);
        if (ext.empty())
        {
            std::cerr << "Cannot deduce mode from file without extension!\n";
            exit(1);
        }
        if (ext == ".png" || ext == ".tbg")
            _mode = Mode::convert;
        else if (ext == ".tbx")
            _mode = Mode::extract;
        else
        {
            std::cerr << "Unknown file extension: " << ext << "\n";
            exit(1);
        }
    }
}

std::string get_outname(const std::string& tbx_name, int index)
{
    std::stringstream ss;
    ss << tbx_name;
    ss << '_';
    if (index < 10)
        ss << '0';
    ss << index << ".png";
    return ss.str();
}

/* CONVERSION */
int tbg_to_png(std::vector<uint8_t>& filedata)
{
    if (filedata.size() < sizeof(TbgHeader))
        return error_msg("Not a valid TBG file!");
    const TbgHeader& hdr = *reinterpret_cast<const TbgHeader*>(filedata.data());
    if (hdr.magic != TBG_MAGIC)
        return error_msg("Not a valid TBG file!");
    if (filedata.size() < (size_t)hdr.data_offset + hdr.data_length)
        return error_msg("File content exceeds file size!");

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

    uint8_t* data = filedata.data() + hdr.data_offset;
    swap_colors(data, hdr.data_length, channels); // 2-4 ms

    std::vector<uint8_t> png;
    lodepng::State state;
    state.info_raw.colortype = channels == 3 ? LodePNGColorType::LCT_RGB : LodePNGColorType::LCT_RGBA;
    state.info_png.color.colortype = state.info_raw.colortype;
    state.encoder.auto_convert = false;
    auto err = lodepng::encode(png, data, hdr.width, hdr.height, state);
    if (err)
    {
        std::cerr << "Failed to encode PNG image!\n";
        return 1;
    }
    filedata = std::move(png);
    return 0;
}
int tbg_to_png(const std::string& name)
{
    std::ifstream fin(name, std::ios::binary | std::ios::ate);
    std::vector<uint8_t> data(fin.tellg());
    fin.seekg(0);
    fin.read((char*)data.data(), data.size());
    
    tbg_to_png(data);

    std::string outname = name.substr(0, name.find_last_of('.')) + ".png";
    lodepng::save_file(data, outname);
    return 0;
}

int png_to_tbg(std::vector<uint8_t>& filedata)
{
    uint32_t w, h, c;
    std::vector<uint8_t> data;
    lodepng::State state;
    state.decoder.color_convert = false;
    lodepng::decode(data, w, h, state, filedata);

    if (state.info_raw.colortype == LodePNGColorType::LCT_RGB)
        c = 3;
    else if (state.info_raw.colortype == LodePNGColorType::LCT_RGBA)
        c = 4;
    else
    {
        std::cout << "Unsupported color type, converting to RGBA...\n";
        state.decoder.color_convert = true;
        state.info_raw.colortype = LodePNGColorType::LCT_RGBA;
        lodepng::decode(data, w, h, state, filedata);
        c = 4;
    }

    swap_colors(data.data(), data.size(), c);

    std::vector<uint8_t> tbg(TBG_HEADER_SIZE + data.size());
    std::memset(tbg.data(), 0, TBG_HEADER_SIZE);
    std::memcpy(tbg.data() + TBG_HEADER_SIZE, data.data(), data.size());
    TbgHeader& hdr = *reinterpret_cast<TbgHeader*>(tbg.data());
    hdr.width = w;
    hdr.height = h;
    hdr.magic = TBG_MAGIC;
    hdr.data_offset = TBG_HEADER_SIZE;
    hdr.data_length = data.size();
    hdr.flags = ((c == 3 ? 0x98 : 0x0C) << 24u) | 0x1000;
    hdr.resx = 1.0;
    hdr.resy = 1.0;
    hdr.image_count = 1;
    
    filedata = std::move(tbg);
    return 0;
}
int png_to_tbg(const std::string& filename)
{
    std::vector<uint8_t> data;
    lodepng::load_file(data, filename);

    png_to_tbg(data);
  
    std::string name = filename.substr(0, filename.find_last_of('.')) + ".tbg";
    std::ofstream fout(name, std::ios::binary);
    if (!fout.is_open())
    {
        std::cerr << "Could not open file " << name;
        return 1;
    }
    fout.write((char*)data.data(), data.size());
    return 0;
}

/* TBX */
void repack()
{

}

void extract_one(std::istream& fin, uint32_t offset, uint32_t length, std::string out_filename)
{
    std::vector<uint8_t> buffer(length);
    file_mutex.lock(); // lock mutex over input file
    fin.seekg(offset);
    fin.read((char*)buffer.data(), length);
    file_mutex.unlock();

    tbg_to_png(buffer);

    lodepng::save_file(buffer, out_filename);
}

void thread_extract(
    std::istream& fin,
    std::deque<TbxEntry>& todo,
    const std::string& outname)
{
    TbxEntry task;
    std::string fname;
    while (true)
    {
        todo_mutex.lock();
        if (todo.empty())
        {
            todo_mutex.unlock();
            return;
        }
        task = todo.front();
        todo.pop_front();
        fname = get_outname(outname, ++universal_counter);
        todo_mutex.unlock();

        extract_one(fin, task.offset, task.length, fname);
    }
}

int extract(const std::string& name)
{
    uint64_t fsize;
    TbxHeader hdr;

    std::cout << name << '\n';

    std::ifstream fin(name, std::ios::binary | std::ios::ate);
    if (!fin.is_open())
    {
        std::cerr << "Could not open " << name << '\n';
        return 1;
    }
    fsize = fin.tellg();
    if (fsize < sizeof(TbxHeader))
    {
        std::cerr << "Not a valid TBX file: " << name << '\n';
        return 1;
    }

    fin.seekg(0);
    fin.read((char*)&hdr, sizeof(TbxHeader));
    if (hdr.magic != TBX_MAGIC || 
        fsize < hdr.table_offset + sizeof(TbxEntry) * hdr.file_count ||
        fsize < hdr.data_offset)
    {
        std::cerr << "Not a valid TBX file: " << name << '\n';
        return 1;
    }

    std::vector<TbxEntry> entries(hdr.file_count);
    fin.read((char*)entries.data(), hdr.file_count * sizeof(TbxEntry));

    std::string out_filename;
    size_t dotpos = name.find_last_of('.');
    if (dotpos == -1)
        out_filename = name;
    else
        out_filename = name.substr(0, dotpos);
    
    uint32_t threadc;
    std::vector<std::thread> threads;
    if (hdr.file_count > THREADS) // use thread pool
    {
        std::deque<TbxEntry> todo;
        for (auto& entry : entries)
            todo.push_back(entry);
        universal_counter = 0;
        threadc = THREADS - 1;
        threads.reserve(threadc);
        for (int i = 0; i < threadc; i++)
            threads.emplace_back(
                thread_extract,
                std::ref(fin),
                std::ref(todo),
                std::ref(out_filename));
        thread_extract(fin, todo, out_filename);
        for (auto& t : threads)
            t.join();
    }
    else // don't use thread pool
    {
        threadc = hdr.file_count - 1;
        threads.reserve(threadc);
        for (int i = 0; i < threadc; i++)
            threads.emplace_back(
                extract_one,
                std::ref(fin),
                entries[i].offset,
                entries[i].length,
                get_outname(out_filename, i + 1));
        extract_one(
            std::ref(fin),
            entries.back().offset,
            entries.back().length,
            get_outname(out_filename, hdr.file_count));
        for (auto& t : threads)
            t.join();
    }
}

/* TBP */
int convert(const std::string& name)
{
    std::cout << name + '\n';
    auto dotpos = name.find_last_of('.');
    if (dotpos == -1)
    {
        std::cerr << "Unknown file type.\n";
        return 1;
    }
    std::string ext = name.substr(dotpos);
    if (ext == ".png")
        png_to_tbg(name);
    else if (ext == ".tbg")
        tbg_to_png(name);
    else
    {
        std::cerr << "Unknown file type.\n";
        return 1;
    }
    return 0;
}

void thread_convert(std::deque<std::string>& todo)
{
    std::string filename;
    while (true)
    {
        todo_mutex.lock();
        if (todo.empty())
        {
            todo_mutex.unlock();
            return;
        }
        filename = todo.front();
        todo.pop_front();
        todo_mutex.unlock();
        convert(filename);
    }
}

/* MAIN PROGRAMS */

int main_convert(int argc, const char** argv)
{
    const int argn = argc - 1;
    if (argn == 1) // only 1 image, no need for multi threading
    {
        convert(argv[1]);
        return 0;
    }

    std::cout << "Using " << THREADS << " threads for conversion...\n";
    const int threadc = std::min(argn, THREADS) - 1;
    std::vector<std::thread> threads(threadc);
    if (argn <= THREADS) // enough threads available, no need for thread pooling
    {
        for (int i = 0; i < argn - 1; i++)
            threads[i] = std::thread(convert, argv[i + 1]);
        convert(argv[argn]);
    }
    else // many images, use thread pool
    {
        std::deque<std::string> todo;
        for (int i = 1; i < argc; i++)
            todo.emplace_back(argv[i]);
        for (int i = 0; i < threadc; i++)
            threads[i] = std::thread(thread_convert, std::ref(todo));
        thread_convert(std::ref(todo));
    }

    for (auto& t : threads)
        t.join();
    return 0;
}

int main_extract(int argc, const char** argv)
{
    std::string filename;
    for (int i = 1; i < argc; i++)
        extract(argv[i]);
    return 0;
}

int main_pack(int argc, const char** argv)
{
    int i = 1;
    bool regex = false;
    std::string arcname(argv[i]);
    if (arcname == "-r")
    {
        regex = true;
        i++;
        arcname = argv[i];
    }
    size_t uspos = arcname.find_last_of('_');
    if (uspos == -1)
    {
        size_t dotpos = arcname.find_last_of('.');
        if (dotpos == -1)
            arcname += ".tbx";
        else arcname = arcname.substr(0, dotpos) + ".tbx";
    }
    else // contains underscore '_'
        arcname = arcname.substr(0, uspos) + ".tbx"; // because files *should* be named myarc_00.png, myarc_01.png, ...

    std::set<std::string> args; // to make sure they are in the right order
    if (regex) // match file names in currend WD with given regex
    {
        for (; i < argc; i++)
        {
            std::string fn;
            std::smatch match;
            std::string arg(argv[i]);
            const std::regex re(arg, std::regex_constants::ECMAScript);
            std::cout << "Collecting files matching " << arg << std::endl;
            for (auto& direntry : fs::directory_iterator("."))
            {
                fn = direntry.path().filename().string();
                if (std::regex_match(fn, match, re))
                {
              //      std::cout << fn << std::endl;
                    args.emplace(std::move(fn));
                }
            }
        }
    }
    else
    {
        for (; i < argc; i++)
            args.emplace(argv[i]);
    }


    TbxHeader hdr;
    hdr.magic = TBX_MAGIC;
    hdr.file_count = args.size();
    hdr.table_offset = sizeof(TbxHeader);
    hdr.data_offset = sizeof(TbxHeader) + hdr.file_count * sizeof(TbxEntry);

    std::ofstream fout(arcname, std::ios::binary);
    fout.write((char*)&hdr, sizeof(TbxHeader));
    fout.seekp(hdr.data_offset);

    std::vector<TbxEntry> entries(hdr.file_count);

    size_t dotpos;
    bool is_png;
    std::ifstream fin;
    std::vector<uint8_t> buffer;
    i = 0;
    for (auto& arg : args)
    {
        std::cout << arg << std::endl;
        size_t dotpos = arg.find_last_of('.');
        if (dotpos == -1)
            is_png = false;
        else
        {
            std::string ext(arg.substr(dotpos));
            std::transform(ext.begin(), ext.end(), ext.begin(), ::tolower);
            if (ext == ".png")
                is_png = true;
            else is_png = false;
        }

        if (is_png)
        {
            if (lodepng::load_file(buffer, arg))
            {
                std::cerr << "Failed to load " << arg << '\n';
                return 1;
            }
            png_to_tbg(buffer);
        }
        else // don't convert, leave as is
        {
            fin.open(arg, std::ios::binary | std::ios::ate);
            if (!fin.is_open())
            {
                std::cerr << "Failed to open " << arg << '\n';
                return 1;
            }
            uint64_t fsize = fin.tellg();
            if (fsize > UINT32_MAX)
            {
                std::cerr << "File " << arg << " is too big! (Max. total size: 4 GB)\n";
                return 1;
            }
            buffer.resize(fsize);
            fin.read((char*)buffer.data(), fsize);
            fin.close();
        }

        entries[i].offset = fout.tellp();
        entries[i].length = buffer.size();
        fout.write((char*)buffer.data(), buffer.size());

        i++;
    }

    fout.seekp(hdr.table_offset);
    fout.write((char*)entries.data(), entries.size() * sizeof(TbxEntry));

    return 0;
}

int main(int argc, const char** argv)
{
    process_args(argc, argv);

    if (argv[1][0] == '-') // arg was used
    {
        argv++; // this is hacky af, i know
        argc--;
    }

    switch (_mode)
    {
    case Mode::convert:
        return main_convert(argc, argv);
    case Mode::extract:
        return main_extract(argc, argv);
    case Mode::pack:
        return main_pack(argc, argv);
    default:
        std::cerr << "Failed to find the correct mode.\n";
        return 1;
    }
}