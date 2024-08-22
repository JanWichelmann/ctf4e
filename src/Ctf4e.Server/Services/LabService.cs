using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.MappingProfiles;
using Ctf4e.Server.Models;
using Ctf4e.Server.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services;

public interface ILabService
{
    ValueTask<List<Lab>> GetLabsAsync(CancellationToken cancellationToken);
    Task<List<AdminLabListEntry>> GetLabListAsync(CancellationToken cancellationToken);
    Task<Lab> FindLabByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> LabExistsAsync(int id, CancellationToken cancellationToken);
    Task<Lab> CreateLabAsync(Lab lab, CancellationToken cancellationToken);
    Task UpdateLabAsync(Lab lab, CancellationToken cancellationToken);
    Task DeleteLabAsync(int id, CancellationToken cancellationToken);
}

public class LabService(CtfDbContext dbContext, IMapper mapper, GenericCrudService<CtfDbContext> genericCrudService) : ILabService
{
    public ValueTask<List<Lab>> GetLabsAsync(CancellationToken cancellationToken)
       => genericCrudService.GetAllAsync<Lab, LabEntity>()
           .ToListAsync(cancellationToken);

    public Task<List<AdminLabListEntry>> GetLabListAsync(CancellationToken cancellationToken)
    {
        return dbContext.Labs.AsNoTracking()
            .OrderBy(l => l.Id)
            .ProjectTo<AdminLabListEntry>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
    
    public Task<Lab> FindLabByIdAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<Lab, LabEntity>(l => l.Id == id, cancellationToken);

    public Task<bool> LabExistsAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Labs.AsNoTracking()
            .Where(l => l.Id == id)
            .AnyAsync(cancellationToken);
    }

    public Task<Lab> CreateLabAsync(Lab lab, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<Lab, LabEntity>(lab, cancellationToken);

    public Task UpdateLabAsync(Lab lab, CancellationToken cancellationToken)
        => genericCrudService.UpdateAsync<Lab, LabEntity>(lab, cancellationToken);

    public Task DeleteLabAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.DeleteAsync<LabEntity>([id], cancellationToken);

    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<LabEntity, AdminLabListEntry>()
            .ForMember(l => l.ExerciseCount, opt => opt.MapFrom(l => l.Exercises.Count))
            .ForMember(l => l.FlagCount, opt => opt.MapFrom(l => l.Flags.Count))
            .ForMember(l => l.ExecutionCount, opt => opt.MapFrom(l => l.Executions.Count));
    }
}