using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebHooks;
using Newtonsoft.Json.Linq;
using Octopus.Client;
using Serilog;

namespace githubhooks.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class GitHubController : ControllerBase
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly OctopusServerEndpoint octopusServerEndpoint;

        public GitHubController(IHttpClientFactory httpClientFactory, OctopusServerEndpoint octopusServerEndpoint)
        {
            this.httpClientFactory = httpClientFactory;
            this.octopusServerEndpoint = octopusServerEndpoint;
        }

        [GitHubWebHook]
        public async Task<IActionResult> GitHub(string id, JObject data)
        {
            
            
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var action = (string)data["action"];

                Log.Information("GitHub Hook with {@action}", action);
                if ((string)data["action"] == "published")
                {
                    var httpClient = httpClientFactory.CreateClient("GitHub");
                    var assets = JArray.Parse(await httpClient.GetStringAsync((string)data["release"]["assets_url"]));
                    await UploadToOctopus(httpClient, assets);
                }
                return Ok();
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception ocurred {e}");
                throw e;
            }

            
        }

        private async Task UploadToOctopus(HttpClient httpClient, JArray assets)
        {
            Log.Information("Uploading assets to Octopus");
            using (var octoClient = await OctopusAsyncClient.Create(octopusServerEndpoint))
            {
                foreach (var assetToken in assets)
                {
                    var assetName = (string)assetToken["name"];
                    var assetUrl = (string)assetToken["browser_download_url"];
                    using (var response = await httpClient.GetAsync(assetUrl))
                    {
                        using (var content = response.Content)
                        {
                            Log.Information("Uploading asset {@asset} to octopus", new { name = assetName });
                            await octoClient.Repository.BuiltInPackageRepository.PushPackage(assetName, await content.ReadAsStreamAsync(), false);
                        }
                    }

                }
            }
        }
    }
}