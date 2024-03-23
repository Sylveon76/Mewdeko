using System.Net.Http;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Games.Common.ChatterBot
{
    /// <summary>
    /// Represents a session with the official Cleverbot API.
    /// </summary>
    public class OfficialCleverbotSession : IChatterBotSession
    {
        private string cs;
        private readonly string key;

        private string QueryString =>
            $"https://www.cleverbot.com/getreply?key={{apiKey}}&wrapper=Mewdeko&input={{input}}&cs={{cs}}";

        private readonly IHttpClientFactory factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="OfficialCleverbotSession"/> class with the specified API key and HTTP client factory.
        /// </summary>
        /// <param name="apiKey">The API key for accessing the Cleverbot API.</param>
        /// <param name="factory">The factory for creating HTTP clients.</param>
        public OfficialCleverbotSession(string apiKey, IHttpClientFactory factory)
        {
            this.factory = factory;
            key = apiKey;
        }

        /// <inheritdoc/>
        public async Task<string>? Think(string input)
        {
            using var http = factory.CreateClient();
            var dataString = await http
                .GetStringAsync(QueryString.Replace("{apiKey}", key).Replace("{input}", input).Replace("{cs}", cs))
                .ConfigureAwait(false);
            try
            {
                var data = JsonConvert.DeserializeObject<CleverbotResponse>(dataString);

                cs = data?.Cs;
                return data?.Output;
            }
            catch
            {
                Log.Warning("Unexpected cleverbot response received: ");
                Log.Warning(dataString);
                return null;
            }
        }
    }

    /// <summary>
    /// Represents a session with the Cleverbot.io API.
    /// </summary>
    public class CleverbotIoSession : IChatterBotSession
    {
        private readonly string askEndpoint = "https://cleverbot.io/1.0/ask";
        private readonly string createEndpoint = "https://cleverbot.io/1.0/create";
        private readonly IHttpClientFactory httpFactory;
        private readonly string key;
        private readonly AsyncLazy<string> nick;
        private readonly string user;

        /// <summary>
        /// Initializes a new instance of the <see cref="CleverbotIoSession"/> class with the specified user, API key, and HTTP client factory.
        /// </summary>
        /// <param name="user">The user for the Cleverbot.io session.</param>
        /// <param name="key">The API key for accessing the Cleverbot.io API.</param>
        /// <param name="factory">The factory for creating HTTP clients.</param>
        public CleverbotIoSession(string user, string key, IHttpClientFactory factory)
        {
            this.user = user;
            this.key = key;
            httpFactory = factory;
            nick = new AsyncLazy<string>(GetNick);
        }

        /// <inheritdoc/>
        public async Task<string> Think(string input)
        {
            using var http = httpFactory.CreateClient();
            using var msg = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", user), new KeyValuePair<string, string>("key", key),
                new KeyValuePair<string, string>("nick", await nick), new KeyValuePair<string, string>("text", input)
            });
            using var data = await http.PostAsync(askEndpoint, msg).ConfigureAwait(false);
            var str = await data.Content.ReadAsStringAsync().ConfigureAwait(false);
            var obj = JsonConvert.DeserializeObject<CleverbotIoAskResponse>(str);
            if (obj.Status != "success")
                throw new OperationCanceledException(obj.Status);

            return obj.Response;
        }

        private async Task<string> GetNick()
        {
            using var http = httpFactory.CreateClient();
            using var msg = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", user), new KeyValuePair<string, string>("key", key)
            });
            using var data = await http.PostAsync(createEndpoint, msg).ConfigureAwait(false);
            var str = await data.Content.ReadAsStringAsync().ConfigureAwait(false);
            var obj = JsonConvert.DeserializeObject<CleverbotIoCreateResponse>(str);
            if (obj.Status != "success")
                throw new OperationCanceledException(obj.Status);

            return obj.Nick;
        }
    }
}