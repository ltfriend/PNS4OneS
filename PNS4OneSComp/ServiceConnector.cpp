#ifdef _WINDOWS
#include <WinSock2.h>
#include <WS2tcpip.h>
#else
#include <sys/socket.h>
#include <netdb.h>
#include <unistd.h>
#include <cerrno>
#include <thread>
#endif

#include "ConversionWchar.h"
#include "ServiceConnector.h"
#include "crypt.h"

#ifdef _WINDOWS
#pragma comment(lib, "Ws2_32.lib")
#endif

constexpr auto CONNECTION_CLOSED = -1;

static wchar_t g_SourceId[] = L"com_ptolkachev_pns4ones";
static wchar_t g_EventId[] = L"message";
static WcharWrapper s_SourceId(g_SourceId);
static WcharWrapper s_EventId(g_EventId);

// В Visual Studio под Windows для русских букв исходный файл должен быть в кодировке ASCII, а в Linux - в UTF-8.
// В результате в той или иной системе, в зависимости от кодировки, получаются "кракозябры". Поэтому символы заданы
// в виде Escape-последовательностей.

// Сервис уведомлений прекратил работу
static wchar_t g_ErrorMessageServiceStopped[] = L"{\"error\": \"\x0421\x0435\x0440\x0432\x0438\x0441\x0020\x0443\x0432\x0435\x0434\x043E\x043C\x043B\x0435\x043D\x0438\x0439\x0020\x043F\x0440\x0435\x043A\x0440\x0430\x0442\x0438\x043B\x0020\x0440\x0430\x0431\x043E\x0442\x0443\"}";
// Не удалось расшифровать сообщение. Возможно, указан неверный ключ клиента
static wchar_t g_ErrorEncryptMessage[] = L"{\"error\": \"\x041D\x0435\x0020\x0443\x0434\x0430\x043B\x043E\x0441\x044C\x0020\x0440\x0430\x0441\x0448\x0438\x0444\x0440\x043E\x0432\x0430\x0442\x044C\x0020\x0441\x043E\x043E\x0431\x0449\x0435\x043D\x0438\x0435\x002E\x0020\x0412\x043E\x0437\x043C\x043E\x0436\x043D\x043E\x002C\x0020\x0443\x043A\x0430\x0437\x0430\x043D\x0020\x043D\x0435\x0432\x0435\x0440\x043D\x044B\x0439\x0020\x043A\x043B\x044E\x0447\x0020\x043A\x043B\x0438\x0435\x043D\x0442\x0430\"}";
// Возникла ошибка при получении уведомлений от сервиса
static wchar_t g_ErrorMessageCommon[] = L"{\"error\": \"\x0412\x043E\x0437\x043D\x0438\x043A\x043B\x0430\x0020\x043E\x0448\x0438\x0431\x043A\x0430\x0020\x043F\x0440\x0438\x0020\x043F\x043E\x043B\x0443\x0447\x0435\x043D\x0438\x0438\x0020\x0443\x0432\x0435\x0434\x043E\x043C\x043B\x0435\x043D\x0438\x0439\x0020\x043E\x0442\x0020\x0441\x0435\x0440\x0432\x0438\x0441\x0430\"}";
static WcharWrapper s_ErrorMessageServiceStopped(g_ErrorMessageServiceStopped);
static WcharWrapper s_ErrorEncryptMessage(g_ErrorEncryptMessage);
static WcharWrapper s_ErrorMessageCommon(g_ErrorMessageCommon);

IAddInDefBaseEx *conn;
AesKey aesKey;

#ifdef _WINDOWS
SOCKET sock = INVALID_SOCKET;
#else
int sock = -1;
#endif

extern void SetLastServiceError(const wchar_t *message);

void ConnectDataToByteArray(
    const char* appId,
    const char* ibId,
    const char* userId,
    const WCHAR_T* userGroup,
    unsigned char** byteArray,
    int *byteArrayLen
) {
    size_t appIdLen = strlen(appId) + 1;
    size_t ibIdLen = strlen(ibId) + 1;
    size_t userIdLen = strlen(userId) + 1;
    char* userGroupUtf8 = nullptr;
    size_t userGroupLen = convFromShortWcharToUtf8(&userGroupUtf8, userGroup) + 1;

    *byteArrayLen = (int)(appIdLen + ibIdLen + userIdLen + userGroupLen);
    *byteArray = new unsigned char[*byteArrayLen];
    memset(*byteArray, 0, *byteArrayLen);

    unsigned char* pos = *byteArray;
    memcpy(pos, appId, appIdLen);

    pos += appIdLen;
    memcpy(pos, ibId, ibIdLen);

    pos += ibIdLen;
    memcpy(pos, userId, userIdLen);

    pos += userIdLen;
    memcpy(pos, userGroupUtf8, userGroupLen);

    delete[] userGroupUtf8;
}

