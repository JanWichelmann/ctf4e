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
    Task<List<User>> GetUsersAsync(CancellationToken cancellationToken);
    Task<List<User>> GetUsersWithGroupsAsync(CancellationToken cancellationToken);
    Task<List<User>> GetGroupMembersAsync(int groupId, CancellationToken cancellationToken);
    Task<bool> AnyUsers(CancellationToken cancellationToken);
    Task<User> FindUserByMoodleUserIdAsync(int moodleUserId, CancellationToken cancellationToken);
    Task<User> FindUserByIdAsync(int id, CancellationToken cancellationToken);
    Task<User> FindUserByMoodleNameAsync(string moodleName, CancellationToken cancellationToken);
    Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken);
    Task<User> CreateUserAsync(User user, CancellationToken cancellationToken);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken);
    Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken);
    Task<int> GetGroupsCountAsync(CancellationToken cancellationToken);
    Task<List<Group>> GetGroupsInSlotAsync(int slotId, CancellationToken cancellationToken);
    Task<Group> FindGroupByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> GroupExistsAsync(int id, CancellationToken cancellationToken);
    Task<int> CreateGroupFromCodesAsync(Group group, List<string> groupFindingCodes, CancellationToken cancellationToken);
    Task<Group> CreateGroupAsync(Group group, CancellationToken cancellationToken);
    Task UpdateGroupAsync(Group group, CancellationToken cancellationToken);
    Task DeleteGroupAsync(int id, CancellationToken cancellationToken);
}

public class UserService(CtfDbContext dbContext, IMapper mapper, GenericCrudService<CtfDbContext> genericCrudService) : IUserService
{
    private readonly ConcurrentDictionary<int, User> _cachedUsers = new();

    public Task<List<User>> GetUsersAsync(CancellationToken cancellationToken)
    {
        return dbContext.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ProjectTo<User>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task<List<User>> GetUsersWithGroupsAsync(CancellationToken cancellationToken)
    {
        return dbContext.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ProjectTo<User>(mapper.ConfigurationProvider, u => u.Group)
            .ToListAsync(cancellationToken);
    }

    public Task<List<User>> GetGroupMembersAsync(int groupId, CancellationToken cancellationToken)
    {
        return dbContext.Users.AsNoTracking()
            .Where(u => u.GroupId == groupId)
            .OrderBy(u => u.DisplayName)
            .ProjectTo<User>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyUsers(CancellationToken cancellationToken)
        => genericCrudService.AnyAsync<UserEntity>(cancellationToken);

    public async Task<User> FindUserByIdAsync(int id, CancellationToken cancellationToken)
    {
        if(_cachedUsers.TryGetValue(id, out var user))
            return user;

        // We often also need the group, so include it here
        user = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .ProjectTo<User>(mapper.ConfigurationProvider, u => u.Group)
            .FirstOrDefaultAsync(cancellationToken);

        _cachedUsers.TryAdd(id, user);
        return user;
    }

    public Task<User> FindUserByMoodleUserIdAsync(int moodleUserId, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<User, UserEntity>(u => u.MoodleUserId == moodleUserId, cancellationToken);

    public Task<User> FindUserByMoodleNameAsync(string moodleName, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<User, UserEntity>(u => u.MoodleName == moodleName, cancellationToken);

    public Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Users.AsNoTracking()
            .Where(s => s.Id == id)
            .AnyAsync(cancellationToken);
    }

    public Task<User> CreateUserAsync(User user, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<User, UserEntity>(user, cancellationToken);

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken)
    {
        await genericCrudService.UpdateAsync<User, UserEntity>(user, cancellationToken);

        // Invalidate cache
        _cachedUsers.TryRemove(user.Id, out _);
    }

    public Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken)
    {
        return dbContext.Groups.AsNoTracking()
            .OrderBy(g => g.SlotId)
            .ThenBy(g => g.DisplayName)
            .ProjectTo<Group>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
    
    public Task<int> GetGroupsCountAsync(CancellationToken cancellationToken)
        => dbContext.Groups.AsNoTracking().CountAsync(cancellationToken);

    public Task<List<Group>> GetGroupsInSlotAsync(int slotId, CancellationToken cancellationToken)
    {
        return dbContext.Groups.AsNoTracking()
            .Where(g => g.SlotId == slotId)
            .OrderBy(g => g.DisplayName)
            .ProjectTo<Group>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task<Group> FindGroupByIdAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<Group, GroupEntity>(g => g.Id == id, cancellationToken);

    public Task<bool> GroupExistsAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Groups.AsNoTracking()
            .Where(s => s.Id == id)
            .AnyAsync(cancellationToken);
    }

    public async Task<int> CreateGroupFromCodesAsync(Group group, List<string> groupFindingCodes, CancellationToken cancellationToken)
    {
        // Retrieve affected users
        var userEntities = await dbContext.Users
            .Where(u => groupFindingCodes.Contains(u.GroupFindingCode))
            .Include(u => u.Group)
            .ToListAsync(cancellationToken);
        if(userEntities.Count != groupFindingCodes.Count)
            throw new ArgumentException("At least one group finding code is invalid.");
        if(userEntities.Any(u => u.Group != null))
            throw new InvalidOperationException("At least one user is already assigned to a group.");

        // Create new group
        var groupEntity = dbContext.Groups.Add(mapper.Map<GroupEntity>(group)).Entity;

        // Update users
        foreach(var userEntity in userEntities)
        {
            userEntity.GroupId = groupEntity.Id;
            groupEntity.Members.Add(userEntity);
        }

        // Apply changes
        await dbContext.SaveChangesAsync(cancellationToken);

        return groupEntity.Id;
    }

    public Task<Group> CreateGroupAsync(Group group, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<Group, GroupEntity>(group, cancellationToken);

    public Task UpdateGroupAsync(Group group, CancellationToken cancellationToken)
        => genericCrudService.UpdateAsync<Group, GroupEntity>(group, cancellationToken);

    public Task DeleteGroupAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.DeleteAsync<GroupEntity>([id], cancellationToken);
}