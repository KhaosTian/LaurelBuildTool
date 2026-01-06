#pragma once

#include <string>

#if defined(_WIN32) || defined(__CYGWIN__)
    #ifdef STRINGLIB_EXPORTS
        #define STRINGLIB_API __declspec(dllexport)
    #else
        #define STRINGLIB_API __declspec(dllimport)
    #endif
#else
    #define STRINGLIB_API __attribute__((visibility("default")))
#endif

namespace stringlib {

STRINGLIB_API std::string to_upper(const std::string& str);
STRINGLIB_API std::string to_lower(const std::string& str);
STRINGLIB_API std::string trim(const std::string& str);

}
