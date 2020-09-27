using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.LabServer.Models.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ctf4e.LabServer.Options;
using Ctf4e.LabServer.ViewModels;
using Nito.AsyncEx;

namespace Ctf4e.LabServer.Services
{
    public interface IStateService
    {
        /// <summary>
        /// Checks whether the given user state object exists. If it does exist, the old user state is updated, else a new one is generated.
        /// </summary>
        /// <returns></returns>
        Task ProcessLoginAsync(int userId, int? groupId);

        /// <summary>
        /// Checks whether the given input is correct and updates the user status.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="input">Input.</param>
        /// <returns></returns>
        Task<bool> CheckInputAsync(int exerciseId, int userId, string input);

        /// <summary>
        /// Marks the given exercise as solved.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <returns></returns>
        Task MarkExerciseSolvedAsync(int exerciseId, int userId);

        /// <summary>
        /// Resets the given exercise.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <returns></returns>
        Task ResetExerciseStatusAsync(int exerciseId, int userId);

        /// <summary>
        /// Returns the user scoreboard.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns></returns>
        Task<UserScoreboard> GetUserScoreboardAsync(int userId);
    }

    /// <summary>
    /// Contains and manages the lab state.
    /// </summary>
    public class StateService : IDisposable, IStateService
    {
        private readonly IOptionsMonitor<LabOptions> _optionsMonitor;
        private readonly IDisposable _optionsMonitorListener;

        private ConcurrentDictionary<int, UserState> _userStates;

        private SortedDictionary<int, LabOptionsExerciseEntry> _exercises;

        /// <summary>
        /// Top level lock to prevent concurrent reading and writing of state objects.
        /// </summary>
        private readonly AsyncReaderWriterLock _configLock = new AsyncReaderWriterLock();

        /// <summary>
        /// Helper lock to prevent a possible race condition when a user tries to login with two requests in parallel.
        /// </summary>
        private readonly SemaphoreSlim _loginLock = new SemaphoreSlim(1, 1);

        public StateService(IOptionsMonitor<LabOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _optionsMonitorListener = _optionsMonitor.OnChange(Reload);

            // Load state
            Reload(_optionsMonitor.CurrentValue, null);
        }

        /// <summary>
        /// Reloads the configuration and updates caches.
        /// </summary>
        /// <param name="options">Current options value.</param>
        /// <param name="optionsName">Ignored.</param>
        private void Reload(LabOptions options, string optionsName)
        {
            var configWriteLock = _configLock.WriterLock();
            try
            {
                // Read exercises
                _exercises = new SortedDictionary<int, LabOptionsExerciseEntry>(options.Exercises.ToDictionary(e => e.Id));

                // Check for solutions empty

                // Read user data
                _userStates = new ConcurrentDictionary<int, UserState>();
                Regex userIdRegex = new Regex("u([0-9]+)\\.json$", RegexOptions.Compiled);
                foreach(string userStateFileName in Directory.GetFiles(options.GroupStateDirectory, "u*.json"))
                {
                    // Read file
                    int userId = int.Parse(userIdRegex.Match(userStateFileName).Groups[1].Value);
                    var userStateFile = ReadUserStateFile(userStateFileName);
                    if(userStateFile == null)
                    {
                        throw new Exception($"Could not read state file for user #{userId}");
                    }

                    // Store user state
                    var userState = new UserState
                    {
                        Lock = new SemaphoreSlim(1, 1),
                        GroupId = userStateFile.GroupId,
                        GroupMembers = new List<UserState>(),
                        Exercises = new ConcurrentDictionary<int, UserStateFileExerciseEntry>(userStateFile.Exercises.ToDictionary(e => e.ExerciseId))
                    };
                    _userStates.TryAdd(userId, userState);

                    // Fix exercise list, if necessary
                    // - Add entries for new exercises
                    // - Update expected solutions
                    // Old exercise entries are not deleted, in order to prevent accidental data loss.
                    bool userStateChanged = false;
                    foreach(var exercise in _exercises)
                    {
                        if(!userState.Exercises.TryGetValue(exercise.Key, out var exerciseState))
                        {
                            // Create empty state for this exercise
                            exerciseState = new UserStateFileExerciseEntry
                            {
                                ExerciseId = exercise.Value.Id,
                                Solved = false,
                            };

                            // Require a specific, randomly picked solution?
                            if(!exercise.Value.AllowAnySolution)
                            {
                                var random = exercise.Value.ValidSolutions[RandomNumberGenerator.GetInt32(exercise.Value.ValidSolutions.Length)];
                                exerciseState.SolutionName = random.Name;
                            }

                            userState.Exercises.TryAdd(exercise.Value.Id, exerciseState);
                            userStateChanged = true;
                            continue;
                        }

                        // Is the expected solution still valid?
                        if(exercise.Value.AllowAnySolution)
                        {
                            // We allow any solution, so no need to store a name
                            if(exerciseState.SolutionName != null)
                            {
                                // Drop previous solution name
                                exerciseState.SolutionName = null;
                                userStateChanged = true;
                            }
                        }
                        else
                        {
                            // Check whether the exercise requires a specific, existing solution
                            if(exerciseState.SolutionName == null || exercise.Value.ValidSolutions.All(s => s.Name != exerciseState.SolutionName))
                            {
                                // Pick a random solution
                                var random = exercise.Value.ValidSolutions[RandomNumberGenerator.GetInt32(exercise.Value.ValidSolutions.Length)];
                                exerciseState.SolutionName = random.Name;
                                userStateChanged = true;
                            }
                        }
                    }

                    // Update user state file, if necessary
                    if(userStateChanged)
                        WriteUserStateFile(userId);
                }

                // Link groups
                foreach(var g in _userStates.Where(u => u.Value.GroupId != null).GroupBy(u => u.Value.GroupId))
                {
                    if(g.Count() <= 1)
                        continue;

                    foreach(var u in g)
                    foreach(var u2 in g)
                    {
                        if(u.Key != u2.Key)
                            u.Value.GroupMembers.Add(u2.Value);
                    }
                }
            }
            finally
            {
                configWriteLock.Dispose();
            }
        }

