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
    public interface ILabService
    {
        IAsyncEnumerable<Lab> GetLabsAsync();
        IAsyncEnumerable<Lab> GetFullLabsAsync();
        Task<Lab> GetLabAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> LabExistsAsync(int id, CancellationToken cancellationToken = default);
        Task<Lab> CreateLabAsync(Lab lab, CancellationToken cancellationToken = default);
        Task UpdateLabAsync(Lab lab, CancellationToken cancellationToken = default);
        Task DeleteLabAsync(int id, CancellationToken cancellationToken = default);
    }

    public class LabService : ILabService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;

        public LabService(CtfDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public IAsyncEnumerable<Lab> GetLabsAsync()
        {
            return _dbContext.Labs.AsNoTracking()
                .OrderBy(l => l.Id)
                .ProjectTo<Lab>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Lab> GetFullLabsAsync()
        {
            return _dbContext.Labs.AsNoTracking()
                .Include(l => l.Exercises)
                .Include(l => l.Flags)
                .OrderBy(l => l.Id)
                .ProjectTo<Lab>(_mapper.ConfigurationProvider, l => l.Exercises, l => l.Flags, l => l.Executions)
                .AsAsyncEnumerable();
        }

        public Task<Lab> GetLabAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Labs.AsNoTracking()
                .Include(l => l.Exercises)
                .Include(l => l.Flags)
                .Where(l => l.Id == id)
                .ProjectTo<Lab>(_mapper.ConfigurationProvider, l => l.Exercises, l => l.Flags, l => l.Executions)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> LabExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Labs.AsNoTracking()
                .Where(l => l.Id == id)
                .AnyAsync(cancellationToken);
        }

        public async Task<Lab> CreateLabAsync(Lab lab, CancellationToken cancellationToken = default)
        {
            // Create new lab
            var labEntity = _dbContext.Labs.Add(new LabEntity
            {
                Name = lab.Name,
                ApiCode = lab.ApiCode,
                ServerBaseUrl = lab.ServerBaseUrl,
                MaxPoints = lab.MaxPoints,
                MaxFlagPoints = lab.MaxFlagPoints,
                Visible = lab.Visible,
                Exercises = new List<ExerciseEntity>(),
                Flags = new List<FlagEntity>(),
                Executions = new List<LabExecutionEntity>()
            }).Entity;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<Lab>(labEntity);
        }

        public async Task UpdateLabAsync(Lab lab, CancellationToken cancellationToken = default)
        {
            // Try to retrieve existing entity
            var labEntity = await _dbContext.Labs.FindAsync(new object[] { lab.Id }, cancellationToken);
            if(labEntity == null)
                throw new InvalidOperationException("Dieses Praktikum existiert nicht");

            // Update entry
            labEntity.Name = lab.Name;
            labEntity.ApiCode = lab.ApiCode;
            labEntity.ServerBaseUrl = lab.ServerBaseUrl;
            labEntity.MaxPoints = lab.MaxPoints;
            labEntity.MaxFlagPoints = lab.MaxFlagPoints;
            labEntity.Visible = lab.Visible;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteLabAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete entry
                _dbContext.Labs.Remove(new LabEntity { Id = id });
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