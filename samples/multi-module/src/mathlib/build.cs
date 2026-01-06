// Math library module (static library)
Target("mathlib")
    .SetKind("static")
    .AddFiles("math.cpp")
    .AddIncludeDir(Visibility.Public, "include");  // Public for dependent projects
