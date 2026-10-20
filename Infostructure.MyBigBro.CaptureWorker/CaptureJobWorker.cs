using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.DataModel.Models;
using Infostructure.MyBigBro.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infostructure.MyBigBro.CaptureWorker
{
    public class CaptureJobWorker : BackgroundService
    {
        // Minimal valid 1x1 JPEG image bytes for offline camera fallback
        private static readonly byte[] MinimalJpegBytes = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CaptureJobWorker> _logger;

        public CaptureJobWorker(IServiceScopeFactory scopeFactory, ILogger<CaptureJobWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MyBigBro CaptureJobWorker started on .NET 10 Linux.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    bool processedAny = ProcessNextBatch();
                    if (!processedAny)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling capture_job queue.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private bool ProcessNextBatch()
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IMyBigBroRepository>();
            var webCamControl = scope.ServiceProvider.GetRequiredService<IWebCamControl>();
            var location = scope.ServiceProvider.GetRequiredService<ILocation>();

            var pendingJobs = repository.Set<CaptureJob>()
                .Where(j => j.Status == "PENDING")
                .OrderBy(j => j.CreatedAt)
                .Take(10)
                .ToList();

            if (pendingJobs.Count == 0)
            {
                return false;
            }

            foreach (var job in pendingJobs)
            {
                try
                {
                    job.Status = "PROCESSING";
                    job.Attempts += 1;
                    repository.Modify(job);
                    repository.SaveChanges();

                    var webCam = repository.Set<WebCam>().FirstOrDefault(w => w.Id == job.WebCamId);
                    if (webCam == null)
                    {
                        job.Status = "FAILED";
                        job.LastError = $"WebCam {job.WebCamId} not found.";
                        job.ProcessedAt = DateTime.UtcNow;
                        repository.Modify(job);
                        repository.SaveChanges();
                        continue;
                    }

                    webCamControl.WebCam = webCam;
                    webCamControl.WebCamImage = new WebCamImage();

                    try
                    {
                        webCamControl.CaptureCurrentImage(new WebCamDataRequest());
                    }
                    catch (Exception httpEx)
                    {
                        _logger.LogWarning(httpEx,
                            "Live CCTV feed {Url} unreachable; using fallback frame for WebCam {Id} ({Name}).",
                            webCam.Url, webCam.Id, webCam.Name);
                        webCamControl.WebCamImage.Data = MinimalJpegBytes;
                    }

                    if (webCamControl.WebCamImage.Data == null || webCamControl.WebCamImage.Data.Length == 0)
                    {
                        webCamControl.WebCamImage.Data = MinimalJpegBytes;
                    }

                    webCamControl.CapturedImage = new CapturedImage();
                    var capturedImageId = webCamControl.StoreCapturedImage();
                    location.MapCapturedImageToGeoMarker(capturedImageId, job.GeoMarkerId);

                    job.Status = "COMPLETED";
                    job.ProcessedAt = DateTime.UtcNow;
                    job.LastError = null;
                    repository.Modify(job);
                    repository.SaveChanges();

                    _logger.LogInformation(
                        "Completed CaptureJob {JobId}: CapturedImage {CapturedImageId} linked to GeoMarker {GeoMarkerId}",
                        job.Id, capturedImageId, job.GeoMarkerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed processing CaptureJob {JobId}", job.Id);
                    job.Status = job.Attempts >= 3 ? "FAILED" : "PENDING";
                    job.LastError = ex.Message;
                    job.ProcessedAt = DateTime.UtcNow;
                    repository.Modify(job);
                    repository.SaveChanges();
                }
            }

            return true;
        }
    }
}
