var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Orleans silo configuration
builder.UseOrleans(siloBuilder =>
{
    siloBuilder.AddActivityPropagation();
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