        /// <summary>
        /// Parses the state file for the given user.
        /// </summary>
        /// <param name="userStateFileName">File path.</param>
        /// <returns></returns>
        private UserStateFile ReadUserStateFile(string userStateFileName)
        {
            // Read user state file
            string path = Path.Combine(_optionsMonitor.CurrentValue.GroupStateDirectory, userStateFileName);
            if(!File.Exists(path))
                return null;
            return JsonConvert.DeserializeObject<UserStateFile>(File.ReadAllText(path));
        }

        /// <summary>
        /// Updates the state file for the given user.
        /// </summary>
        /// <param name="userId">User ID.</param>
        private void WriteUserStateFile(int userId)
        {
            // Create new object
            var userState = _userStates[userId];
            var userStateFile = new UserStateFile
            {
                Exercises = userState.Exercises.Values.ToArray()
            };
            File.WriteAllText(Path.Combine(_optionsMonitor.CurrentValue.GroupStateDirectory, $"u{userId}.json"), JsonConvert.SerializeObject(userStateFile));
        }

        /// <summary>
        /// Checks whether the given user state object exists. If it does exist, the old user state is updated, else a new one is generated.
        /// </summary>
        /// <returns></returns>
        public async Task ProcessLoginAsync(int userId, int? groupId)
        {
            // Ensure synchronized access
            var configReadLock = await _configLock.WriterLockAsync();
            await _loginLock.WaitAsync();
            try
            {
                // Does the user exist?
                if(_userStates.TryGetValue(userId, out var userState))
                {
                    // Update user state if necessary
                    await userState.Lock.WaitAsync();
                    try
                    {
                        if(userState.GroupId == groupId)
                            return;

                        userState.GroupId = groupId;
                        userState.GroupMembers = new List<UserState>(); // Will be filled later
                    }
                    finally
                    {
                        userState.Lock.Release();
                    }
                }
                else
                {
                    // Create new account
                    userState = new UserState
                    {
                        Lock = new SemaphoreSlim(1, 1),
                        GroupId = groupId,
                        GroupMembers = new List<UserState>(), // Will be filled later
                        Exercises = new ConcurrentDictionary<int, UserStateFileExerciseEntry>()
                    };

                    // Initialize exercises
                    foreach(var exercise in _exercises)
                    {
                        // Create empty state for this exercise
                        var exerciseState = new UserStateFileExerciseEntry
                        {
                            ExerciseId = exercise.Value.Id,
                            Solved = false
                        };

                        // Require a specific, randomly picked solution?
                        if(!exercise.Value.AllowAnySolution)
                        {
                            var random = exercise.Value.ValidSolutions[RandomNumberGenerator.GetInt32(exercise.Value.ValidSolutions.Length)];
                            exerciseState.SolutionName = random.Name;
                        }

                        userState.Exercises.TryAdd(exercise.Value.Id, exerciseState);
                    }

                    // Store new user state
                    _userStates.TryAdd(userId, userState);
                }

                // Find group members
                if(groupId != null)
                    userState.GroupMembers.AddRange(_userStates.Where(u => u.Value.GroupId == groupId).Select(u => u.Value));

                // Save new user state
                WriteUserStateFile(userId);
            }
            finally
            {
                _loginLock.Release();
                configReadLock.Dispose();
            }
        }

