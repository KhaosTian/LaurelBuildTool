// Multi-module example
SetName("MultiModule");
SetVersion("1.0.0");
SetLanguages("c++17");

// Include sub-modules
Include("src/mathlib");

// Create main executable that links mathlib
Target("main")
    .AddFiles("src/main.cpp")
    .AddDeps("mathlib");  // Automatically gets include dirs and links library
