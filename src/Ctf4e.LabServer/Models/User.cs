namespace Ctf4e.LabServer.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string UserDisplayName { get; set; }

        public int? GroupId { get; set; }

        public string GroupName { get; set; }
    }
}