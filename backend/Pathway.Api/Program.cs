using Microsoft.EntityFrameworkCore;
using Pathway.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<PathwayDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Pathway")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:Pathway before using persistence.")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
