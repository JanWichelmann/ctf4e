using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.LabServer.Configuration.Exercises;
using Ctf4e.LabServer.Models.State;
using Ctf4e.LabServer.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ctf4e.LabServer.ViewModels;
using Ctf4e.Utilities;
using Nito.AsyncEx;

namespace Ctf4e.LabServer.Services
{
    public interface IStateService
    {
        /// <summary>
        /// Reloads the configuration and updates caches.
        /// </summary>
        void Reload();

        /// <summary>
        /// Checks whether the given user state object exists. If it does exist, the old user state is updated, else a new one is generated.
        /// </summary>
        /// <returns></returns>
        Task ProcessLoginAsync(int userId, int? groupId, CancellationToken cancellationToken);

        /// <summary>
        /// Checks whether the given input is correct and updates the user status.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="input">Input.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> CheckInputAsync(int exerciseId, int userId, object input, CancellationToken cancellationToken);

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
    public class StateService : IStateService, IDisposable
    {
        private readonly IOptions<LabOptions> _options;
        private readonly ILabConfigurationService _labConfiguration;
        private readonly IDockerService _dockerService;

        private ConcurrentDictionary<int, UserState> _userStates;

        private SortedDictionary<int, LabConfigurationExerciseEntry> _exercises;

        private readonly bool _dockerSupportEnabled;

        /// <summary>
        /// Top level lock to prevent concurrent reading and writing of state objects.
        /// </summary>
        private readonly AsyncReaderWriterLock _configLock = new();

        /// <summary>
        /// Helper lock to prevent a possible race condition when a user tries to login with two requests in parallel.
        /// </summary>
        private readonly SemaphoreSlim _loginLock = new(1, 1);

        public StateService(IOptions<LabOptions> options, ILabConfigurationService labConfiguration, IDockerService dockerService)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _labConfiguration = labConfiguration ?? throw new ArgumentNullException(nameof(labConfiguration));
            _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));

            _dockerSupportEnabled = !string.IsNullOrWhiteSpace(_options.Value.DockerContainerName);

