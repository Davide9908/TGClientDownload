using RestSharp;
using TGClientDownloadDAL.Entities;
using TGClientDownloadWorkerService.Extensions;

namespace TGClientDownloadWorkerService.Services
{
    public class MalApiService
    {
        private readonly ILogger<MalApiService> _log;
        private readonly RestClient? _client;
        private readonly ConfigParameterService _configParameterService;

        private const string MAL_WATCHING = "watching";
        private const string MAL_COMPLETED = "completed";
        private const string PARAMETER_X_MAL_CLIENT_ID = "X-MAL-CLIENT-ID";
        private const string PARAMETER_LIMIT = "limit";
        private const int PARAMETER_LIMIT_VALUE = 1000;
        private const string PARAMETER_FIELDS = "fields";
        private const string PARAMETER_FIELDS_VALUE = "list_status";
        private const string PARAMETER_STATUS = "status";
        private const string PARAMETER_NSFW = "nsfw";
        public MalApiService(ILogger<MalApiService> logger, ConfigParameterService configParameterService)
        {
            _log = logger;
            _configParameterService = configParameterService;

            string? link = _configParameterService.GetValue(ParameterNames.MALApiLink);
            if (!string.IsNullOrEmpty(link))
            {
                _client = new RestClient(new RestClientOptions(link));
            }
        }

        /// <summary>
        /// Get watching anime list from MyAnimeList 
        /// </summary>
        /// <returns>null if needed parameters is missing or request failed, otherwise the list of anime with status "watching"</returns>
        public List<MALAnimeData>? GetWatchingAnimeList()
        {
            return GetAnimeListByStatus(MAL_WATCHING);
        }

        /// <summary>
        /// Get completed anime list from MyAnimeList 
        /// </summary>
        /// <returns>null if needed parameters is missing or request failed, otherwise the list of anime with status "completed"</returns>
        public List<MALAnimeData>? GetCompletedAnimeList()
        {
            return GetAnimeListByStatus(MAL_COMPLETED);
        }

        private List<MALAnimeData>? GetAnimeListByStatus(string status, bool includeNSFW = true)
        {
            if (_client is null)
            {
                _log.Warning("MAL RestClient is null, probably parameter is missing");
                return null;
            }
            var username = _configParameterService.GetValue(ParameterNames.MALUsername);
            var apiId = _configParameterService.GetValue(ParameterNames.MALApiID);
            if (username is null || apiId is null)
            {
                _log.Warning("Username or ApiId parameters is missing");
                return null;
            }

            var request = new RestRequest($"users/{username}/animelist");
            request.AddHeader(PARAMETER_X_MAL_CLIENT_ID, apiId);
            request.AddParameter(PARAMETER_LIMIT, PARAMETER_LIMIT_VALUE);
            request.AddParameter(PARAMETER_FIELDS, PARAMETER_FIELDS_VALUE);
            request.AddParameter(PARAMETER_STATUS, status);
            request.AddParameter(PARAMETER_NSFW, includeNSFW);

            RestResponse<Root> restResponse = _client.ExecuteGet<Root>(request);
            //should not happen
            if (restResponse is null)
            {
                _log.Warning("restResponse is null");
                return null;
            }
            if (!restResponse.IsSuccessful)
            {
                _log.Error($"An error occured executing GetAnimeListByStatus request with status \"{status}\". HTTP code: {restResponse.StatusCode}, error message: {restResponse.ErrorMessage}", restResponse.ErrorException);
                return null;
            }

            return restResponse.Data?.data ?? [];
        }

    }
}
