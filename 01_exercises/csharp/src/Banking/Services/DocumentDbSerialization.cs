using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Banking.Services;

public static class DocumentDbSerialization
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new StringEnumConverter() }
    };

    public static BsonDocument ToDocument<T>(T value)
    {
        BsonDocument document = BsonDocument.Parse(JsonConvert.SerializeObject(value, Settings));
        EnsureMongoId(document);
        return document;
    }

    public static T FromDocument<T>(BsonDocument document)
    {
        BsonDocument copy = document.DeepClone().AsBsonDocument;
        copy.Remove("_id");
        return JsonConvert.DeserializeObject<T>(copy.ToJson(), Settings)
            ?? throw new JsonSerializationException($"Unable to deserialize {typeof(T).Name}.");
    }

    public static void EnsureMongoId(BsonDocument document)
    {
        if (!document.Contains("_id"))
        {
            if (!document.TryGetValue("id", out BsonValue? id) || !id.IsString)
            {
                throw new ArgumentException("Document must contain a string 'id' property.");
            }

            document["_id"] = id;
        }
    }
}