using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ctf4e.Server.Services
{
    public interface IFlagPointService
    {
        /// <summary>
        /// Loads configuration data.
        /// </summary>
        /// <param name="configurationService">Configuration service.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task ReloadAsync(IConfigurationService configurationService, CancellationToken cancellationToken = default);

        int GetFlagPoints(int flagBasePoints, int submissionCount);
    }

    /// <summary>
    /// Offers functions to calculate flag points.
    /// </summary>
    public class FlagPointService : IFlagPointService
    {
        private double _minPointsMultiplier;
        private int _halfPointsCount;

        private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);

        public FlagPointService()
        {
        }

        /// <summary>
        /// Loads configuration data.
        /// </summary>
        /// <param name="configurationService">Configuration service.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public async Task ReloadAsync(IConfigurationService configurationService, CancellationToken cancellationToken = default)
        {
            await _updateLock.WaitAsync(cancellationToken);
            try
            {
                // Configured constants
                int minPointsDivisor = await configurationService.GetFlagMinimumPointsDivisorAsync(cancellationToken);
                _minPointsMultiplier = 1.0 / minPointsDivisor;
                _halfPointsCount = await configurationService.GetFlagHalfPointsSubmissionCountAsync(cancellationToken);
            }
            finally
            {
                _updateLock.Release();
            }
        }

        public int GetFlagPoints(int flagBasePoints, int submissionCount)
        {
            // a: Base points
            // b: Min points = multiplier*a
            // c: 50% points y = (a+b)/2
            // d: 50% points x

            // Flag points depending on submission count x:
            // (a-b)*((a-b)/(c-b))^(1/(d-1)*(-x+1))+b
            // (base is solution of (a-b)*y^(-d+1)+b=c)


            // (a-b)
            double amb = flagBasePoints - _minPointsMultiplier * flagBasePoints;

            // (c-b)=(a+b)/2-b=(a-b)/2
            // -> (a-b)/(c-b)=2

            double points = (amb * Math.Pow(2, (-submissionCount + 1.0) / (_halfPointsCount - 1))) + (_minPointsMultiplier * flagBasePoints);
            return (int)Math.Round(points);
        }
    }
}
