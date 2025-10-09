using ModelContextProtocol.Client;
using System.Net.Http.Headers;
using System.Reflection;

namespace MultiAgentCopilot.Transport;

/// <summary>
/// Factory for creating authenticated HTTP transports with proper header injection
/// </summary>
public static class AuthenticatedHttpTransportFactory
{
    /// <summary>
    /// Creates an HttpClientTransport with authentication headers
    /// This implementation uses reflection to inject custom headers into the underlying HttpClient
    /// </summary>
    public static HttpClientTransport Create(string endpoint, string apiKey)
    {
        Console.WriteLine($"?? DEBUG: Creating authenticated HTTP transport for endpoint: {endpoint}");
        Console.WriteLine($"?? DEBUG: API Key length: {apiKey?.Length ?? 0} chars, first 4 chars: {(apiKey != null && apiKey.Length >= 4 ? apiKey[..4] : apiKey ?? "null")}...");

        // Create the standard transport first
        Console.WriteLine($"?? DEBUG: Creating HttpClientTransport with endpoint: {endpoint}");
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpoint)
        });
        Console.WriteLine($"? DEBUG: HttpClientTransport created successfully");

        // Try to inject authentication headers using reflection
        try
        {
            Console.WriteLine($"?? DEBUG: Attempting to inject authentication headers...");
            InjectAuthenticationHeaders(transport, apiKey);
            Console.WriteLine("? DEBUG: Successfully injected authentication headers into HTTP transport");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? DEBUG: Could not inject authentication headers: {ex.Message}");
            Console.WriteLine($"?? DEBUG: The transport will work but without authentication headers.");
            Console.WriteLine($"?? DEBUG: Exception details: {ex}");
        }

        return transport;
    }

    private static void InjectAuthenticationHeaders(HttpClientTransport transport, string apiKey)
    {
        // Get the type of HttpClientTransport
        var transportType = transport.GetType();
        
        Console.WriteLine($"?? DEBUG: Attempting to inject headers into transport type: {transportType.Name}");
        
        // Try multiple approaches to find the HttpClient
        HttpClient? httpClient = null;
        
        // Approach 1: Look for common HttpClient field names
        var possibleFieldNames = new[] { "_httpClient", "_client", "httpClient", "client" };
        foreach (var fieldName in possibleFieldNames)
        {
            var field = transportType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(HttpClient))
            {
                httpClient = field.GetValue(transport) as HttpClient;
                Console.WriteLine($"? DEBUG: Found HttpClient in field: {fieldName}");
                break;
            }
        }
        
        // Approach 2: Look for HttpClient properties
        if (httpClient == null)
        {
            var possiblePropertyNames = new[] { "HttpClient", "Client" };
            foreach (var propName in possiblePropertyNames)
            {
                var prop = transportType.GetProperty(propName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (prop != null && prop.PropertyType == typeof(HttpClient) && prop.CanRead)
                {
                    httpClient = prop.GetValue(transport) as HttpClient;
                    Console.WriteLine($"? DEBUG: Found HttpClient in property: {propName}");
                    break;
                }
            }
        }
        
        // Approach 3: Look for any field/property that contains an HttpClient
        if (httpClient == null)
        {
            Console.WriteLine($"?? DEBUG: Searching all fields for HttpClient...");
            var allFields = transportType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in allFields)
            {
                Console.WriteLine($"  ?? DEBUG: Field {field.Name}: {field.FieldType.Name}");
                if (field.FieldType == typeof(HttpClient))
                {
                    httpClient = field.GetValue(transport) as HttpClient;
                    Console.WriteLine($"? DEBUG: Found HttpClient in field: {field.Name}");
                    break;
                }
            }
        }
        
        // Approach 4: Search all properties
        if (httpClient == null)
        {
            Console.WriteLine($"?? DEBUG: Searching all properties for HttpClient...");
            var allProperties = transportType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in allProperties)
            {
                Console.WriteLine($"  ?? DEBUG: Property {prop.Name}: {prop.PropertyType.Name}");
                if (prop.PropertyType == typeof(HttpClient) && prop.CanRead)
                {
                    try
                    {
                        httpClient = prop.GetValue(transport) as HttpClient;
                        if (httpClient != null)
                        {
                            Console.WriteLine($"? DEBUG: Found HttpClient in property: {prop.Name}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"?? DEBUG: Exception accessing property {prop.Name}: {ex.Message}");
                    }
                }
            }
        }

        if (httpClient != null)
        {
            Console.WriteLine($"?? DEBUG: HttpClient found, attempting to add authentication header...");
            // Add the authentication header
            try
            {
                // Check if header already exists
                if (httpClient.DefaultRequestHeaders.Contains("X-MCP-API-Key"))
                {
                    Console.WriteLine($"?? DEBUG: Removing existing X-MCP-API-Key header...");
                    httpClient.DefaultRequestHeaders.Remove("X-MCP-API-Key");
                }
                
                httpClient.DefaultRequestHeaders.Add("X-MCP-API-Key", apiKey);
                Console.WriteLine($"? DEBUG: Successfully added X-MCP-API-Key header to HttpClient");
                
                // Log all headers for debugging
                Console.WriteLine($"?? DEBUG: All HttpClient headers after injection:");
                foreach (var header in httpClient.DefaultRequestHeaders)
                {
                    var headerValue = header.Key.Contains("Key", StringComparison.OrdinalIgnoreCase) ? "***REDACTED***" : string.Join(", ", header.Value);
                    Console.WriteLine($"  {header.Key}: {headerValue}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? DEBUG: Failed to add header: {ex.Message}");
                throw;
            }
        }
        else
        {
            Console.WriteLine($"? DEBUG: Could not find HttpClient in transport");
            // Print debugging information
            Console.WriteLine("?? DEBUG: Available fields:");
            foreach (var field in transportType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
            {
                Console.WriteLine($"  - {field.Name}: {field.FieldType.Name}");
            }
            
            Console.WriteLine("?? DEBUG: Available properties:");
            foreach (var prop in transportType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
            {
                Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
            }
            
            throw new InvalidOperationException("Could not find HttpClient in transport to inject headers");
        }
    }
}

/// <summary>
/// Alternative approach: Custom HTTP client factory
/// </summary>
public class AuthenticatedHttpClientFactory
{
    public static HttpClient CreateAuthenticatedClient(string apiKey)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("X-MCP-API-Key", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        
        Console.WriteLine($"Created authenticated HttpClient with API key: {apiKey[..Math.Min(4, apiKey.Length)]}...");
        return client;
    }
}

/// <summary>
/// Simple wrapper for demonstration and logging
/// </summary>
public class AuthenticatedHttpWrapper
{
    private readonly string _apiKey;
    private readonly string _endpoint;

    public AuthenticatedHttpWrapper(string endpoint, string apiKey)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
    }

    public HttpClientTransport CreateTransport()
    {
        Console.WriteLine($"?? DEBUG: AuthenticatedHttpWrapper.CreateTransport called");
        Console.WriteLine($"?? DEBUG: Endpoint: {_endpoint}");
        Console.WriteLine($"?? DEBUG: API Key length: {_apiKey?.Length ?? 0}");
        
        var transport = AuthenticatedHttpTransportFactory.Create(_endpoint, _apiKey);
        Console.WriteLine($"? DEBUG: Transport created and returned");
        return transport;
    }
}