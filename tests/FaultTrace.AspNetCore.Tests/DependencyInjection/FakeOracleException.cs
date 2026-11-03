using System;

// Same full name as the ODP.NET type, so detection can be tested without a reference to the driver.
namespace Oracle.ManagedDataAccess.Client;

public class OracleException : Exception
{
    public OracleException(string message, int number)
        : base(message)
    {
        Number = number;
    }

    public int Number { get; }
}
