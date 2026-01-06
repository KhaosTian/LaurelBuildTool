// Main application with static and dynamic libraries
SetName("MixedLinking");
SetVersion("1.0.0");
SetLanguages("c++20");

// Include sub-modules
Include("mathlib");
Include("stringlib");

// Main executable
Target("app")
    .AddFiles("app/src/*.cpp")
    .AddDeps("mathlib", "stringlib");  // Dependencies automatically export include dirs and link libraries
