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

namespace Ctf4e.Server.Services;

public interface IExerciseService
{
    Task<List<Exercise>> GetExercisesAsync(int labId, CancellationToken cancellationToken);
    Task<Exercise> FindExerciseByIdAsync(int id, CancellationToken cancellationToken);
    Task<Exercise> FindExerciseByNumberAsync(int labId, int exerciseNumber, CancellationToken cancellationToken);
    Task<Exercise> CreateExerciseAsync(Exercise exercise, CancellationToken cancellationToken);
    Task UpdateExerciseAsync(Exercise exercise, CancellationToken cancellationToken);
    Task DeleteExerciseAsync(int id, CancellationToken cancellationToken);
    Task<ExerciseSubmission> CreateExerciseSubmissionAsync(ExerciseSubmission submission, CancellationToken cancellationToken);
    Task DeleteExerciseSubmissionAsync(int id, CancellationToken cancellationToken);
    Task DeleteExerciseSubmissionsAsync(List<int> ids, CancellationToken cancellationToken);
    Task ClearExerciseSubmissionsAsync(int exerciseId, int userId, CancellationToken cancellationToken);
}

public class ExerciseService(CtfDbContext dbContext, IMapper mapper, GenericCrudService<CtfDbContext> genericCrudService) : IExerciseService
{
    public Task<List<Exercise>> GetExercisesAsync(int labId, CancellationToken cancellationToken)
    {
        return dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.Id)
            .ProjectTo<Exercise>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }

    public Task<Exercise> FindExerciseByIdAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<Exercise, ExerciseEntity>(e => e.Id == id, cancellationToken);

    public Task<Exercise> FindExerciseByNumberAsync(int labId, int exerciseNumber, CancellationToken cancellationToken)
        => genericCrudService.FindAsync<Exercise, ExerciseEntity>(e => e.LabId == labId && e.ExerciseNumber == exerciseNumber, cancellationToken);

    public Task<Exercise> CreateExerciseAsync(Exercise exercise, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<Exercise, ExerciseEntity>(exercise, cancellationToken);

    public Task UpdateExerciseAsync(Exercise exercise, CancellationToken cancellationToken)
        => genericCrudService.UpdateAsync<Exercise, ExerciseEntity>(exercise, cancellationToken);

    public Task DeleteExerciseAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.DeleteAsync<ExerciseEntity>([id], cancellationToken);

    public Task<ExerciseSubmission> CreateExerciseSubmissionAsync(ExerciseSubmission submission, CancellationToken cancellationToken)
        => genericCrudService.CreateAsync<ExerciseSubmission, ExerciseSubmissionEntity>(submission, cancellationToken);

    public Task DeleteExerciseSubmissionAsync(int id, CancellationToken cancellationToken)
        => genericCrudService.DeleteAsync<ExerciseSubmissionEntity>([id], cancellationToken);

    public async Task DeleteExerciseSubmissionsAsync(List<int> ids, CancellationToken cancellationToken)
    {
        dbContext.ExerciseSubmissions.RemoveRange(ids.Select(id => new ExerciseSubmissionEntity { Id = id }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task ClearExerciseSubmissionsAsync(int exerciseId, int userId, CancellationToken cancellationToken)
    {
        // Delete all matching submissions
        dbContext.ExerciseSubmissions.RemoveRange(
            dbContext.ExerciseSubmissions
                .Where(es => es.ExerciseId == exerciseId && es.UserId == userId)
        );
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}