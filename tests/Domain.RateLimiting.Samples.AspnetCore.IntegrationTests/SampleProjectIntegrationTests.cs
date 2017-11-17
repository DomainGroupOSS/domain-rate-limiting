using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Domain.RateLimiting.Samples.AspnetCore.IntegrationTests
{
    public class SampleProjectIntegrationTests
    {
        private readonly HttpClient _httpClient;
        
        public SampleProjectIntegrationTests()
        {
            var startupAssembly = typeof(Domain.RateLimiting.Samples.AspNetCore.Startup)
                .GetTypeInfo().Assembly;

            var contentRoot = GetProjectPath("samples", startupAssembly);

            var builder = new WebHostBuilder()
                .ConfigureServices(InitializeServices<Domain.RateLimiting.Samples.AspNetCore.Startup>)
                .UseEnvironment("Development")
                .UseContentRoot(contentRoot)
                .UseStartup(typeof(Domain.RateLimiting.Samples.AspNetCore.Startup));

            var server = new TestServer(builder);

            _httpClient = server.CreateClient();
            _httpClient.BaseAddress = new Uri("http://localhost");
            
        }
        
        [Fact(Skip = "You need to have redis installed before running this test")]
        public async void Calling_api_globallylimited_id_ShouldNotThrottleOnTheFirstFiveCallsButShouldThrottleOnTheSixthCall()
        {
            for (int i = 4; i >= 0; i--)
            {
                var response = await _httpClient.GetAsync($"/api/globallylimited/{i}");
                Assert.Equal(true, response.Headers.Contains("X-RateLimit-Remaining"));
                Assert.Equal(true, response.Headers.Contains("X-RateLimit-Limit"));
                Assert.Equal(i.ToString(), response.Headers.GetValues("X-RateLimit-Remaining").First());
                Assert.Equal("5 calls PerMinute", response.Headers.GetValues("X-RateLimit-Limit").First());
                Assert.Equal(false, response.Headers.Contains("X-RateLimit-VPolicyName"));
                Assert.Equal(false, response.Headers.Contains("X-RateLimit-VPolicyRate"));
                Assert.Equal("value", await response.Content.ReadAsStringAsync());
            }

            ShouldThrottleTheNextCall();
        }
        
        public async void ShouldThrottleTheNextCall()
        {
            var response = await _httpClient.GetAsync("/api/globallylimited/1");
            Assert.Equal(false, response.Headers.Contains("X-RateLimit-Remaining"));
            Assert.Equal(false, response.Headers.Contains("X-RateLimit-Limit"));
            Assert.Equal(true, response.Headers.Contains("X-RateLimit-VPolicyName"));
            Assert.Equal(true, response.Headers.Contains("X-RateLimit-VCallRate"));
            Assert.Equal("StaticPolicy_0", response.Headers.GetValues("X-RateLimit-VPolicyName").First());
            Assert.Equal("5 calls PerMinute", response.Headers.GetValues("X-RateLimit-VCallRate").First());
        }
        
        [Fact(Skip = "You need to have redis installed before running this test")]
        public async void ShouldNotApplyRateLimitingToUnlimitedControllerEndpoints()
        {
            var response = await _httpClient.GetAsync("/api/unlimited/1");
            Assert.Equal(false, response.Headers.Contains("X-RateLimit-Remaining"));
            Assert.Equal(false, response.Headers.Contains("X-RateLimit-Limit"));
            Assert.Equal(false, response.Headers.Contains("X-RateLimit-VPolicyName"));
            Assert.Equal(false, response.Headers.Contains("X-RateLimit-VCallRate"));
            Assert.Equal("value", await response.Content.ReadAsStringAsync());
        }

        protected virtual void InitializeServices<TStartup>(IServiceCollection services)
        {
            var startupAssembly = typeof(TStartup).GetTypeInfo().Assembly;

            // Inject a custom application part manager. 
            // Overrides AddMvcCore() because it uses TryAdd().
            var manager = new ApplicationPartManager();
            manager.ApplicationParts.Add(new AssemblyPart(startupAssembly));
            manager.FeatureProviders.Add(new ControllerFeatureProvider());

            services.AddSingleton(manager);
        }

        private static string GetProjectPath(string projectRelativePath, Assembly startupAssembly)
        {
            // Get name of the target project which we want to test
            var projectName = startupAssembly.GetName().Name;

            // Get currently executing test project path
            var applicationBasePath = System.AppContext.BaseDirectory;

            // Find the path to the target project
            var directoryInfo = new DirectoryInfo(applicationBasePath);
            do
            {
                directoryInfo = directoryInfo.Parent;

                var projectDirectoryInfo = new DirectoryInfo(Path.Combine(directoryInfo.FullName, projectRelativePath));
                if (projectDirectoryInfo.Exists)
                {
                    var projectFileInfo = new FileInfo(Path.Combine(projectDirectoryInfo.FullName, projectName, $"{projectName}.csproj"));
                    if (projectFileInfo.Exists)
                    {
                        return Path.Combine(projectDirectoryInfo.FullName, projectName);
                    }
                }
            }
            while (directoryInfo.Parent != null);

            throw new Exception($"Project root could not be located using the application root {applicationBasePath}.");
        }
    }
}
