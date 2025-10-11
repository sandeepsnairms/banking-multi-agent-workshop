using Azure.AI.OpenAI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Banking.Services
{

    public class EmbeddingService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deployment;

        public EmbeddingService(AzureOpenAIClient client, string deployment)
        {
            _client = client;
            _deployment = deployment;
        }

        // TO DO: Update GenerateEmbeddingAsync
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            return new float[] { 0.0f, 0.0f, 0.0f };
            
        }
    }
}

