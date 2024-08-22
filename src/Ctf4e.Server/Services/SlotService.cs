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

public interface ISlotService
{
    Task<List<Slot>> GetSlotsAsync(CancellationToken cancellationToken);
    Task<List<AdminSlotListEntry>> GetSlotListAsync(CancellationToken cancellationToken);
    Task<Slot> FindSlotByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> SlotExistsAsync(int id, CancellationToken cancellationToken);
    Task<Slot> CreateSlotAsync(Slot slot, CancellationToken cancellationToken);
    Task UpdateSlotAsync(Slot slot, CancellationToken cancellationToken);
    Task DeleteSlotAsync(int id, CancellationToken cancellationToken);
}

public class SlotService(CtfDbContext dbContext, IMapper mapper, GenericCrudService<CtfDbContext> genericCrudService) : ISlotService
{
    public Task<List<Slot>> GetSlotsAsync(CancellationToken cancellationToken)
    {
        return dbContext.Slots.AsNoTracking()
            .OrderBy(s => s.Id)
            .ProjectTo<Slot>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
    
    public Task<List<AdminSlotListEntry>> GetSlotListAsync(CancellationToken cancellationToken)
    {
        return dbContext.Slots.AsNoTracking()
            .OrderBy(s => s.Id)
            .ProjectTo<AdminSlotListEntry>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task<Slot> FindSlotByIdAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<Slot, SlotEntity>(s => s.Id == id, cancellationToken);

    public Task<bool> SlotExistsAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Slots.AsNoTracking()
            .Where(s => s.Id == id)
            .AnyAsync(cancellationToken);
    }

    public Task<Slot> CreateSlotAsync(Slot slot, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<Slot, SlotEntity>(slot, cancellationToken);

    public Task UpdateSlotAsync(Slot slot, CancellationToken cancellationToken)
        => genericCrudService.UpdateAsync<Slot, SlotEntity>(slot, cancellationToken);

    public Task DeleteSlotAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.DeleteAsync<SlotEntity>([id], cancellationToken);
    
    public static void RegisterMappings(Profile mappingProfile)
    {
        mappingProfile.CreateMap<SlotEntity, AdminSlotListEntry>()
            .ForMember(se => se.GroupCount, opt => opt.MapFrom(s => s.Groups.Count));
    }
}