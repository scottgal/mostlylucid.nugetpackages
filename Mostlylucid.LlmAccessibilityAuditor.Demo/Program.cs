using Mostlylucid.LlmAccessibilityAuditor.Extensions;
using Mostlylucid.LlmAccessibilityAuditor.Middleware;
using Mostlylucid.LlmAccessibilityAuditor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add accessibility auditor with Ollama
builder.Services.AddAccessibilityAuditor(options =>
{
    options.Enabled = true;
    options.OnlyInDevelopment = false; // Enable for demo
    options.EnableLlmAnalysis = true;
    options.EnableInlineReport = true;
    options.EnableDiagnosticEndpoint = true;
    options.EnableDiagnosticLogging = true;

    // Ollama configuration
    options.Ollama.Endpoint = builder.Configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434";
    options.Ollama.Model = builder.Configuration.GetValue<string>("Ollama:Model") ?? "llama3.2:3b";
    options.Ollama.TimeoutSeconds = 120;
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

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Enable developer exception page in development
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseCors();

// Use accessibility audit middleware
app.UseAccessibilityAudit();

// Map diagnostic endpoints
app.MapAccessibilityDiagnostics();

// Demo pages
app.MapGet("/", () => Results.Redirect("/index.html"));

// API endpoint to audit HTML directly
app.MapPost("/api/audit", async (HttpRequest request, IAccessibilityAuditor auditor) =>
{
    using var reader = new StreamReader(request.Body);
    var html = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(html)) return Results.BadRequest(new { error = "HTML content is required" });

    var report = await auditor.AuditAsync(html, "direct-api-audit");
    return Results.Ok(report);
});

// API endpoint for quick audit (no LLM)
app.MapPost("/api/audit/quick", async (HttpRequest request, IAccessibilityAuditor auditor) =>
{
    using var reader = new StreamReader(request.Body);
    var html = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(html)) return Results.BadRequest(new { error = "HTML content is required" });

    var result = await auditor.QuickAuditAsync(html);
    return Results.Ok(result);
});

// Health check
app.MapGet("/api/health", async (IAccessibilityAuditor auditor) =>
{
    var isReady = await auditor.IsReadyAsync();
    return Results.Ok(new { ready = isReady, status = isReady ? "healthy" : "unhealthy" });
});

// Sample pages with varying accessibility issues
app.MapGet("/demo/good", () => Results.Content(GetGoodPage(), "text/html"));
app.MapGet("/demo/bad", () => Results.Content(GetBadPage(), "text/html"));
app.MapGet("/demo/mixed", () => Results.Content(GetMixedPage(), "text/html"));

app.Run();

static string GetGoodPage()
{
    return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Accessible Page Example</title>
</head>
<body>
    <a href=""#main"" class=""sr-only"">Skip to main content</a>

    <header>
        <nav aria-label=""Main navigation"">
            <ul>
                <li><a href=""/"">Home</a></li>
                <li><a href=""/about"">About</a></li>
                <li><a href=""/contact"">Contact</a></li>
            </ul>
        </nav>
    </header>

    <main id=""main"">
        <h1>Welcome to Our Accessible Website</h1>

        <section aria-labelledby=""about-heading"">
            <h2 id=""about-heading"">About Us</h2>
            <p>We are committed to making our website accessible to everyone.</p>
            <img src=""/images/team.jpg"" alt=""Our diverse team of 5 people standing together in the office"">
        </section>

        <section aria-labelledby=""contact-heading"">
            <h2 id=""contact-heading"">Contact Us</h2>
            <form>
                <div>
                    <label for=""name"">Your Name:</label>
                    <input type=""text"" id=""name"" name=""name"" required>
                </div>
                <div>
                    <label for=""email"">Email Address:</label>
                    <input type=""email"" id=""email"" name=""email"" required>
                </div>
                <div>
                    <label for=""message"">Message:</label>
                    <textarea id=""message"" name=""message"" rows=""4"" required></textarea>
                </div>
                <button type=""submit"">Send Message</button>
            </form>
        </section>

        <section aria-labelledby=""data-heading"">
            <h2 id=""data-heading"">Our Statistics</h2>
            <table>
                <caption>Quarterly Performance</caption>
                <thead>
                    <tr>
                        <th scope=""col"">Quarter</th>
                        <th scope=""col"">Revenue</th>
                        <th scope=""col"">Growth</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td>Q1</td>
                        <td>$1.2M</td>
                        <td>12%</td>
                    </tr>
                    <tr>
                        <td>Q2</td>
                        <td>$1.5M</td>
                        <td>25%</td>
                    </tr>
                </tbody>
            </table>
        </section>
    </main>

    <footer>
        <p>&copy; 2024 Accessible Company. All rights reserved.</p>
    </footer>
</body>
</html>";
}

