using FileRenamer.Api.Interfaces;
using FileRenamer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("VueJsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
builder.Services.AddControllers();

builder.Services.AddScoped<IFileRenamingService, FileRenamingService>();
builder.Services.AddScoped<ITvDbService, TvDbService>();
builder.Services.AddScoped<IOpenSubtitlesService, OpenSubtitlesService>();

builder.Services.AddLogging();
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseCors("VueJsPolicy");

app.UseAuthorization();

app.MapControllers();

app.Run();
