# Mostlylucid Alt Text Demo

A demonstration of automatic alt text generation and OCR using Microsoft Florence-2 vision model in C#.

## Features

- **AI-Powered Alt Text Generation**: Generate descriptive alt text for images using Florence-2
- **OCR**: Extract text content from images
- **Modern Web UI**: Drag-and-drop interface for easy image upload
- **Multiple Task Types**: Support for different captioning styles (brief, detailed, more detailed)
- **Privacy-Focused**: All processing happens server-side without third-party API calls

## Technologies Used

- **ASP.NET Core 9.0**: Web framework
- **Florence2**: Microsoft's vision-language model via ONNX Runtime
- **SixLabors.ImageSharp**: Image processing
- **Modern JavaScript**: Drag-and-drop file handling

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- At least 4GB RAM (8GB+ recommended for optimal performance)
- Optional: CUDA-capable GPU for faster inference

### Installation

1. Clone the repository
2. Navigate to the demo directory:
   ```bash
   cd Mostlylucid.AltText.Demo
   ```

3. Restore dependencies:
   ```bash
   dotnet restore
   ```

4. Run the application:
   ```bash
   dotnet run
   ```

5. Open your browser to `https://localhost:5001` (or the URL shown in the console)

### First Run

On first run, the application will download the Florence-2 model files (~2.5GB). This may take several minutes depending
on your internet connection. Subsequent runs will use the cached models.

## API Endpoints

### POST /api/ImageAnalysis/analyze

Analyzes an image and returns both alt text and extracted text.

**Request**: Multipart form data with image file
**Response**:

```json
{
  "fileName": "example.jpg",
  "altText": "A scenic view of mountains...",
  "extractedText": "Any text found in the image",
  "size": 1234567
}
```

### POST /api/ImageAnalysis/alt-text

Generates only alt text for an image.

**Query Parameters**:

- `taskType`: Caption detail level (CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION)

### POST /api/ImageAnalysis/ocr

Extracts only text from an image.

## Model Information

This demo uses the Florence-2-base model from Microsoft Research. Florence-2 is a vision-language foundation model
capable of:

- Image captioning at multiple detail levels
- Optical Character Recognition (OCR)
- Object detection
- Visual grounding
- Dense captioning

### Hardware Requirements

- **CPU Mode**: Works on any modern CPU, but inference may be slow (5-10 seconds per image)
- **GPU Mode**: Requires CUDA-capable GPU for faster inference (~1-2 seconds per image)

### Model Storage

Models are downloaded to `./models` directory in the application folder. Ensure you have sufficient disk space (~2.5GB).

## Configuration

### Logging

Logging is configured in `Program.cs`. By default, console and debug logging are enabled.

### CORS

CORS is enabled for all origins in development mode. Adjust in `Program.cs` for production deployments.

### File Size Limits

The maximum upload size is set to 10MB. Modify the `[RequestSizeLimit]` attribute in the controller to change this.

## Usage Examples

### Basic Usage

1. Open the web interface
2. Drag and drop an image or click to browse
3. Wait for analysis to complete
4. View generated alt text and extracted text

### Programmatic Usage

```csharp
using Mostlylucid.AltText.Demo.Services;

// Create service
var service = new Florence2ImageAnalysisService(logger);

// Analyze image
using var imageStream = File.OpenRead("image.jpg");
var (altText, extractedText) = await service.AnalyzeImageAsync(imageStream);

Console.WriteLine($"Alt Text: {altText}");
Console.WriteLine($"OCR Text: {extractedText}");
```

## Performance Optimization

- **Model Caching**: The service is registered as a singleton to keep the model in memory
- **Stream Processing**: Images are processed as streams to minimize memory usage
- **Async/Await**: All operations are fully asynchronous for better scalability

## Troubleshooting

### Model Download Fails

- Check internet connection
- Ensure sufficient disk space
- Check firewall settings

### Out of Memory

- Reduce image size before upload
- Consider using a machine with more RAM
- Enable GPU mode if available

### Slow Performance

- Enable GPU mode for faster inference
- Consider using the smaller Florence-2-base model
- Reduce image resolution before processing

## License

This demo is part of the Mostlylucid project. See the main project license for details.

## Credits

- **Florence-2**: Microsoft Research
- **Florence2-Sharp**: Curiosity GmbH
- **ONNX Runtime**: Microsoft
