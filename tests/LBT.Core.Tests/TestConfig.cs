using Xunit;

// Disable test parallelization because tests share BuildSystem static state
[assembly: CollectionBehavior(DisableTestParallelization = true)]
