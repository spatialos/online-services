namespace MemoryStore
{
    public class FailedConditionException : MemoryStoreException
    {
        public string Condition { get; }

        public FailedConditionException(string condition, string message = null) : base(message ?? $"Condition '{condition}' failed")
        {
            Condition = condition;
        }
    }
}
