// String library (dynamic/shared library)
Target("stringlib")
    .SetKind("shared")
    .AddFiles("src/*.cpp")
    .AddIncludeDir(Visibility.Public, "include");  // Public for dependent projects
