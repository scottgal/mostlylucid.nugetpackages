# Mostlylucid.GeoDetection.Contributor

A bot detection contributor that provides detailed geographic location analysis for request validation. This package bridges `Mostlylucid.GeoDetection` with `Mostlylucid.BotDetection` to enable geo-based bot detection.

## Features

- **Geographic Location Signals**: Provides country, region, city, and coordinates for requests
- **Geo-Inconsistency Detection**: Detects bots claiming to be from one location but IP suggests another
- **Bot Verification**: Validates that claimed bots (e.g., Googlebot) originate from expected geographic regions
- **VPN/Proxy Detection**: Flags requests from known VPN/proxy/hosting providers
- **Timezone Validation**: Detects mismatches between claimed timezone and IP-based location

## Installation

```bash
dotnet add package Mostlylucid.GeoDetection.Contributor
```

## Usage

```csharp
// In Program.cs or Startup.cs
services.AddGeoLocationServices(options =>
{
    options.DefaultProvider = GeoProvider.MaxMind;
    options.GeoLite2DatabasePath = "/path/to/GeoLite2-City.mmdb";
});

services.AddBotDetection(options => { /* ... */ });

// Add the geo contributor
services.AddGeoDetectionContributor(options =>
{
    options.EnableBotVerification = true;
    options.EnableInconsistencyDetection = true;
    options.SuspiciousCountries = ["CN", "RU", "KP"]; // Optional
});
```

## Signals Emitted

The contributor emits the following signals to the blackboard:

| Signal Key | Type | Description |
|------------|------|-------------|
| `geo.country_code` | string | ISO 3166-1 alpha-2 country code |
| `geo.country_name` | string | Full country name |
| `geo.region_code` | string | Region/state code |
| `geo.city` | string | City name |
| `geo.latitude` | double | Latitude coordinate |
| `geo.longitude` | double | Longitude coordinate |
| `geo.timezone` | string | Timezone (e.g., "America/New_York") |
| `geo.is_vpn` | bool | Whether IP is known VPN |
| `geo.is_hosting` | bool | Whether IP is from hosting provider |
| `geo.continent_code` | string | Continent code (e.g., "NA", "EU") |

## Geo-Inconsistency Detection

The contributor detects several types of geo-based inconsistencies:

### Bot Origin Verification

Known search engine bots should originate from specific countries:

| Bot | Expected Countries |
|-----|-------------------|
| Googlebot | US |
| Bingbot | US |
| Yandex | RU |
| Baidu | CN |

If a User-Agent claims to be Googlebot but the IP is from China, this is flagged as highly suspicious.

### Timezone Mismatches

If the Accept-Language header suggests a specific locale (e.g., "en-US") but the IP originates from a different timezone, this is flagged.

### Datacenter + Consumer Locale

Browser User-Agents claiming consumer locales from datacenter IPs are flagged.

## Configuration Options

```csharp
public class GeoContributorOptions
{
    // Enable verification that known bots come from expected countries
    public bool EnableBotVerification { get; set; } = true;

    // Enable geo-inconsistency detection
    public bool EnableInconsistencyDetection { get; set; } = true;

    // Countries to flag as suspicious (higher weight)
    public List<string> SuspiciousCountries { get; set; } = [];

    // Countries to always trust (lower weight)
    public List<string> TrustedCountries { get; set; } = [];

    // Priority for this detector (lower = runs earlier)
    public int Priority { get; set; } = 15;
}
```

## License

MIT
