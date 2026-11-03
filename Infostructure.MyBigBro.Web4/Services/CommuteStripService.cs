#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infostructure.MyBigBro.Web4.Services
{
    public class CameraFrameEntry
    {
        public string FrameId { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "image/jpeg";
    }

    public class CommuteCameraDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public string City { get; set; } = "auckland";
        public List<CommuteFrameMetaDto> Frames { get; set; } = new();
    }

    public class CommuteFrameMetaDto
    {
        public string FrameId { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class CommuteCorridorDto
    {
        public string Id { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InboundLabel { get; set; } = "Inbound (Towards City)";
        public string OutboundLabel { get; set; } = "Outbound (From City)";
        public List<CommuteCameraDto> Cameras { get; set; } = new();
    }

    public class SupportStatusDto
    {
        public decimal MonthlyCostUsd { get; set; } = 20m;
        public decimal CurrentFundedUsd { get; set; } = 5m;
        public decimal CoffeePriceUsd { get; set; } = 3m;
        public int SupporterCount { get; set; } = 2;
        public string SupportUrl { get; set; } = "https://buymeacoffee.com/onrampcam";
        public string ButtonLabel { get; set; } = "☕ Buy a Coffee ($3)";
        public string CurrentMonth { get; set; } = string.Empty;
    }

    public class CommuteStripService : BackgroundService
    {
        public const int MaxFramesPerCamera = 15;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

        private readonly ILogger<CommuteStripService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, List<CameraFrameEntry>> _frameBuffers = new();
        private readonly List<CommuteCorridorDto> _corridors;
        private readonly SemaphoreSlim _pollLock = new(1, 1);
        private readonly object _supportLock = new();
        private SupportStatusDto _supportStatus;
        private DateTime _lastLondonRefreshUtc = DateTime.MinValue;

        public CommuteStripService(ILogger<CommuteStripService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyBigBro-CommuteStrip/2.0 (+https://github.com/bernardoleary/MyBigBro-Azure)");
            _corridors = BuildDefaultCorridors();
            _supportStatus = LoadOrCreateSupportStatus();
        }

        public SupportStatusDto GetSupportStatus()
        {
            lock (_supportLock)
            {
                string currentMonth = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                if (_supportStatus.CurrentMonth != currentMonth)
                {
                    _supportStatus.CurrentMonth = currentMonth;
                    _supportStatus.CurrentFundedUsd = GetDefaultFundedUsd();
                    _supportStatus.SupporterCount = _supportStatus.CurrentFundedUsd > 0 ? 1 : 0;
                    SaveSupportStatus(_supportStatus);
                }

                // Always allow live override of URL and Monthly Cost via environment variables
                string? envUrl = Environment.GetEnvironmentVariable("ONRAMP_SUPPORT_URL");
                if (!string.IsNullOrWhiteSpace(envUrl))
                {
                    _supportStatus.SupportUrl = envUrl.Trim();
                }
                else if (string.IsNullOrWhiteSpace(_supportStatus.SupportUrl) ||
                         _supportStatus.SupportUrl.Contains("bernardoleary", StringComparison.OrdinalIgnoreCase))
                {
                    _supportStatus.SupportUrl = "https://buymeacoffee.com/onrampcam";
                    SaveSupportStatus(_supportStatus);
                }

                string? envCost = Environment.GetEnvironmentVariable("ONRAMP_MONTHLY_COST_USD");
                if (decimal.TryParse(envCost, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedCost) && parsedCost > 0)
                {
                    _supportStatus.MonthlyCostUsd = parsedCost;
                }

                return new SupportStatusDto
                {
                    MonthlyCostUsd = _supportStatus.MonthlyCostUsd,
                    CurrentFundedUsd = _supportStatus.CurrentFundedUsd,
                    CoffeePriceUsd = _supportStatus.CoffeePriceUsd,
                    SupporterCount = _supportStatus.SupporterCount,
                    SupportUrl = _supportStatus.SupportUrl,
                    ButtonLabel = _supportStatus.ButtonLabel,
                    CurrentMonth = _supportStatus.CurrentMonth
                };
            }
        }

        public SupportStatusDto RecordSupportContribution(decimal amountUsd)
        {
            if (amountUsd <= 0)
            {
                return GetSupportStatus();
            }

            lock (_supportLock)
            {
                var status = GetSupportStatus();
                _supportStatus.CurrentFundedUsd = Math.Round(_supportStatus.CurrentFundedUsd + amountUsd, 2);
                _supportStatus.SupporterCount += 1;
                SaveSupportStatus(_supportStatus);
                return GetSupportStatus();
            }
        }

        private static string GetSupportFilePath()
        {
            string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
            string dir = Directory.Exists(Path.Combine(home, "site")) ? Path.Combine(home, "site") : "/tmp";
            return Path.Combine(dir, "onramp-support-status.json");
        }

        private static decimal GetDefaultFundedUsd()
        {
            string? envFunded = Environment.GetEnvironmentVariable("ONRAMP_FUNDED_USD");
            if (decimal.TryParse(envFunded, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) && val >= 0)
            {
                return val;
            }
            return 5m;
        }

        private SupportStatusDto LoadOrCreateSupportStatus()
        {
            string currentMonth = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            string path = GetSupportFilePath();
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<SupportStatusDto>(json);
                    if (loaded != null && loaded.CurrentMonth == currentMonth)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Non-fatal error reading support status file.");
            }

            var initial = new SupportStatusDto
            {
                MonthlyCostUsd = 20m,
                CurrentFundedUsd = GetDefaultFundedUsd(),
                CoffeePriceUsd = 3m,
                SupporterCount = 2,
                SupportUrl = Environment.GetEnvironmentVariable("ONRAMP_SUPPORT_URL") ?? "https://buymeacoffee.com/onrampcam",
                ButtonLabel = "☕ Buy a Coffee ($3)",
                CurrentMonth = currentMonth
            };
            SaveSupportStatus(initial);
            return initial;
        }

        private void SaveSupportStatus(SupportStatusDto dto)
        {
            try
            {
                string path = GetSupportFilePath();
                File.WriteAllText(path, JsonSerializer.Serialize(dto));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Non-fatal error saving support status file.");
            }
        }

        public IReadOnlyList<CommuteCorridorDto> GetCorridors(string? city = null)
        {
            var query = _corridors.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(c => string.Equals(c.City, city.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return query.Select(CloneCorridorWithFrames).ToList();
        }

        public CommuteCorridorDto? GetCorridorById(string corridorId)
        {
            var corridor = _corridors.FirstOrDefault(c => string.Equals(c.Id, corridorId, StringComparison.OrdinalIgnoreCase));
            return corridor == null ? null : CloneCorridorWithFrames(corridor);
        }

        public CameraFrameEntry? GetFrame(string cameraId, string frameId)
        {
            if (!_frameBuffers.TryGetValue(cameraId, out var list))
            {
                return null;
            }

            lock (list)
            {
                return list.FirstOrDefault(f => string.Equals(f.FrameId, frameId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public async Task<int> PollCorridorCamerasNowAsync(string? corridorId = null, CancellationToken cancellationToken = default)
        {
            if (!await _pollLock.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken))
            {
                return 0;
            }

            try
            {
                IEnumerable<CommuteCameraDto> targetCameras;
                if (!string.IsNullOrWhiteSpace(corridorId))
                {
                    var corridor = _corridors.FirstOrDefault(c => string.Equals(c.Id, corridorId, StringComparison.OrdinalIgnoreCase));
                    if (corridor == null)
                    {
                        return 0;
                    }
                    if (string.Equals(corridor.City, "london", StringComparison.OrdinalIgnoreCase))
                    {
                        await RefreshLondonJamCamsIfNeededAsync(force: true, cancellationToken);
                        return corridor.Cameras.Count;
                    }
                    targetCameras = corridor.Cameras;
                }
                else
                {
                    targetCameras = _corridors
                        .Where(c => !string.Equals(c.City, "london", StringComparison.OrdinalIgnoreCase))
                        .SelectMany(c => c.Cameras)
                        .GroupBy(cam => cam.Id)
                        .Select(g => g.First());
                }

                var camerasList = targetCameras.ToList();
                int capturedCount = 0;

                using var throttle = new SemaphoreSlim(6);
                var tasks = camerasList.Select(async cam =>
                {
                    await throttle.WaitAsync(cancellationToken);
                    try
                    {
                        bool ok = await FetchAndStoreCameraFrameAsync(cam, cancellationToken);
                        if (ok)
                        {
                            Interlocked.Increment(ref capturedCount);
                        }
                    }
                    finally
                    {
                        throttle.Release();
                    }
                });

                await Task.WhenAll(tasks);
                return capturedCount;
            }
            finally
            {
                _pollLock.Release();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CommuteStripService starting initial camera warm-up...");

            try
            {
                await RefreshLondonJamCamsIfNeededAsync(force: false, stoppingToken);
                await PollCorridorCamerasNowAsync(null, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Initial camera warm-up encountered a non-fatal error.");
            }

            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RefreshLondonJamCamsIfNeededAsync(force: false, stoppingToken);
                    int count = await PollCorridorCamerasNowAsync(null, stoppingToken);
                    _logger.LogInformation("CommuteStripService polled {Count} Auckland motorway cameras.", count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during periodic commute camera poll.");
                }
            }
        }

        private async Task<bool> FetchAndStoreCameraFrameAsync(CommuteCameraDto cam, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(cam.ImageUrl))
            {
                return false;
            }

            try
            {
                string requestUrl = cam.ImageUrl.Contains('?')
                    ? $"{cam.ImageUrl}&_ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
                    : $"{cam.ImageUrl}?_ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (bytes.Length < 1024)
                {
                    return false;
                }

                string contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var now = DateTime.UtcNow;
                var entry = new CameraFrameEntry
                {
                    FrameId = now.ToString("yyyyMMddHHmmssfff"),
                    CapturedAtUtc = now,
                    ImageBytes = bytes,
                    ContentType = contentType
                };

                var list = _frameBuffers.GetOrAdd(cam.Id, _ => new List<CameraFrameEntry>());
                lock (list)
                {
                    list.Add(entry);
                    while (list.Count > MaxFramesPerCamera)
                    {
                        list.RemoveAt(0);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to fetch camera frame for {CameraId} ({Url})", cam.Id, cam.ImageUrl);
                return false;
            }
        }

        private async Task RefreshLondonJamCamsIfNeededAsync(bool force, CancellationToken cancellationToken)
        {
            if (!force && (DateTime.UtcNow - _lastLondonRefreshUtc) < TimeSpan.FromMinutes(10))
            {
                return;
            }

            try
            {
                using var resp = await _httpClient.GetAsync("https://api.tfl.gov.uk/Place/Type/JamCam", cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    return;
                }

                using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                var lookup = new Dictionary<string, (string imageUrl, string videoUrl, string view)>(StringComparer.OrdinalIgnoreCase);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (!el.TryGetProperty("id", out var idProp))
                        continue;

                    string? id = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    string imageUrl = string.Empty;
                    string videoUrl = string.Empty;
                    string view = string.Empty;

                    if (el.TryGetProperty("additionalProperties", out var props) && props.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var p in props.EnumerateArray())
                        {
                            string? key = p.TryGetProperty("key", out var k) ? k.GetString() : null;
                            string? val = p.TryGetProperty("value", out var v) ? v.GetString() : null;
                            if (key == "imageUrl" && !string.IsNullOrWhiteSpace(val))
                                imageUrl = val;
                            else if (key == "videoUrl" && !string.IsNullOrWhiteSpace(val))
                                videoUrl = val;
                            else if (key == "view" && !string.IsNullOrWhiteSpace(val))
                                view = val;
                        }
                    }

                    lookup[id] = (imageUrl, videoUrl, view);
                }

                foreach (var corridor in _corridors.Where(c => c.City == "london"))
                {
                    foreach (var cam in corridor.Cameras)
                    {
                        if (lookup.TryGetValue(cam.Id, out var info))
                        {
                            if (!string.IsNullOrWhiteSpace(info.imageUrl))
                                cam.ImageUrl = info.imageUrl;
                            if (!string.IsNullOrWhiteSpace(info.videoUrl))
                                cam.VideoUrl = info.videoUrl;
                            if (!string.IsNullOrWhiteSpace(info.view))
                                cam.Subtitle = info.view;
                        }
                    }
                }

                _lastLondonRefreshUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Non-fatal error refreshing TfL JamCam metadata.");
            }
        }

        private CommuteCorridorDto CloneCorridorWithFrames(CommuteCorridorDto source)
        {
            return new CommuteCorridorDto
            {
                Id = source.Id,
                City = source.City,
                Name = source.Name,
                ShortName = source.ShortName,
                Description = source.Description,
                InboundLabel = source.InboundLabel,
                OutboundLabel = source.OutboundLabel,
                Cameras = source.Cameras.Select(cam =>
                {
                    var frames = new List<CommuteFrameMetaDto>();
                    if (_frameBuffers.TryGetValue(cam.Id, out var buffer))
                    {
                        lock (buffer)
                        {
                            frames = buffer.Select(f => new CommuteFrameMetaDto
                            {
                                FrameId = f.FrameId,
                                CapturedAtUtc = f.CapturedAtUtc,
                                Url = $"/api/commute/frames/{Uri.EscapeDataString(cam.Id)}/{Uri.EscapeDataString(f.FrameId)}"
                            }).ToList();
                        }
                    }

                    return new CommuteCameraDto
                    {
                        Id = cam.Id,
                        Name = cam.Name,
                        Subtitle = cam.Subtitle,
                        Lat = cam.Lat,
                        Lng = cam.Lng,
                        ImageUrl = cam.ImageUrl,
                        VideoUrl = cam.VideoUrl,
                        City = cam.City,
                        Frames = frames
                    };
                }).ToList()
            };
        }

        private static List<CommuteCorridorDto> BuildDefaultCorridors()
        {
            return new List<CommuteCorridorDto>
            {
                // =========================================================================
                // AUCKLAND, NEW ZEALAND (NZTA Waka Kotahi Live Motorway Corridors)
                // =========================================================================
                new CommuteCorridorDto
                {
                    Id = "ak-sh16",
                    City = "auckland",
                    ShortName = "SH16 Northwestern",
                    Name = "SH16 Northwestern Motorway (CBD ↔ Westgate)",
                    Description = "Central Motorway Junction, Pt Chevalier, Causeway, Te Atatu, Lincoln Rd & Westgate",
                    InboundLabel = "Citybound (Westgate → CBD)",
                    OutboundLabel = "Westbound (CBD → Westgate)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        Cam("ak-29", "SH1/SH16 Central Mwy Jct", "Grafton / CMJ", -36.860, 174.757, "https://www.trafficnz.info/camera/60.jpg"),
                        Cam("ak-3",  "SH16 Bond St Overbridge", "Kingsland / Newton", -36.869, 174.746, "https://www.trafficnz.info/camera/130.jpg"),
                        Cam("ak-117","SH16 Carrington Rd", "Pt Chevalier / Unitec", -36.8717, 174.7102, "https://www.trafficnz.info/camera/654.jpg"),
                        Cam("ak-4",  "SH16 / SH20 Waterview Interchange", "Great North Rd / Waterview", -36.872, 174.706, "https://www.trafficnz.info/camera/140.jpg"),
                        Cam("ak-266","SH16 Causeway", "Waitemata Harbour Causeway", -36.8721, 174.6897, "https://www.trafficnz.info/camera/688.jpg"),
                        Cam("ak-268","SH16 Rosebank Rd", "Avondale / Rosebank Peninsula", -36.8700, 174.6736, "https://www.trafficnz.info/camera/697.jpg"),
                        Cam("ak-265","SH16 Patiki Rd", "Rosebank / Patiki Interchange", -36.8659, 174.6662, "https://www.trafficnz.info/camera/687.jpg"),
                        Cam("ak-267","SH16 Whau River Bridge", "Approach to Te Atatu", -36.8606, 174.6609, "https://www.trafficnz.info/camera/689.jpg"),
                        Cam("ak-5",  "SH16 Te Atatu Rd", "Te Atatu Interchange", -36.858, 174.653, "https://www.trafficnz.info/camera/150.jpg"),
                        Cam("ak-6",  "SH16 Lincoln Rd South", "Henderson / Lincoln East", -36.849, 174.632, "https://www.trafficnz.info/camera/160.jpg"),
                        Cam("ak-7",  "SH16 Lincoln Rd North", "Henderson / Lincoln West", -36.847, 174.630, "https://www.trafficnz.info/camera/170.jpg"),
                        Cam("ak-8",  "SH16 / SH18 Westgate South", "Royal Rd / Massey", -36.819, 174.614, "https://www.trafficnz.info/camera/270.jpg"),
                        Cam("ak-9",  "SH16 Hobsonville Rd / Westgate", "Westgate Interchange", -36.813, 174.612, "https://www.trafficnz.info/camera/271.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "ak-sh1-north",
                    City = "auckland",
                    ShortName = "SH1 Northern & Bridge",
                    Name = "SH1 Northern Motorway (CBD ↔ North Shore / Albany)",
                    Description = "Harbour Bridge Approaches, Onewa Rd, Esmonde Rd, Tristram Ave, Constellation & Albany",
                    InboundLabel = "Citybound (Albany → CBD)",
                    OutboundLabel = "Northbound (CBD → Albany)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        Cam("ak-29", "SH1/SH16 Central Mwy Jct", "CBD / Fanshawe Approach", -36.860, 174.757, "https://www.trafficnz.info/camera/60.jpg"),
                        Cam("ak-28", "SH1 Onewa Rd Interchange", "Northcote Point / Bridge North", -36.812, 174.754, "https://www.trafficnz.info/camera/50.jpg"),
                        Cam("ak-27", "SH1 Esmonde Rd", "Takapuna / Akoranga", -36.799, 174.762, "https://www.trafficnz.info/camera/40.jpg"),
                        Cam("ak-26", "SH1 Northcote Rd", "Hillcrest / Northcote", -36.789, 174.753, "https://www.trafficnz.info/camera/30.jpg"),
                        Cam("ak-25", "SH1 Tristram Ave", "Wairau Valley / Forrest Hill", -36.7718, 174.7428, "https://www.trafficnz.info/camera/20.jpg"),
                        Cam("ak-238","SH1 Sunset Rd Overbridge", "Sunnynook / Totara Vale", -36.7539, 174.7284, "https://www.trafficnz.info/camera/822.jpg"),
                        Cam("ak-237","SH1 Constellation Dr", "Mairangi Bay / SH18 Link", -36.7506, 174.7263, "https://www.trafficnz.info/camera/821.jpg"),
                        Cam("ak-247","SH1 Rosedale", "Rosedale / Bush Rd", -36.7434, 174.7204, "https://www.trafficnz.info/camera/820.jpg"),
                        Cam("ak-24", "SH1 Greville Rd", "Albany / Pinehill", -36.735, 174.717, "https://www.trafficnz.info/camera/10.jpg"),
                        Cam("ak-245","SH1 McClymonts Rd", "Albany North / Oteha Valley", -36.7243, 174.7139, "https://www.trafficnz.info/camera/818.jpg"),
                        Cam("ak-256","SH1 Redvale", "Lonely Track Rd", -36.6857, 174.7012, "https://www.trafficnz.info/camera/723.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "ak-sh1-south",
                    City = "auckland",
                    ShortName = "SH1 Southern",
                    Name = "SH1 Southern Motorway (CBD ↔ Manukau / Papakura)",
                    Description = "Newmarket, Greenlane, Mt Wellington, Highbrook, Manukau, Takanini & Papakura",
                    InboundLabel = "Citybound (Papakura → CBD)",
                    OutboundLabel = "Southbound (CBD → Papakura)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        Cam("ak-29", "SH1/SH16 Central Mwy Jct", "Symonds St / Khyber Pass", -36.860, 174.757, "https://www.trafficnz.info/camera/60.jpg"),
                        Cam("ak-22", "SH1 Market Rd", "Epsom / Remuera", -36.878, 174.782, "https://www.trafficnz.info/camera/225.jpg"),
                        Cam("ak-31", "SH1 Greenlane Interchange", "Greenlane / Ellerslie", -36.889, 174.796, "https://www.trafficnz.info/camera/80.jpg"),
                        Cam("ak-32", "SH1 South Eastern Hwy", "Mt Wellington / Sylvia Park", -36.915, 174.826, "https://www.trafficnz.info/camera/90.jpg"),
                        Cam("ak-34", "SH1 Bairds Rd Overbridge", "Otara / East Tamaki", -36.957, 174.861, "https://www.trafficnz.info/camera/110.jpg"),
                        Cam("ak-33", "SH1 Redoubt Rd", "Manukau City Centre", -36.992, 174.888, "https://www.trafficnz.info/camera/100.jpg"),
                        Cam("ak-42", "SH1 / SH20 Interchange", "Manukau Southern Link", -36.997, 174.891, "https://www.trafficnz.info/camera/212.jpg"),
                        Cam("ak-258","SH1 Orams Rd", "Manurewa North", -37.0097, 174.9033, "https://www.trafficnz.info/camera/799.jpg"),
                        Cam("ak-259","SH1 Hill Road", "Manurewa / Botanic Gardens", -37.0144, 174.9071, "https://www.trafficnz.info/camera/797.jpg"),
                        Cam("ak-35", "SH1 Alfriston Rd", "Takanini North", -37.021, 174.910, "https://www.trafficnz.info/camera/120.jpg"),
                        Cam("ak-36", "SH1 Takanini", "Takanini Straight", -37.0288, 174.9103, "https://www.trafficnz.info/camera/121.jpg"),
                        Cam("ak-39", "SH1 Papakura Interchange", "Papakura / Beach Rd", -37.0616, 174.9245, "https://www.trafficnz.info/camera/124.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "ak-sh20",
                    City = "auckland",
                    ShortName = "SH20 Southwest & Airport",
                    Name = "SH20 Southwestern & Airport (Waterview ↔ Manukau)",
                    Description = "Waterview Tunnel, Mt Roskill, Hillsborough, Mangere Bridge, Airport (SH20A) & Manukau",
                    InboundLabel = "Northbound (Manukau → Waterview)",
                    OutboundLabel = "Southbound (Waterview → Airport / Manukau)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        Cam("ak-276","SH20 Waterview Tunnel Portal", "Great North Rd / Tunnel South", -36.8750, 174.7038, "https://www.trafficnz.info/camera/669.jpg"),
                        Cam("ak-274","SH20 Owairaka", "Mt Albert / Richardson Rd", -36.8998, 174.7136, "https://www.trafficnz.info/camera/667.jpg"),
                        Cam("ak-53", "SH20 Maioro St", "New Windsor / Sandringham", -36.9041, 174.7231, "https://www.trafficnz.info/camera/194.jpg"),
                        Cam("ak-52", "SH20 Dominion Rd", "Mt Roskill", -36.911, 174.740, "https://www.trafficnz.info/camera/193.jpg"),
                        Cam("ak-50", "SH20 Hillsborough Rd", "Hillsborough", -36.917, 174.759, "https://www.trafficnz.info/camera/191.jpg"),
                        Cam("ak-49", "SH20 Queenstown Rd", "Onehunga North", -36.9214, 174.7673, "https://www.trafficnz.info/camera/190.jpg"),
                        Cam("ak-55", "SH20 Onehunga Wharf", "Mangere Bridge North Approach", -36.931, 174.786, "https://www.trafficnz.info/camera/202.jpg"),
                        Cam("ak-58", "SH20 Coronation Rd", "Mangere Bridge South", -36.953, 174.792, "https://www.trafficnz.info/camera/205.jpg"),
                        Cam("ak-280","SH20 / SH20A Airport Jct", "Turnoff to Auckland Airport", -36.964, 174.800, "https://www.trafficnz.info/camera/306.jpg"),
                        Cam("ak-45", "SH20A George Bolt Mem Dr", "Auckland Airport Approach", -36.973, 174.789, "https://www.trafficnz.info/camera/275.jpg"),
                        Cam("ak-59", "SH20 Puhinui Rd", "Papatoetoe / Airport East", -36.993, 174.843, "https://www.trafficnz.info/camera/206.jpg"),
                        Cam("ak-62", "SH20 Lambie Dr", "Manukau Central", -36.996, 174.874, "https://www.trafficnz.info/camera/209.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "ak-sh18",
                    City = "auckland",
                    ShortName = "SH18 Upper Harbour",
                    Name = "SH18 Upper Harbour Highway (Westgate ↔ Constellation)",
                    Description = "Hobsonville, Brigham Creek, Upper Harbour Bridge, Greenhithe & Albany Hwy",
                    InboundLabel = "Eastbound (Westgate → North Shore)",
                    OutboundLabel = "Westbound (North Shore → Westgate)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        Cam("ak-18", "SH18 Trig Rd", "Westgate / Whenuapai South", -36.808, 174.621, "https://www.trafficnz.info/camera/221.jpg"),
                        Cam("ak-20", "SH18 Brigham Creek", "Whenuapai / Brigham Creek", -36.800, 174.638, "https://www.trafficnz.info/camera/223.jpg"),
                        Cam("ak-21", "SH18 Hobsonville", "Hobsonville Point Turnoff", -36.796, 174.645, "https://www.trafficnz.info/camera/224.jpg"),
                        Cam("ak-30", "SH18 Squadron Dr", "Hobsonville East", -36.791, 174.655, "https://www.trafficnz.info/camera/70.jpg"),
                        Cam("ak-16", "SH18 Upper Harbour Bridge", "Waitemata Harbour Crossing", -36.786, 174.662, "https://www.trafficnz.info/camera/177.jpg"),
                        Cam("ak-10", "SH18 Tauhinu Rd", "Greenhithe West", -36.782, 174.678, "https://www.trafficnz.info/camera/171.jpg"),
                        Cam("ak-14", "SH18 Greenhithe Rd", "Greenhithe Village", -36.771, 174.686, "https://www.trafficnz.info/camera/175.jpg"),
                        Cam("ak-11", "SH18 Albany Hwy", "Rosedale / Albany Hwy", -36.757, 174.703, "https://www.trafficnz.info/camera/172.jpg"),
                        Cam("ak-270","SH18 Unsworth Heights", "Paul Matthews / Unsworth", -36.7538, 174.7098, "https://www.trafficnz.info/camera/823.jpg"),
                        Cam("ak-269","SH1 / SH18 Constellation Jct", "Northern Motorway Merge", -36.7504, 174.7225, "https://www.trafficnz.info/camera/815.jpg"),
                    }
                },

                // =========================================================================
                // WELLINGTON, NEW ZEALAND (NZTA Waka Kotahi Live Motorway Corridors)
                // =========================================================================
                new CommuteCorridorDto
                {
                    Id = "wlg-sh1-urban",
                    City = "wellington",
                    ShortName = "SH1 Urban & Ngauranga",
                    Name = "SH1 Urban Motorway & Ngauranga Gorge (CBD ↔ Tawa)",
                    Description = "Terrace Tunnel, Tinakori Rd, SH1/SH2 Ngauranga Interchange, Ngauranga Gorge, Johnsonville, Churton Park & Tawa",
                    InboundLabel = "Citybound (Tawa / Johnsonville → Wellington CBD)",
                    OutboundLabel = "Northbound (Wellington CBD → Ngauranga / Tawa)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        WlgCam("wlg-828", "SH1 Clifton Terrace (Terrace Tunnel)", "Terrace Tunnel Northern Approach", -41.2834, 174.7738, "https://www.trafficnz.info/camera/828.jpg"),
                        WlgCam("wlg-230", "SH1 Tinakori Rd On-ramp", "Thorndon / Urban Motorway", -41.2763, 174.7731, "https://www.trafficnz.info/camera/230.jpg"),
                        WlgCam("wlg-282", "SH1 / SH2 Ngauranga Interchange", "Hutt Rd / Gorge Split", -41.2481, 174.8136, "https://www.trafficnz.info/camera/282.jpg"),
                        WlgCam("wlg-228", "SH1 Ngauranga Gorge", "Mid-Gorge Climb", -41.2410, 174.8090, "https://www.trafficnz.info/camera/228.jpg"),
                        WlgCam("wlg-227", "SH1 Newlands Interchange", "Newlands Rd Overbridge", -41.2319, 174.8095, "https://www.trafficnz.info/camera/227.jpg"),
                        WlgCam("wlg-226", "SH1 Johnsonville Off-ramp", "Johnsonville Town Centre", -41.2280, 174.8081, "https://www.trafficnz.info/camera/226.jpg"),
                        WlgCam("wlg-795", "SH1 Churton Park South", "Glenside / Westchester Dr", -41.2057, 174.8184, "https://www.trafficnz.info/camera/795.jpg"),
                        WlgCam("wlg-796", "SH1 Churton Park North", "Churton Park Interchange", -41.2045, 174.8214, "https://www.trafficnz.info/camera/796.jpg"),
                        WlgCam("wlg-277", "SH1 Tawa / Linden Merge", "Transmission Gully / SH59 Split", -41.1777, 174.8300, "https://www.trafficnz.info/camera/277.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "wlg-sh1-tg",
                    City = "wellington",
                    ShortName = "SH1 Transmission Gully",
                    Name = "SH1 Transmission Gully & Kapiti Expressway (Tawa ↔ Otaki)",
                    Description = "Linden Merge, Kenepuru, Waitangirua, SH58 Pauatahanui, Paekakariki, Mackays Crossing & Otaki",
                    InboundLabel = "Southbound (Kapiti / Transmission Gully → Wellington)",
                    OutboundLabel = "Northbound (Tawa → Transmission Gully / Kapiti)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        WlgCam("wlg-277", "SH1 Tawa / Linden Merge", "Southern Portal of Transmission Gully", -41.1777, 174.8300, "https://www.trafficnz.info/camera/277.jpg"),
                        WlgCam("wlg-794", "SH1 Kenepuru Interchange", "Porirua South / Kenepuru Link", -41.1519, 174.8449, "https://www.trafficnz.info/camera/794.jpg"),
                        WlgCam("wlg-792", "SH1 Waitangirua South", "Cannons Creek / Waitangirua Link", -41.1321, 174.9012, "https://www.trafficnz.info/camera/792.jpg"),
                        WlgCam("wlg-793", "SH1 Waitangirua North", "Approach to James Cook Saddle", -41.1318, 174.9000, "https://www.trafficnz.info/camera/793.jpg"),
                        WlgCam("wlg-790", "SH1 / SH58 Pauatahanui South", "Haywards Hill / Hutt Valley Turnoff", -41.1133, 174.9185, "https://www.trafficnz.info/camera/790.jpg"),
                        WlgCam("wlg-791", "SH1 / SH58 Pauatahanui North", "Battle Hill / Wainui Saddle Approach", -41.1122, 174.9178, "https://www.trafficnz.info/camera/791.jpg"),
                        WlgCam("wlg-789", "SH1 Paekakariki Interchange", "SH59 Coastal Merge South", -40.9827, 174.9707, "https://www.trafficnz.info/camera/789.jpg"),
                        WlgCam("wlg-787", "SH1 Mackays Crossing", "Queen Elizabeth Park / Kapiti Approach", -40.9712, 174.9830, "https://www.trafficnz.info/camera/787.jpg"),
                        WlgCam("wlg-804", "SH1 Te Horo / Makahuri", "PP2O Expressway South", -40.8205, 175.1024, "https://www.trafficnz.info/camera/804.jpg"),
                        WlgCam("wlg-808", "SH1 Otaki Gorge Rd", "Otaki River Approach", -40.7749, 175.1448, "https://www.trafficnz.info/camera/808.jpg"),
                        WlgCam("wlg-810", "SH1 Otaki / Rahui Rd", "Otaki Township Overbridge", -40.7615, 175.1599, "https://www.trafficnz.info/camera/810.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "wlg-sh59",
                    City = "wellington",
                    ShortName = "SH59 Porirua & Coast",
                    Name = "SH59 Porirua & Kapiti Coastal Route (Linden ↔ Paekakariki)",
                    Description = "Linden, Paremata Roundabout, Mana / Plimmerton, Pukerua Bay & Paekakariki Coastal",
                    InboundLabel = "Southbound (Paekakariki / Plimmerton → Porirua / CBD)",
                    OutboundLabel = "Northbound (Porirua → Paremata / Kapiti Coast)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        WlgCam("wlg-277", "SH1 / SH59 Linden Merge", "Tawa / Porirua Motorway Split", -41.1777, 174.8300, "https://www.trafficnz.info/camera/277.jpg"),
                        WlgCam("wlg-637", "SH59 Paremata Roundabout", "Paremata Bridge / SH58 Turnoff", -41.1056, 174.8686, "https://www.trafficnz.info/camera/637.jpg"),
                        WlgCam("wlg-280", "SH59 Plimmerton Roundabout", "Mana Esplanade / Plimmerton", -41.0830, 174.8690, "https://www.trafficnz.info/camera/280.jpg"),
                        WlgCam("wlg-720", "SH59 Airlie Rd", "Pukerua Bay South Climb", -41.0576, 174.8760, "https://www.trafficnz.info/camera/720.jpg"),
                        WlgCam("wlg-719", "SH59 Pukerua Bay", "Pukerua Bay Summit", -41.0288, 174.8994, "https://www.trafficnz.info/camera/719.jpg"),
                        WlgCam("wlg-279", "SH59 Paekakariki Coastal", "Centennial Highway / Beach", -40.9997, 174.9397, "https://www.trafficnz.info/camera/279.jpg"),
                        WlgCam("wlg-787", "SH1 / SH59 Mackays Crossing", "Rejoin SH1 Kapiti Expressway", -40.9712, 174.9830, "https://www.trafficnz.info/camera/787.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "wlg-sh2",
                    City = "wellington",
                    ShortName = "SH2 Hutt Valley",
                    Name = "SH2 Hutt Valley & Petone Foreshore (CBD ↔ Upper Hutt)",
                    Description = "Terrace Tunnel, Ngauranga Interchange, Petone Foreshore, Kennedy Good Bridge & Stokes Valley",
                    InboundLabel = "Citybound (Upper / Lower Hutt → Wellington CBD)",
                    OutboundLabel = "Northbound (Wellington CBD → Petone / Hutt Valley)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        WlgCam("wlg-828", "SH1 Clifton Terrace (Terrace Tunnel)", "Wellington CBD Approach", -41.2834, 174.7738, "https://www.trafficnz.info/camera/828.jpg"),
                        WlgCam("wlg-230", "SH1 Tinakori Rd On-ramp", "Thorndon Elevated Motorway", -41.2763, 174.7731, "https://www.trafficnz.info/camera/230.jpg"),
                        WlgCam("wlg-282", "SH1 / SH2 Ngauranga Interchange", "SH2 Hutt Road Harbour Edge", -41.2481, 174.8136, "https://www.trafficnz.info/camera/282.jpg"),
                        WlgCam("wlg-232", "SH2 Petone Interchange", "Petone Esplanade / Korokoro", -41.2240, 174.8600, "https://www.trafficnz.info/camera/232.jpg"),
                        WlgCam("wlg-722", "SH2 Kennedy Good Bridge", "Lower Hutt / Belmont / Avalon", -41.1899, 174.9299, "https://www.trafficnz.info/camera/722.jpg"),
                        WlgCam("wlg-801", "SH2 Stokes Valley Roundabout SB", "Silverstream / Eastern Hutt Rd", -41.1609, 174.9812, "https://www.trafficnz.info/camera/801.jpg"),
                        WlgCam("wlg-802", "SH2 Stokes Valley Roundabout EB", "Stokes Valley Entrance / Manor Park", -41.1609, 174.9813, "https://www.trafficnz.info/camera/802.jpg"),
                    }
                },

                // =========================================================================
                // LONDON, UNITED KINGDOM (TfL Live JamCam Video Corridors)
                // =========================================================================
                new CommuteCorridorDto
                {
                    Id = "ldn-a406",
                    City = "london",
                    ShortName = "A406 North Circular",
                    Name = "A406 North Circular (Chiswick ↔ Hanger Lane ↔ Walthamstow)",
                    Description = "Gunnersbury, Hanger Lane Gyratory, Ace Cafe / Stonebridge, Brentfield & Billet Underpass",
                    InboundLabel = "Eastbound (Chiswick → East London)",
                    OutboundLabel = "Westbound (East London → Chiswick)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        LondonCam("JamCams_00001.08008", "A406 Gunnersbury Ave", "Chiswick / Gunnersbury Lane", 51.5006, -0.2843, "00001.08008"),
                        LondonCam("JamCams_00001.08000", "A406 Hanger Lane / Uxbridge Rd", "Ealing Common Approach", 51.5117, -0.2922, "00001.08000"),
                        LondonCam("JamCams_00001.08001", "A406 Hanger Lane / Queens Parade", "Hanger Lane Approach", 51.5169, -0.2912, "00001.08001"),
                        LondonCam("JamCams_00001.07317", "A40 / Hanger Lane Gyratory", "Hanger Lane Tunnel West Portal", 51.5302, -0.2949, "00001.07317"),
                        LondonCam("JamCams_00002.00116", "A406 Iveagh Ave", "Park Royal", 51.5378, -0.2834, "00002.00116"),
                        LondonCam("JamCams_00002.00114", "A406 Ace Cafe / Stonebridge", "North Circular Stonebridge Park", 51.5398, -0.2785, "00002.00114"),
                        LondonCam("JamCams_00002.00115", "A406 Abbey Road Underpass", "Wembley / Park Royal", 51.5395, -0.2780, "00002.00115"),
                        LondonCam("JamCams_00002.00113", "A406 Harrow Rd / Brentfield", "Brentfield / Ikea", 51.5448, -0.2729, "00002.00113"),
                        LondonCam("JamCams_00001.08009", "A406 Great Central Way", "Neasden / Brent Reservoir", 51.5527, -0.2552, "00001.08009"),
                        LondonCam("JamCams_00002.00865", "A406 Billet Underpass", "Walthamstow / Chingford", 51.6007, -0.0159, "00002.00865"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "ldn-east-blackwall",
                    City = "london",
                    ShortName = "Blackwall & A13 East",
                    Name = "Blackwall Tunnel, Limehouse Link & A13 Commercial Rd",
                    Description = "Tower Hill / The Highway, Limehouse Link Tunnel, Canary Wharf, A13 & Blackwall Approach",
                    InboundLabel = "Westbound (Blackwall / Docklands → City)",
                    OutboundLabel = "Eastbound (City → Docklands / Blackwall)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        LondonCam("JamCams_00002.00362", "Limehouse Tunnel / The Highway", "Wapping / Tower Hill Approach", 51.5107, -0.0405, "00002.00362"),
                        LondonCam("JamCams_00002.00421", "A13 Branch Road", "Rotherhithe / Limehouse", 51.5128, -0.0388, "00002.00421"),
                        LondonCam("JamCams_00001.02258", "A13 Commercial Rd / Salmon Ln", "Limehouse", 51.5124, -0.0320, "00001.02258"),
                        LondonCam("JamCams_00002.00330", "A13 Burdett Rd", "Mile End / Canary Wharf Turnoff", 51.5119, -0.0285, "00002.00330"),
                        LondonCam("JamCams_00002.00403", "Limehouse Tunnel Westferry Portal", "Canary Wharf Westferry Entrance", 51.5077, -0.0277, "00002.00403"),
                        LondonCam("JamCams_00002.00389", "Limehouse Tunnel East Portal", "Poplar / Aspen Way", 51.5085, -0.0223, "00002.00389"),
                        LondonCam("JamCams_00002.00390", "Limehouse Link / Aspen Way", "Canary Wharf North", 51.5082, -0.0212, "00002.00390"),
                        LondonCam("JamCams_00002.00329", "A13 East India Dock / Cotton St", "Blackwall / Canning Town", 51.5113, -0.0108, "00002.00329"),
                        LondonCam("JamCams_00002.00327", "A12 Blackwall Tunnel North App", "Bow / Gillender St Approach", 51.5212, -0.0103, "00002.00327"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "ldn-a4-a40-west",
                    City = "london",
                    ShortName = "A4 / M4 & A40 West",
                    Name = "A4 Great West Rd (Heathrow / M4) & A40 Western Ave",
                    Description = "Heathrow Approach, Henlys Roundabout, Syon Lane, Chiswick Flyover & A40 Hoover Building",
                    InboundLabel = "Eastbound (Heathrow / West → Central London)",
                    OutboundLabel = "Westbound (Central London → M4 / Heathrow)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        LondonCam("JamCams_00001.06948", "A4 Colnbrook / Heathrow West", "M25 / Stanwell Moor Rd", 51.4839, -0.4925, "00001.06948"),
                        LondonCam("JamCams_00001.06506", "A4 Heathrow Tunnel / Sipson Rd", "M4 Spur & Emirates Roundabout", 51.4814, -0.4510, "00001.06506"),
                        LondonCam("JamCams_00001.06914", "A4 Bath Rd / Henlys Roundabout", "Hounslow West", 51.4751, -0.3944, "00001.06914"),
                        LondonCam("JamCams_00001.06910", "A4 Great West Rd / Sutton Ln", "Osterley", 51.4781, -0.3724, "00001.06910"),
                        LondonCam("JamCams_00001.06611", "A4 / M4 Elevated at Syon Lane", "Brentford / Gillette Corner", 51.4833, -0.3261, "00001.06611"),
                        LondonCam("JamCams_00001.06648", "A4 Chiswick Roundabout", "M4 J1 / North Circular", 51.4912, -0.2829, "00001.06648"),
                        LondonCam("JamCams_00001.06605", "A4 Great West Rd / Sutton Court", "Chiswick Flyover East", 51.4875, -0.2671, "00001.06605"),
                        LondonCam("JamCams_00001.07312", "A40 Western Ave / Argyle Rd", "Perivale / Hoover Building", 51.5339, -0.3296, "00001.07312"),
                        LondonCam("JamCams_00001.07310", "A40 Western Ave / Connell Cres", "Park Royal / Hanger Lane", 51.5287, -0.2875, "00001.07310"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "ldn-thames-bridges",
                    City = "london",
                    ShortName = "Thames Bridges & Central",
                    Name = "Central London Thames Crossings & Embankment",
                    Description = "Albert Bridge, Chelsea Bridge, Vauxhall Cross, Westminster Bridge & Waterloo Bridge",
                    InboundLabel = "Eastbound (Chelsea → Waterloo)",
                    OutboundLabel = "Westbound (Waterloo → Chelsea)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        LondonCam("JamCams_00001.06549", "Chelsea Embankment / Albert Bridge", "Albert Bridge North", 51.4837, -0.1673, "00001.06549"),
                        LondonCam("JamCams_00001.06500", "Chelsea Embankment / Royal Hospital", "Chelsea Physic Garden", 51.4838, -0.1636, "00001.06500"),
                        LondonCam("JamCams_00001.06530", "Chelsea Bridge / Embankment", "Battersea Park North", 51.4858, -0.1497, "00001.06530"),
                        LondonCam("JamCams_00001.04324", "Vauxhall Bridge North", "Pimlico / Rampayne St", 51.4903, -0.1331, "00001.04324"),
                        LondonCam("JamCams_00001.04256", "Vauxhall Cross Gyratory", "Vauxhall Station / Albert Embankment", 51.4864, -0.1237, "00001.04256"),
                        LondonCam("JamCams_00001.04502", "Westminster Bridge / York Rd", "County Hall / Victoria Embankment", 51.5009, -0.1176, "00001.04502"),
                        LondonCam("JamCams_00001.04227", "Westminster Bridge South", "St Thomas' / Waterloo Approach", 51.5012, -0.1158, "00001.04227"),
                        LondonCam("JamCams_00001.04223", "Waterloo Bridge / Strand", "Kingsway Underpass South", 51.5053, -0.1140, "00001.04223"),
                    }
                },

                // =========================================================================
                // QUEENSLAND, AUSTRALIA (TMR QLDTraffic Live Motorway Corridors)
                // =========================================================================
                new CommuteCorridorDto
                {
                    Id = "qld-m1-pacific",
                    City = "queensland",
                    ShortName = "M1 Pacific (Bris ↔ GC)",
                    Name = "M1 Pacific Motorway (Brisbane ↔ Gold Coast)",
                    Description = "Woolloongabba, Upper Mt Gravatt, Rochedale, Loganholme, Beenleigh, Coomera & Helensvale",
                    InboundLabel = "Northbound (Gold Coast → Brisbane CBD)",
                    OutboundLabel = "Southbound (Brisbane CBD → Gold Coast)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        QldCam("qld-137", "M3 / M1 Woolloongabba", "Arrow St / Coronation Dr Merge", -27.4951, 153.0381, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/MRMETRO-1214.jpg"),
                        QldCam("qld-39",  "M1 Upper Mt Gravatt", "Klumpp Rd / Garden City", -27.5509, 153.0677, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/Upper_MtGravatt_Pac_Mwy_Nth.jpg"),
                        QldCam("qld-42",  "M1 Rochedale / Gateway Merge", "Gateway Motorway Overpass", -27.5813, 153.1034, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/Rochedale_Pac_Mwy_Sth.jpg"),
                        QldCam("qld-101", "M1 Rochedale South (Exit 19)", "200m North of Exit 19", -27.6067, 153.1197, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/MRSCHD-298.jpg"),
                        QldCam("qld-105", "M1 Slacks Creek (Exit 23)", "Chatswood Rd / Paradise Rd", -27.6379, 153.1375, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/MRSCHD-294.jpg"),
                        QldCam("qld-99",  "M1 Daisy Hill (Exit 24)", "Winnetts Rd Interchange", -27.6437, 153.1447, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/MRSCHD-300.jpg"),
                        QldCam("qld-33",  "M1 Loganholme / Grandis St", "Logan Motorway Approach", -27.6634, 153.1719, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/GrandisNth.jpg"),
                        QldCam("qld-89",  "M1 Logan River Bridge", "Beenleigh North", -27.6980, 153.1957, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/MRSCHD-311.jpg"),
                        QldCam("qld-93",  "M1 Yatala South (Exit 41)", "Computer Rd / Yatala", -27.7549, 153.2378, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/m1-exit-41-yatala-south.jpg"),
                        QldCam("qld-102", "M1 Coomera (Exit 54)", "Dreamworld / Coomera Interchange", -27.8580, 153.3085, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/MRSCHD-297.jpg"),
                        QldCam("qld-100", "M1 Oxenford (Exit 57)", "Tamborine-Oxenford Rd", -27.8852, 153.3149, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/MRSCHD-299.jpg"),
                        QldCam("qld-106", "M1 Helensvale (Exit 60)", "Gold Coast Hwy Interchange", -27.9082, 153.3178, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/MRSCHD-293.jpg"),
                        QldCam("qld-22",  "M1 Gaven / Smith St Mwy (Exit 66)", "Southport / Smith St Turnoff", -27.9509, 153.3433, "https://cameras.qldtraffic.qld.gov.au/Gold_Coast/m1-smithstreet.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "qld-gateway",
                    City = "queensland",
                    ShortName = "M1 Gateway Motorway",
                    Name = "M1 Gateway Motorway (Bracken Ridge ↔ Gateway Bridge ↔ Rochedale)",
                    Description = "Bracken Ridge, Nudgee, Airport / Southern Cross Way, Sir Leo Hielscher Bridge & Eight Mile Plains",
                    InboundLabel = "Northbound (Rochedale → Airport / Bracken Ridge)",
                    OutboundLabel = "Southbound (Bracken Ridge → Gateway Bridge / M1)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        QldCam("qld-180", "M1 Gateway Bracken Ridge (Exit 123)", "Bracken Ridge Rd Interchange", -27.3116, 153.0369, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/118696-8-Eastbound_Mainline_coverage_including_Bracken_Ridge_Rd_Exit_lane.jpg"),
                        QldCam("qld-175", "M1 Gateway Nudgee Interchange", "Nudgee Rd / Banyo", -27.3724, 153.0974, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/118696-3-Southbound_Nudgee_Interchange.jpg"),
                        QldCam("qld-173", "M1 Gateway / Southern Cross Way", "Brisbane Airport Link Merge", -27.3874, 153.0944, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/118696-1-Southern_Cross_Way_interchange.jpg"),
                        QldCam("qld-37",  "M1 Gateway Eagle Farm", "Kingsford Smith Dr Interchange", -27.4344, 153.0863, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/MRMETRO-1219d.jpg"),
                        QldCam("qld-31",  "M1 Sir Leo Hielscher (Gateway) Bridge", "Brisbane River Crossing", -27.4553, 153.1073, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/gateway.jpg"),
                        QldCam("qld-78",  "M1 Gateway Murarrie", "Lytton Rd / Port of Brisbane Link", -27.4551, 153.1070, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/MRMETRO-1465.jpg"),
                        QldCam("qld-83",  "M1 Gateway Wishart", "Mt Gravatt-Capalaba Rd", -27.5452, 153.1157, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/105223_Metropolitan-Wishart-Mt-Gravatt-Capalaba-Rd.jpg"),
                        QldCam("qld-41",  "M1 Gateway Rochedale South", "Miles Platting Rd / Pacific Mwy Merge", -27.5777, 153.1058, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/rochedale_gateway_mwy_sth.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "qld-bruce-north",
                    City = "queensland",
                    ShortName = "M1 Bruce Hwy North",
                    Name = "M1 Bruce Highway (North Lakes ↔ Caboolture ↔ Sunshine Coast)",
                    Description = "North Lakes, Narangba, Burpengary, Caboolture / Bribie Island Rd & Beerburrum",
                    InboundLabel = "Southbound (Sunshine Coast → Brisbane)",
                    OutboundLabel = "Northbound (Brisbane → Sunshine Coast)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        QldCam("qld-126", "M1 Bruce Hwy North Lakes", "Anzac Ave / Redcliffe Turnoff", -27.2461, 153.0173, "https://cameras.qldtraffic.qld.gov.au/Sunshine_Coast/MRNCHD-423.jpg"),
                        QldCam("qld-55",  "M1 Bruce Hwy Narangba", "Boundary Rd Interchange", -27.2125, 152.9916, "https://cameras.qldtraffic.qld.gov.au/Sunshine_Coast/Bndry-Rd-Nrngba-sth.jpg"),
                        QldCam("qld-58",  "M1 Bruce Hwy Burpengary", "Station Rd / Deception Bay Rd", -27.1531, 152.9774, "https://cameras.qldtraffic.qld.gov.au/Sunshine_Coast/bruce_hwy_burpengary.jpg"),
                        QldCam("qld-57",  "M1 Bruce Hwy Uhlmann Rd", "Burpengary North", -27.1391, 152.9780, "https://cameras.qldtraffic.qld.gov.au/Sunshine_Coast/bruce-uhlmann-north.jpg"),
                        QldCam("qld-56",  "M1 Bruce Hwy Caboolture", "Bribie Island Rd Interchange", -27.0855, 152.9753, "https://cameras.qldtraffic.qld.gov.au/Sunshine_Coast/Bribie_Island_Rd_Interchange_South.jpg"),
                        QldCam("qld-146", "M1 Bruce Hwy Beerburrum", "Steve Irwin Way / Red Rd", -26.9578, 152.9870, "https://cameras.qldtraffic.qld.gov.au/Sunshine_Coast/Bruce_Hwy_Red_Rd_Beerburrum_South.jpg"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "qld-west-ipswich",
                    City = "queensland",
                    ShortName = "M2 / M5 West & Ipswich",
                    Name = "M5 Western & Centenary Fwy + M2 Ipswich Motorway",
                    Description = "Toowong, Indooroopilly, Centenary Bridge, Darra, Archerfield, Goodna & Dinmore",
                    InboundLabel = "Citybound (Ipswich / West → Brisbane)",
                    OutboundLabel = "Westbound (Brisbane → Centenary / Ipswich)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        QldCam("qld-38",  "M5 Western Fwy Toowong", "Mt Coot-tha Rd Roundabout", -27.4783, 152.9848, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/Toowong_MtCootha_West.jpg"),
                        QldCam("qld-6",   "M5 Western Fwy Indooroopilly", "Moggill Rd Interchange", -27.5007, 152.9627, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/Indooroopilly_Western_Fwy_Sth.jpg"),
                        QldCam("qld-127", "M5 Centenary Bridge", "Fig Tree Pocket / Jindalee", -27.5243, 152.9472, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/MRMETRO-1226.jpg"),
                        QldCam("qld-136", "M5 Centenary Hwy Darra", "Sumner Rd / Ipswich Mwy Approach", -27.5598, 152.9396, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/MRMETRO-1215.jpg"),
                        QldCam("qld-1",   "M7 Ipswich Rd Archerfield", "Granard Rd / Rocklea", -27.5552, 153.0087, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/Archerfield_Ipswich_Mwy_sth.jpg"),
                        QldCam("qld-32",  "M2 Ipswich Mwy Goodna", "Railway Tce / Woogaroo Creek", -27.6016, 152.8866, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/Goodna_Ipswich_Mwy_East.jpg"),
                        QldCam("qld-29",  "M2 Ipswich Mwy Dinmore", "Warrego Hwy Merge", -27.5957, 152.8415, "https://cameras.qldtraffic.qld.gov.au/Metropolitan/Dinmore_Ipswich_Mwy_east.jpg"),
                    }
                },

                // =========================================================================
                // NEW YORK CITY, USA (NYC DOT / NYCTMC Live Commute Corridors)
                // =========================================================================
                new CommuteCorridorDto
                {
                    Id = "nyc-fdr",
                    City = "nyc",
                    ShortName = "FDR Drive (East Side)",
                    Name = "FDR Drive & Harlem River Dr (Battery Park ↔ Harlem)",
                    Description = "Battery Park Underpass, Brooklyn Bridge, Lower East Side, Midtown Tunnel, Upper East Side & Harlem",
                    InboundLabel = "Southbound (Harlem → Downtown / Battery)",
                    OutboundLabel = "Northbound (Battery → Midtown / Harlem)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        NycCam("nyc-fdr-bpu", "FDR Dr @ Battery Park Underpass", "Lower Manhattan / South Ferry", 40.7018, -74.0108, "4048d465-2272-446e-bccb-c49ea6d62bd3"),
                        NycCam("nyc-fdr-bb",  "FDR Dr @ Brooklyn Bridge", "Civic Center / Peck Slip", 40.7084, -73.9985, "ecba28cb-ac70-4d25-abcb-6506111ea120"),
                        NycCam("nyc-fdr-grd", "FDR Dr @ Grand St", "Lower East Side / Williamsburg Br", 40.7132, -73.9775, "24c402b2-915d-49f8-9529-f723a171c134"),
                        NycCam("nyc-fdr-e6",  "FDR Dr @ E 6th St", "East Village", 40.7215, -73.9746, "c75873dc-16d0-4e9c-b5b5-1c6ede0b0b63"),
                        NycCam("nyc-fdr-e23", "FDR Dr @ E 23rd St", "Stuyvesant Town / Kips Bay", 40.7348, -73.9747, "e278de5c-4cfa-46e7-aa85-59512db61f0d"),
                        NycCam("nyc-fdr-e36", "FDR Dr @ E 36th St", "Queens-Midtown Tunnel Approach", 40.7444, -73.9712, "04ffc69c-92a7-4c02-aa18-2b16e7ba81f2"),
                        NycCam("nyc-fdr-e53", "FDR Dr @ E 53rd St", "Sutton Place / UN Plaza North", 40.7547, -73.9622, "8d7e94a5-7a81-4e92-8171-9f6c0f0ca7bf"),
                        NycCam("nyc-fdr-e64", "FDR Dr @ E 64th St", "Upper East Side / Queensboro Br", 40.7612, -73.9557, "c5040f93-f4ec-4803-8370-8b931b0443e2"),
                        NycCam("nyc-fdr-e79", "FDR Dr @ E 79th St", "Yorkville / Gracie Mansion", 40.7703, -73.9476, "0267c1e2-6eaf-49d4-a063-df89e9242993"),
                        NycCam("nyc-fdr-e96", "FDR Dr @ E 96th St", "Carnegie Hill / East Harlem", 40.7821, -73.9437, "155b2bff-5dd2-4109-bd10-e098376c8476"),
                        NycCam("nyc-fdr-122", "FDR Dr @ E 122nd St", "RFK Triborough Bridge Approach", 40.7987, -73.9291, "ec1e7b42-18de-4475-8c89-9e80f21e5b6c"),
                        NycCam("nyc-fdr-145", "Harlem River Dr @ 145th St", "Harlem / Bronx Bridge Crossings", 40.8196, -73.9342, "6c2996f1-3d8f-44a9-8ce0-a827ae8d3680"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "nyc-bqe",
                    City = "nyc",
                    ShortName = "I-278 BQE (Bklyn ↔ Qns)",
                    Name = "I-278 Brooklyn-Queens Expressway (Bay Ridge ↔ Astoria)",
                    Description = "Belt Pkwy Merge, Gowanus, Atlantic Ave, Brooklyn Heights Cantilever, Williamsburg, Kosciuszko Br & Queens Blvd",
                    InboundLabel = "Southbound (Astoria / Queens → Brooklyn)",
                    OutboundLabel = "Northbound (Brooklyn → Kosciuszko / Queens)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        NycCam("nyc-bqe-60",    "I-278 BQE @ Belt Pkwy & 60th St", "Sunset Park / Verrazzano Approach", 40.6419, -74.0203, "2b06cd6d-e698-4c1a-b306-6827fb19ced7"),
                        NycCam("nyc-bqe-union", "I-278 BQE @ Union St", "Carroll Gardens / Gowanus Canal", 40.6841, -74.0015, "ea774ec2-327b-4f3d-97ac-fd404c612cb1"),
                        NycCam("nyc-bqe-atl",   "I-278 BQE @ Atlantic Ave", "Cobble Hill / Brooklyn Pier 6", 40.6906, -73.9985, "47070afa-8638-4dfe-8df0-bdfa391f57b7"),
                        NycCam("nyc-bqe-fur",   "I-278 BQE @ Furman St Cantilever", "Brooklyn Heights Promenade", 40.7000, -73.9962, "03051d28-db9c-4527-97c7-0bb3f35e7cef"),
                        NycCam("nyc-bqe-sands", "I-278 BQE @ Sands St", "DUMBO / Manhattan Bridge Merge", 40.6994, -73.9839, "4be502c3-830e-40d3-8c39-1047d50adc9c"),
                        NycCam("nyc-bqe-wythe", "I-278 BQE @ Wythe Ave", "Williamsburg / Bedford Ave", 40.7017, -73.9612, "6d42b7fb-604b-4172-b35d-c0556c5d3fcf"),
                        NycCam("nyc-bqe-metro", "I-278 BQE @ Metropolitan Ave", "Williamsburg Bridge Approach", 40.7137, -73.9533, "6232350a-c27c-4964-a6e8-7bd0507b6826"),
                        NycCam("nyc-bqe-kosc",  "I-278 BQE @ Kosciuszko Bridge", "Greenpoint / Newtown Creek", 40.7250, -73.9341, "433c7f12-86e0-41ea-bb29-781bef1b2444"),
                        NycCam("nyc-bqe-qblvd", "I-278 BQE @ Queens Blvd", "Woodside / LIE Interchange", 40.7388, -73.9016, "d872e1a0-8c5f-47da-9469-bf40a3673ade"),
                        NycCam("nyc-bqe-30av",  "I-278 BQE @ 30th Ave", "Astoria / Grand Central Pkwy Link", 40.7594, -73.9017, "13064b49-6f97-43a2-9cf8-947767afaa12"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "nyc-west-side",
                    City = "nyc",
                    ShortName = "NY-9A West Side Hwy",
                    Name = "NY-9A West Side Highway / 12th Ave (Battery ↔ Midtown West)",
                    Description = "Brooklyn-Battery Tunnel, WTC, Tribeca, Holland Tunnel (Canal St), Chelsea Piers, Lincoln Tunnel & 57th St",
                    InboundLabel = "Southbound (Midtown West → Financial District)",
                    OutboundLabel = "Northbound (Battery → Lincoln Tunnel / 57th St)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        NycCam("nyc-ws-bat", "NY-9A West St @ Battery Place", "Battery Park / South Ferry", 40.7049, -74.0169, "140a20d7-7ae7-409c-8c18-67d6b2ef7b2d"),
                        NycCam("nyc-ws-bbt", "NY-9A West St @ Battery Tunnel", "Hugh L. Carey Tunnel Portal", 40.7077, -74.0156, "86d418ab-10bc-4b73-a283-153abffabb0f"),
                        NycCam("nyc-ws-wtc", "NY-9A West St @ Fulton St", "One World Trade Center", 40.7130, -74.0140, "5c4582a7-6492-41ac-9bac-fa872878117b"),
                        NycCam("nyc-ws-chm", "NY-9A West St @ Chambers St", "Tribeca", 40.7172, -74.0129, "34616ac5-0cc5-4b86-ac7a-82c77238a536"),
                        NycCam("nyc-ws-cnl", "NY-9A West St @ Canal St", "Holland Tunnel (I-78) Approach", 40.7258, -74.0111, "7d10cfbb-0465-4958-90fc-ddd5349d6635"),
                        NycCam("nyc-ws-hou", "NY-9A West St @ W Houston St", "Hudson Square / Pier 40", 40.7291, -74.0105, "67a4769b-dfb2-4a0d-9605-114df107646c"),
                        NycCam("nyc-ws-22",  "NY-9A 12th Ave @ W 22nd St", "Chelsea Piers", 40.7483, -74.0076, "9b923606-16ba-45ad-a105-aabcc98ef1fa"),
                        NycCam("nyc-ws-34",  "NY-9A 12th Ave @ W 34th St", "Hudson Yards / Lincoln Tunnel South", 40.7570, -74.0048, "3809d95f-f99e-4eca-9121-9c7db73f8cbd"),
                        NycCam("nyc-ws-42",  "NY-9A 12th Ave @ W 42nd St", "Lincoln Tunnel / Circle Line", 40.7620, -74.0011, "9bd74b87-32d1-4767-8081-86a2e83f28f2"),
                        NycCam("nyc-ws-46",  "NY-9A 12th Ave @ W 46th St", "Intrepid Museum Pier 86", 40.7643, -73.9986, "2be7f4d2-3a72-45ba-95ad-b526643ef248"),
                        NycCam("nyc-ws-57",  "NY-9A 12th Ave @ W 57th St", "Henry Hudson Pkwy Start", 40.7715, -73.9942, "3095997f-dd41-4e71-a810-9290576c17a6"),
                    }
                },
                new CommuteCorridorDto
                {
                    Id = "nyc-crossings",
                    City = "nyc",
                    ShortName = "East River Bridges & LIE",
                    Name = "East River Bridges, I-495 Long Island Expy & Grand Central Pkwy",
                    Description = "Brooklyn Bridge, Manhattan Bridge, Williamsburg Bridge, Queensboro Bridge, LIE & LaGuardia Airport",
                    InboundLabel = "Manhattan-bound (Queens / Brooklyn → Manhattan)",
                    OutboundLabel = "Outbound (Manhattan → Brooklyn / Queens / LGA)",
                    Cameras = new List<CommuteCameraDto>
                    {
                        NycCam("nyc-br-bb",   "Brooklyn Bridge Midspan", "North Roadway Midspan", 40.7060, -73.9967, "32651453-d9de-4cde-af4b-be42b8de2775"),
                        NycCam("nyc-br-mhb",  "Manhattan Bridge Upper Rdwy", "Brooklyn Tower Walkway", 40.7050, -73.9896, "00077ee3-2c13-4674-adbd-a51b3484b0be"),
                        NycCam("nyc-br-wbb",  "Williamsburg Bridge @ Delancey", "Manhattan Approach / FDR", 40.7148, -73.9766, "ae2f7e1a-e781-4d24-9434-12f4f4ef4f2b"),
                        NycCam("nyc-br-qbb1", "Queensboro Bridge West Channel", "Manhattan / Roosevelt Island Span", 40.7580, -73.9570, "0173e3be-ac26-49b0-bf76-642428d0721a"),
                        NycCam("nyc-br-qbb2", "Queensboro Bridge Queens Pier", "Upper Roadway East Span", 40.7553, -73.9509, "bfcb2b32-7ae7-4060-a39b-80f71a1db289"),
                        NycCam("nyc-br-qbb3", "Queensboro Bridge @ Thomson Ave", "Long Island City Plaza", 40.7456, -73.9394, "b963b25c-9fcf-4a12-b502-c57468510315"),
                        NycCam("nyc-lie-grd", "I-495 Long Island Expy @ Grand Ave", "Maspeth / BQE Interchange", 40.7258, -73.8956, "7c92c63b-2786-4600-9108-62cea2806345"),
                        NycCam("nyc-lie-gcp", "I-495 LIE @ Grand Central Pkwy", "Corona / Flushing Meadows", 40.7401, -73.8450, "6504ada3-3a93-4811-b2cd-31c377066f66"),
                        NycCam("nyc-gcp-lga", "Grand Central Pkwy @ LGA Exit", "LaGuardia Airport 94th St", 40.7715, -73.8775, "9fa5b0dd-e955-449e-97e1-9c53ad9c23a8"),
                    }
                }
            };
        }

        private static CommuteCameraDto Cam(string id, string name, string subtitle, double lat, double lng, string imageUrl)
        {
            return new CommuteCameraDto
            {
                Id = id,
                Name = name,
                Subtitle = subtitle,
                Lat = lat,
                Lng = lng,
                ImageUrl = imageUrl,
                City = "auckland"
            };
        }

        private static CommuteCameraDto WlgCam(string id, string name, string subtitle, double lat, double lng, string imageUrl)
        {
            return new CommuteCameraDto
            {
                Id = id,
                Name = name,
                Subtitle = subtitle,
                Lat = lat,
                Lng = lng,
                ImageUrl = imageUrl,
                City = "wellington"
            };
        }

        private static CommuteCameraDto LondonCam(string id, string name, string subtitle, double lat, double lng, string s3Key)
        {
            return new CommuteCameraDto
            {
                Id = id,
                Name = name,
                Subtitle = subtitle,
                Lat = lat,
                Lng = lng,
                ImageUrl = $"https://s3-eu-west-1.amazonaws.com/jamcams.tfl.gov.uk/{s3Key}.jpg",
                VideoUrl = $"https://s3-eu-west-1.amazonaws.com/jamcams.tfl.gov.uk/{s3Key}.mp4",
                City = "london"
            };
        }

        private static CommuteCameraDto QldCam(string id, string name, string subtitle, double lat, double lng, string imageUrl)
        {
            return new CommuteCameraDto
            {
                Id = id,
                Name = name,
                Subtitle = subtitle,
                Lat = lat,
                Lng = lng,
                ImageUrl = imageUrl,
                City = "queensland"
            };
        }

        private static CommuteCameraDto NycCam(string id, string name, string subtitle, double lat, double lng, string nycUuid)
        {
            return new CommuteCameraDto
            {
                Id = id,
                Name = name,
                Subtitle = subtitle,
                Lat = lat,
                Lng = lng,
                ImageUrl = $"https://nycgrid.mketiku.com/api/camera-image/{nycUuid}",
                City = "nyc"
            };
        }
    }
}
