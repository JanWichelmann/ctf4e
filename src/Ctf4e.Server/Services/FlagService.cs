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
    public interface IFlagService
    {
        IAsyncEnumerable<Flag> GetFlagsAsync(int labId);
        Task<Flag> GetFlagAsync(int id, CancellationToken cancellationToken = default);
        Task<Flag> CreateFlagAsync(Flag flag, CancellationToken cancellationToken = default);
        Task UpdateFlagAsync(Flag flag, CancellationToken cancellationToken = default);
        Task DeleteFlagAsync(int id, CancellationToken cancellationToken = default);
        Task<FlagSubmission> CreateFlagSubmissionAsync(FlagSubmission submission, CancellationToken cancellationToken = default);
        Task DeleteFlagSubmissionAsync(int userId, int flagId, CancellationToken cancellationToken = default);
        Task<bool> SubmitFlagAsync(int userId, int labId, string flagCode, CancellationToken cancellationToken = default);
    }

    public class FlagService : IFlagService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;

        public FlagService(CtfDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public IAsyncEnumerable<Flag> GetFlagsAsync(int labId)
        {
            return _dbContext.Flags.AsNoTracking()
                .Where(f => f.LabId == labId)
                .OrderBy(f => f.Id)
                .ProjectTo<Flag>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
        }

        public Task<Flag> GetFlagAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Flags.AsNoTracking()
                .Where(f => f.Id == id)
                .ProjectTo<Flag>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Flag> CreateFlagAsync(Flag flag, CancellationToken cancellationToken = default)
        {
            // Create new Flag
            var flagEntity = _dbContext.Flags.Add(new FlagEntity
            {
                Code = flag.Code,
                Description = flag.Description,
                BasePoints = flag.BasePoints,
                IsBounty = flag.IsBounty,
                LabId = flag.LabId,
                Submissions = new List<FlagSubmissionEntity>()
            }).Entity;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<Flag>(flagEntity);
        }

        public async Task UpdateFlagAsync(Flag flag, CancellationToken cancellationToken = default)
        {
            // Try to retrieve existing entity
            var flagEntity = await _dbContext.Flags.FindAsync(new object[] { flag.Id }, cancellationToken);
            if(flagEntity == null)
                throw new InvalidOperationException("Diese Flag existiert nicht");

            // Update entry
            flagEntity.Code = flag.Code;
            flagEntity.Description = flag.Description;
            flagEntity.BasePoints = flag.BasePoints;
            flagEntity.IsBounty = flag.IsBounty;
            flagEntity.LabId = flag.LabId;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteFlagAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete entry
                _dbContext.Flags.Remove(new FlagEntity { Id = id });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
            {
                // Most likely a non-existent entry, just forward the exception
                throw;
            }
        }

        public async Task<FlagSubmission> CreateFlagSubmissionAsync(FlagSubmission submission, CancellationToken cancellationToken = default)
        {
            // Create new submission
            var submissionEntity = _dbContext.FlagSubmissions.Add(new FlagSubmissionEntity
            {
                FlagId = submission.FlagId,
                UserId = submission.UserId,
                SubmissionTime = submission.SubmissionTime
            }).Entity;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<FlagSubmission>(submissionEntity);
        }

        public async Task DeleteFlagSubmissionAsync(int userId, int flagId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete entry
                _dbContext.FlagSubmissions.Remove(new FlagSubmissionEntity { UserId = userId, FlagId = flagId });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
            {
                // Most likely a non-existent entry, just forward the exception
                throw;
            }
        }

        public async Task<bool> SubmitFlagAsync(int userId, int labId, string flagCode, CancellationToken cancellationToken = default)
        {
            // Try to find matching flag
            var flag = await _dbContext.Flags.AsNoTracking()
                .FirstOrDefaultAsync(f => f.LabId == labId && f.Code == flagCode, cancellationToken);
            if(flag == null)
                return false;

            try
            {
                // Create new submission
                _dbContext.FlagSubmissions.Add(new FlagSubmissionEntity
                {
                    FlagId = flag.Id,
                    UserId = userId,
                    SubmissionTime = DateTime.Now
                });

                // Apply changes
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException || ex is DbUpdateException)
            {
                // Most likely the flag has been submitted already
                return false;
            }
        }
    }
}
