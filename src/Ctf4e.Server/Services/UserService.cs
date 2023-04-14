using System;
using System.Collections.Concurrent;
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

namespace Ctf4e.Server.Services;

/// <summary>
///     Functionality for managing users and groups.
/// </summary>
public interface IUserService
{
    IAsyncEnumerable<User> GetUsersAsync();
    IAsyncEnumerable<User> GetUsersWithGroupsAsync();
    IAsyncEnumerable<User> GetGroupMembersAsync(int groupId);
    Task<bool> AnyUsers(CancellationToken cancellationToken);
    Task<User> FindUserByMoodleUserIdAsync(int moodleUserId, CancellationToken cancellationToken);
    Task<User> FindUserByIdAsync(int id, CancellationToken cancellationToken);
    Task<User> FindUserByMoodleNameAsync(string moodleName, CancellationToken cancellationToken);
    Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken);
    Task<User> CreateUserAsync(User user, CancellationToken cancellationToken);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken);
    IAsyncEnumerable<Group> GetGroupsAsync();
    IAsyncEnumerable<Group> GetGroupsInSlotAsync(int slotId);
    Task<Group> GetGroupAsync(int id, CancellationToken cancellationToken);
    Task<bool> GroupExistsAsync(int id, CancellationToken cancellationToken);
    Task<int> CreateGroupAsync(Group group, List<string> groupFindingCodes, CancellationToken cancellationToken);
    Task<Group> CreateGroupAsync(Group group, CancellationToken cancellationToken);
    Task UpdateGroupAsync(Group group, CancellationToken cancellationToken);
    Task DeleteGroupAsync(int id, CancellationToken cancellationToken);
}

public class UserService : IUserService
{
    private readonly CtfDbContext _dbContext;
    private readonly IMapper _mapper;

    private ConcurrentDictionary<int, User> _cachedUsers = new();

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

    public IAsyncEnumerable<User> GetUsersWithGroupsAsync()
    {
        return _dbContext.Users.AsNoTracking()
            .Include(u => u.Group)
            .OrderBy(u => u.DisplayName)
            .ProjectTo<User>(_mapper.ConfigurationProvider, u => u.Group)
            .AsAsyncEnumerable();
    }

    public IAsyncEnumerable<User> GetGroupMembersAsync(int groupId)
    {
        return _dbContext.Users.AsNoTracking()
            .Where(u => u.GroupId == groupId)
            .OrderBy(u => u.DisplayName)
            .ProjectTo<User>(_mapper.ConfigurationProvider)
            .AsAsyncEnumerable();
    }

