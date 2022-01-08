#ifdef __linux__
#include <cstdlib>
#include <cwchar>
#endif

#include <clocale>
#include "AddInNative.h"
#include "ConversionWchar.h"
#include "ServiceConnector.h"

static const wchar_t* g_MethodNames[] =
{
    L"Connect",
    L"Shutdown",
    L"GetLastError"
};

// В Visual Studio под Windows для русских букв исходный файл должен быть в кодировке ASCII, а в Linux - в UTF-8.
// В результате в той или иной системе, в зависимости от кодировки, получаются "кракозябры". Поэтому символы заданы
// в виде Escape-последовательностей.
static const wchar_t* g_MethodNamesRu[] =
{
    L"\x041F\x043E\x0434\x043A\x043B\x044E\x0447\x0438\x0442\x044C", // Подключить
    L"\x041E\x0442\x043A\x043B\x044E\x0447\x0438\x0442\x044C",       // Отключить
    L"\x041F\x043E\x043B\x0443\x0447\x0438\x0442\x044C\x041E\x0448\x0438\x0431\x043A\x0443" // ПолучитьОшибку
};

static const wchar_t g_kClassNames[] = L"PNS4OneSComp";
static const wchar_t g_ComponentNameType[] = L"com_ptolkachev_PNS4OneSCompExtension";
static WcharWrapper s_kClassNames(g_kClassNames);

static AppCapabilities g_capabilities = eAppCapabilitiesInvalid;

static WCHAR_T* pwstrLastError = nullptr;

void SetLastServiceError(const wchar_t* message)
{
    if (pwstrLastError != nullptr) {
        delete[] pwstrLastError;
        pwstrLastError = nullptr;
    }

    convToShortWchar(&pwstrLastError, message);
}

