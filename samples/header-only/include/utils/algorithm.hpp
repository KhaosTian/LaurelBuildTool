#pragma once

#include <vector>
#include <algorithm>
#include <numeric>

// Header-only library example
namespace utils {

// Template function (header-only)
template<typename T>
std::vector<T> filter(const std::vector<T>& vec, bool (*predicate)(const T&)) {
    std::vector<T> result;
    for (const auto& item : vec) {
        if (predicate(item)) {
            result.push_back(item);
        }
    }
    return result;
}

// Inline functions
template<typename T>
inline T sum(const std::vector<T>& vec) {
    return std::accumulate(vec.begin(), vec.end(), T{0});
}

template<typename T>
inline double average(const std::vector<T>& vec) {
    if (vec.empty()) return 0.0;
    return static_cast<double>(sum(vec)) / vec.size();
}

} // namespace utils