WORD ConnectDataToSendBuf(
    const char* appId,
    const char* ibId,
    const char* userId,
    const WCHAR_T* userGroup,
    unsigned char* hmacKey,
    int hmacKeySize,
    char** ppSendBuf
) {
    unsigned char* dataByteArray = NULL, * hmacHash = NULL;
    char* sendBuf, * bufPos;
    int dataSize;
    int hashSize;

    ConnectDataToByteArray(appId, ibId, userId, userGroup, &dataByteArray, &dataSize);

    if (!hmacsha256_sign(dataByteArray, dataSize, hmacKey, hmacKeySize, &hmacHash, &hashSize)) {
        if (!dataByteArray)
            delete[] dataByteArray;

        return -1;
    }

    // В буфер для отправки помещаются следующие данные:
    //  2 байта - общая длина данных;
    //  2 байта - длина подписи (хеш HMAC-SHA256);
    //  подпись (хеш HMAC-SHA256);
    //  идентификатор приложения, заканчивающийся нулем;
    //  идентификатор базы данных, заканчивающийся нулем;
    //  идентификатор пользователя, заканчивающийся нулем;
    //  имя группы пользователя, заканчивающееся нулем.
    int sendBufSize = dataSize + hashSize + (int)sizeof(WORD);
    sendBuf = new char[sendBufSize + sizeof(WORD)];
    bufPos = sendBuf;

    WORD* pSendBufSize = (WORD*)bufPos;
    *pSendBufSize = (WORD)sendBufSize;
    bufPos += sizeof(WORD);

    WORD* pHashSize = (WORD*)bufPos;
    *pHashSize = (WORD)hashSize;
    bufPos += sizeof(WORD);

    memcpy(bufPos, hmacHash, hashSize);
    bufPos += hashSize;

    memcpy(bufPos, dataByteArray, dataSize);

    delete[] dataByteArray;
    delete[] hmacHash;

    *ppSendBuf = sendBuf;
    return (WORD)(sendBufSize + sizeof(WORD));
}

bool SocketInit(const char *hostname, const char *port, struct addrinfo **pAddrInfo) {
#ifdef _WINDOWS
    WSADATA wsaData;

    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != NO_ERROR) {
        // Ошибка инициализации сетевого соединения
        SetLastServiceError(L"\x041E\x0448\x0438\x0431\x043A\x0430\x0020\x0438\x043D\x0438\x0446\x0438\x0430\x043B\x0438\x0437\x0430\x0446\x0438\x0438\x0020\x0441\x0435\x0442\x0435\x0432\x043E\x0433\x043E\x0020\x0441\x043E\x0435\x0434\x0438\x043D\x0435\x043D\x0438\x044F");
        return false;
    }
#endif

    struct addrinfo hinst{};
    memset(&hinst, 0, sizeof(hinst));
    hinst.ai_family = AF_INET;
    hinst.ai_socktype = SOCK_STREAM;
    hinst.ai_protocol = IPPROTO_TCP;

    if (getaddrinfo(hostname, port, &hinst, pAddrInfo) != 0) {
        // Указан неверный адрес сервера
        SetLastServiceError(L"\x0423\x043A\x0430\x0437\x0430\x043D\x0020\x043D\x0435\x0432\x0435\x0440\x043D\x044B\x0439\x0020\x0430\x0434\x0440\x0435\x0441\x0020\x0441\x0435\x0440\x0432\x0435\x0440\x0430");
#ifdef _WINDOWS
        WSACleanup();
#endif
        return false;
    }

    sock = socket((*pAddrInfo)->ai_family, (*pAddrInfo)->ai_socktype, (*pAddrInfo)->ai_protocol);
#ifdef _WINDOWS
    bool invalidSocket = (sock == INVALID_SOCKET);
#else
    bool invalidSocket = (sock == -1);
#endif
    if (invalidSocket) {
        // Ошибка создания сетевого соединения
        SetLastServiceError(L"\x041E\x0448\x0438\x0431\x043A\x0430\x0020\x0441\x043E\x0437\x0434\x0430\x043D\x0438\x044F\x0020\x0441\x0435\x0442\x0435\x0432\x043E\x0433\x043E\x0020\x0441\x043E\x0435\x0434\x0438\x043D\x0435\x043D\x0438\x044F");
#ifdef _WINDOWS
        WSACleanup();
#endif
        return false;
    }

    return true;
}

