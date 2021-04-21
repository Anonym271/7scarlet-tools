#include <iostream>
#include <filesystem>

#include "farc.hpp"

namespace fs = std::filesystem;

enum class Settings
{
	none, input, output, lzss
};

void usage()
{
	std::cout <<
		"Usage: <me> [options] <input-file> [output-dir]\n"
		"Options:\n"
		"  -i <input>          define input explicitly\n"
		"  -o <output>         define output explicitly\n"
		"  -p, --pack          pack archive\n"
		"  -e, --extract       extract archive (default)\n"
		"  -l, --lzss <bool>   set lzss compression flag\n";
}
void usage_and_exit(int code = 0)
{
	usage();
	exit(code);
}
void usage_error_and_exit(const std::string& msg, int code = 1)
{
	usage();
	std::cout << std::endl;
	std::cerr << msg << std::endl;
	exit(code);
}
void invalid_argument(const std::string& arg)
{
	usage_error_and_exit("Invalid argument: " + arg);
}

bool str_to_bool(const std::string& str)
{
	if (str == "0" || str == "true")
		return true;
	else if (str == "1" || str == "false")
		return false;
	usage_error_and_exit("Expected boolean value, got " + str);
}

int main(int argc, const char** argv)
{
	if (argc < 2)
		usage_and_exit();
	fs::path inpath;
	fs::path outpath;
	std::string arg;
	Settings setting = Settings::none;
	bool lzss = false;
	char mode = 0;

	for (int i = 1; i < argc; i++)
	{
		arg = argv[i];
		if (setting == Settings::none) // no setting flag set
		{
			if (arg.empty()) // ignore empty args
				continue;
			if (arg[0] == '-') // set setting for next arg
			{
				if (arg == "-i")
					setting = Settings::input;
				else if (arg == "-o")
					setting = Settings::output;
				else if (arg == "-p" || arg == "-pack" || arg == "--pack")
				{
					if (mode)
						usage_error_and_exit("Multiple modes defined!");
					mode = 'p';
					setting = Settings::none;
				}
				else if (arg == "-e" || arg == "-extract" || arg == "--extract")
				{
					if (mode)
						usage_error_and_exit("Multiple modes defined!");
					mode = 'e';
					setting = Settings::none;
				}
				else if (arg == "-l" || arg == "-lzss" || arg == "--lzss")
					setting = Settings::lzss;
				else invalid_argument(arg);
			}
			else // no option: interpret as input / output
			{
				if (inpath.empty())
					inpath = arg;
				else if (outpath.empty())
					outpath = arg;
				else usage_error_and_exit("Input and output are already defined!");
			}
		}
		else // option was set
		{
			switch (setting)
			{
			case Settings::input:
				if (inpath.empty())
					inpath = arg;
				else usage_error_and_exit("Multiple inputs defined!");
				break;
			case Settings::output:
				if (outpath.empty())
					outpath = arg;
				else usage_error_and_exit("Multiple outputs defined!");
				break;
			case Settings::lzss:
				lzss = str_to_bool(arg);
				break;
			default:
				usage_error_and_exit("Internal error lul");
			}
			setting = Settings::none;
		}
	}
	if (setting != Settings::none)
		usage_error_and_exit("Expected value for last option!");
	if (!mode)
		mode = 'e';
	if (inpath.empty())
		usage_error_and_exit("Please specify an input!");

	if (outpath.empty())
	{
		outpath = inpath;
		if (mode == 'e')
			outpath.replace_extension();
		else outpath += ".bin";
	}

	try
	{
		if (mode == 'e')
			farc::extract(inpath, outpath);
		else
			farc::pack(inpath, outpath);
	}
	catch (const std::ios::failure& fail)
	{
		std::cerr << "IO ERROR: " << fail.what() << std::endl 
			<< "Please check your file paths!" << std::endl;
	}
	catch (const std::exception& exc)
	{
		std::cerr << "ERROR: " << exc.what() << std::endl;
		return 1;
	}

	return 0;
}