namespace Improbable.OnlineServices.Common
{
    public interface ISecretProvider
    {
        string this[string key] { get; }
    }
}
