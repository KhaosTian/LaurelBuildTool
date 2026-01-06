// Header-only library example
SetName("HeaderOnly");
SetVersion("1.0.0");
SetLanguages("c++20");

// Header-only library (interface target)
Target("utils")
    .SetKind("interface")
    .ExportIncludeDir("include");  // Export headers for dependent projects

// Main executable that uses header-only library
Target("app")
    .AddFiles("src/*.cpp")
    .AddDeps("utils");  // Add dependency, automatically gets include dirs