            // Load state
            Reload();
        }

        /// <summary>
        /// Reloads the configuration and updates caches.
        /// </summary>
        public void Reload()
        {
            // We update the entire state, ensure that no one is reading right now
            // This lock will be automatically released upon return
            using var configWriteLock = _configLock.WriterLock();

            // Read exercises
            _exercises = new SortedDictionary<int, LabConfigurationExerciseEntry>(_labConfiguration.CurrentConfiguration.Exercises.ToDictionary(e => e.Id));

            // Read user data
            _userStates = new ConcurrentDictionary<int, UserState>();
            Regex userIdRegex = new Regex("u([0-9]+)\\.json$", RegexOptions.Compiled);
            foreach(string userStateFileName in Directory.GetFiles(_options.Value.UserStateDirectory, "u*.json"))
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
                    UserName = userStateFile.UserName,
                    Password = userStateFile.Password,
                    Exercises = new ConcurrentDictionary<int, UserStateFileExerciseEntry>(userStateFile.Exercises.ToDictionary(e => e.ExerciseId)),
                    Log = new UserStateLogContainer(Math.Max(1, _options.Value.UserStateLogSize))
                };
                _userStates.TryAdd(userId, userState);

                // Fix exercise list, if necessary
                // - Add entries for new exercises, or exercises which have a new type
                // - Update existing states to be consistent with new exercise data
                // Old exercise entries are not deleted, in order to prevent accidental data loss.
                bool userStateChanged = false;
                foreach(var exercise in _exercises)
                {
                    if(exercise.Value is LabConfigurationStringExerciseEntry stringExercise)
                    {
                        if(!userState.Exercises.TryGetValue(exercise.Key, out var exerciseState) || !(exerciseState is UserStateFileStringExerciseEntry stringExerciseState))
                        {
                            // Set new empty state
                            userState.Exercises[exercise.Value.Id] = UserStateFileStringExerciseEntry.CreateState(stringExercise);
                            userStateChanged = true;
                            continue;
                        }

                        userStateChanged = stringExerciseState.Update(stringExercise);
                    }
                    else if(exercise.Value is LabConfigurationMultipleChoiceExerciseEntry multipleChoiceExercise)
                    {
                        if(!userState.Exercises.TryGetValue(exercise.Key, out var exerciseState) || !(exerciseState is UserStateFileMultipleChoiceExerciseEntry multipleChoiceExerciseState))
                        {
                            // Set new empty state
                            userState.Exercises[exercise.Value.Id] = UserStateFileMultipleChoiceExerciseEntry.CreateState(multipleChoiceExercise);
                            userStateChanged = true;
                            continue;
                        }

                        userStateChanged = multipleChoiceExerciseState.Update(multipleChoiceExercise);
                    }
                    else if(exercise.Value is LabConfigurationScriptExerciseEntry scriptExercise)
                    {
                        if(!userState.Exercises.TryGetValue(exercise.Key, out var exerciseState) || !(exerciseState is UserStateFileScriptExerciseEntry scriptExerciseState))
                        {
                            // Set new empty state
                            userState.Exercises[exercise.Value.Id] = UserStateFileScriptExerciseEntry.CreateState(scriptExercise);
                            userStateChanged = true;
                            continue;
                        }

                        userStateChanged = scriptExerciseState.Update(scriptExercise);
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

        /// <summary>
        /// Parses the state file for the given user.
        /// </summary>
        /// <param name="userStateFileName">File path.</param>
        /// <returns></returns>
        private UserStateFile ReadUserStateFile(string userStateFileName)
        {
            // Read user state file
            string path = Path.Combine(_options.Value.UserStateDirectory, userStateFileName);
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
                GroupId = userState.GroupId,
                UserName = userState.UserName,
                Password = userState.Password,
                Exercises = userState.Exercises.Values.ToList()
            };
            File.WriteAllText(Path.Combine(_options.Value.UserStateDirectory, $"u{userId}.json"), JsonConvert.SerializeObject(userStateFile));
        }

        /// <summary>
        /// Checks whether the given user state object exists. If it does exist, the old user state is updated, else a new one is generated.
        /// </summary>
        /// <returns></returns>
        public async Task ProcessLoginAsync(int userId, int? groupId, CancellationToken cancellationToken)
        {
            // Ensure synchronized access
            using var configReadLock = await _configLock.ReaderLockAsync(cancellationToken);
            await _loginLock.WaitAsync(cancellationToken);
            try
            {
                // Does the user exist?
                if(_userStates.TryGetValue(userId, out var userState))
                {
                    // Update user state if necessary
                    await userState.Lock.WaitAsync(cancellationToken);
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
                        Exercises = new ConcurrentDictionary<int, UserStateFileExerciseEntry>(),
                        UserName = _dockerSupportEnabled ? $"user{userId}" : null,
                        Password = _dockerSupportEnabled ? RandomStringGenerator.GetRandomString(10) : null,
                        Log = new UserStateLogContainer(Math.Max(1, _options.Value.UserStateLogSize))
                    };

                    // Initialize exercises
                    foreach(var exercise in _exercises)
                    {
                        if(exercise.Value is LabConfigurationStringExerciseEntry stringExercise)
                            userState.Exercises.TryAdd(exercise.Value.Id, UserStateFileStringExerciseEntry.CreateState(stringExercise));
                        else if(exercise.Value is LabConfigurationMultipleChoiceExerciseEntry multipleChoiceExercise)
                            userState.Exercises.TryAdd(exercise.Value.Id, UserStateFileMultipleChoiceExerciseEntry.CreateState(multipleChoiceExercise));
                        else if(exercise.Value is LabConfigurationScriptExerciseEntry scriptExercise)
                            userState.Exercises.TryAdd(exercise.Value.Id, UserStateFileScriptExerciseEntry.CreateState(scriptExercise));
                    }

                    // Initialize Docker container account, if needed
                    if(_dockerSupportEnabled && !string.IsNullOrWhiteSpace(_options.Value.DockerContainerInitUserScriptPath))
                        await _dockerService.InitUserAsync(userId, userState.UserName, userState.Password, cancellationToken);

                    // Store new user state
                    _userStates.TryAdd(userId, userState);
                }

                // New user/new group: Update group member lists
                // Remove user from existing group member lists
                if(userState.GroupMembers.Any())
                {
                    foreach(var groupMember in userState.GroupMembers)
                    {
                        await groupMember.Lock.WaitAsync(cancellationToken);
                        try
                        {
                            groupMember.GroupMembers.Remove(userState);
                        }
                        finally
                        {
                            groupMember.Lock.Release();
                        }
                    }

                    userState.GroupMembers.Clear();
                }

                // Find group members of current user
                if(groupId != null)
                    userState.GroupMembers.AddRange(_userStates.Where(u => u.Value.GroupId == groupId && u.Key != userId).Select(u => u.Value));

                // Update lists of group members
                foreach(var groupMember in userState.GroupMembers)
                {
                    await groupMember.Lock.WaitAsync(cancellationToken);
                    try
                    {
                        groupMember.GroupMembers.Add(userState);
                    }
                    finally
                    {
                        groupMember.Lock.Release();
                    }
                }

                // Save new user state
                WriteUserStateFile(userId);
            }
            finally
            {
                _loginLock.Release();
            }
        }

        /// <summary>
        /// Checks whether the given input is correct and updates the user status.
        /// </summary>
        /// <param name="exerciseId">Exercise ID.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="input">Input.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> CheckInputAsync(int exerciseId, int userId, object input, CancellationToken cancellationToken)
        {
            // Ensure synchronized access
            using var configReadLock = await _configLock.ReaderLockAsync(cancellationToken);

            // Check input values
            if(!_userStates.ContainsKey(userId) || !_exercises.ContainsKey(exerciseId))
                throw new ArgumentException();

            // Get state object
            var userState = _userStates[userId];

            // Log input
            userState.Log.AddMessage($"Checking input for exercise #{exerciseId}", input?.ToString());

            // There can only be one grading operation per user at once
            await userState.Lock.WaitAsync(cancellationToken);
            try
            {
                // Get exercise data
                var exercise = _exercises[exerciseId];
                var exerciseState = userState.Exercises[exerciseId];

                // Check correctness
                bool correct = exerciseState switch
                {
                    UserStateFileStringExerciseEntry stringExerciseState => stringExerciseState.ValidateInput(exercise, input),
                    UserStateFileMultipleChoiceExerciseEntry multipleChoiceExerciseState => multipleChoiceExerciseState.ValidateInput(exercise, input),
                    _ => false
                };
                if(exerciseState.Type == UserStateFileExerciseEntryType.Script)
                {
                    if(!_dockerSupportEnabled)
                        throw new NotSupportedException("Docker support is not enabled, so script exercises cannot be graded.");

                    // Run script
                    string stderr;
                    (correct, stderr) = await _dockerService.GradeAsync(userId, exerciseId, input as string, cancellationToken);

                    userState.Log.AddMessage("Docker stderr", stderr);
                }

                // Update user, if state changed
                if(!exerciseState.Solved && correct)
                {
                    exerciseState.Solved = true;
                    WriteUserStateFile(userId);
                }

                userState.Log.AddMessage($"Grade result: {(correct ? "Correct" : "Not correct")}", null);
                return correct;
            }
            finally
            {
                userState.Lock.Release();
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
            using var configReadLock = await _configLock.ReaderLockAsync();

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

                userState.Log.AddMessage($"Exercise #{exerciseId} marked as \"solved\" by admin", null);
            }
            finally
            {
                userState.Lock.Release();
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
            using var configReadLock = await _configLock.ReaderLockAsync();

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

                userState.Log.AddMessage($"Exercise #{exerciseId} state reset by admin", null);
            }
            finally
            {
                userState.Lock.Release();
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
            using var configReadLock = await _configLock.ReaderLockAsync();

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
                    DockerUserName = userState.UserName,
                    DockerPassword = userState.Password,
                    Exercises = _exercises.Select(e =>
                    {
                        var exerciseState = userState.Exercises[e.Key];
                        string description = exerciseState switch
                        {
                            UserStateFileStringExerciseEntry stringExerciseState => stringExerciseState.FormatDescriptionString(e.Value),
                            UserStateFileMultipleChoiceExerciseEntry multipleChoiceExerciseState => multipleChoiceExerciseState.FormatDescriptionString(e.Value),
                            UserStateFileScriptExerciseEntry scriptExerciseState => scriptExerciseState.FormatDescriptionString(e.Value),
                            _ => null
                        };

                        return new UserScoreboardExerciseEntry
                        {
                            Exercise = e.Value,
                            Solved = exerciseState.Solved,
                            SolvedByGroupMember = userState.GroupMembers.Any(g => g.Exercises[e.Key].Solved),
                            Description = description
                        };
                    }).ToArray(),
                    Log = userState.Log
                };

                // Done
                return scoreboard;
            }
            finally
            {
                userState.Lock.Release();
            }
        }

        public void Dispose()
        {
            foreach(var u in _userStates)
                u.Value.Lock?.Dispose();
            _loginLock?.Dispose();
        }
    }
}