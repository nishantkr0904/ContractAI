namespace ContractAI.Core.Interfaces;

// Raised when the clause engine cannot produce a result. The native boundary
// reports failure as a status code rather than an exception, so this is where
// those codes become something the pipeline can catch.
public sealed class ClauseParserException : Exception
{
    public ClauseParserException(string message) : base(message)
    {
    }

    public ClauseParserException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
