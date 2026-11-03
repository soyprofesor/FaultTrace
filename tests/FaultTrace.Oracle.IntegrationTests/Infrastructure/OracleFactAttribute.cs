using Xunit;

namespace FaultTrace.Oracle.IntegrationTests.Infrastructure;

// Without a configured database the test is skipped instead of failing.
public sealed class OracleFactAttribute : FactAttribute
{
    public OracleFactAttribute()
    {
        if (!OracleTestDatabase.IsConfigured)
        {
            Skip = OracleTestDatabase.SkipReason;
        }
    }
}