static string GetBadPage()
{
    return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
</head>
<body>
    <div class=""header"">
        <div onclick=""location.href='/'""></div>
        <div onclick=""alert('menu')"" class=""menu-btn""></div>
    </div>

    <h1>Welcome</h1>
    <h4>About Our Company</h4>
    <h2>What We Do</h2>

    <img src=""/logo.png"">
    <img src=""/banner.jpg"">
    <img src=""/team.png"" alt="""">

    <form>
        <input type=""text"" placeholder=""Enter your name"">
        <input type=""email"" placeholder=""Enter your email"">
        <select>
            <option>Choose an option</option>
            <option>Option 1</option>
        </select>
        <button></button>
    </form>

    <a href=""/read-more""></a>
    <a href=""/download""><img src=""/download-icon.png""></a>

    <button aria-label="""">
        <svg></svg>
    </button>

    <table>
        <tr>
            <td>Name</td>
            <td>Age</td>
            <td>City</td>
        </tr>
        <tr>
            <td>John</td>
            <td>30</td>
            <td>NYC</td>
        </tr>
    </table>

    <h1>Another Main Heading</h1>

    <p class=""text-gray-400"">This text might be hard to read due to low contrast.</p>
    <span class=""opacity-50"">This text uses opacity that may affect readability.</span>
</body>
</html>";
}

static string GetMixedPage()
{
    return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Mixed Accessibility Example</title>
</head>
<body>
    <header>
        <nav>
            <a href=""/"">Home</a>
            <a href=""/about"">About</a>
            <button onclick=""toggleMenu()"">Menu</button>
        </nav>
    </header>

    <h1>Welcome to Our Site</h1>
    <h4>Quick Links</h4>

    <main>
        <section>
            <h2>Featured Products</h2>
            <div class=""product"">
                <img src=""/product1.jpg"" alt=""Blue wireless headphones with noise cancellation"">
                <h3>Wireless Headphones</h3>
                <p>Great sound quality!</p>
                <button>Add to Cart</button>
            </div>
            <div class=""product"">
                <img src=""/product2.jpg"">
                <h3>Smart Watch</h3>
                <p class=""text-gray-500"">Track your fitness goals.</p>
                <a href=""/buy""></a>
            </div>
        </section>

        <section>
            <h2>Contact Form</h2>
            <form>
                <label for=""contact-name"">Name:</label>
                <input type=""text"" id=""contact-name"" name=""name"">

                <input type=""email"" placeholder=""Your email"">

                <button type=""submit"">Submit</button>
            </form>
        </section>

        <section>
            <h2>Our Team</h2>
            <table>
                <tr>
                    <th>Name</th>
                    <th>Role</th>
                </tr>
                <tr>
                    <td>Alice</td>
                    <td>Developer</td>
                </tr>
            </table>
        </section>
    </main>

    <footer>
        <p>&copy; 2024 Mixed Company</p>
        <div onclick=""location.href='/privacy'"" style=""cursor:pointer"">Privacy Policy</div>
    </footer>
</body>
</html>";
}