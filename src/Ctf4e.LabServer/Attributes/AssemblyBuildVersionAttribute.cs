using System;

namespace Ctf4e.LabServer.Attributes;

public class AssemblyBuildVersionAttribute : Attribute
{
    public string Version { get; }

    public AssemblyBuildVersionAttribute(string version)
    {
        Version = version;
    }
}