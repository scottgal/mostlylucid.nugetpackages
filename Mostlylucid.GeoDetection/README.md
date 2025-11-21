# Mostlylucid.GeoDetection

> **Note**: These packages are provided as-is. I'll get them working good enough to release but I can't commit to support. However they are Unlicense so have at it!

Geographic location detection and routing middleware for ASP.NET Core applications with country-based routing and IP
geolocation.

## Installation

```bash
dotnet add package Mostlylucid.GeoDetection
```

## Quick Start

### 1. Configure Services

```csharp
using Mostlylucid.GeoDetection;

var builder = WebApplication.CreateBuilder(args);

// Add geo detection services
builder.Services.AddGeoDetection(options =>
{
    options.AllowedCountries = new[] { "US", "CA", "GB", "DE", "FR" };
    options.DefaultAction = GeoAction.Allow;
});

var app = builder.Build();

// Use geo routing middleware
app.UseGeoRouting();

app.Run();
```

### 2. Configure Settings (Optional)

Add to your `appsettings.json`:

```json
{
  "GeoRouting": {
    "AllowedCountries": ["US", "CA", "GB", "DE", "FR"],
    "BlockedCountries": [],
    "DefaultAction": "Allow",
    "CacheExpirationMinutes": 60,
    "EnableLogging": true
  }
}
```

## Features

### Country-Based Routing

Route or block requests based on the visitor's country:

```csharp
// Allow only specific countries
options.AllowedCountries = new[] { "US", "CA" };

// Or block specific countries
options.BlockedCountries = new[] { "XX", "YY" };
```

### GeoRoute Attribute

Apply geographic restrictions to specific endpoints:

```csharp
[GeoRoute(AllowedCountries = new[] { "US", "CA" })]
public class NorthAmericaController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

[GeoRoute(BlockedCountries = new[] { "XX" })]
public IActionResult RestrictedAction()
{
    return View();
}
```

### Access Location Data

```csharp
public class MyController : Controller
{
    private readonly IGeoLocationService _geoService;

    public MyController(IGeoLocationService geoService)
    {
        _geoService = geoService;
    }

    public async Task<IActionResult> Index()
    {
        var location = await _geoService.GetLocationAsync(HttpContext);

        ViewBag.Country = location?.CountryCode;
        ViewBag.Region = location?.Region;
        ViewBag.City = location?.City;

        return View();
    }
}
```

### Endpoint Extension Methods

```csharp
app.MapGet("/us-only", () => "US Only Content")
   .RequireGeoRoute(allowedCountries: new[] { "US" });

app.MapGet("/eu-content", () => "EU Content")
   .RequireGeoRoute(allowedCountries: new[] { "DE", "FR", "IT", "ES", "NL" });
```

## Configuration Options

| Option                   | Type      | Default | Description                                      |
|--------------------------|-----------|---------|--------------------------------------------------|
| `AllowedCountries`       | string[]  | []      | Countries allowed to access (ISO 3166-1 alpha-2) |
| `BlockedCountries`       | string[]  | []      | Countries blocked from access                    |
| `DefaultAction`          | GeoAction | Allow   | Action when country not in lists                 |
| `CacheExpirationMinutes` | int       | 60      | Cache duration for location lookups              |
| `EnableLogging`          | bool      | true    | Enable logging of geo routing decisions          |
| `RedirectUrl`            | string    | null    | URL to redirect blocked users                    |
| `BlockedStatusCode`      | int       | 403     | HTTP status code for blocked requests            |

## GeoLocation Model

```csharp
public class GeoLocation
{
    public string? IpAddress { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Timezone { get; set; }
}
```

## Notes

- Uses IP address from request headers (supports X-Forwarded-For, CF-Connecting-IP)
- Country codes follow ISO 3166-1 alpha-2 standard
- Results are cached to improve performance
- Works with reverse proxies and CDNs

## License

Unlicense - Public Domain

## Links

- [GitHub Repository](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.GeoDetection)
- [NuGet Package](https://www.nuget.org/packages/Mostlylucid.GeoDetection/)
