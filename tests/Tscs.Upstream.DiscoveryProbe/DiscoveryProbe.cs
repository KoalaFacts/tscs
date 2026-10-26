// Deliberately one genuine source-configuration identity from the frozen
// 15,523-target catalogue; this canary tests MTP discovery, not TypeScript.
using System;
using Xunit;

namespace Tscs.Upstream.DiscoveryProbe;

public sealed class DiscoveryProbe
{
    [Fact(DisplayName = "tsc/testdata/tests/cases/compiler/2dArrays.ts [default] configuration:67cb1471604eb4443e5e229479f1458d2551e01344642b8b7d7e3795b626c824", Skip = "UNIMPLEMENTED: ORIGINAL_COMPILER_RUNNER")]
    public void Target_67cb1471604eb4443e5e229479f1458d2551e01344642b8b7d7e3795b626c824() =>
        throw new NotImplementedException("configuration:67cb1471604eb4443e5e229479f1458d2551e01344642b8b7d7e3795b626c824");
}
