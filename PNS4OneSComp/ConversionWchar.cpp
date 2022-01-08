#ifdef __linux__
#include <cwchar>
#endif

#include "ConversionWchar.h"

constexpr auto MAX_UTF8_CHAR_LEN = 3;

size_t convToShortWchar(WCHAR_T **Dest, const wchar_t *Source, size_t len) {
    if (!len)
        len = ::wcslen(Source) + 1;
    if (!*Dest)
        *Dest = new WCHAR_T[len];

    WCHAR_T *tmpShort = *Dest;
    auto *tmpWChar = (wchar_t *) Source;
    size_t res = 0;

    ::memset(*Dest, 0, len * sizeof(WCHAR_T));
    do {
        *tmpShort++ = (WCHAR_T) *tmpWChar++;
        ++res;
    } while (len-- && *tmpWChar);

    return res;
}

size_t convFromShortWchar(wchar_t **Dest, const WCHAR_T *Source, size_t len) {
    if (!len)
        len = (size_t)(getLenShortWcharStr(Source) + 1);

    if (!*Dest)
        *Dest = new wchar_t[len];

    wchar_t *tmpWChar = *Dest;
    auto *tmpShort = (WCHAR_T *) Source;
    size_t res = 0;

    ::memset(*Dest, 0, len * sizeof(wchar_t));
    do {
        *tmpWChar++ = (wchar_t) *tmpShort++;
        ++res;
    } while (len-- && *tmpShort);

    return res;
}

size_t convFromShortWcharToAscii(char **Dest, const WCHAR_T *Source, size_t len) {
    if (!len)
        len = getLenShortWcharStr(Source) + 1;
    if (!*Dest)
        *Dest = new char[len];

    char* tmpAscii = *Dest;
    auto* tmpWChar = (WCHAR_T*)Source;
    size_t res = 0;

    ::memset(*Dest, 0, len);
    do {
        *tmpAscii++ = (char)*tmpWChar++;
        ++res;
    } while (len-- && *tmpWChar);

    return res;
}

size_t convFromUtf8ToShortWchar(WCHAR_T **Dest, const char *Source, size_t len) {
    if (!len)
        len = ::strlen(Source) + 1;
    if (!*Dest)
        *Dest = new WCHAR_T[len * sizeof(WCHAR_T)];

    size_t res = 0;
    WCHAR_T* tmpWChar = *Dest;
    auto* tmpUtf8 = (char*)Source;
    char mask = 0;

    ::memset(*Dest, 0, len * sizeof(WCHAR_T));
    for (size_t i = len; i > 0 && *tmpUtf8; i--, tmpWChar++, res++) {
        if ((*tmpUtf8 & 0b10000000) == 0) {
            *tmpWChar = (WCHAR_T)*tmpUtf8;
            tmpUtf8++;
        }
        else if ((*tmpUtf8 & 0b11110000) == 0b11110000) {
            // 4 bytes not support.
            *tmpWChar = 0xFFFD;
            tmpUtf8 += 4;
        }
        else {
            int byteCount;
            if ((*tmpUtf8 & 0b11100000) == 0b11100000) {
                byteCount = 3;
            }
            else if ((*tmpUtf8 & 0b11000000) == 0b11000000) {
                byteCount = 2;
            }
            else {
                // Error format - incorrect byte.
                *tmpWChar = 0;
                break;
            }

            *tmpWChar = 0;
            mask = 0b00011111;

            for (int j = 0; j < byteCount; j++) {
                if (*tmpUtf8 == 0) {
                    // Error format - not enough bytes.
                    break;
                }
                *tmpWChar = *tmpWChar * 64 + (*tmpUtf8 & mask);
                tmpUtf8++;
                mask = 0b00111111;
            }
        }
    }

    return res;
}

size_t convFromShortWcharToUtf8(char** Dest, const WCHAR_T* Source, size_t len) {
    if (!len)
        len = getLenShortWcharStr(Source) + 1;
    if (!*Dest)
        *Dest = new char[len * MAX_UTF8_CHAR_LEN];

    size_t res = 0;
    char* tmpChar = *Dest;
    auto* tmpWChar = (WCHAR_T*)Source;

    ::memset(*Dest, 0, len * MAX_UTF8_CHAR_LEN);

    for (; len > 0 && *tmpChar; tmpChar++, len--) {
        if (*tmpWChar < 0x80) {
            *tmpChar = (char)*tmpWChar;
            tmpChar++;
            res += 1;
        }
        else if (*tmpWChar < 0x800) {
            char b1 = *tmpWChar % 0x40;
            char b2 = (*tmpWChar - b1) / 0x40;

            *tmpChar = b2 + 0xC0;
            tmpChar++;

            *tmpChar = b1 + 0x80;
            tmpChar++;

            res += 2;
        }
        else if (*tmpWChar < 0x10000) {
            char b1 = *tmpWChar % 0x40;
            char b2 = ((*tmpWChar - b1) / 0x40) % 0x40;
            char b3 = (*tmpWChar - b1 - (b2 * 0x40)) / 0x1000;

            *tmpWChar = b3 + 0xE0;
            tmpChar++;

            *tmpWChar = b2 + 0x80;
            tmpChar++;

            *tmpWChar = b1 + 0x80;
            tmpChar++;

            res += 3;
        }
        else {
            // more 0x10000 code symbol not support.
            *tmpChar = (char)0xEF;
            tmpChar++;
            *tmpChar = (char)0xBF;
            tmpChar++;
            *tmpChar = (char)0xBD;
            tmpChar++;
            res += 3;
        }
    }

    return res;
}

size_t getLenShortWcharStr(const WCHAR_T *Source) {
    size_t res = 0;
    auto *tmpShort = (WCHAR_T *) Source;

    while (*tmpShort++)
        ++res;

    return res;
}

#ifdef __linux__
WcharWrapper::WcharWrapper(const WCHAR_T *str) :
        m_str_WCHAR(nullptr), m_str_wchar(nullptr) {
    if (str) {
        size_t len = getLenShortWcharStr(str);
        m_str_WCHAR = new WCHAR_T[len + 1];
        memset(m_str_WCHAR, 0, sizeof(WCHAR_T) * (len + 1));
        memcpy(m_str_WCHAR, str, sizeof(WCHAR_T) * len);
        ::convFromShortWchar(&m_str_wchar, m_str_WCHAR);
    }
}
#endif

WcharWrapper::WcharWrapper(const wchar_t *str) :
#ifdef __linux__
        m_str_WCHAR(nullptr),
#endif
        m_str_wchar(nullptr) {
    if (str) {
        size_t len = wcslen(str);
        m_str_wchar = new wchar_t[len + 1];
        memset(m_str_wchar, 0, sizeof(wchar_t) * (len + 1));
        memcpy(m_str_wchar, str, sizeof(wchar_t) * len);
#ifdef __linux__
        ::convToShortWchar(&m_str_WCHAR, m_str_wchar);
#endif
    }
}

WcharWrapper::~WcharWrapper() {
#ifdef __linux__
    if (m_str_WCHAR) {
        delete[] m_str_WCHAR;
        m_str_WCHAR = nullptr;
    }
#endif
    if (m_str_wchar) {
        delete[] m_str_wchar;
        m_str_wchar = nullptr;
    }
}
