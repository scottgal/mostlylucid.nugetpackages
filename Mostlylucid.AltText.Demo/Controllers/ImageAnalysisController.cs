using Microsoft.AspNetCore.Mvc;
using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.AltText.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageAnalysisController : ControllerBase
{
    private readonly IImageAnalysisService _imageAnalysisService;
    private readonly ILogger<ImageAnalysisController> _logger;

    public ImageAnalysisController(
        IImageAnalysisService imageAnalysisService,
        ILogger<ImageAnalysisController> logger)
    {
        _imageAnalysisService = imageAnalysisService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<IActionResult> AnalyzeImage(IFormFile image)
    {
        if (image == null || image.Length == 0) return BadRequest(new { error = "No image file provided" });

        // Validate image type
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(image.ContentType.ToLower()))
            return BadRequest(new { error = "Invalid image type. Allowed types: JPEG, PNG, GIF, WebP" });

        try
        {
            _logger.LogInformation("Analyzing image: {FileName}, Size: {Size} bytes",
                image.FileName, image.Length);

            using var stream = image.OpenReadStream();
            var result = await _imageAnalysisService.AnalyzeWithClassificationAsync(stream);

            return Ok(new
            {
                fileName = image.FileName,
                altText = result.AltText,
                extractedText = result.ExtractedText,
                contentType = result.ContentType.ToString(),
                contentTypeConfidence = result.ContentTypeConfidence,
                hasSignificantText = result.HasSignificantText,
                size = image.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image {FileName}", image.FileName);
            return StatusCode(500, new { error = "An error occurred while analyzing the image", details = ex.Message });
        }
    }

    [HttpPost("alt-text")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> GenerateAltText(IFormFile image,
        [FromQuery] string taskType = "MORE_DETAILED_CAPTION")
    {
        if (image == null || image.Length == 0) return BadRequest(new { error = "No image file provided" });

        try
        {
            _logger.LogInformation("Generating alt text for image: {FileName}", image.FileName);

            using var stream = image.OpenReadStream();
            var altText = await _imageAnalysisService.GenerateAltTextAsync(stream, taskType);

            return Ok(new
            {
                fileName = image.FileName,
                altText,
                taskType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating alt text for {FileName}", image.FileName);
            return StatusCode(500, new { error = "An error occurred while generating alt text", details = ex.Message });
        }
    }

    [HttpPost("ocr")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ExtractText(IFormFile image)
    {
        if (image == null || image.Length == 0) return BadRequest(new { error = "No image file provided" });

        try
        {
            _logger.LogInformation("Extracting text from image: {FileName}", image.FileName);

            using var stream = image.OpenReadStream();
            var extractedText = await _imageAnalysisService.ExtractTextAsync(stream);

            return Ok(new
            {
                fileName = image.FileName, extractedText
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from {FileName}", image.FileName);
            return StatusCode(500, new { error = "An error occurred while extracting text", details = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "Image Analysis API" });
    }
}