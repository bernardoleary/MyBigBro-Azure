#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    public class CommuteStripService : BackgroundService
    {
        public const int MaxFramesPerCamera = 15;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

        private readonly ILogger<CommuteStripService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, List<CameraFrameEntry>> _frameBuffers = new();
        private readonly List<CommuteCorridorDto> _corridors;
        private readonly SemaphoreSlim _pollLock = new(1, 1);
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
                        .Where(c => string.Equals(c.City, "auckland", StringComparison.OrdinalIgnoreCase))
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
    }
}
