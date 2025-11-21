using Microsoft.AspNetCore.Mvc;
using Mostlylucid.GeoDetection.Extensions;
using Mostlylucid.GeoDetection.Filters;

namespace Mostlylucid.BotDetection.Demo.Examples;

/// <summary>
///     Examples of country-based routing
/// </summary>
public static class GeoRoutingExamples
{
    /// <summary>
    ///     Example 1: Simple country-based content serving
    /// </summary>
    public static void MapSimpleCountryContent(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", (HttpContext context) =>
        {
            var country = context.GetCountryCode();

            return country switch
            {
                "CN" => Results.Content("<h1>欢迎来到我们的网站</h1><p>中国访客专用内容</p>", "text/html"),
                "RU" => Results.Content("<h1>Добро пожаловать</h1><p>Свободу Украине!</p>", "text/html"),
                "FR" => Results.Content("<h1>Bienvenue</h1><p>Contenu pour les visiteurs français</p>", "text/html"),
                _ => Results.Content("<h1>Welcome</h1><p>Default English content</p>", "text/html")
            };
        });
    }

    /// <summary>
    ///     Example 2: Route to different pages by country
    /// </summary>
    public static void MapCountrySpecificPages(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/products", (HttpContext context) =>
        {
            var country = context.GetCountryCode();

            return country switch
            {
                "CN" => Results.Redirect("/cn/products"),
                "JP" => Results.Redirect("/jp/products"),
                "DE" => Results.Redirect("/de/products"),
                _ => Results.Redirect("/en/products")
            };
        });
    }

    /// <summary>
    ///     Example 3: Using ServeByCountry helper
    /// </summary>
    public static void MapWithServeByCountry(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/offer", () => "Default offer")
            .ServeByCountry(new Dictionary<string, Func<Task<IResult>>>
                {
                    ["CN"] = () => Task.FromResult<IResult>(Results.Content("Special offer for China: 50% off!")),
                    ["US"] = () =>
                        Task.FromResult<IResult>(Results.Content("Special offer for USA: Buy one get one free!")),
                    ["GB"] = () => Task.FromResult<IResult>(Results.Content("Special offer for UK: Free shipping!"))
                },
                () => Task.FromResult<IResult>(Results.Content("Standard offer: 10% off")));
    }

    /// <summary>
    ///     Example 4: Redirect by country helper
    /// </summary>
    public static void MapWithRedirectByCountry(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/store", () => "Store")
            .RedirectByCountry(new Dictionary<string, string>
                {
                    ["CN"] = "https://china.store.com",
                    ["EU"] = "https://eu.store.com",
                    ["US"] = "https://us.store.com"
                },
                "https://global.store.com");
    }

    /// <summary>
    ///     Example 5: Country-based API responses
    /// </summary>
    public static void MapCountryBasedApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/pricing", (HttpContext context) =>
        {
            var country = context.GetCountryCode();

            var pricing = country switch
            {
                "CN" => new { price = 99, currency = "CNY", tax = 0.13 },
                "GB" => new { price = 12, currency = "GBP", tax = 0.20 },
                "US" => new { price = 15, currency = "USD", tax = 0.08 },
                _ => new { price = 15, currency = "USD", tax = 0.00 }
            };

            return Results.Json(pricing);
        });
    }

    /// <summary>
    ///     Example 6: Block China but show specific message
    /// </summary>
    public static void MapWithChinaMessage(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/restricted", (HttpContext context) =>
        {
            var country = context.GetCountryCode();

            if (country == "CN")
                return Results.Content(
                    "<h1>访问受限</h1><p>此内容在您所在地区不可用</p>" +
                    "<p>请访问我们的中国站点: <a href='https://cn.example.com'>cn.example.com</a></p>",
                    "text/html",
                    statusCode: 451
                );

            return Results.Content("<h1>Restricted Content</h1><p>Available content here</p>", "text/html");
        });
    }

    /// <summary>
    ///     Example 7: Using MapByCountry for grouped routing
    /// </summary>
    public static void MapGroupedByCountry(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapByCountry("/landing", routes =>
        {
            routes.ForCountry("CN", (HttpContext ctx) =>
                    Task.FromResult<IResult>(Results.Content("Chinese landing page")))
                .ForCountry("US", (HttpContext ctx) =>
                    Task.FromResult<IResult>(Results.Content("US landing page")))
                .Default((HttpContext ctx) =>
                    Task.FromResult<IResult>(Results.Content("Default landing page")));
        });
    }
}

/// <summary>
///     MVC Controller examples
/// </summary>
[ApiController]
[Route("api/[controller]")]
// Changed from ControllerBase to Controller to enable View() support
public class GeoController : Controller
{
    /// <summary>
    ///     Example: MVC action with GeoRoute attribute
    /// </summary>
    [HttpGet("home")]
    [GeoRoute(CountryViews = "CN:home-cn,RU:home-ru", DefaultView = "home-default")]
    public IActionResult Home()
    {
        // View will be automatically selected based on country
        return View();
    }

    /// <summary>
    ///     Example: Different actions by country
    /// </summary>
    [HttpGet("shop")]
    [GeoRoute(CountryActions = "CN:ShopChina,US:ShopUSA")]
    public IActionResult Shop()
    {
        return View();
    }

    /// <summary>
    ///     Example: Redirect to country-specific site
    /// </summary>
    [HttpGet("redirect")]
    [GeoRoute(CountryRoutes = "CN:/cn/home,FR:/fr/accueil")]
    public IActionResult Redirect()
    {
        return View();
    }

    /// <summary>
    ///     Example: Serve different content by country
    /// </summary>
    [HttpGet("offer")]
    [ServeByCountry("CN:<h1>中国特别优惠</h1>", "US:<h1>US Special Offer</h1>")]
    public IActionResult Offer()
    {
        return Content("<h1>Default Offer</h1>");
    }

    /// <summary>
    ///     Example: Manual country checking
    /// </summary>
    [HttpGet("manual")]
    public IActionResult ManualCountryCheck()
    {
        var country = HttpContext.GetCountryCode();

        if (country == "CN") return Content("Chinese content", "text/html; charset=utf-8");

        return Content("Default content");
    }
}