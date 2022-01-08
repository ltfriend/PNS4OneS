#ifndef __BASE64_H__
#define __BASE64_H__

#include <cstddef>

int base64_decode(const char* base64, unsigned char* out, size_t size);

#endif //__BASE64_H__
