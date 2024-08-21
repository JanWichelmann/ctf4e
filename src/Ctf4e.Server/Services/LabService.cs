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

public interface ILabService
{
    ValueTask<List<Lab>> GetLabsAsync(CancellationToken cancellationToken);
    Task<List<Lab>> GetFullLabsAsync(CancellationToken cancellationToken);
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

    public Task<List<Lab>> GetFullLabsAsync(CancellationToken cancellationToken)
    {
        // TODO dedicated viewmodel
        return dbContext.Labs.AsNoTracking()
            .OrderBy(l => l.Id)
            .ProjectTo<Lab>(mapper.ConfigurationProvider, l => l.Exercises, l => l.Flags, l => l.Executions)
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
}