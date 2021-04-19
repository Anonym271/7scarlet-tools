#include "utils.hpp"

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
