// Math library (static library)
Target("mathlib")
    .SetKind("static")
    .AddFiles("src/*.cpp")
    .AddIncludeDir(Visibility.Public, "include");  // Public for dependent projects
