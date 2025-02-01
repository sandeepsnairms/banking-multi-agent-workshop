using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace SKMultiAgent
{
    internal class Settings
    {
        private readonly IConfigurationRoot configRoot;

        private AzureOpenAISettings azureOpenAI;
        private OpenAISettings openAI;

        public AzureOpenAISettings AzureOpenAI => this.azureOpenAI ??= this.GetSettings<Settings.AzureOpenAISettings>();
        public OpenAISettings OpenAI => this.openAI ??= this.GetSettings<Settings.OpenAISettings>();

        public class OpenAISettings
        {
            public string ChatModel { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
        }

        public class AzureOpenAISettings
        {
            public string ChatModelDeployment { get; set; } = string.Empty;
            public string Endpoint { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
        }

        public TSettings GetSettings<TSettings>() where TSettings : class, new()
        {
            return this.configRoot.GetSection(typeof(TSettings).Name).Get<TSettings>() ?? new TSettings();
        }

        public Settings()
        {

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true);

            this.configRoot = configuration.Build();

        }
    }
}