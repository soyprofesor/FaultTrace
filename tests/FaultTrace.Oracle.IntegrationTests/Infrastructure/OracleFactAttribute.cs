using Xunit;

namespace FaultTrace.Oracle.IntegrationTests.Infrastructure;

// Without a database the test shows as skipped instead of failing, so CI and other machines stay green.
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
