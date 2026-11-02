using System;
using System.Collections.Generic;

// Same full name as the ODP.NET type, so detection can be tested without a reference to the driver.
namespace Oracle.ManagedDataAccess.Client
{
    public class OracleException : Exception
    {
        public OracleException(string message, int number = 0, IEnumerable<OracleError>? errors = null)
            : base(message)
        {
            NumberValue = number;
            Errors = errors;
        }

        public virtual int Number => NumberValue;

        public virtual IEnumerable<OracleError>? Errors { get; }

        private int NumberValue { get; }
    }

    public class OracleError
    {
        public OracleError(int number, string message)
        {
            Number = number;
            Message = message;
        }

        public int Number { get; }

        public string Message { get; }
    }

    public class DerivedOracleException : OracleException
    {
        public DerivedOracleException(string message, int number)
            : base(message, number)
        {
        }
    }

    public class BrokenOracleException : OracleException
    {
        public BrokenOracleException(string message)
            : base(message)
        {
        }

        public override int Number => throw new InvalidOperationException("Driver failure");

        public override IEnumerable<OracleError>? Errors => throw new InvalidOperationException("Driver failure");
    }
}
