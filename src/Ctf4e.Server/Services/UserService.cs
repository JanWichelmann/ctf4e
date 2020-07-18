using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services
{
    /// <summary>
    /// Functionality for managing users and groups.
    /// </summary>
    public interface IUserService
    {
        IAsyncEnumerable<User> GetUsersAsync();
        Task<bool> AnyUsers(CancellationToken cancellationToken = default);
        Task<User> FindUserByMoodleUserIdAsync(int moodleUserId, CancellationToken cancellationToken = default);
        Task<User> GetUserAsync(int id, CancellationToken cancellationToken = default);
        Task<User> CreateUserAsync(User user, CancellationToken cancellationToken = default);
        Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);
        IAsyncEnumerable<Group> GetGroupsAsync();
        IAsyncEnumerable<Group> GetGroupsInSlotAsync(int slotId);
        Task<Group> GetGroupAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> GroupExistsAsync(int id, CancellationToken cancellationToken = default);
        Task CreateGroupAsync(Group group, bool createSplitGroups, string groupFindingCode1, string groupFindingCode2, CancellationToken cancellationToken = default);
        Task<Group> CreateGroupAsync(Group group, CancellationToken cancellationToken = default);
        Task UpdateGroupAsync(Group group, CancellationToken cancellationToken = default);
        Task DeleteGroupAsync(int id, CancellationToken cancellationToken = default);
    }

    public class UserService : IUserService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;

        public UserService(CtfDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public IAsyncEnumerable<User> GetUsersAsync()
        {
            return _dbContext.Users.AsNoTracking()
                .OrderBy(u => u.DisplayName)
                .ProjectTo<User>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
        }

        public Task<bool> AnyUsers(CancellationToken cancellationToken = default)
        {
            return _dbContext.Users.AsNoTracking()
                .AnyAsync(cancellationToken);
        }

        public Task<User> FindUserByMoodleUserIdAsync(int moodleUserId, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users.AsNoTracking()
                 .Include(u => u.Group)
                 .Where(u => u.MoodleUserId == moodleUserId)
                 .ProjectTo<User>(_mapper.ConfigurationProvider, u => u.Group)
                 .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<User> GetUserAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users.AsNoTracking()
                .Include(u => u.Group)
                .Where(u => u.Id == id)
                .ProjectTo<User>(_mapper.ConfigurationProvider, u => u.Group)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<User> CreateUserAsync(User user, CancellationToken cancellationToken = default)
        {
            // Create new user
            var userEntity = _dbContext.Users.Add(new UserEntity
            {
                DisplayName = user.DisplayName,
                MoodleUserId = user.MoodleUserId,
                MoodleName = user.MoodleName,
                IsAdmin = user.IsAdmin,
                IsTutor = user.IsTutor,
                GroupFindingCode = user.GroupFindingCode
            }).Entity;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<User>(userEntity);
        }

        public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
        {
            // Try to retrieve existing user entity
            var userEntity = await _dbContext.Users.FindAsync(new object[] { user.Id }, cancellationToken);
            if(userEntity == null)
                throw new InvalidOperationException("Dieser Benutzer existiert nicht");

            // Update entry
            userEntity.DisplayName = user.DisplayName;
            userEntity.MoodleUserId = user.MoodleUserId;
            userEntity.MoodleName = user.MoodleName;
            userEntity.IsAdmin = user.IsAdmin;
            userEntity.IsTutor = user.IsTutor;
            userEntity.GroupFindingCode = user.GroupFindingCode;
            userEntity.GroupId = user.GroupId;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public IAsyncEnumerable<Group> GetGroupsAsync()
        {
            return _dbContext.Groups.AsNoTracking()
                .OrderBy(g => g.SlotId)
                .ThenBy(g => g.DisplayName)
                .ProjectTo<Group>(_mapper.ConfigurationProvider, g => g.Slot, g => g.Members)
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Group> GetGroupsInSlotAsync(int slotId)
        {
            return _dbContext.Groups.AsNoTracking()
                .Include(g => g.Slot)
                .Include(g => g.Members)
                .Where(g => g.SlotId == slotId)
                .OrderBy(g => g.DisplayName)
                .ProjectTo<Group>(_mapper.ConfigurationProvider, g => g.Slot, g => g.Members)
                .AsAsyncEnumerable();
        }

        public Task<Group> GetGroupAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Groups.AsNoTracking()
                .Include(g => g.Slot)
                .Include(g => g.Members)
                .Where(g => g.Id == id)
                .ProjectTo<Group>(_mapper.ConfigurationProvider, g => g.Slot, g => g.Members)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> GroupExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Groups.AsNoTracking()
                .Where(s => s.Id == id)
                .AnyAsync(cancellationToken);
        }

        public async Task CreateGroupAsync(Group group, bool createSplitGroups, string groupFindingCode1, string groupFindingCode2, CancellationToken cancellationToken = default)
        {
            // Retrieve affected users
            var userEntities = await _dbContext.Users.AsQueryable()
                .Where(u => u.GroupFindingCode == groupFindingCode1 || u.GroupFindingCode == groupFindingCode2)
                .Include(u => u.Group)
                .ToListAsync(cancellationToken);
            if(userEntities.Count != 2)
                throw new ArgumentException("Ungültiger Code.");
            if(userEntities.Any(u => u.Group != null))
                throw new InvalidOperationException("Mindestens ein Benutzer ist bereits einer Gruppe zugewiesen.");

            // Create new group(s)
            var groupEntity1 = _dbContext.Groups.Add(new GroupEntity
            {
                SlotId = group.SlotId,
                DisplayName = group.DisplayName,
                ShowInScoreboard = group.ShowInScoreboard,
                Members = new List<UserEntity>(),
                ExerciseSubmissions = new List<ExerciseSubmissionEntity>(),
                FlagSubmissions = new List<FlagSubmissionEntity>()
            }).Entity;
            var groupEntity2 = groupEntity1;
            if(createSplitGroups)
            {
                groupEntity1.DisplayName = groupEntity1.DisplayName + " (1)";
                groupEntity2 = _dbContext.Groups.Add(new GroupEntity
                {
                    SlotId = group.SlotId,
                    DisplayName = group.DisplayName + " (2)",
                    ShowInScoreboard = group.ShowInScoreboard,
                    Members = new List<UserEntity>(),
                    ExerciseSubmissions = new List<ExerciseSubmissionEntity>(),
                    FlagSubmissions = new List<FlagSubmissionEntity>()
                }).Entity;
            }

            // Update users
            userEntities[0].GroupId = groupEntity1.Id;
            groupEntity1.Members.Add(userEntities[0]);
            userEntities[1].GroupId = groupEntity2.Id;
            groupEntity2.Members.Add(userEntities[1]);

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<Group> CreateGroupAsync(Group group, CancellationToken cancellationToken = default)
        {
            // Create new group
            var groupEntity = _dbContext.Groups.Add(new GroupEntity
            {
                SlotId = group.SlotId,
                DisplayName = group.DisplayName,
                ShowInScoreboard = group.ShowInScoreboard,
                Members = new List<UserEntity>(),
                ExerciseSubmissions = new List<ExerciseSubmissionEntity>(),
                FlagSubmissions = new List<FlagSubmissionEntity>()
            }).Entity;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<Group>(groupEntity);
        }

        public async Task UpdateGroupAsync(Group group, CancellationToken cancellationToken = default)
        {
            // Try to retrieve existing user entity
            var groupEntity = await _dbContext.Groups.FindAsync(new object[] { group.Id }, cancellationToken);
            if(groupEntity == null)
                throw new InvalidOperationException("Diese Gruppe existiert nicht");

            // Update entry
            groupEntity.DisplayName = group.DisplayName;
            groupEntity.SlotId = group.SlotId;
            groupEntity.ShowInScoreboard = group.ShowInScoreboard;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteGroupAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete entry
                _dbContext.Groups.Remove(new GroupEntity { Id = id });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
            {
                // Most likely a non-existent entry, just forward the exception
                throw;
            }
        }
    }
}
