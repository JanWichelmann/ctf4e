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
using Ctf4e.Server.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services;

/// <summary>
///     Functionality for managing users and groups.
/// </summary>
public interface IUserService
{
    Task<List<User>> GetUsersAsync(CancellationToken cancellationToken);
    Task<List<AdminUserListEntry>> GetUserListAsync(CancellationToken cancellationToken);
    Task<List<User>> GetGroupMembersAsync(int groupId, CancellationToken cancellationToken);
    Task<bool> AnyUsers(CancellationToken cancellationToken);
    Task<User> FindUserByLtiUserIdAsync(int moodleUserId, CancellationToken cancellationToken);
    Task<User> FindUserByIdAsync(int id, CancellationToken cancellationToken);
    Task<User> FindUserByMoodleNameAsync(string moodleName, CancellationToken cancellationToken);
    Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken);
    Task<User> CreateUserAsync(User user, CancellationToken cancellationToken);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken);
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

    public Task<List<AdminUserListEntry>> GetUserListAsync(CancellationToken cancellationToken)
    {
        return dbContext.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ProjectTo<AdminUserListEntry>(mapper.ConfigurationProvider)
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

    public Task<User> FindUserByLtiUserIdAsync(int moodleUserId, CancellationToken cancellationToken)
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

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<UserEntity, AdminUserListEntry>()
            .ForMember(dst => dst.GroupName, opt => opt.MapFrom(src => src.Group.DisplayName));
    }
}