namespace Ctf4e.Server.Options
{
    public class MainOptions
    {
        public bool DevelopmentMode { get; set; }
        public bool ProxySupport { get; set; }
        public string ProxyNetworkAddress { get; set; }
        public int ProxyNetworkPrefix { get; set; }
        public string DefaultCulture { get; set; }
        public string SecretsDirectory { get; set; }
    }
}