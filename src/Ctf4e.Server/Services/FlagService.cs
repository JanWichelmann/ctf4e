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

public interface IFlagService
{
    Task<List<Flag>> GetFlagsAsync(int labId, CancellationToken cancellationToken);
    Task<List<AdminFlagListEntry>> GetFlagListAsync(int labId, CancellationToken cancellationToken);
    Task<Flag> FindFlagByIdAsync(int id, CancellationToken cancellationToken);
    Task<Flag> CreateFlagAsync(Flag flag, CancellationToken cancellationToken);
    Task UpdateFlagAsync(Flag flag, CancellationToken cancellationToken);
    Task DeleteFlagAsync(int id, CancellationToken cancellationToken);
    Task<FlagSubmission> CreateFlagSubmissionAsync(FlagSubmission submission, CancellationToken cancellationToken);
    Task DeleteFlagSubmissionAsync(int userId, int flagId, CancellationToken cancellationToken);
    Task<bool> SubmitFlagAsync(int userId, int labId, string flagCode, CancellationToken cancellationToken);
}

public class FlagService(CtfDbContext dbContext, IMapper mapper, GenericCrudService<CtfDbContext> genericCrudService) : IFlagService
{
    public Task<List<Flag>> GetFlagsAsync(int labId, CancellationToken cancellationToken)
    {
        return dbContext.Flags.AsNoTracking()
            .Where(f => f.LabId == labId)
            .OrderBy(f => f.Id)
            .ProjectTo<Flag>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
    
    public Task<List<AdminFlagListEntry>> GetFlagListAsync(int labId, CancellationToken cancellationToken)
    {
        return dbContext.Flags.AsNoTracking()
            .Where(f => f.LabId == labId)
            .OrderBy(f => f.Id)
            .ProjectTo<AdminFlagListEntry>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task<Flag> FindFlagByIdAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<Flag, FlagEntity>(f => f.Id == id, cancellationToken);

    public Task<Flag> CreateFlagAsync(Flag flag, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<Flag, FlagEntity>(flag, cancellationToken);

    public Task UpdateFlagAsync(Flag flag, CancellationToken cancellationToken)
        => genericCrudService.UpdateAsync<Flag, FlagEntity>(flag, cancellationToken);

    public Task DeleteFlagAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.DeleteAsync<FlagEntity>([id], cancellationToken);

    public Task<FlagSubmission> CreateFlagSubmissionAsync(FlagSubmission submission, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<FlagSubmission, FlagSubmissionEntity>(submission, cancellationToken);

    public async Task DeleteFlagSubmissionAsync(int userId, int flagId, CancellationToken cancellationToken)
        => await genericCrudService.DeleteAsync<FlagSubmissionEntity>([flagId, userId], cancellationToken);

    public async Task<bool> SubmitFlagAsync(int userId, int labId, string flagCode, CancellationToken cancellationToken)
    {
        // Try to find matching flag
        var flag = await genericCrudService.FindAsync<FlagEntity>(f => f.LabId == labId && f.Code == flagCode, cancellationToken);
        if(flag == null)
            return false;

        try
        {
            // Create new submission
            dbContext.FlagSubmissions.Add(new FlagSubmissionEntity
            {
                FlagId = flag.Id,
                UserId = userId,
                SubmissionTime = DateTime.Now
            });

            // Apply changes
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch(DbUpdateException)
        {
            // Most likely the flag has been submitted already
            return false;
        }
    }

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<FlagEntity, AdminFlagListEntry>();
    }
}