bool SocketConnect(addrinfo *pAddrInfo) {
    if (connect(sock, pAddrInfo->ai_addr, (int)pAddrInfo->ai_addrlen)) {
        // Не удалось установить соединение, возможно, сервис недоступен
        SetLastServiceError(L"\x041D\x0435\x0020\x0443\x0434\x0430\x043B\x043E\x0441\x044C\x0020\x0443\x0441\x0442\x0430\x043D\x043E\x0432\x0438\x0442\x044C\x0020\x0441\x043E\x0435\x0434\x0438\x043D\x0435\x043D\x0438\x0435\x002C\x0020\x0432\x043E\x0437\x043C\x043E\x0436\x043D\x043E\x002C\x0020\x0441\x0435\x0440\x0432\x0438\x0441\x0020\x043D\x0435\x0434\x043E\x0441\x0442\x0443\x043F\x0435\x043D");
#ifdef _WINDOWS
        closesocket(sock);
        WSACleanup();
#else
        close(sock);
#endif
        return false;
    }
    return true;
}

void CloseServiceConnection() {
#ifdef _WINDOWS
    shutdown(sock, SD_BOTH);
    closesocket(sock);
    WSACleanup();
    sock = INVALID_SOCKET;
#else
    shutdown(sock, SHUT_RDWR);
    close(sock);
    sock = 0;
#endif
    dispose_aes_key(aesKey);
}

bool ConnectToService(
    const char *hostname,
    const char *port,
    const char *appId,
    const char *ibId,
    const char *userId,
    const WCHAR_T *userGroup,
    unsigned char *hmacKey,
    int hmacKeySize
) {
    struct addrinfo *pAddrInfo = nullptr;

    if (!SocketInit(hostname, port, &pAddrInfo)) {
        if (pAddrInfo)
            freeaddrinfo(pAddrInfo);
        return false;
    }
    if (!SocketConnect(pAddrInfo)) {
        freeaddrinfo(pAddrInfo);
        return false;
    }

    freeaddrinfo(pAddrInfo);

    char *buf;
    WORD bufSize = ConnectDataToSendBuf(appId, ibId, userId, userGroup, hmacKey, hmacKeySize, &buf);
    if (bufSize == -1)
    {
        // Ошибка при подписании запроса на подключение
        SetLastServiceError(L"\x041E\x0448\x0438\x0431\x043A\x0430\x0020\x043F\x0440\x0438\x0020\x043F\x043E\x0434\x043F\x0438\x0441\x0430\x043D\x0438\x0438\x0020\x0437\x0430\x043F\x0440\x043E\x0441\x0430\x0020\x043D\x0430\x0020\x043F\x043E\x0434\x043A\x043B\x044E\x0447\x0435\x043D\x0438\x0435");
        delete[] buf;
        return false;
    }

    bool result = true;
    if (send(sock, buf, bufSize, 0) == -1) {
        // Ошибка при регистрации получателя уведомлений, возможно, сервис недоступен
        SetLastServiceError(L"\x041E\x0448\x0438\x0431\x043A\x0430\x0020\x043F\x0440\x0438\x0020\x0440\x0435\x0433\x0438\x0441\x0442\x0440\x0430\x0446\x0438\x0438\x0020\x043F\x043E\x043B\x0443\x0447\x0430\x0442\x0435\x043B\x044F\x0020\x0443\x0432\x0435\x0434\x043E\x043C\x043B\x0435\x043D\x0438\x0439\x002C\x0020\x0432\x043E\x0437\x043C\x043E\x0436\x043D\x043E\x002C\x0020\x0441\x0435\x0440\x0432\x0438\x0441\x0020\x043D\x0435\x0434\x043E\x0441\x0442\x0443\x043F\x0435\x043D");
        CloseServiceConnection();
        result = false;
    }

    delete[] buf;
    return result;
}

void ProceedReceivedMessage(WCHAR_T *message) {
    conn->ExternalEvent(s_SourceId, s_EventId, message);
}

int ReadUInt32(uint32_t *res) {
    char buf[sizeof(uint32_t)];
    char bufRes[sizeof(uint32_t)];
    long count, bytesRead = 0;

    memset(bufRes, 0, sizeof(uint32_t));

    while (bytesRead < sizeof(uint32_t)) {
        count = recv(sock, buf, sizeof(uint32_t) - bytesRead, 0);

        if (count == 0)
            return CONNECTION_CLOSED;
        else if (count == -1)
#ifdef _WINDOWS
            return WSAGetLastError();
#else
            return errno;
#endif

        memcpy(bufRes + bytesRead, buf, count);
        bytesRead += count;
    }

    *res = *((uint32_t *) bufRes);

    return 0;
}

