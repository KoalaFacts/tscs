using Xunit.v3;
using Xunit.Sdk;

// Preserve the original NUnit suite's non-parallel execution.
[assembly: Parallelization(Mode = ParallelMode.None)]
