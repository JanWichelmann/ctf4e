using Newtonsoft.Json;

namespace Ctf4e.Api
{
    /// <summary>
    ///     Defines a login request to a lab server.
    /// </summary>
    public class UserLoginRequest
    {
        public int UserId { get; set; }

        public string UserDisplayName { get; set; }

        public int? GroupId { get; set; }

        public string GroupName { get; set; }

        public bool AdminMode { get; set; }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        public static UserLoginRequest Deserialize(string serialized)
        {
            return JsonConvert.DeserializeObject<UserLoginRequest>(serialized);
        }
    }
}