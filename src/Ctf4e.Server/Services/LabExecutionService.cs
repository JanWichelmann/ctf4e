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
using Dapper;
using Microsoft.EntityFrameworkCore;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;

namespace Ctf4e.Server.Services;

public interface ILabExecutionService
{
    IAsyncEnumerable<LabExecution> GetLabExecutionsAsync();
    Task<LabExecution> GetLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken = default);
    Task<LabExecution> GetLabExecutionForUserAsync(int userId, int labId, CancellationToken cancellationToken = default);
    Task<LabExecution> GetMostRecentLabExecutionAsync(int groupId, CancellationToken cancellationToken = default);
    Task<LabExecution> GetMostRecentLabExecutionAsync(CancellationToken cancellationToken = default);
    Task<LabExecution> CreateLabExecutionAsync(LabExecution labExecution, bool updateExisting, CancellationToken cancellationToken = default);
    Task UpdateLabExecutionAsync(LabExecution labExecution, CancellationToken cancellationToken = default);
    Task DeleteLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken = default);
    Task DeleteLabExecutionsForSlotAsync(int slotId, int labId, CancellationToken cancellationToken = default);
}

public class LabExecutionService : ILabExecutionService
{
    private readonly CtfDbContext _dbContext;
    private readonly IMapper _mapper;

    public LabExecutionService(CtfDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public IAsyncEnumerable<LabExecution> GetLabExecutionsAsync()
    {
        return _dbContext.LabExecutions.AsNoTracking()
            .Include(l => l.Lab)
            .Include(l => l.Group)
            .OrderBy(l => l.LabId)
            .ThenBy(l => l.Group.DisplayName)
            .ProjectTo<LabExecution>(_mapper.ConfigurationProvider, l => l.Lab, l => l.Group)
            .AsAsyncEnumerable();
    }

    public Task<LabExecution> GetLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken = default)
    {
        return _dbContext.LabExecutions.AsNoTracking()
            .Include(l => l.Lab)
            .Include(l => l.Group)
            .Where(l => l.GroupId == groupId && l.LabId == labId)
            .ProjectTo<LabExecution>(_mapper.ConfigurationProvider, l => l.Lab, l => l.Group)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LabExecution> GetLabExecutionForUserAsync(int userId, int labId, CancellationToken cancellationToken = default)
    {
        // We have to match against user IDs, which does not seem to be supported by EF
        var dbConn = new ProfiledDbConnection(_dbContext.Database.GetDbConnection(), MiniProfiler.Current);
        var labExecutionEntity = (await dbConn.QueryAsync<LabExecutionEntity>(@"
                 SELECT le.*
                 FROM `LabExecutions` le
                 INNER JOIN `Groups` g ON g.`Id` = le.`GroupId`
                 WHERE le.`LabId` = @labId
                 AND g.`Id` = (
                     SELECT u.`GroupId`
                     FROM `Users` u
                     WHERE u.`Id` = @userId
                 )", new {labId, userId})).FirstOrDefault();
        return _mapper.Map<LabExecutionEntity, LabExecution>(labExecutionEntity);
    }

    public Task<LabExecution> GetMostRecentLabExecutionAsync(int groupId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        return _dbContext.LabExecutions.AsNoTracking()
            .Where(l => l.GroupId == groupId)
            .OrderByDescending(l => l.PreStart <= now && now < l.End) // Pick an active one...
            .ThenBy(l => Math.Abs(EF.Functions.DateDiffMicrosecond(l.PreStart, now))) // ...and/or the one with the most recent pre start time
            .ProjectTo<LabExecution>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<LabExecution> GetMostRecentLabExecutionAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        return _dbContext.LabExecutions.AsNoTracking()
            .Include(l => l.Group)
            .OrderByDescending(l => l.PreStart <= now && now < l.End) // Pick an active one...
            .ThenBy(l => Math.Abs(EF.Functions.DateDiffMicrosecond(l.PreStart, now))) // ...and/or the one with the most recent pre start time
            .ProjectTo<LabExecution>(_mapper.ConfigurationProvider, l => l.Group)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LabExecution> CreateLabExecutionAsync(LabExecution labExecution, bool updateExisting, CancellationToken cancellationToken = default)
    {
        // Update existing one?
        LabExecutionEntity labExecutionEntity;
        if(updateExisting && (labExecutionEntity = await _dbContext.LabExecutions.FindAsync(new object[] { labExecution.GroupId, labExecution.LabId }, cancellationToken)) != null)
        {
            labExecutionEntity.PreStart = labExecution.PreStart;
            labExecutionEntity.Start = labExecution.Start;
            labExecutionEntity.End = labExecution.End;
        }
        else
        {
            // Create new labExecution
            labExecutionEntity = _dbContext.LabExecutions.Add(new LabExecutionEntity
            {
                GroupId = labExecution.GroupId,
                LabId = labExecution.LabId,
                PreStart = labExecution.PreStart,
                Start = labExecution.Start,
                End = labExecution.End
            }).Entity;
        }

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LabExecution>(labExecutionEntity);
    }

    public async Task UpdateLabExecutionAsync(LabExecution labExecution, CancellationToken cancellationToken = default)
    {
        // Try to retrieve existing entity
        var labExecutionEntity = await _dbContext.LabExecutions.FindAsync(new object[] { labExecution.GroupId, labExecution.LabId }, cancellationToken);
        if(labExecutionEntity == null)
            throw new InvalidOperationException("Diese Ausführung existiert nicht");

        // Update entry
        labExecutionEntity.PreStart = labExecution.PreStart;
        labExecutionEntity.Start = labExecution.Start;
        labExecutionEntity.End = labExecution.End;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteLabExecutionAsync(int groupId, int labId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Delete entry
            _dbContext.LabExecutions.Remove(new LabExecutionEntity { GroupId = groupId, LabId = labId });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
        {
            // Most likely a non-existent entry, just forward the exception
            throw;
        }
    }

    public Task DeleteLabExecutionsForSlotAsync(int slotId, int labId, CancellationToken cancellationToken = default)
    {
        // Delete all matching entries
        var labExecutionsInSlot = _dbContext.LabExecutions
            .Include(l => l.Group)
            .Where(l => l.LabId == labId && l.Group.SlotId == slotId);
        _dbContext.LabExecutions.RemoveRange(labExecutionsInSlot);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}