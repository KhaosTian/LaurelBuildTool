// Simple hello-world example
SetName("HelloWorld");
SetVersion("1.0.0");
SetLanguages("c++17");

Target("hello")
    .AddFiles("src/*.cpp")
    .AddIncludeDir("include");