        /// <summary>
        /// Checks whether the given input is correct and updates the user status.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="input">Input.</param>
        /// <returns></returns>
        public async Task<bool> CheckInputAsync(int exerciseId, int userId, string input)
        {
            // Ensure synchronized access
            var configReadLock = await _configLock.WriterLockAsync();
            try
            {
                // Check input values
                if(!_userStates.ContainsKey(userId) || !_exercises.ContainsKey(exerciseId))
                    throw new ArgumentException();

                // Get state object
                var userState = _userStates[userId];
                await userState.Lock.WaitAsync();
                try
                {
                    // Get exercise data
                    var exercise = _exercises[exerciseId];
                    var userExercise = userState.Exercises[exerciseId];

                    // Update user
                    bool correct;
                    if(exercise.AllowAnySolution)
                        correct = exercise.ValidSolutions.Any(s => s.Value == input);
                    else
                        correct = exercise.ValidSolutions.FirstOrDefault(s => s.Name == userExercise.SolutionName)?.Value == input;
                    if(!userExercise.Solved && correct)
                    {
                        userExercise.Solved = true;
                        WriteUserStateFile(userId);
                    }

                    return correct;
                }
                finally
                {
                    userState.Lock.Release();
                }
            }
            finally
            {
                configReadLock.Dispose();
            }
        }

        /// <summary>
        /// Marks the given exercise as solved.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <returns></returns>
        public async Task MarkExerciseSolvedAsync(int exerciseId, int userId)
        {
            // Ensure synchronized access
            var configReadLock = await _configLock.WriterLockAsync();
            try
            {
                // Check input values
                if(!_userStates.ContainsKey(userId) || !_exercises.ContainsKey(exerciseId))
                    throw new ArgumentException();

                // Get state object
                var userState = _userStates[userId];
                await userState.Lock.WaitAsync();
                try
                {
                    // Get exercise data
                    var userExercise = userState.Exercises[exerciseId];

                    // Update user
                    if(!userExercise.Solved)
                    {
                        userExercise.Solved = true;
                        WriteUserStateFile(userId);
                    }
                }
                finally
                {
                    userState.Lock.Release();
                }
            }
            finally
            {
                configReadLock.Dispose();
            }
        }

        /// <summary>
        /// Resets the given exercise.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <returns></returns>
        public async Task ResetExerciseStatusAsync(int exerciseId, int userId)
        {
            // Ensure synchronized access
            var configReadLock = await _configLock.WriterLockAsync();
            try
            {
                // Check input values
                if(!_userStates.ContainsKey(userId) || !_exercises.ContainsKey(exerciseId))
                    throw new ArgumentException();

                // Get state object
                var userState = _userStates[userId];
                await userState.Lock.WaitAsync();
                try
                {
                    // Get exercise data
                    var userExercise = userState.Exercises[exerciseId];

                    // Update user
                    if(userExercise.Solved)
                    {
                        userExercise.Solved = false;
                        WriteUserStateFile(userId);
                    }
                }
                finally
                {
                    userState.Lock.Release();
                }
            }
            finally
            {
                configReadLock.Dispose();
            }
        }

        /// <summary>
        /// Returns the user scoreboard.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns></returns>
        public async Task<UserScoreboard> GetUserScoreboardAsync(int userId)
        {
            // Ensure synchronized access
            var configReadLock = await _configLock.WriterLockAsync();
            try
            {
                // Check input values
                if(!_userStates.ContainsKey(userId))
                    throw new ArgumentException();

                // Get state object
                var userState = _userStates[userId];
                await userState.Lock.WaitAsync();
                try
                {
                    // Output object
                    var scoreboard = new UserScoreboard
                    {
                        Exercises = _exercises.Select(e => new UserScoreboardExerciseEntry
                        {
                            Exercise = e.Value,
                            Solved = userState.Exercises[e.Key].Solved,
                            SolvedByGroupMember = userState.GroupMembers.Any(g => g.Exercises[e.Key].Solved),
                            Description = string.Format(e.Value.DescriptionFormat, userState.Exercises[e.Key].SolutionName)
                        }).ToArray()
                    };

                    // Done
                    return scoreboard;
                }
                finally
                {
                    userState.Lock.Release();
                }
            }
            finally
            {
                configReadLock.Dispose();
            }
        }

        public void Dispose()
        {
            foreach(var u in _userStates)
                u.Value.Lock?.Dispose();
            _loginLock?.Dispose();
            _optionsMonitorListener?.Dispose();
        }
    }
}