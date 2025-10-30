using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WebApi.Common;
using WebApi.Rag;
using WebApi.Rag.Api;
using WebApi.Rag.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<AppSettings>(builder.Configuration);
builder.AddInfra();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<RagDbContext>();
dbContext.Database.Migrate();

app.UseSwagger();
app.UseSwaggerUI();
app.MapRag();

app.Run();