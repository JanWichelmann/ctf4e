﻿using System;
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
    IAsyncEnumerable<Exercise> GetExercisesAsync(int labId);
    Task<Exercise> GetExerciseAsync(int id, CancellationToken cancellationToken);
    Task<Exercise> FindExerciseAsync(int labId, int exerciseNumber, CancellationToken cancellationToken);
    Task<Exercise> CreateExerciseAsync(Exercise exercise, CancellationToken cancellationToken);
    Task UpdateExerciseAsync(Exercise exercise, CancellationToken cancellationToken);
    Task DeleteExerciseAsync(int id, CancellationToken cancellationToken);
    Task<ExerciseSubmission> CreateExerciseSubmissionAsync(ExerciseSubmission submission, CancellationToken cancellationToken);
    Task DeleteExerciseSubmissionAsync(int id, CancellationToken cancellationToken);
    Task DeleteExerciseSubmissionsAsync(List<int> ids, CancellationToken cancellationToken);
    Task ClearExerciseSubmissionsAsync(int exerciseId, int userId, CancellationToken cancellationToken);
}

public class ExerciseService : IExerciseService
{
    private readonly CtfDbContext _dbContext;
    private readonly IMapper _mapper;

    public ExerciseService(CtfDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public IAsyncEnumerable<Exercise> GetExercisesAsync(int labId)
    {
        return _dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.Id)
            .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
            .AsAsyncEnumerable();
    }

    public Task<Exercise> GetExerciseAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Exercises.AsNoTracking()
            .Where(e => e.Id == id)
            .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Exercise> FindExerciseAsync(int labId, int exerciseNumber, CancellationToken cancellationToken)
    {
        return _dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId && e.ExerciseNumber == exerciseNumber)
            .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Exercise> CreateExerciseAsync(Exercise exercise, CancellationToken cancellationToken)
    {
        // Create new exercise
        var exerciseEntity = _dbContext.Exercises.Add(new ExerciseEntity
        {
            LabId = exercise.LabId,
            ExerciseNumber = exercise.ExerciseNumber,
            Name = exercise.Name,
            IsMandatory = exercise.IsMandatory,
            BasePoints = exercise.BasePoints,
            PenaltyPoints = exercise.PenaltyPoints,
            Submissions = new List<ExerciseSubmissionEntity>()
        }).Entity;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<Exercise>(exerciseEntity);
    }

    public async Task UpdateExerciseAsync(Exercise exercise, CancellationToken cancellationToken)
    {
        // Try to retrieve existing entity
        var exerciseEntity = await _dbContext.Exercises.FindAsync(new object[] { exercise.Id }, cancellationToken);
        if(exerciseEntity == null)
            throw new InvalidOperationException("Diese Aufgabe existiert nicht");

        // Update entry
        exerciseEntity.LabId = exercise.LabId;
        exerciseEntity.ExerciseNumber = exercise.ExerciseNumber;
        exerciseEntity.Name = exercise.Name;
        exerciseEntity.IsMandatory = exercise.IsMandatory;
        exerciseEntity.BasePoints = exercise.BasePoints;
        exerciseEntity.PenaltyPoints = exercise.PenaltyPoints;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExerciseAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            // Delete entry
            _dbContext.Exercises.Remove(new ExerciseEntity { Id = id });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
        {
            // Most likely a non-existent entry, just forward the exception
            throw;
        }
    }

    public async Task<ExerciseSubmission> CreateExerciseSubmissionAsync(ExerciseSubmission submission, CancellationToken cancellationToken)
    {
        // Create new submission
        var submissionEntity = _dbContext.ExerciseSubmissions.Add(new ExerciseSubmissionEntity
        {
            ExerciseId = submission.ExerciseId,
            ExercisePassed = submission.ExercisePassed,
            UserId = submission.UserId,
            SubmissionTime = submission.SubmissionTime,
            Weight = submission.Weight
        }).Entity;

        // Apply changes
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<ExerciseSubmission>(submissionEntity);
    }

    public async Task DeleteExerciseSubmissionAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            // Delete entry
            _dbContext.ExerciseSubmissions.Remove(new ExerciseSubmissionEntity { Id = id });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
        {
            // Most likely a non-existent entry, just forward the exception
            throw;
        }
    }

    public async Task DeleteExerciseSubmissionsAsync(List<int> ids, CancellationToken cancellationToken)
    {
        try
        {
            // Delete entries
            _dbContext.ExerciseSubmissions.RemoveRange(ids.Select(id => new ExerciseSubmissionEntity { Id = id }));

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch(Exception ex) when(ex is DbUpdateConcurrencyException || ex is InvalidOperationException)
        {
            // Most likely a non-existent entry, just forward the exception
            throw;
        }
    }

    public Task ClearExerciseSubmissionsAsync(int exerciseId, int userId, CancellationToken cancellationToken)
    {
        // Delete all matching submissions
        _dbContext.ExerciseSubmissions.RemoveRange(_dbContext.ExerciseSubmissions.AsQueryable().Where(es => es.ExerciseId == exerciseId && es.UserId == userId));
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}