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
using Dapper;
using Microsoft.EntityFrameworkCore;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;

namespace Ctf4e.Server.Services;

public interface ILabExecutionService
{
    Task<List<LabExecution>> GetLabExecutionsAsync(CancellationToken cancellationToken);
    Task<List<AdminLabExecutionListEntry>> GetLabExecutionListAsync(CancellationToken cancellationToken);
    Task<bool> AnyLabExecutionsForLabAsync(int labId, CancellationToken cancellationToken);
    Task<LabExecution> FindLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken);
    Task<LabExecution> FindLabExecutionByUserAndLabAsync(int userId, int labId, CancellationToken cancellationToken);
    Task<LabExecution> FindMostRecentLabExecutionByGroupAsync(int groupId, CancellationToken cancellationToken);
    Task<LabExecution> FindMostRecentLabExecutionAsync(CancellationToken cancellationToken);
    Task<LabExecution> CreateLabExecutionAsync(LabExecution labExecution, bool updateExisting, CancellationToken cancellationToken);
    Task UpdateLabExecutionAsync(LabExecution labExecution, CancellationToken cancellationToken);
    Task DeleteLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken);
    Task DeleteLabExecutionsForSlotAsync(int slotId, int labId, CancellationToken cancellationToken);
}

public class LabExecutionService(CtfDbContext dbContext, IMapper mapper, GenericCrudService<CtfDbContext> genericCrudService) : ILabExecutionService
{
    public Task<List<LabExecution>> GetLabExecutionsAsync(CancellationToken cancellationToken)
    {
        return dbContext.LabExecutions.AsNoTracking()
            .OrderBy(l => l.LabId)
            .ThenBy(l => l.Group.DisplayName)
            .ProjectTo<LabExecution>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
    
    public Task<List<AdminLabExecutionListEntry>> GetLabExecutionListAsync(CancellationToken cancellationToken)
    {
        return dbContext.LabExecutions.AsNoTracking()
            .OrderBy(l => l.LabId)
            .ThenBy(l => l.Group.DisplayName)
            .ProjectTo<AdminLabExecutionListEntry>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyLabExecutionsForLabAsync(int labId, CancellationToken cancellationToken)
    {
        return dbContext.LabExecutions.AsNoTracking()
            .Where(l => l.LabId == labId)
            .AnyAsync(cancellationToken);
    }

    public Task<LabExecution> FindLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<LabExecution, LabExecutionEntity>(l => l.GroupId == groupId && l.LabId == labId, cancellationToken);

    public async Task<LabExecution> FindLabExecutionByUserAndLabAsync(int userId, int labId, CancellationToken cancellationToken)
    {
        // We have to first convert user ID to group ID, which does not seem to be supported by EF
        var dbConn = new ProfiledDbConnection(dbContext.Database.GetDbConnection(), MiniProfiler.Current);
        var labExecutionEntity = (await dbConn.QueryAsync<LabExecutionEntity>(@"
                 SELECT le.*
                 FROM `LabExecutions` le
                 INNER JOIN `Groups` g ON g.`Id` = le.`GroupId`
                 WHERE le.`LabId` = @labId
                 AND g.`Id` = (
                     SELECT u.`GroupId`
                     FROM `Users` u
                     WHERE u.`Id` = @userId
                 )", new { labId, userId })).FirstOrDefault();
        return mapper.Map<LabExecutionEntity, LabExecution>(labExecutionEntity);
    }

    public Task<LabExecution> FindMostRecentLabExecutionByGroupAsync(int groupId, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        return dbContext.LabExecutions.AsNoTracking()
            .Where(l => l.GroupId == groupId)
            .OrderByDescending(l => l.Start <= now && now < l.End) // Pick an active one...
            .ThenBy(l => Math.Abs(EF.Functions.DateDiffMicrosecond(l.Start, now))) // ...and/or the one with the most recent pre start time
            .ProjectTo<LabExecution>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<LabExecution> FindMostRecentLabExecutionAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        return dbContext.LabExecutions.AsNoTracking()
            .OrderByDescending(l => l.Start <= now && now < l.End) // Pick an active one...
            .ThenBy(l => Math.Abs(EF.Functions.DateDiffMicrosecond(l.Start, now))) // ...and/or the one with the most recent pre start time
            .ProjectTo<LabExecution>(mapper.ConfigurationProvider, l => l.Group)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LabExecution> CreateLabExecutionAsync(LabExecution labExecution, bool updateExisting, CancellationToken cancellationToken)
    {
        // Update existing one?
        LabExecutionEntity labExecutionEntity;
        if(updateExisting && (labExecutionEntity = await dbContext.LabExecutions.FindAsync([labExecution.GroupId, labExecution.LabId], cancellationToken)) != null)
            mapper.Map(labExecution, labExecutionEntity);
        else
            labExecutionEntity = dbContext.LabExecutions.Add(mapper.Map<LabExecutionEntity>(labExecution)).Entity;

        // Apply changes
        await dbContext.SaveChangesAsync(cancellationToken);
        return mapper.Map<LabExecution>(labExecutionEntity);
    }

    public Task UpdateLabExecutionAsync(LabExecution labExecution, CancellationToken cancellationToken)
        => genericCrudService.UpdateAsync<LabExecution, LabExecutionEntity>(labExecution, cancellationToken);

    public Task DeleteLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken)
        => genericCrudService.DeleteAsync<LabExecutionEntity>([groupId, labId], cancellationToken);

    public Task DeleteLabExecutionsForSlotAsync(int slotId, int labId, CancellationToken cancellationToken)
    {
        // Delete all matching entries
        var labExecutionsInSlot = dbContext.LabExecutions
            .Where(l => l.LabId == labId && l.Group.SlotId == slotId);
        dbContext.LabExecutions.RemoveRange(labExecutionsInSlot);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
    
    public static void RegisterMappings(Profile profile)
    {
        profile.CreateMap<LabExecutionEntity, AdminLabExecutionListEntry>();
    }
}