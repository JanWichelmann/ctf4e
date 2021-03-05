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
    public interface ILessonService
    {
        IAsyncEnumerable<Lesson> GetLessonsAsync();
        IAsyncEnumerable<Lesson> GetFullLessonsAsync();
        Task<Lesson> GetLessonAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> LessonExistsAsync(int id, CancellationToken cancellationToken = default);
        Task<Lesson> CreateLessonAsync(Lesson lesson, CancellationToken cancellationToken = default);
        Task UpdateLessonAsync(Lesson lesson, CancellationToken cancellationToken = default);
        Task DeleteLessonAsync(int id, CancellationToken cancellationToken = default);
    }

    public class LessonService : ILessonService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;

        public LessonService(CtfDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public IAsyncEnumerable<Lesson> GetLessonsAsync()
        {
            return _dbContext.Lessons.AsNoTracking()
                .OrderBy(l => l.Id)
                .ProjectTo<Lesson>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Lesson> GetFullLessonsAsync()
        {
            return _dbContext.Lessons.AsNoTracking()
                .Include(l => l.Exercises)
                .Include(l => l.Flags)
                .OrderBy(l => l.Id)
                .ProjectTo<Lesson>(_mapper.ConfigurationProvider, l => l.Exercises, l => l.Flags, l => l.Executions)
                .AsAsyncEnumerable();
        }

        public Task<Lesson> GetLessonAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Lessons.AsNoTracking()
                .Include(l => l.Exercises)
                .Include(l => l.Flags)
                .Where(l => l.Id == id)
                .ProjectTo<Lesson>(_mapper.ConfigurationProvider, l => l.Exercises, l => l.Flags, l => l.Executions)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> LessonExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Lessons.AsNoTracking()
                .Where(l => l.Id == id)
                .AnyAsync(cancellationToken);
        }

        public async Task<Lesson> CreateLessonAsync(Lesson lesson, CancellationToken cancellationToken = default)
        {
            // Create new lesson
            var lessonEntity = _dbContext.Lessons.Add(new LessonEntity
            {
                Name = lesson.Name,
                ApiCode = lesson.ApiCode,
                ServerBaseUrl = lesson.ServerBaseUrl,
                MaxFlagPoints = lesson.MaxFlagPoints,
                Exercises = new List<ExerciseEntity>(),
                Flags = new List<FlagEntity>(),
                Executions = new List<LessonExecutionEntity>()
            }).Entity;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<Lesson>(lessonEntity);
        }

        public async Task UpdateLessonAsync(Lesson lesson, CancellationToken cancellationToken = default)
        {
            // Try to retrieve existing entity
            var lessonEntity = await _dbContext.Lessons.FindAsync(new object[] { lesson.Id }, cancellationToken);
            if(lessonEntity == null)
                throw new InvalidOperationException("Dieses Praktikum existiert nicht");

            // Update entry
            lessonEntity.Name = lesson.Name;
            lessonEntity.ApiCode = lesson.ApiCode;
            lessonEntity.ServerBaseUrl = lesson.ServerBaseUrl;
            lessonEntity.MaxFlagPoints = lesson.MaxFlagPoints;

            // Apply changes
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteLessonAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete entry
                _dbContext.Lessons.Remove(new LessonEntity { Id = id });
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