    public Task<bool> AnyUsers(CancellationToken cancellationToken)
    {
        return _dbContext.Users.AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public Task<User> FindUserByMoodleUserIdAsync(int moodleUserId, CancellationToken cancellationToken)
    {
        return _dbContext.Users.AsNoTracking()
            .Include(u => u.Group)
            .Where(u => u.MoodleUserId == moodleUserId)
            .ProjectTo<User>(_mapper.ConfigurationProvider, u => u.Group)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User> FindUserByIdAsync(int id, CancellationToken cancellationToken)
    {
        if(_cachedUsers.TryGetValue(id, out var user))
            return user;
        
        user = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Group)
            .Where(u => u.Id == id)
            .ProjectTo<User>(_mapper.ConfigurationProvider, u => u.Group)
            .FirstOrDefaultAsync(cancellationToken);

        _cachedUsers.TryAdd(id, user);
        return user;
    }

    public Task<User> FindUserByMoodleNameAsync(string moodleName, CancellationToken cancellationToken)
    {
        return _dbContext.Users.AsNoTracking()
            .Where(u => u.MoodleName == moodleName)
            .ProjectTo<User>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Users.AsNoTracking()
            .Where(s => s.Id == id)
            .AnyAsync(cancellationToken);
    }

    public async Task<User> CreateUserAsync(User user, CancellationToken cancellationToken)
    {
        // Create new user
        var userEntity = _dbContext.Users.Add(new UserEntity
        {
            DisplayName = user.DisplayName,
            MoodleUserId = user.MoodleUserId,
            MoodleName = user.MoodleName,
            PasswordHash = user.PasswordHash,
            Privileges = user.Privileges,
            IsTutor = user.IsTutor,
            GroupFindingCode = user.GroupFindingCode,
            ExerciseSubmissions = new List<ExerciseSubmissionEntity>(),
            FlagSubmissions = new List<FlagSubmissionEntity>()
        }).Entity;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<User>(userEntity);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken)
    {
        // Try to retrieve existing user entity
        var userEntity = await _dbContext.Users.FindAsync(new object[] { user.Id }, cancellationToken);
        if(userEntity == null)
            throw new ArgumentException("This user does not exist.", nameof(user));

        // Update entry
        userEntity.DisplayName = user.DisplayName;
        userEntity.MoodleUserId = user.MoodleUserId;
        userEntity.MoodleName = user.MoodleName;
        userEntity.Privileges = user.Privileges;
        userEntity.PasswordHash = user.PasswordHash;
        userEntity.IsTutor = user.IsTutor;
        userEntity.GroupFindingCode = user.GroupFindingCode;
        userEntity.GroupId = user.GroupId;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // Invalidate cache
        _cachedUsers.TryRemove(user.Id, out _);
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

    public Task<Group> GetGroupAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Groups.AsNoTracking()
            .Include(g => g.Slot)
            .Include(g => g.Members)
            .Where(g => g.Id == id)
            .ProjectTo<Group>(_mapper.ConfigurationProvider, g => g.Slot, g => g.Members)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> GroupExistsAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Groups.AsNoTracking()
            .Where(s => s.Id == id)
            .AnyAsync(cancellationToken);
    }

    public async Task<int> CreateGroupAsync(Group group, List<string> groupFindingCodes, CancellationToken cancellationToken)
    {
        // Retrieve affected users
        var userEntities = await _dbContext.Users.AsQueryable()
            .Where(u => groupFindingCodes.Contains(u.GroupFindingCode))
            .Include(u => u.Group)
            .ToListAsync(cancellationToken);
        if(userEntities.Count != groupFindingCodes.Count)
            throw new ArgumentException("At least one group finding code is invalid.");
        if(userEntities.Any(u => u.Group != null))
            throw new InvalidOperationException("At least one user is already assigned to a group.");

        // Create new group
        var groupEntity = _dbContext.Groups.Add(new GroupEntity
        {
            SlotId = group.SlotId,
            DisplayName = group.DisplayName,
            ScoreboardAnnotation = group.ScoreboardAnnotation,
            ScoreboardAnnotationHoverText = group.ScoreboardAnnotationHoverText,
            ShowInScoreboard = group.ShowInScoreboard,
            Members = new List<UserEntity>()
        }).Entity;

        // Update users
        foreach(var userEntity in userEntities)
        {
            userEntity.GroupId = groupEntity.Id;
            groupEntity.Members.Add(userEntity);
        }

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);

        return groupEntity.Id;
    }

    public async Task<Group> CreateGroupAsync(Group group, CancellationToken cancellationToken)
    {
        // Create new group
        var groupEntity = _dbContext.Groups.Add(new GroupEntity
        {
            SlotId = group.SlotId,
            DisplayName = group.DisplayName,
            ScoreboardAnnotation = group.ScoreboardAnnotation,
            ScoreboardAnnotationHoverText = group.ScoreboardAnnotationHoverText,
            ShowInScoreboard = group.ShowInScoreboard,
            Members = new List<UserEntity>()
        }).Entity;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<Group>(groupEntity);
    }

    public async Task UpdateGroupAsync(Group group, CancellationToken cancellationToken)
    {
        // Try to retrieve existing user entity
        var groupEntity = await _dbContext.Groups.FindAsync(new object[] { group.Id }, cancellationToken);
        if(groupEntity == null)
            throw new ArgumentException("The group does not exist.", nameof(group));

        // Update entry
        groupEntity.DisplayName = group.DisplayName;
        groupEntity.ScoreboardAnnotation = group.ScoreboardAnnotation;
        groupEntity.ScoreboardAnnotationHoverText = group.ScoreboardAnnotationHoverText;
        groupEntity.SlotId = group.SlotId;
        groupEntity.ShowInScoreboard = group.ShowInScoreboard;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGroupAsync(int id, CancellationToken cancellationToken)
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