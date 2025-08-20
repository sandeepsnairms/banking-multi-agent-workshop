namespace MultiAgentCopilot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // The WebApplication.CreateBuilder automatically sets up configuration
            // but let's ensure the development file is loaded properly
            var environment = builder.Environment.EnvironmentName;
            Console.WriteLine($"Running in environment: {environment}");
            
            // WebApplication.CreateBuilder already adds appsettings.json and appsettings.{Environment}.json
            // but we can verify configuration values are loaded
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Configuration.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
            builder.Configuration.AddEnvironmentVariables();

            // Debug configuration loading
            Console.WriteLine($"CosmosDBSettings:CosmosUri = {builder.Configuration["CosmosDBSettings:CosmosUri"]}");
            Console.WriteLine($"SemanticKernelServiceSettings:AzureOpenAISettings:Endpoint = {builder.Configuration["SemanticKernelServiceSettings:AzureOpenAISettings:Endpoint"]}");

            // Add CORS policy
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
            });

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            if (!builder.Environment.IsDevelopment())                
                builder.Services.AddApplicationInsightsTelemetry();

            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Services.Configure<LoggerFilterOptions>(options =>
            {
                options.MinLevel = LogLevel.Trace;
            });

            builder.AddCosmosDBService();
            builder.AddSemanticKernelService();

            builder.AddChatService();
            builder.Services.AddScoped<ChatEndpoints>();

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            app.UseCors("AllowAllOrigins");

            app.UseExceptionHandler(exceptionHandlerApp
                    => exceptionHandlerApp.Run(async context
                        => await Results.Problem().ExecuteAsync(context)));

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseAuthorization();

            // Map the chat REST endpoints:
            using (var scope = app.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetService<MultiAgentCopilot.ChatEndpoints>();
                service?.Map(app);
            }

            app.Run();
        }
    }
}
