namespace LtiAdvantageTools;

public class LtiAdvantageOptions
{
    public ToolOptions Tool { get; set; } = new();
    public PlatformOptions Platform { get; set; } = new();


    public class ToolOptions
    {
        public string? ClientId { get; set; }
        public string? LaunchRedirectUri { get; set; }
    }

    public class PlatformOptions
    {
        public string? Issuer { get; set; }
        public string? AuthorizationEndpoint { get; set; }
        public string? JwksUri { get; set; }
        public string? DeploymentId { get; set; }
    }
}