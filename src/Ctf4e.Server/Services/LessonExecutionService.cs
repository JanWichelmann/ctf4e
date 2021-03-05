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

namespace Ctf4e.Server.Services
{
    public interface ILessonExecutionService
    {
        IAsyncEnumerable<LessonExecution> GetLessonExecutionsAsync();
        Task<LessonExecution> GetLessonExecutionAsync(int groupId, int lessonId, CancellationToken cancellationToken = default);
        Task<LessonExecution> GetLessonExecutionForUserAsync(int userId, int lessonId, CancellationToken cancellationToken = default);
        Task<LessonExecution> GetMostRecentLessonExecutionAsync(int groupId, CancellationToken cancellationToken = default);
        Task<LessonExecution> GetMostRecentLessonExecutionAsync(CancellationToken cancellationToken = default);
        Task<LessonExecution> CreateLessonExecutionAsync(LessonExecution lessonExecution, bool updateExisting, CancellationToken cancellationToken = default);
        Task UpdateLessonExecutionAsync(LessonExecution lessonExecution, CancellationToken cancellationToken = default);
        Task DeleteLessonExecutionAsync(int groupId, int lessonId, CancellationToken cancellationToken = default);
        Task DeleteLessonExecutionsForSlotAsync(int slotId, int lessonId, CancellationToken cancellationToken = default);
    }

    public class LessonExecutionService : ILessonExecutionService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;

        public LessonExecutionService(CtfDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public IAsyncEnumerable<LessonExecution> GetLessonExecutionsAsync()
        {
            return _dbContext.LessonExecutions.AsNoTracking()
                .Include(l => l.Lesson)
                .Include(l => l.Group)
                .OrderBy(l => l.LessonId)
                .ThenBy(l => l.Group.DisplayName)
                .ProjectTo<LessonExecution>(_mapper.ConfigurationProvider, l => l.Lesson, l => l.Group)
                .AsAsyncEnumerable();
        }

        public Task<LessonExecution> GetLessonExecutionAsync(int groupId, int lessonId, CancellationToken cancellationToken = default)
        {
            return _dbContext.LessonExecutions.AsNoTracking()
                .Include(l => l.Lesson)
                .Include(l => l.Group)
                .Where(l => l.GroupId == groupId && l.LessonId == lessonId)
                .ProjectTo<LessonExecution>(_mapper.ConfigurationProvider, l => l.Lesson, l => l.Group)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<LessonExecution> GetLessonExecutionForUserAsync(int userId, int lessonId, CancellationToken cancellationToken = default)
        {
            // We have to match against user IDs, which does not seem to be supported by EF
            var dbConn = new ProfiledDbConnection(_dbContext.Database.GetDbConnection(), MiniProfiler.Current);
            var lessonExecutionEntity = (await dbConn.QueryAsync<LessonExecutionEntity>(@"
                 SELECT le.*
                 FROM `LessonExecutions` le
                 INNER JOIN `Groups` g ON g.`Id` = le.`GroupId`
                 WHERE le.`LessonId` = @lessonId
                 AND g.`Id` = (
                     SELECT u.`GroupId`
                     FROM `Users` u
                     WHERE u.`Id` = @userId
                 )", new {lessonId, userId})).FirstOrDefault();
            return _mapper.Map<LessonExecutionEntity, LessonExecution>(lessonExecutionEntity);
        }

        public Task<LessonExecution> GetMostRecentLessonExecutionAsync(int groupId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.Now;
            return _dbContext.LessonExecutions.AsNoTracking()
                .Where(l => l.GroupId == groupId)
                .OrderByDescending(l => l.PreStart <= now && now < l.End) // Pick an active one...
                .ThenBy(l => Math.Abs(EF.Functions.DateDiffMicrosecond(l.PreStart, now))) // ...and/or the one with the most recent pre start time
                .ProjectTo<LessonExecution>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<LessonExecution> GetMostRecentLessonExecutionAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.Now;
            return _dbContext.LessonExecutions.AsNoTracking()
                .Include(l => l.Group)
                .OrderByDescending(l => l.PreStart <= now && now < l.End) // Pick an active one...
                .ThenBy(l => Math.Abs(EF.Functions.DateDiffMicrosecond(l.PreStart, now))) // ...and/or the one with the most recent pre start time
                .ProjectTo<LessonExecution>(_mapper.ConfigurationProvider, l => l.Group)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<LessonExecution> CreateLessonExecutionAsync(LessonExecution lessonExecution, bool updateExisting, CancellationToken cancellationToken = default)
        {
            // Update existing one?
            LessonExecutionEntity lessonExecutionEntity;
            if(updateExisting && (lessonExecutionEntity = await _dbContext.LessonExecutions.FindAsync(new object[] { lessonExecution.GroupId, lessonExecution.LessonId }, cancellationToken)) != null)
            {
                lessonExecutionEntity.PreStart = lessonExecution.PreStart;
                lessonExecutionEntity.Start = lessonExecution.Start;
                lessonExecutionEntity.End = lessonExecution.End;
            }
            else
            {
                // Create new lessonExecution
                lessonExecutionEntity = _dbContext.LessonExecutions.Add(new LessonExecutionEntity
                {
                    GroupId = lessonExecution.GroupId,
                    LessonId = lessonExecution.LessonId,
                    PreStart = lessonExecution.PreStart,
                    Start = lessonExecution.Start,
                    End = lessonExecution.End
                }).Entity;
            }

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<LessonExecution>(lessonExecutionEntity);
        }

        public async Task UpdateLessonExecutionAsync(LessonExecution lessonExecution, CancellationToken cancellationToken = default)
        {
            // Try to retrieve existing entity
            var lessonExecutionEntity = await _dbContext.LessonExecutions.FindAsync(new object[] { lessonExecution.GroupId, lessonExecution.LessonId }, cancellationToken);
            if(lessonExecutionEntity == null)
                throw new InvalidOperationException("Diese Ausführung existiert nicht");

            // Update entry
            lessonExecutionEntity.PreStart = lessonExecution.PreStart;
            lessonExecutionEntity.Start = lessonExecution.Start;
            lessonExecutionEntity.End = lessonExecution.End;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteLessonExecutionAsync(int groupId, int lessonId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete entry
                _dbContext.LessonExecutions.Remove(new LessonExecutionEntity { GroupId = groupId, LessonId = lessonId });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
            {
                // Most likely a non-existent entry, just forward the exception
                throw;
            }
        }

        public Task DeleteLessonExecutionsForSlotAsync(int slotId, int lessonId, CancellationToken cancellationToken = default)
        {
            // Delete all matching entries
            var lessonExecutionsInSlot = _dbContext.LessonExecutions
                .Include(l => l.Group)
                .Where(l => l.LessonId == lessonId && l.Group.SlotId == slotId);
            _dbContext.LessonExecutions.RemoveRange(lessonExecutionsInSlot);
            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}