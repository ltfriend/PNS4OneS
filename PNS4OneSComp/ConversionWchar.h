#ifndef __CONVERSIONWCHAR_H__
#define __CONVERSIONWCHAR_H__

#include "include/types.h"

size_t convToShortWchar(WCHAR_T** Dest, const wchar_t* Source, size_t len = 0);
size_t convFromShortWchar(wchar_t** Dest, const WCHAR_T* Source, size_t len = 0);
size_t convFromShortWcharToAscii(char** Dest, const WCHAR_T* Source, size_t len = 0);
size_t convFromUtf8ToShortWchar(WCHAR_T** Dest, const char* Source, size_t len = 0);
size_t convFromShortWcharToUtf8(char** Dest, const WCHAR_T* Source, size_t len = 0);
size_t getLenShortWcharStr(const WCHAR_T* Source);

class WcharWrapper
{
public:
#ifdef __linux__
    WcharWrapper(const WCHAR_T* str);
#endif
    WcharWrapper(const wchar_t* str);
    ~WcharWrapper();

#ifdef __linux__
    operator const WCHAR_T* () { return m_str_WCHAR; }
    operator WCHAR_T* () { return m_str_WCHAR; }
#endif
    operator const wchar_t* () { return m_str_wchar; }
    operator wchar_t* () { return m_str_wchar; }
private:
    WcharWrapper& operator = (const WcharWrapper& other) { return *this; }
    WcharWrapper(const WcharWrapper& other) : m_str_wchar(nullptr) { }
private:
#ifdef __linux__
    WCHAR_T* m_str_WCHAR;
#endif
    wchar_t* m_str_wchar;
};

#endif //__CONVERSIONWCHAR_H__