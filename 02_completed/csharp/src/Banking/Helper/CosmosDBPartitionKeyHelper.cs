using Microsoft.Azure.Cosmos;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace  Banking.Helper
{

    public static class PartitionManager
    {
        
        public static PartitionKey GetAccountsDataFullPK(string tenantId, string accountId)
        {
            PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(tenantId)
                .Add(accountId)
                .Build();
            return partitionKey;
        }

        public static PartitionKey GetAccountsPartialPK(string tenantId)
        {
            PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(tenantId)
                .Build();
            return partitionKey;
        }

        public static PartitionKey GetUserDataFullPK(string tenantId)
        {
            PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(tenantId)
                .Build();
            return partitionKey;
        }
    }
}
