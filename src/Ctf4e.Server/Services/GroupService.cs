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
using Ctf4e.Server.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services;

public interface IGroupService
{
    Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken);
    Task<List<AdminGroupListEntry>> GetGroupListAsync(CancellationToken cancellationToken);
    Task<int> GetGroupsCountAsync(CancellationToken cancellationToken);
    Task<List<Group>> GetGroupsInSlotAsync(int slotId, CancellationToken cancellationToken);
    Task<int> GetGroupMemberCount(int id, CancellationToken cancellationToken);
    Task<Group> FindGroupByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> GroupExistsAsync(int id, CancellationToken cancellationToken);
    Task<int> CreateGroupFromCodesAsync(Group group, List<string> groupFindingCodes, CancellationToken cancellationToken);
    Task<Group> CreateGroupAsync(Group group, CancellationToken cancellationToken);
    Task UpdateGroupAsync(Group group, CancellationToken cancellationToken);
    Task DeleteGroupAsync(int id, CancellationToken cancellationToken);
}

public class GroupService(CtfDbContext dbContext, IMapper mapper, GenericCrudService<CtfDbContext> genericCrudService) : IGroupService
{
    public Task<List<Group>> GetGroupsAsync(CancellationToken cancellationToken)
    {
        return dbContext.Groups.AsNoTracking()
            .OrderBy(g => g.SlotId)
            .ThenBy(g => g.DisplayName)
            .ProjectTo<Group>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
    
    public Task<List<AdminGroupListEntry>> GetGroupListAsync(CancellationToken cancellationToken)
    {
        return dbContext.Groups.AsNoTracking()
            .OrderBy(g => g.SlotId)
            .ThenBy(g => g.DisplayName)
            .ProjectTo<AdminGroupListEntry>(mapper.ConfigurationProvider)
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
    
    public async Task<int> GetGroupMemberCount(int id, CancellationToken cancellationToken)
    {
        var memberCount = await dbContext.Users.AsNoTracking()
            .CountAsync(u => u.GroupId == id, cancellationToken);

        return memberCount;
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

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<GroupEntity, AdminGroupListEntry>()
            .ForMember(g => g.SlotName, opt => opt.MapFrom(g => g.Slot.Name))
            .ForMember(g => g.MemberCount, opt => opt.MapFrom(g => g.Members.Count))
            .ForMember(g => g.LabExecutionCount, opt => opt.MapFrom(g => g.LabExecutions.Count));
    }
}