cmake_minimum_required(VERSION 3.20)
project(pns4onescomp)

set(CMAKE_CXX_STANDARD 11)

set(pns4onescomp_SRC
        dllmain.cpp
        AddInNative.cpp
        AddInNative.h
        ConversionWchar.cpp
        ConversionWchar.h
        crypt.cpp
        crypt.h
        base64.cpp
        base64.h
        ServiceConnector.cpp
        ServiceConnector.h
        include/AddInDefBase.h
        include/com.h
        include/ComponentBase.h
        include/IMemoryManager.h
        include/types.h)
if (WIN32)
    list(APPEND pns4onescomp_SRC resource.h pns4onescomp.rc pns4onescomp.def)
endif()

include_directories(${CMAKE_SOURCE_DIR}/include)

if (WIN32)
    add_definitions(-DUNICODE -DWIN32 -D_USRDLL -D_CRT_SECURE_NO_WARNINGS -D_CRT_SECURE_NO_DEPRECATE -DADDINCPP_EXPORTS)
    if(TARGET_PLATFORM_32)
        add_definitions(-D_USE_32BIT_TIME_T )
    endif()
endif()

if (UNIX)
link_libraries("-lcrypto")
endif()

add_library(pns4onescomp SHARED ${pns4onescomp_SRC})