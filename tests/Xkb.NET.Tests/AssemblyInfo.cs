using System.Runtime.Versioning;
using Xunit;

[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("freebsd")]

[assembly: CollectionBehavior(DisableTestParallelization = true)]
