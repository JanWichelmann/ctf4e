using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable CollectionNeverQueried.Local

namespace Ctf4e.Server.Services.Sync
{
    public interface IDumpService
    {
        Task<string> GetGroupDataAsync(CancellationToken cancellationToken);
    }

    public class DumpService : IDumpService
    {
        private readonly CtfDbContext _dbContext;

        public DumpService(CtfDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<string> GetGroupDataAsync(CancellationToken cancellationToken)
        {
            // Get users and groups
            var users = await _dbContext.Users.AsNoTracking()
                .Where(u => u.GroupId != null)
                .ToListAsync(cancellationToken);
            var groups = await _dbContext.Groups.AsNoTracking()
                .ToListAsync(cancellationToken);

            // Create map of groups and users
            var groupData = new List<GroupData>();
            foreach(var g in groups)
            {
                var gData = new GroupData
                {
                    Id = g.Id,
                    Name = g.DisplayName,
                    Users = new List<GroupData.GroupDataUserEntry>()
                };
                groupData.Add(gData);

                foreach(var u in users.Where(u => u.GroupId == g.Id))
                {
                    gData.Users.Add(new GroupData.GroupDataUserEntry
                    {
                        Id = u.Id,
                        Name = u.DisplayName,
                        MoodleId = u.MoodleUserId,
                        MoodleName = u.MoodleName
                    });
                }
            }

            return JsonConvert.SerializeObject(new { GroupData = groupData }, Formatting.Indented);
        }

        private class GroupData
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public List<GroupDataUserEntry> Users { get; set; }

            public class GroupDataUserEntry
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public int MoodleId { get; set; }
                public string MoodleName { get; set; }
            }
        }
    }
}