int ReadBytesArray(unsigned char** bytesArray, int *arrayLength)
{
    uint32_t msgLen, bytesRead = 0;
    int res;
    long count;
    unsigned char* pos;

    if ((res = ReadUInt32(&msgLen)) != 0)
        return res;

    *bytesArray = new unsigned char[(size_t)msgLen];
    *arrayLength = msgLen;

    pos = *bytesArray;

    do {
        count = recv(sock, (char*)pos, msgLen - bytesRead, 0);

        if (count == 0)
            return CONNECTION_CLOSED;
        else if (count == -1)
#ifdef _WINDOWS
            return WSAGetLastError();
#else
            return errno;
#endif

        pos += count;
        bytesRead += count;

    } while (bytesRead < msgLen);

    return 0;
}

#ifdef _WINDOWS
DWORD WINAPI ListenService(LPVOID lpParam)
#else
void *ListenService(void *lpParam)
#endif
{
    unsigned char* encrypted, * decrypted;
    int encryptedSize = 0, decryptedSize = 0;
    char* utf8Array;
    WCHAR_T* message;
    int res = 0;

    conn = (IAddInDefBaseEx *) lpParam;

    while (res == 0) {
        encrypted = nullptr;
        decrypted = nullptr;
        utf8Array = nullptr;
        message = nullptr;

        res = ReadBytesArray(&encrypted, &encryptedSize);

        if (res == 0) {
            decryptedSize = bytearray4_to_int(encrypted);
            if (aes_decrypt(encrypted + 4, encryptedSize - 4, aesKey, &decrypted, decryptedSize)) {
                utf8Array = new char[(size_t)decryptedSize + 1];
                memcpy(utf8Array, decrypted, decryptedSize);
                utf8Array[decryptedSize] = 0;

                convFromUtf8ToShortWchar(&message, utf8Array);
                ProceedReceivedMessage(message);
            }
            else {
                ProceedReceivedMessage(s_ErrorEncryptMessage);
                res = -1; // Прерывание цикла
            }
        }
        else if (res == CONNECTION_CLOSED)
            ProceedReceivedMessage(s_ErrorMessageServiceStopped);
        else
            ProceedReceivedMessage(s_ErrorMessageCommon);

        if (encrypted)
            delete[] encrypted;
        if (decrypted)
            delete[] decrypted;
        if (utf8Array)
            delete[] utf8Array;
        if (message)
            delete[] message;
    }

    CloseServiceConnection();
#ifdef _WINDOWS
    return 0;
#else
    return nullptr;
#endif
}

bool StartListenService(
    const char *hostname,
    const char *port,
    const char *appId,
    const char *ibId,
    const char *userId,
    const char* clientKey,
    const WCHAR_T *userGroup,
    IAddInDefBaseEx *piConnect
) {
    if (!get_aes_keys_from_base64(clientKey, &aesKey)) {
        // Некорректный ключ клиента
        SetLastServiceError(L"\x041D\x0435\x043A\x043E\x0440\x0440\x0435\x043A\x0442\x043D\x044B\x0439\x0020\x043A\x043B\x044E\x0447\x0020\x043A\x043B\x0438\x0435\x043D\x0442\x0430");
        return false;
    }

    if (!ConnectToService(hostname, port, appId, ibId, userId, userGroup, aesKey.Key, aesKey.KeySize)) {
        return false;
    }

#ifdef _WINDOWS
    DWORD dwThreadId;
    HANDLE hThread;

    if ((hThread = CreateThread(NULL, 0, ListenService, piConnect, 0, &dwThreadId)) == NULL) {
        CloseServiceConnection();
        // Ошибка инициализации прослушивания сообщений
        SetLastServiceError(L"\x041E\x0448\x0438\x0431\x043A\x0430\x0020\x0438\x043D\x0438\x0446\x0438\x0430\x043B\x0438\x0437\x0430\x0446\x0438\x0438\x0020\x043F\x0440\x043E\x0441\x043B\x0443\x0448\x0438\x0432\x0430\x043D\x0438\x044F\x0020\x0441\x043E\x043E\x0431\x0449\x0435\x043D\x0438\x0439");
        return false;
    }
#else
    pthread_t thr;
    pthread_create(&thr, nullptr, ListenService, piConnect);
#endif

    return true;
}

void StopListenService() {
#ifdef _WINDOWS
    bool invalidSocket = (sock == INVALID_SOCKET);
#else
    bool invalidSocket = (sock == -1);
#endif
    if (!invalidSocket) {
        CloseServiceConnection();
    }
}