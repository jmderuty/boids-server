using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Server.Plugins.Steam.Models;
using Stormancer.Server.Components;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using Server.Plugins.Configuration;

namespace Server.Plugins.Steam
{
    public class SteamService : ISteamService
    {
        private const string ApiRoot = "https://partner.steam-api.com";
        private const string FallbackApiRoot = "https://api.steampowered.com";
        private const string FallbackApiRooWithIp = "https://208.64.202.87";

        private readonly string _apiKey;
        private readonly uint _appId;
       
        private bool _usemockup;

        public SteamService(IConfiguration configuration)
        {

            var steamElement = configuration.Settings?.steam;

            _apiKey = (string)steamElement?.apiKey;

            var dynamicAppId = steamElement?.appId;
            if (dynamicAppId != null)
            {
                _appId = (uint)dynamicAppId;
            }

            ApplyConfig(steamElement);

            configuration.SettingsChanged += (sender, settings) => ApplyConfig(settings?.steam);
        }

        private void ApplyConfig(dynamic steamElement)
        {
            var dynamicUseMockup = steamElement?.usemockup;
            if (dynamicUseMockup != null)
            {
                _usemockup = (bool)dynamicUseMockup;
            }
        }

        public async Task<ulong?> AuthenticateUserTicket(string ticket)
        {
            if(_usemockup)
            {
                return (ulong)ticket.GetHashCode();
            }

            const string AuthenticateUri = "ISteamUserAuth/AuthenticateUserTicket/v0001/";

            var querystring = $"?key={_apiKey}"
                + $"&appid={_appId}"
                + $"&ticket={ticket}";

            using (var response = await TryGetAsync(AuthenticateUri + querystring))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var steamResponse = JsonConvert.DeserializeObject<SteamAuthenticationResponse>(json);

                if (steamResponse.response.error != null)
                {
                    return null;
                }
                else
                {
                    return steamResponse.response.@params.steamid;
                }
            }


        }

        public async Task<Dictionary<ulong, SteamPlayerSummary>> GetPlayerSummaries(IEnumerable<ulong> steamIds)
        {
            if(_usemockup)
            {
                return steamIds.ToDictionary(id => id, id => new SteamPlayerSummary());
            }

            const string GetPlayerSummariesUri = "ISteamUser/GetPlayerSummaries/V0002/";

            var steamIdsWithoutRepeat = steamIds.Distinct().ToList();
            Dictionary<ulong, SteamPlayerSummary> result = new Dictionary<ulong, SteamPlayerSummary>();

            for (var i = 0; i * 100 < steamIdsWithoutRepeat.Count; i++)
            {
                var querystring = $"?key={_apiKey}"
                    + $"&steamids={string.Join(",", steamIdsWithoutRepeat.Skip(100 * i).Take(100).Select(v => v.ToString()))}";

                using (var response = await TryGetAsync(GetPlayerSummariesUri + querystring))
                {

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var steamResponse = JsonConvert.DeserializeObject<SteamPlayerSummariesResponse>(json);

                    foreach (var summary in steamResponse.response.players)
                    {
                        result.Add(summary.steamid, summary);
                    }
                }
            }

            return result;
        }

        private async Task<HttpResponseMessage> TryGetAsync(string requestUrl)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    return await client.GetAsync(new Uri(new Uri(ApiRoot), requestUrl));
                }
                catch (HttpRequestException)
                {
                    try
                    {
                        return await client.GetAsync(new Uri(new Uri(FallbackApiRoot), requestUrl));
                    }
                    catch (HttpRequestException)
                    {
                        return await client.GetAsync(new Uri(new Uri(FallbackApiRooWithIp), requestUrl));
                    }
                }
            }
        }
    }
}