//---------------------------------------------------------------------------//
long GetClassObject(const WCHAR_T* wsName, IComponentBase** pInterface)
{
    if (!*pInterface)
    {
        *pInterface = new CAddInNative();
        return (long)*pInterface;
    }
    return 0;
}
//---------------------------------------------------------------------------//
AppCapabilities SetPlatformCapabilities(const AppCapabilities capabilities)
{
    g_capabilities = capabilities;
    return eAppCapabilitiesLast;
}
//---------------------------------------------------------------------------//
long DestroyObject(IComponentBase** pIntf)
{
    if (pwstrLastError)
        delete[] pwstrLastError;

    if (!*pIntf)
        return -1;

    delete* pIntf;
    *pIntf = nullptr;
    return 0;
}
//---------------------------------------------------------------------------//
const WCHAR_T* GetClassNames()
{
    return s_kClassNames;
}
//---------------------------------------------------------------------------//
//CAddInNative
CAddInNative::CAddInNative() : m_iConnect(nullptr), m_iMemory(nullptr)
{ }
//---------------------------------------------------------------------------//
CAddInNative::~CAddInNative()
{
}
//---------------------------------------------------------------------------//
bool CAddInNative::Init(void* pConnection)
{
    m_iConnect = (IAddInDefBaseEx*)pConnection;
    m_iConnect->SetEventBufferDepth(1000);
    return m_iConnect != nullptr;
}
//---------------------------------------------------------------------------//
long CAddInNative::GetInfo()
{
    return 1000;
}
//---------------------------------------------------------------------------//
void CAddInNative::Done()
{
    StopListenService();
    m_iConnect = nullptr;
    m_iMemory = nullptr;
}
//---------------------------------------------------------------------------//
bool CAddInNative::RegisterExtensionAs(WCHAR_T** wsExtensionName)
{
    const wchar_t* wsExtension = g_ComponentNameType;
    uint32_t iActualSize = static_cast<uint32_t>(::wcslen(wsExtension) + 1);

    if (m_iMemory)
    {
        if (m_iMemory->AllocMemory((void**)wsExtensionName, iActualSize * sizeof(WCHAR_T)))
        {
            convToShortWchar(wsExtensionName, wsExtension, iActualSize);
            return true;
        }
    }

    return false;
}
//---------------------------------------------------------------------------//
long CAddInNative::GetNProps()
{
    return eLastProp;
}
//---------------------------------------------------------------------------//
long CAddInNative::FindProp(const WCHAR_T* wsPropName)
{
    return -1;
}
//---------------------------------------------------------------------------//
const WCHAR_T* CAddInNative::GetPropName(long lPropNum, long lPropAlias)
{
    return nullptr;
}
//---------------------------------------------------------------------------//
bool CAddInNative::GetPropVal(const long lPropNum, tVariant* pvarPropVal)
{
    return false;
}
//---------------------------------------------------------------------------//
bool CAddInNative::SetPropVal(const long lPropNum, tVariant* varPropVal)
{
    return false;
}
//---------------------------------------------------------------------------//
bool CAddInNative::IsPropReadable(const long lPropNum)
{
    return false;
}
//---------------------------------------------------------------------------//
bool CAddInNative::IsPropWritable(const long lPropNum)
{
    return false;
}
//---------------------------------------------------------------------------//
long CAddInNative::GetNMethods()
{
    return eLastMethod;
}
//---------------------------------------------------------------------------//
long CAddInNative::FindMethod(const WCHAR_T* wsMethodName)
{
    long plMethodNum = -1;
    wchar_t* name = 0;
    convFromShortWchar(&name, wsMethodName);

    plMethodNum = findName(g_MethodNames, name, eLastMethod);

    if (plMethodNum == -1)
        plMethodNum = findName(g_MethodNamesRu, name, eLastMethod);

    delete[] name;

    return plMethodNum;
}
//---------------------------------------------------------------------------//
const WCHAR_T* CAddInNative::GetMethodName(const long lMethodNum,
    const long lMethodAlias)
{
    if (lMethodNum >= eLastMethod)
        return nullptr;

    wchar_t* wsCurrentName = nullptr;
    WCHAR_T* wsMethodName = nullptr;

    switch (lMethodAlias)
    {
    case 0: // First language (english)
        wsCurrentName = (wchar_t*)g_MethodNames[lMethodNum];
        break;
    case 1: // Second language (local)
        wsCurrentName = (wchar_t*)g_MethodNamesRu[lMethodNum];
        break;
    default:
        return 0;
    }

    uint32_t iActualSize = static_cast<uint32_t>(wcslen(wsCurrentName) + 1);

    if (m_iMemory && wsCurrentName)
    {
        if (m_iMemory->AllocMemory((void**)&wsMethodName, iActualSize * sizeof(WCHAR_T)))
            convToShortWchar(&wsMethodName, wsCurrentName, iActualSize);
    }

    return wsMethodName;
}
//---------------------------------------------------------------------------//
long CAddInNative::GetNParams(const long lMethodNum)
{
    if (lMethodNum == eMethConnect)
        return 7;
    else
        return 0;
}
//---------------------------------------------------------------------------//
bool CAddInNative::GetParamDefValue(const long lMethodNum, const long lParamNum,
    tVariant* pvarParamDefValue)
{
    TV_VT(pvarParamDefValue) = VTYPE_EMPTY;
    return false;
}
//---------------------------------------------------------------------------//
bool CAddInNative::HasRetVal(const long lMethodNum)
{
    return (lMethodNum == eMethConnect || lMethodNum == eMethGetLastError);
}
//---------------------------------------------------------------------------//
bool CAddInNative::CallAsProc(const long lMethodNum,
    tVariant* paParams, const long lSizeArray)
{
    if (lMethodNum == eMethShutdown) {
        StopListenService();
        return true;
    }
    else {
        return false;
    }
}
//---------------------------------------------------------------------------//
bool CAddInNative::CallAsFunc(const long lMethodNum,
    tVariant* pvarRetValue, tVariant* paParams, const long lSizeArray)
{
    switch (lMethodNum) {
    case eMethConnect: {
        int result = true;

        tVariant& pHostName = paParams[0];
        tVariant& pPort = paParams[1];
        tVariant& pAppId = paParams[2];
        tVariant& pIbId = paParams[3];
        tVariant& pUserId = paParams[4];
        tVariant& pUserGroup = paParams[5];
        tVariant& pClientKey = paParams[6];

        char* pstrHostName = nullptr;
        char* pstrPort = nullptr;
        char* pstrAppId = nullptr;
        char* pstrIbId = nullptr;
        char* pstrUserId = nullptr;
        char* pstrClientKey = nullptr;
        auto* pwstrUserGroupWrapper = new WcharWrapper(pUserGroup.pwstrVal);

        convFromShortWcharToAscii(&pstrHostName, pHostName.pwstrVal);
        convFromShortWcharToAscii(&pstrPort, pPort.pwstrVal);
        convFromShortWcharToAscii(&pstrAppId, pAppId.pwstrVal);
        convFromShortWcharToAscii(&pstrIbId, pIbId.pwstrVal);
        convFromShortWcharToAscii(&pstrUserId, pUserId.pwstrVal);
        convFromShortWcharToAscii(&pstrClientKey, pClientKey.pwstrVal);

        if (!StartListenService(
            pstrHostName,
            pstrPort,
            pstrAppId,
            pstrIbId,
            pstrUserId,
            pstrClientKey,
            *pwstrUserGroupWrapper,
            m_iConnect
        )) {
            result = false;
        }

        delete[] pstrHostName;
        delete[] pstrPort;
        delete[] pstrAppId;
        delete[] pstrIbId;
        delete[] pstrUserId;
        delete[] pstrClientKey;
        delete pwstrUserGroupWrapper;

        TV_VT(pvarRetValue) = VTYPE_BOOL;
        TV_BOOL(pvarRetValue) = result;
        return true;
    }
    case eMethGetLastError: {
        WCHAR_T* pwstrResult = nullptr;

        if (pwstrLastError != nullptr) {
            int size = (int)getLenShortWcharStr(pwstrLastError) + 1;
            if (m_iMemory->AllocMemory((void**)&pwstrResult, size * sizeof(WCHAR_T))) {
                memcpy(pwstrResult, pwstrLastError, size * sizeof(WCHAR_T));
                pvarRetValue->wstrLen = size - 1;
            }
        }

        TV_VT(pvarRetValue) = VTYPE_PWSTR;
        TV_WSTR(pvarRetValue) = pwstrResult;
        return true;
    }
    default:
        return false;
    }
}
//---------------------------------------------------------------------------//
void CAddInNative::SetLocale(const WCHAR_T* loc)
{
#ifndef __linux__
    _wsetlocale(LC_ALL, loc);
#else
    int size = 0;
    char* mbstr = 0;
    wchar_t* tmpLoc = 0;
    convFromShortWchar(&tmpLoc, loc);
    size = wcstombs(0, tmpLoc, 0) + 1;
    mbstr = new char[size];

    if (!mbstr)
    {
        delete[] tmpLoc;
        return;
    }

    memset(mbstr, 0, size);
    size = wcstombs(mbstr, tmpLoc, wcslen(tmpLoc));
    setlocale(LC_ALL, mbstr);
    delete[] tmpLoc;
    delete[] mbstr;
#endif
}
//---------------------------------------------------------------------------//
bool CAddInNative::setMemManager(void* mem)
{
    m_iMemory = (IMemoryManager*)mem;
    return m_iMemory != 0;
}
//---------------------------------------------------------------------------//
void CAddInNative::addError(uint32_t wcode, const wchar_t* source,
    const wchar_t* descriptor, long code)
{
    if (m_iConnect)
    {
        WCHAR_T* err = 0;
        WCHAR_T* descr = 0;

        convToShortWchar(&err, source);
        convToShortWchar(&descr, descriptor);

        m_iConnect->AddError(wcode, err, descr, code);

        delete[] descr;
        delete[] err;
    }
}
//---------------------------------------------------------------------------//
long CAddInNative::findName(const wchar_t* names[], const wchar_t* name,
    const uint32_t size) const
{
    long ret = -1;
    for (uint32_t i = 0; i < size; i++)
    {
        if (!wcscmp(names[i], name))
        {
            ret = i;
            break;
        }
    }
    return ret;
}
//---------------------------------------------------------------------------//
