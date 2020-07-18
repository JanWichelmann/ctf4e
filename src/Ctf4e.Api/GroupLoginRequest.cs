using System;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Ctf4e.Api
{
    /// <summary>
    /// Defines a login request to a lab server.
    /// </summary>
    public class GroupLoginRequest
    {
        public int GroupId { get; set; }

        public string UserDisplayName { get; set; }

        public bool AdminMode { get; set; }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }

        public static GroupLoginRequest Deserialize(string serialized)
        {
            return JsonConvert.DeserializeObject<GroupLoginRequest>(serialized);
        }
    }
}
