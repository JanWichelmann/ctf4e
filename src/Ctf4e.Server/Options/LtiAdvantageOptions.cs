namespace Ctf4e.Server.Options;

public class LtiAdvantageOptions
{
    public ToolOptions Tool { get; set; } = new();
    public PlatformOptions Platform { get; set; } = new();
    

    public class ToolOptions
    {
        public string ClientId { get; set; } = null;
        public string LaunchRedirectUri { get; set; } = null;
    }
    
    public class PlatformOptions
    {
        public string Issuer { get; set; } = null;
        public string AuthorizationEndpoint { get; set; } = null;
        public string JwksUri { get; set; } = null;
        public string DeploymentId { get; set; } = null;
    }
}