//using BankingAPI.Interfaces;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.Extensions.DependencyInjection;


//namespace BankingAPI
//{
//    public class Program
//    {
//        public static void Main(string[] args)
//        {

//            var builder = WebApplication.CreateBuilder(args);

//            if (!builder.Environment.IsDevelopment())
//                builder.Services.AddApplicationInsightsTelemetry();



//            builder.AddChatService();
//            builder.Services.AddScoped<BankingAPI.Controllers.BankController>();

//            // Add services to the container.
//            builder.Services.AddAuthorization();

//            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//            builder.Services.AddEndpointsApiExplorer();
//            builder.Services.AddSwaggerGen();

//            var app = builder.Build();

//            app.UseExceptionHandler(exceptionHandlerApp
//                    => exceptionHandlerApp.Run(async context
//                        => await Results.Problem().ExecuteAsync(context)));

//            // Configure the HTTP request pipeline.
//            app.UseSwagger();
//            app.UseSwaggerUI();

//            app.UseAuthorization();

//            // Map the chat REST endpoints:
//            using (var scope = app.Services.CreateScope())
//            {
//                var service = scope.ServiceProvider.GetService<BankingAPI.Controllers.BankController>();
//                service?.Map(app);
//            }

//            app.Run();
//        }
//    }
//}
