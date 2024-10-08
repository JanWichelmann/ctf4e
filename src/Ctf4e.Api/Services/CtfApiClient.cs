﻿using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Api.Exceptions;
using Ctf4e.Api.Models;
using Ctf4e.Api.Options;
using Microsoft.Extensions.Options;
using RestSharp;

namespace Ctf4e.Api.Services
{
    public interface ICtfApiClient
    {
        Task CreateExerciseSubmissionAsync(ApiExerciseSubmission submission, CancellationToken cancellationToken);
        Task ClearExerciseSubmissionsAsync(int exerciseNumber, int userId, CancellationToken cancellationToken);
        Task CreateGroupExerciseSubmissionAsync(ApiGroupExerciseSubmission submission, CancellationToken cancellationToken);
        Task ClearGroupExerciseSubmissionsAsync(int exerciseNumber, int groupId, CancellationToken cancellationToken);
    }

    public class CtfApiClient(IOptions<CtfApiOptions> options, ICryptoService cryptoService) : ICtfApiClient
    {
        public Task CreateExerciseSubmissionAsync(ApiExerciseSubmission submission, CancellationToken cancellationToken)
            => RunApiPostRequestAsync("exercisesubmission/create", submission, cancellationToken);

        public Task ClearExerciseSubmissionsAsync(int exerciseNumber, int userId, CancellationToken cancellationToken)
            => RunApiPostRequestAsync("exercisesubmission/clear", new ApiExerciseSubmission { ExerciseNumber = exerciseNumber, UserId = userId }, cancellationToken);

        public Task CreateGroupExerciseSubmissionAsync(ApiGroupExerciseSubmission submission, CancellationToken cancellationToken)
            => RunApiPostRequestAsync("exercisesubmission-group/create", submission, cancellationToken);

        public Task ClearGroupExerciseSubmissionsAsync(int exerciseNumber, int groupId, CancellationToken cancellationToken)
            => RunApiPostRequestAsync("exercisesubmission-group/clear", new ApiGroupExerciseSubmission { ExerciseNumber = exerciseNumber, GroupId = groupId }, cancellationToken);

        private async Task RunApiPostRequestAsync(string resource, object payload, CancellationToken cancellationToken)
        {
            // Run request
            var client = new RestClient(options.Value.CtfServerApiBaseUrl);
            var request = new RestRequest(resource, Method.Post);
            request.AddJsonBody(CtfApiRequest.Create(options.Value.LabId, cryptoService, payload));
            var response = await client.ExecuteAsync(request, cancellationToken);
            
            if(!response.IsSuccessful)
            {
                // Throw an exception which contains the entire response data, for easier debugging

                StringBuilder exceptionContentBuilder = new StringBuilder();
                exceptionContentBuilder.AppendLine($"Resource: {resource}");
                exceptionContentBuilder.AppendLine($"Status: {(int)response.StatusCode} {response.StatusDescription}");

                exceptionContentBuilder.AppendLine();
                exceptionContentBuilder.AppendLine("-- Response content: --");
                exceptionContentBuilder.AppendLine(response.Content ?? "(none)");

                throw new CtfApiException($"The server returned an error status code: {response.StatusCode} {response.StatusDescription}", exceptionContentBuilder.ToString());
            }
        }
    }
}