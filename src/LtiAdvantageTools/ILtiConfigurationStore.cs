namespace LtiAdvantageTools;

public interface ILtiConfigurationStore
{
    Task<string> GetSerializedPublicJsonWebKeyAsync(CancellationToken cancellationToken);
    Task SetSerializedPublicJsonWebKeyAsync(string key, CancellationToken cancellationToken);
}