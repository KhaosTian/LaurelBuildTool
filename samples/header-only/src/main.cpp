#include <iostream>
#include <vector>
#include "utils/algorithm.hpp"

int main() {
    std::cout << "=== Header-Only Library Example ===" << std::endl;
    std::cout << "\nHeader-only libraries don't need compilation." << std::endl;
    std::cout << "Just include the headers and use them!" << std::endl;

    std::vector<int> numbers = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};

    // Use sum function
    std::cout << "\nNumbers: ";
    for (auto n : numbers) std::cout << n << " ";
    std::cout << "\nSum: " << utils::sum(numbers) << std::endl;

    // Use average function
    std::cout << "Average: " << utils::average(numbers) << std::endl;

    // Use filter with simple predicate
    std::vector<int> filtered;
    for (auto n : numbers) {
        if (n % 2 == 0) filtered.push_back(n);
    }
    std::cout << "Even numbers: ";
    for (auto n : filtered) std::cout << n << " ";
    std::cout << std::endl;

    return 0;
}
