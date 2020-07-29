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
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services
{
    public interface ISlotService
    {
        IAsyncEnumerable<Slot> GetSlotsAsync();
        Task<Slot> GetSlotAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> SlotExistsAsync(int id, CancellationToken cancellationToken = default);
        Task<Slot> CreateSlotAsync(Slot slot, CancellationToken cancellationToken = default);
        Task UpdateSlotAsync(Slot slot, CancellationToken cancellationToken = default);
        Task DeleteSlotAsync(int id, CancellationToken cancellationToken = default);
    }

    public class SlotService : ISlotService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;

        public SlotService(CtfDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public IAsyncEnumerable<Slot> GetSlotsAsync()
        {
            return _dbContext.Slots.AsNoTracking()
                .Include(s => s.Groups)
                .OrderBy(s => s.Id)
                .ProjectTo<Slot>(_mapper.ConfigurationProvider, s => s.Groups)
                .AsAsyncEnumerable();
        }

        public Task<Slot> GetSlotAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Slots.AsNoTracking()
                .Include(s => s.Groups)
                .Where(s => s.Id == id)
                .ProjectTo<Slot>(_mapper.ConfigurationProvider, s => s.Groups)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> SlotExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Slots.AsNoTracking()
                .Where(s => s.Id == id)
                .AnyAsync(cancellationToken);
        }

        public async Task<Slot> CreateSlotAsync(Slot slot, CancellationToken cancellationToken = default)
        {
            // Create new Slot
            var slotEntity = _dbContext.Slots.Add(new SlotEntity
            {
                Name = slot.Name,
                Groups = new List<GroupEntity>()
            }).Entity;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<Slot>(slotEntity);
        }

        public async Task UpdateSlotAsync(Slot slot, CancellationToken cancellationToken = default)
        {
            // Try to retrieve existing entity
            var slotEntity = await _dbContext.Slots.FindAsync(new object[] { slot.Id }, cancellationToken);
            if(slotEntity == null)
                throw new InvalidOperationException("Dieser Slot existiert nicht");

            // Update entry
            slotEntity.Name = slot.Name;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteSlotAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete entry
                _dbContext.Slots.Remove(new SlotEntity { Id = id });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
            {
                // Most likely a non-existent entry, just forward the exception
                throw;
            }
        }
    }
}