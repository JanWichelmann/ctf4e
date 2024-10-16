using System.Text.Json;

namespace Ctf4e.Api.Models
{
    /// <summary>
    ///     Defines a login request to a lab server.
    /// </summary>
    public class UserLoginRequest
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        public int UserId { get; set; }

        public string UserDisplayName { get; set; }

        public int? GroupId { get; set; }

        public string GroupName { get; set; }

        public bool AdminMode { get; set; }
        
        public string LabUserName { get; set; }
        
        public string LabPassword { get; set; }

        public string Serialize()
        {
            return JsonSerializer.Serialize(this, _jsonSerializerOptions);
        }

        public static UserLoginRequest Deserialize(string serialized)
        {
            return JsonSerializer.Deserialize<UserLoginRequest>(serialized);
        }
    }
}