using hangfire.Controllers;
using Hangfire;
using Hangfire.SqlServer;
using System.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("HangfireConnection");
builder.Services.AddSwaggerGen(); // Adds Swagger UI

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          // Use SQL Server storage (or MemoryStorage for testing)
          .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection"),
              new SqlServerStorageOptions
              {
                  PrepareSchemaIfNecessary = true,
                  QueuePollInterval = TimeSpan.FromSeconds(15)
              });
});
builder.Services.AddScoped<IChunkProcessor, ChunkProcessor>();
builder.Services.AddScoped<IReportGenerator, ReportGenerator>();
builder.Services.AddScoped<IDataRepository, SqlDataRepository>();
//builder.Services.AddScoped<ReportGenerator>();
// 2️⃣ Add Hangfire server
builder.Services.AddHangfireServer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("Content-Disposition");
        });
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");
app.UseAuthorization();
app.UseHangfireDashboard(); // 3️⃣ Add Hangfire Dashboard

app.MapControllers();

app.Run();
