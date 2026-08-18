namespace JamWeaver.Core.Persistence;

public sealed class PatternPersistenceException : Exception
{
    public PatternPersistenceException(string message) : base(message) { }
    public PatternPersistenceException(string message, Exception innerException) : base(message, innerException) { }
}
