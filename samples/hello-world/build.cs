// Simple hello-world example
SetProject("HelloWorld");
SetVersion("1.0.0");
SetLanguages("c++17");

Target("hello")
    .AddFiles("src/*.cpp")
    .AddIncludeDir("include");

