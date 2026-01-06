#include <iostream>
#include <mathlib/math.hpp>
#include <stringlib/string.hpp>

int main() {
    std::cout << "=== Mixed Linking Example ===" << std::endl;
    std::cout << "Static Library: mathlib" << std::endl;
    std::cout << "Dynamic Library: stringlib" << std::endl;
    std::cout << std::endl;

    // Test static library (mathlib)
    std::cout << "--- Math Operations (Static Lib) ---" << std::endl;
    double a = 10.0, b = 3.0;
    std::cout << a << " + " << b << " = " << mathlib::add(a, b) << std::endl;
    std::cout << a << " - " << b << " = " << mathlib::subtract(a, b) << std::endl;
    std::cout << a << " * " << b << " = " << mathlib::multiply(a, b) << std::endl;
    std::cout << a << " / " << b << " = " << mathlib::divide(a, b) << std::endl;

    // Test dynamic library (stringlib)
    std::cout << "\n--- String Operations (Dynamic Lib) ---" << std::endl;
    std::string text = "  Hello World  ";
    std::cout << "Original: '" << text << "'" << std::endl;
    std::cout << "Trimmed:  '" << stringlib::trim(text) << "'" << std::endl;
    std::cout << "Upper:    '" << stringlib::to_upper(text) << "'" << std::endl;
    std::cout << "Lower:    '" << stringlib::to_lower(text) << "'" << std::endl;

    return 0;
}
