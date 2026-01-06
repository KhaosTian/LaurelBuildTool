// Math library module (static library)
Target("mathlib")
    .SetKind("static")
    .AddFiles("math.cpp")
    .AddIncludeDir("include")
    .ExportIncludeDir("include");
