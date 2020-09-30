using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Api.Models;
using Ctf4e.Api.Options;
using Microsoft.Extensions.Options;
using RestSharp;

namespace Ctf4e.Api.Services
{
    public interface ICtfApiClient
    {
        Task CreateExerciseSubmissionAsync(ApiExerciseSubmission submission, CancellationToken cancellationToken = default);
        Task ClearExerciseSubmissionsAsync(int exerciseNumber, int userId, CancellationToken cancellationToken = default);
    }

    public class CtfApiClient : ICtfApiClient
    {
        private readonly IOptions<CtfApiOptions> _options;
        private readonly ICryptoService _cryptoService;

        public CtfApiClient(IOptions<CtfApiOptions> options, ICryptoService cryptoService)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
        }

        public Task CreateExerciseSubmissionAsync(ApiExerciseSubmission submission, CancellationToken cancellationToken = default)
            => RunApiPostRequestAsync("exercisesubmission/create", submission, cancellationToken);

        public Task ClearExerciseSubmissionsAsync(int exerciseNumber, int userId, CancellationToken cancellationToken = default)
            => RunApiPostRequestAsync("exercisesubmission/clear", new ApiExerciseSubmission { ExerciseNumber = exerciseNumber, UserId = userId }, cancellationToken);

        private async Task RunApiPostRequestAsync(string resource, object payload, CancellationToken cancellationToken = default)
        {
            // Run request
            var client = new RestClient(_options.Value.CtfServerApiBaseUrl);
            var request = new RestRequest(resource, Method.POST);
            request.AddJsonBody(CtfApiRequest.Create(_options.Value.LabId, _cryptoService, payload));
            var response = await client.ExecuteAsync(request, cancellationToken);
            
            // WORKAROUND: The current implementation of RestSharp silently swallows exceptions; check and throw possible exceptions manually
            if(response.ErrorException != null)
                throw response.ErrorException;
            if(!response.IsSuccessful)
                throw new WebException("The server returned an error status code: " + response.StatusDescription);
        }
    }
}