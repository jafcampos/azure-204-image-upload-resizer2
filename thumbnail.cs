// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;

namespace image_upload_resizer2
{
    public static class Thumbnail
    {
        [FunctionName("HttpFunction")]
        public static IActionResult Run3(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,         
            ILogger log)
        {
            string connectionString = Environment.GetEnvironmentVariable("StorageConnectionString");
            log.LogInformation(connectionString);

            return new OkObjectResult($"Welcome to Azure Functions, {req.Query["name"]}!");

        }
        // [FunctionName("EventGridNotification")]
        // public static void Run(
        //     [EventGridTrigger] EventGridEvent eventGridEvent,
        //     ILogger log
        //     )
        // {
        //     log.LogInformation("Event data:\n{0}", eventGridEvent.Data.ToString());
        // }
        [FunctionName("Thumbnail")]
        public static async Task Run2([BlobTrigger("images/{name}", Connection = "ImageBlobStorage")] BlobClient myBlob,
         string name,
         [Blob("images/{name}", FileAccess.Read, Connection = "ImageBlobStorage")] Stream inputBlob,
         ILogger log)
        {

            log.LogInformation($"Blob\n Name:{name} \n Uri: {myBlob.Uri}");
            if (inputBlob != null) log.LogInformation($"Input file \n Name:{name} \n Size: {inputBlob.Length} Bytes");

            try{

                var extension = Path.GetExtension(name);
                var encoder = GetEncoder(extension);
                log.LogInformation("Blob\n Extension: {extension}\n Encoder:{encoder}",extension,encoder);


                var thumbnailWidth = Convert.ToInt32(Environment.GetEnvironmentVariable("THUMBNAIL_WIDTH",EnvironmentVariableTarget.Process));
                log.LogInformation("THUMBNAIL_WIDTH:{0}",thumbnailWidth.ToString());
                var thumbContainerName = Environment.GetEnvironmentVariable("THUMBNAIL_CONTAINER_NAME");
                log.LogInformation("THUMBNAIL_CONTAINER_NAME:{0}",thumbContainerName);
                string connectionString = Environment.GetEnvironmentVariable("ImageBlobStorage");
                log.LogInformation("AzureWebJobsStorage:{0}",connectionString);
                
                var blobServiceClient = new BlobServiceClient(connectionString);

                var blobContainerClient = blobServiceClient.GetBlobContainerClient(thumbContainerName);
                var blobName = name;

                using (var output = new MemoryStream())
                using (Image<Rgba32> image = (Image<Rgba32>)Image.Load(inputBlob))
                {
                    var divisor = image.Width / thumbnailWidth;
                    var height = Convert.ToInt32(Math.Round((decimal)(image.Height / divisor)));

                    image.Mutate(x => x.Resize(thumbnailWidth, height));
                    image.Save(output, encoder);
                    output.Position = 0;
                    await blobContainerClient.UploadBlobAsync(blobName, output);
                }


            }catch (Exception ex)
            {
                log.LogError(ex.Message,ex);
            }
        }
        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var uri = new Uri(bloblUrl);
            var blobClient = new BlobClient(uri);
            return blobClient.Name;
        }
        private static IImageEncoder GetEncoder(string extension)
        {
            IImageEncoder encoder = null;

            extension = extension.Replace(".", "");

            var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            if (isSupported)
            {
                switch (extension.ToLower())
                {
                    case "png":
                        encoder = new PngEncoder();
                        break;
                    case "jpg":
                        encoder = new JpegEncoder();
                        break;
                    case "jpeg":
                        encoder = new JpegEncoder();
                        break;
                    case "gif":
                        encoder = new GifEncoder();
                        break;
                    default:
                        break;
                }
            }

            return encoder;
        }
    }

}
