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

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var embeddingClient = _client.GetEmbeddingClient(_deployment);
            var result = await embeddingClient.GenerateEmbeddingsAsync(new List<string> { text });
            var vector = result.Value[0].ToFloats().ToArray();

            return vector;
        }
    }
}

