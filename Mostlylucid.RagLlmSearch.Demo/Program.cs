using Mostlylucid.RagLlmSearch.Configuration;
using Mostlylucid.RagLlmSearch.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "RAG LLM Search Demo", Version = "v1" });
});

// Add RAG LLM Search services
builder.Services.AddRagLlmSearch(
    options =>
    {
        // Configure from appsettings or use defaults
        builder.Configuration.GetSection("RagLlmSearch").Bind(options);

        // Override with defaults if not configured
        options.OllamaEndpoint = builder.Configuration["RagLlmSearch:OllamaEndpoint"] ?? "http://localhost:11434";
        options.ChatModel = builder.Configuration["RagLlmSearch:ChatModel"] ?? "llama3.2";
        options.EmbeddingModel = builder.Configuration["RagLlmSearch:EmbeddingModel"] ?? "nomic-embed-text";
        options.DatabasePath = builder.Configuration["RagLlmSearch:DatabasePath"] ?? "ragllmsearch_demo.db";
    },
    searchProviders =>
    {
        builder.Configuration.GetSection("SearchProviders").Bind(searchProviders);

        // Configure search providers from environment or appsettings
        searchProviders.Brave.ApiKey = builder.Configuration["SearchProviders:Brave:ApiKey"];
        searchProviders.Tavily.ApiKey = builder.Configuration["SearchProviders:Tavily:ApiKey"];
        searchProviders.SerpApi.ApiKey = builder.Configuration["SearchProviders:SerpApi:ApiKey"];
    });

var app = builder.Build();

// Initialize database
await app.InitializeRagLlmSearchAsync();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapChatHub("/chathub");

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
