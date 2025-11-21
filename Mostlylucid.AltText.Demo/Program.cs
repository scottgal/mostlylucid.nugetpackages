using Mostlylucid.LlmAltText.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register AI-powered alt text generation
builder.Services.AddAltTextGeneration(options =>
{
    options.ModelPath = "./models";
    options.EnableDiagnosticLogging = true;
    options.MaxWords = 90;
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseCors();

app.UseRouting();
app.MapControllers();

// Default route to serve the HTML page
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();