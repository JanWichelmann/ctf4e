namespace Ctf4e.LabServer.Options;

public class LabOptions
{
    public string CtfServerBaseUrl { get; set; }
        
    public bool DevelopmentMode { get; set; }
        
    public string DefaultCulture { get; set; }
        
    public bool ProxySupport { get; set; }
    public string ProxyNetworkAddress { get; set; }
    public int ProxyNetworkPrefix { get; set; }
        
    public string UserStateDirectory { get; set; }
        
    public int UserStateLogSize { get; set; }
        
    public string LabConfigurationFile { get; set; }
        
    public bool EnableDocker { get; set; }
    
    public string DockerContainerName { get; set; }
        
    public string DockerContainerInitUserScriptPath { get; set; }
        
    public string DockerContainerGradeScriptPath { get; set; }
    
    public int? DockerContainerGradingConcurrencyCount { get; set; }
    
    public int? DockerContainerGradingTimeout { get; set; }
    
    public bool PassAsGroup { get; set; }

    public string PageTitle { get; set; }
        
    public string NavbarTitle { get; set; }
}