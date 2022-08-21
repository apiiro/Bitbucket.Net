using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bitbucket.Net.Common;
using Bitbucket.Net.Common.Models;
using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Bitbucket.Net
{
    public partial class BitbucketClient
    {
        private static readonly ISerializer s_serializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
        });

        static BitbucketClient()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            };
        }

        private readonly Url _url;
        private readonly Func<string> _getToken;
        private readonly string _userName;
        private readonly string _password;
        private readonly FlurlClient _flurlClient;

        private BitbucketClient(string url, bool trustSsl, IWebProxy proxy = null, bool allowHttpAutoRedirect = false)
        {
            _url = url;

            var httpClientHandler = new HttpClientHandler { Proxy = proxy, AllowAutoRedirect = allowHttpAutoRedirect };
            if (trustSsl)
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true;
            }

            var httpClient = new HttpClient(httpClientHandler);
            _flurlClient = new FlurlClient(httpClient);
            _flurlClient.Settings.Redirects.Enabled = true;
            _flurlClient.Settings.Redirects.ForwardAuthorizationHeader = true;
            _flurlClient.Settings.Redirects.AllowSecureToInsecure = true;
        }

        public BitbucketClient(string url, string userName, string password, bool trustSsl, IWebProxy proxy = null)
            : this(url, trustSsl, proxy)
        {
            _userName = userName;
            _password = password;
        }

        public BitbucketClient(string url, Func<string> getToken, bool trustSsl, IWebProxy proxy = null)
            : this(url, trustSsl, proxy)
            => _getToken = getToken;

        private IFlurlRequest GetBaseUrl(string root = "/api", string version = "1.0")
        {
            var url = new Url(_url)
                .AppendPathSegment($"/rest{root}/{version}");
            return GetRequest(url);
        }

        private IFlurlRequest GetRequest(Url url)
        {
            return _flurlClient.Request(url)
                .ConfigureRequest(settings => settings.JsonSerializer = s_serializer)
                .WithAuthentication(_getToken, _userName, _password);
        }

        private async Task<TResult> ReadResponseContentAsync<TResult>(IFlurlResponse responseMessage, Func<string, TResult> contentHandler = null)
        {
            string content = await responseMessage.GetJsonAsync().ConfigureAwait(false);
            return contentHandler != null
                ? contentHandler(content)
                : JsonConvert.DeserializeObject<TResult>(content);
        }

        private async Task<bool> ReadResponseContentAsync(IFlurlResponse responseMessage)
        {
            string content = await responseMessage.GetStringAsync().ConfigureAwait(false);
            return content == "";
        }

        private async Task HandleErrorsAsync(IFlurlResponse response)
        {
            if (response.StatusCode >= 300)
            {
                var errorResponse = await ReadResponseContentAsync<ErrorResponse>(response).ConfigureAwait(false);
                string errorMessage = string.Join(Environment.NewLine, errorResponse.Errors.Select(x => x.Message));
                throw new InvalidOperationException($"Http request failed ({(int) response.StatusCode} - {response.StatusCode}):\n{errorMessage}");
            }
        }

        private async Task<TResult> HandleResponseAsync<TResult>(IFlurlResponse responseMessage, Func<string, TResult> contentHandler = null)
        {
            await HandleErrorsAsync(responseMessage).ConfigureAwait(false);
            return await ReadResponseContentAsync(responseMessage, contentHandler).ConfigureAwait(false);
        }

        private async Task<bool> HandleResponseAsync(IFlurlResponse responseMessage)
        {
            await HandleErrorsAsync(responseMessage).ConfigureAwait(false);
            return await ReadResponseContentAsync(responseMessage).ConfigureAwait(false);
        }

        private async Task<IEnumerable<T>> GetPagedResultsAsync<T>(int? maxPages, IDictionary<string, object> queryParamValues, Func<IDictionary<string, object>, Task<PagedResults<T>>> selector)
        {
            var results = new List<T>();
            bool isLastPage = false;
            int numPages = 0;

            while (!isLastPage && (maxPages == null || numPages < maxPages))
            {
                var selectorResults = await selector(queryParamValues).ConfigureAwait(false);
                results.AddRange(selectorResults.Values);

                isLastPage = selectorResults.IsLastPage;
                if (!isLastPage)
                {
                    queryParamValues["start"] = selectorResults.NextPageStart;
                }

                numPages++;
            }

            return results;
        }
    }
}