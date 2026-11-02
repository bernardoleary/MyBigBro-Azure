angular.module('MyBigBroApp.controllers', []).
    controller('mainController', function ($scope, $http, $interval, $timeout, $sce, capturedImagesGeoMarkerService, webCamsService, commuteService) {

        // OpenStreetMap + Leaflet default map setup (centered on Auckland by default)
        var map = L.map('map').setView([-36.8485, 174.7633], 12);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);

        $timeout(function () {
            map.invalidateSize();
        }, 150);

        // Scope variables for Map & Legacy Filters
        $scope.map = map;
        $scope.data = {
            startdatetime: '',
            enddatetime: '',
            devicename: '',
            markerdatetime: ''
        };
        $scope.markers = [];
        $scope.corridorMapMarkers = [];
        $scope.showCorridorCamsOnMap = true;

        // Top-level View Mode: 'strip' (Commute Strip PoC), 'map' (Interactive Map), 'drive' (PWA Wake-Lock GPS Tracker)
        var initialMode = getParameterByName("view");
        var initialDevice = getParameterByName("deviceName");
        $scope.viewMode = initialDevice ? 'map' : (initialMode || 'strip');

        // =========================================================================
        // COMMUTE STRIP & 15-MIN TIME-LAPSE / LIVE JAMCAM STATE
        // =========================================================================
        var savedStarred = [];
        try {
            savedStarred = JSON.parse(localStorage.getItem('mybigbro_starred_cams') || '[]');
        } catch (e) {
            savedStarred = [];
        }

        $scope.commute = {
            city: localStorage.getItem('mybigbro_city') || 'auckland',
            allCorridors: [],
            cityCorridors: [],
            selectedCorridorId: localStorage.getItem('mybigbro_corridor') || 'ak-sh16',
            activeCorridor: null,
            displayedCameras: [],
            isReversed: localStorage.getItem('mybigbro_reversed') === 'true',
            isPlaying: true,
            speedMs: 650,
            frameStep: 0,
            maxFrameCount: 1,
            isPolling: false,
            lastUpdatedText: 'Just now',
            londonVideoMode: true,
            starredIds: savedStarred,
            cacheBust: Date.now()
        };

        // =========================================================================
        // PWA DRIVE TRACKER (Wake Lock + Browser Geolocation) STATE
        // =========================================================================
        $scope.drive = {
            isTracking: false,
            deviceName: localStorage.getItem('mybigbro_drive_device') || 'pwa-commuter-' + Math.floor(100 + Math.random() * 900),
            wakeLockActive: false,
            watchId: null,
            wakeLockSentinel: null,
            pingCount: 0,
            lastLat: null,
            lastLng: null,
            lastPingTime: null,
            nearestCam: null,
            statusMessage: 'Ready to track your commute. Keep this screen open on your dash mount.'
        };

        $scope.trustUrl = function (url) {
            if (!url) return '';
            return $sce.trustAsResourceUrl(url);
        };

        $scope.setViewMode = function (mode) {
            $scope.viewMode = mode;
            if (mode === 'map') {
                $timeout(function () {
                    $scope.map.invalidateSize();
                    drawCorridorCamerasOnMap();
                }, 100);
            }
        };

        // Load Corridors from Backend
        $scope.loadCommuteCorridors = function (preserveCursor) {
            commuteService.getCorridors().success(function (corridors) {
                $scope.commute.allCorridors = corridors || [];
                $scope.commute.lastUpdatedText = moment().format('h:mm:ss a');
                updateCityCorridors(preserveCursor);
                drawCorridorCamerasOnMap();
            });
        };

        function updateCityCorridors(preserveCursor) {
            var city = $scope.commute.city;
            var filtered = [];
            for (var i = 0; i < $scope.commute.allCorridors.length; i++) {
                if ($scope.commute.allCorridors[i].City === city) {
                    filtered.push($scope.commute.allCorridors[i]);
                }
            }
            $scope.commute.cityCorridors = filtered;

            if ($scope.commute.selectedCorridorId === 'starred') {
                buildStarredCorridor(preserveCursor);
                return;
            }

            var found = null;
            for (var j = 0; j < filtered.length; j++) {
                if (filtered[j].Id === $scope.commute.selectedCorridorId) {
                    found = filtered[j];
                    break;
                }
            }
            if (!found && filtered.length > 0) {
                found = filtered[0];
                $scope.commute.selectedCorridorId = found.Id;
                localStorage.setItem('mybigbro_corridor', found.Id);
            }

            $scope.commute.activeCorridor = found;
            rebuildDisplayedCameras(preserveCursor);
        }

        function buildStarredCorridor(preserveCursor) {
            var seen = {};
            var cams = [];
            for (var i = 0; i < $scope.commute.allCorridors.length; i++) {
                var corr = $scope.commute.allCorridors[i];
                for (var j = 0; j < corr.Cameras.length; j++) {
                    var c = corr.Cameras[j];
                    if ($scope.isStarred(c.Id) && !seen[c.Id]) {
                        seen[c.Id] = true;
                        cams.push(c);
                    }
                }
            }
            $scope.commute.activeCorridor = {
                Id: 'starred',
                City: $scope.commute.city,
                ShortName: 'My Starred Cams',
                Name: 'My Custom Commute Strip (' + cams.length + ' cameras)',
                Description: 'Your personal starred cameras across Auckland & London corridors',
                InboundLabel: 'Reverse Order',
                OutboundLabel: 'Saved Order',
                Cameras: cams
            };
            rebuildDisplayedCameras(preserveCursor);
        }

        function rebuildDisplayedCameras(preserveCursor) {
            if (!$scope.commute.activeCorridor || !$scope.commute.activeCorridor.Cameras) {
                $scope.commute.displayedCameras = [];
                $scope.commute.maxFrameCount = 1;
                return;
            }

            var list = $scope.commute.activeCorridor.Cameras.slice();
            if ($scope.commute.isReversed) {
                list.reverse();
            }
            $scope.commute.displayedCameras = list;

            var maxF = 1;
            for (var i = 0; i < list.length; i++) {
                if (list[i].Frames && list[i].Frames.length > maxF) {
                    maxF = list[i].Frames.length;
                }
            }
            $scope.commute.maxFrameCount = maxF;
            if (!preserveCursor || $scope.commute.frameStep >= maxF) {
                $scope.commute.frameStep = maxF - 1;
            }
        }

        $scope.selectCity = function (city) {
            $scope.commute.city = city;
            localStorage.setItem('mybigbro_city', city);
            if ($scope.commute.selectedCorridorId === 'starred') {
                $scope.commute.selectedCorridorId = '';
            }
            updateCityCorridors(false);
            if ($scope.viewMode === 'map') {
                if (city === 'london') {
                    $scope.map.setView([51.509865, -0.118092], 11);
                } else {
                    $scope.map.setView([-36.8485, 174.7633], 11);
                }
                drawCorridorCamerasOnMap();
            }
        };

        $scope.selectCorridor = function (corridorId) {
            $scope.commute.selectedCorridorId = corridorId;
            localStorage.setItem('mybigbro_corridor', corridorId);
            updateCityCorridors(false);
        };

        $scope.toggleDirection = function () {
            $scope.commute.isReversed = !$scope.commute.isReversed;
            localStorage.setItem('mybigbro_reversed', $scope.commute.isReversed ? 'true' : 'false');
            rebuildDisplayedCameras(true);
        };

        $scope.togglePlay = function () {
            $scope.commute.isPlaying = !$scope.commute.isPlaying;
        };

        $scope.setSpeed = function (ms) {
            $scope.commute.speedMs = ms;
            restartPlaybackTimer();
        };

        $scope.isStarred = function (camId) {
            return $scope.commute.starredIds.indexOf(camId) !== -1;
        };

        $scope.toggleStar = function (camId, $event) {
            if ($event) {
                $event.stopPropagation();
            }
            var idx = $scope.commute.starredIds.indexOf(camId);
            if (idx === -1) {
                $scope.commute.starredIds.push(camId);
            } else {
                $scope.commute.starredIds.splice(idx, 1);
            }
            try {
                localStorage.setItem('mybigbro_starred_cams', JSON.stringify($scope.commute.starredIds));
            } catch (e) { }

            if ($scope.commute.selectedCorridorId === 'starred') {
                buildStarredCorridor(true);
            }
        };

        $scope.getActiveFrameUrl = function (cam) {
            if (!cam) return '';
            if (cam.Frames && cam.Frames.length > 0) {
                var idx = $scope.commute.frameStep;
                if (idx >= cam.Frames.length) {
                    idx = cam.Frames.length - 1;
                }
                if (idx < 0) idx = 0;
                return cam.Frames[idx].Url;
            }
            // Fallback to live URL with cache-bust timestamp
            var sep = cam.ImageUrl.indexOf('?') === -1 ? '?' : '&';
            return cam.ImageUrl + sep + '_cb=' + $scope.commute.cacheBust;
        };

        $scope.getActiveFrameTimestamp = function (cam) {
            if (cam && cam.Frames && cam.Frames.length > 0) {
                var idx = $scope.commute.frameStep;
                if (idx >= cam.Frames.length) idx = cam.Frames.length - 1;
                if (idx < 0) idx = 0;
                return moment(cam.Frames[idx].CapturedAtUtc).local().format('h:mm:ss a');
            }
            return 'Live Feed';
        };

        $scope.getLondonVideoUrl = function (cam) {
            if (!cam || !cam.VideoUrl) return '';
            var sep = cam.VideoUrl.indexOf('?') === -1 ? '?' : '&';
            return $scope.trustUrl(cam.VideoUrl + sep + '_cb=' + $scope.commute.cacheBust);
        };

        $scope.pollFreshFrameNow = function () {
            if ($scope.commute.isPolling) return;
            $scope.commute.isPolling = true;
            $scope.commute.cacheBust = Date.now();
            var corrId = ($scope.commute.selectedCorridorId && $scope.commute.selectedCorridorId !== 'starred')
                ? $scope.commute.selectedCorridorId
                : null;
            commuteService.triggerPoll(corrId).success(function () {
                $scope.loadCommuteCorridors(false);
                $scope.commute.isPolling = false;
            }).error(function () {
                $scope.commute.isPolling = false;
            });
        };

        $scope.focusCameraOnMap = function (cam) {
            $scope.setViewMode('map');
            $timeout(function () {
                $scope.map.setView([cam.Lat, cam.Lng], 15);
                var title = cam.Name + (cam.Subtitle ? ' (' + cam.Subtitle + ')' : '');
                var bodyHtml = '';
                if (cam.City === 'london' && cam.VideoUrl) {
                    bodyHtml = '<div class="text-center"><video autoplay loop muted playsinline controls style="width:100%; max-height:420px; background:#000; border-radius:6px;" src="' + cam.VideoUrl + '" poster="' + cam.ImageUrl + '"></video></div>';
                } else {
                    var imgUrl = $scope.getActiveFrameUrl(cam);
                    bodyHtml = '<div class="text-center"><img class="img-responsive img-thumbnail" style="max-height:460px; margin:0 auto;" src="' + imgUrl + '" alt="' + cam.Name + '"></div>';
                }
                $("#mapModal #mapModalTitle").html(title);
                $("#mapModal #mapModalBody").html(bodyHtml);
                $('#mapModal').modal("show");
            }, 200);
        };

        // Time-lapse frame animation timer
        var playbackPromise = null;
        function restartPlaybackTimer() {
            if (playbackPromise) {
                $interval.cancel(playbackPromise);
            }
            playbackPromise = $interval(function () {
                if ($scope.viewMode === 'strip' && $scope.commute.isPlaying && $scope.commute.maxFrameCount > 1) {
                    $scope.commute.frameStep = ($scope.commute.frameStep + 1) % $scope.commute.maxFrameCount;
                }
            }, $scope.commute.speedMs);
        }
        restartPlaybackTimer();

        // Periodically refresh buffered frames metadata from backend every 30s
        var refreshCorridorsPromise = $interval(function () {
            $scope.loadCommuteCorridors(true);
        }, 30000);

        $scope.$on('$destroy', function () {
            if (playbackPromise) $interval.cancel(playbackPromise);
            if (refreshCorridorsPromise) $interval.cancel(refreshCorridorsPromise);
            $scope.stopDriveTracking();
        });

        // Initial load of commute corridors
        $scope.loadCommuteCorridors(false);

        // =========================================================================
        // PWA DRIVE MODE (Browser Geolocation + Screen Wake Lock)
        // =========================================================================
        $scope.toggleDriveTracking = function () {
            if ($scope.drive.isTracking) {
                $scope.stopDriveTracking();
            } else {
                $scope.startDriveTracking();
            }
        };

        $scope.startDriveTracking = function () {
            if (!navigator.geolocation) {
                $scope.drive.statusMessage = 'Geolocation is not supported by your browser.';
                return;
            }
            localStorage.setItem('mybigbro_drive_device', $scope.drive.deviceName);
            $scope.drive.isTracking = true;
            $scope.drive.statusMessage = 'Acquiring GPS lock & requesting Screen Wake Lock...';

            // Request Screen Wake Lock so phone doesn't sleep on dash mount
            if ('wakeLock' in navigator) {
                navigator.wakeLock.request('screen').then(function (sentinel) {
                    $scope.drive.wakeLockSentinel = sentinel;
                    $scope.drive.wakeLockActive = true;
                    sentinel.addEventListener('release', function () {
                        $scope.$applyAsync(function () {
                            $scope.drive.wakeLockActive = false;
                        });
                    });
                    $scope.$applyAsync();
                }).catch(function () {
                    $scope.$applyAsync(function () {
                        $scope.drive.wakeLockActive = false;
                    });
                });
            }

            $scope.drive.watchId = navigator.geolocation.watchPosition(
                function (pos) {
                    $scope.$applyAsync(function () {
                        $scope.sendGpsCoordinate(pos.coords.latitude, pos.coords.longitude);
                    });
                },
                function (err) {
                    $scope.$applyAsync(function () {
                        $scope.drive.statusMessage = 'GPS Warning: ' + err.message + ' (Use "Simulate SH16 Ping" to test on desktop).';
                    });
                },
                {
                    enableHighAccuracy: true,
                    maximumAge: 5000,
                    timeout: 15000
                }
            );
        };

        $scope.stopDriveTracking = function () {
            if ($scope.drive.watchId !== null && navigator.geolocation) {
                navigator.geolocation.clearWatch($scope.drive.watchId);
                $scope.drive.watchId = null;
            }
            if ($scope.drive.wakeLockSentinel) {
                try {
                    $scope.drive.wakeLockSentinel.release();
                } catch (e) { }
                $scope.drive.wakeLockSentinel = null;
            }
            $scope.drive.isTracking = false;
            $scope.drive.wakeLockActive = false;
            $scope.drive.statusMessage = 'Drive tracking stopped.';
        };

        $scope.sendGpsCoordinate = function (lat, lng) {
            $scope.drive.lastLat = lat;
            $scope.drive.lastLng = lng;
            $scope.drive.lastPingTime = moment().format('h:mm:ss a');
            $scope.drive.pingCount++;

            var payload = {
                MarkerDateTime: new Date().toISOString(),
                XCoord: lng,
                YCoord: lat,
                DeviceName: $scope.drive.deviceName
            };

            $http.post('/api/geomarkers', payload).success(function () {
                $scope.drive.statusMessage = 'GPS ping #' + $scope.drive.pingCount + ' recorded (' + lat.toFixed(4) + ', ' + lng.toFixed(4) + '). Checking nearby cameras...';
                webCamsService.getNearestWebCam($scope.drive.deviceName).success(function (nearest) {
                    $scope.drive.nearestCam = nearest;
                });
                drawMarkers();
            }).error(function () {
                $scope.drive.statusMessage = 'Failed to send GPS ping to server.';
            });
        };

        var simStepIndex = 0;
        var sh16SimPoints = [
            { name: 'SH16 Bond St Overbridge', lat: -36.869, lng: 174.746 },
            { name: 'SH16 Carrington Rd', lat: -36.8717, lng: 174.7102 },
            { name: 'SH16 Causeway', lat: -36.8721, lng: 174.6897 },
            { name: 'SH16 Te Atatu Rd', lat: -36.858, lng: 174.653 },
            { name: 'SH16 Lincoln Rd', lat: -36.849, lng: 174.632 }
        ];

        $scope.simulateSh16Ping = function () {
            var pt = sh16SimPoints[simStepIndex % sh16SimPoints.length];
            simStepIndex++;
            localStorage.setItem('mybigbro_drive_device', $scope.drive.deviceName);
            $scope.sendGpsCoordinate(pt.lat, pt.lng);
        };

        // =========================================================================
        // MAP MARKERS (GPS Captures + Corridor Camera Overlay)
        // =========================================================================
        drawMarkers();

        function getParameterByName(name) {
            name = name.replace(/[\[]/, "\\[").replace(/[\]]/, "\\]");
            var regex = new RegExp("[\\?&]" + name + "=([^&#]*)"),
                results = regex.exec(location.search);
            return results === null ? "" : decodeURIComponent(results[1].replace(/\+/g, " "));
        }

        function drawCorridorCamerasOnMap() {
            for (var i = 0; i < $scope.corridorMapMarkers.length; i++) {
                $scope.map.removeLayer($scope.corridorMapMarkers[i]);
            }
            $scope.corridorMapMarkers = [];

            if (!$scope.showCorridorCamsOnMap || !$scope.commute.cityCorridors) {
                return;
            }

            var seen = {};
            for (var c = 0; c < $scope.commute.cityCorridors.length; c++) {
                var corr = $scope.commute.cityCorridors[c];
                for (var k = 0; k < corr.Cameras.length; k++) {
                    var cam = corr.Cameras[k];
                    if (seen[cam.Id]) continue;
                    seen[cam.Id] = true;

                    var camIcon = L.divIcon({
                        className: 'mbb-cam-map-pin',
                        html: '<div style="background:#0284c7;color:#fff;border:2px solid #fff;border-radius:50%;width:26px;height:26px;display:flex;align-items:center;justify-content:center;font-size:12px;box-shadow:0 2px 6px rgba(0,0,0,0.4);" title="' + cam.Name + '">📷</div>',
                        iconSize: [26, 26],
                        iconAnchor: [13, 13]
                    });

                    (function (cameraObj) {
                        var m = L.marker([cameraObj.Lat, cameraObj.Lng], {
                            title: cameraObj.Name,
                            icon: camIcon
                        }).addTo($scope.map);

                        m.on('click', function () {
                            var title = cameraObj.Name + (cameraObj.Subtitle ? ' — ' + cameraObj.Subtitle : '');
                            var bodyHtml = '';
                            if (cameraObj.City === 'london' && cameraObj.VideoUrl) {
                                bodyHtml = '<div class="text-center"><video autoplay loop muted playsinline controls style="width:100%; max-height:420px; background:#000; border-radius:6px;" src="' + cameraObj.VideoUrl + '" poster="' + cameraObj.ImageUrl + '"></video></div>';
                            } else {
                                var imgUrl = $scope.getActiveFrameUrl(cameraObj);
                                bodyHtml = '<div class="text-center"><img class="img-responsive img-thumbnail" style="max-height:460px; margin:0 auto;" src="' + imgUrl + '" alt="' + cameraObj.Name + '"></div>';
                            }
                            $("#mapModal #mapModalTitle").html(title);
                            $("#mapModal #mapModalBody").html(bodyHtml);
                            $('#mapModal').modal("show");
                        });

                        $scope.corridorMapMarkers.push(m);
                    })(cam);
                }
            }
        }

        $scope.toggleCorridorCamsOnMap = function () {
            $scope.showCorridorCamsOnMap = !$scope.showCorridorCamsOnMap;
            drawCorridorCamerasOnMap();
        };

        function drawMarkers() {
            for (var i = 0; i < $scope.markers.length; i++) {
                $scope.map.removeLayer($scope.markers[i]);
            }
            $scope.markers = [];

            if ($scope.data.devicename === undefined || $scope.data.devicename === "") {
                $scope.data.devicename = getParameterByName("deviceName");
            }
            var devicename = $scope.data.devicename;
            var markerCount = getParameterByName("markerCount") === "" ? 10 : getParameterByName("markerCount");

            if (!$scope.data.startdatetime || !$scope.data.enddatetime) {
                if (devicename === undefined || devicename === "") {
                    capturedImagesGeoMarkerService.getCapturedImagesGeoMarkerTop(markerCount).success(function (response) {
                        createMarkers(response);
                    });
                } else {
                    capturedImagesGeoMarkerService.getCapturedImagesGeoMarkerTopAndDeviceName(markerCount, devicename).success(function (response) {
                        createMarkers(response);
                    });
                }
            } else {
                var startdatetimeVal = moment($scope.data.startdatetime).format("YYYY-MM-DDTHH:mm:ss");
                var enddatetimeVal = moment($scope.data.enddatetime).format("YYYY-MM-DDTHH:mm:ss");
                if (devicename === undefined || devicename === "") {
                    capturedImagesGeoMarkerService.getCapturedImagesGeoMarkerForDateTimeWindow(startdatetimeVal, enddatetimeVal).success(function (response) {
                        createMarkers(response);
                    });
                } else {
                    capturedImagesGeoMarkerService.getCapturedImagesGeoMarkerForDateTimeWindowAndDeviceName(startdatetimeVal, enddatetimeVal, devicename).success(function (response) {
                        createMarkers(response);
                    });
                }
            }
        }

        function createMarkers(capturedImagesGeoMarker) {
            if (!capturedImagesGeoMarker || !capturedImagesGeoMarker.length) {
                return;
            }
            for (var i = 0; i < capturedImagesGeoMarker.length; i++) {
                createMarker(capturedImagesGeoMarker[i]);
            }
            if ($scope.viewMode === 'map') {
                fitBounds();
            }
        }

        function fitBounds() {
            if ($scope.markers.length === 0) {
                return;
            }
            $scope.map.invalidateSize();
            var group = L.featureGroup($scope.markers);
            $scope.map.fitBounds(group.getBounds(), { padding: [40, 40], maxZoom: 15 });
        }

        function createMarker(info) {
            var title = "Captured " + moment(info.GeoMarker.MarkerDateTime).format('MMMM Do YYYY, h:mm:ss a');
            var rawUrl = (info.CapturedImage && info.CapturedImage.Url) ? info.CapturedImage.Url : '';
            var imageUrl = rawUrl ? rawUrl.replace('/images/images/', '/images/') : '';
            var content = imageUrl
                ? '<div class="text-center"><img class="img-responsive img-thumbnail" style="max-height: 480px; margin: 0 auto;" src="' + imageUrl + '" alt="Traffic camera capture" onerror="this.onerror=null; this.parentElement.innerHTML=\'<div class=\\\'alert alert-warning\\\'>Captured image could not be loaded from storage.</div>\';"></div>'
                : '<div class="alert alert-info">No image available for this marker.</div>';
            var marker = L.marker([info.GeoMarker.YCoord, info.GeoMarker.XCoord], {
                title: title
            }).addTo($scope.map);
            marker.title = title;
            marker.content = content;
            marker.on('click', function () {
                $("#mapModal #mapModalTitle").html(marker.title);
                $("#mapModal #mapModalBody").html(marker.content);
                $('#mapModal').modal("show");
            });
            $scope.markers.push(marker);
        }

        $scope.openInfoWindow = function (e, selectedMarker) {
            e.preventDefault();
            selectedMarker.fire('click');
        };

        $scope.onStartTimeSet = function (newDate) {
            $scope.data.startdatetime = newDate;
            drawMarkers();
        };

        $scope.onEndTimeSet = function (newDate) {
            $scope.data.enddatetime = newDate;
            drawMarkers();
        };

        $scope.onDeviceNameChange = function ($event) {
            if ($event) {
                $event.preventDefault();
            }
            drawMarkers();
        };

        $scope.onResetFilters = function ($event) {
            if ($event) {
                $event.preventDefault();
            }
            $scope.data.startdatetime = '';
            $scope.data.enddatetime = '';
            $scope.data.devicename = '';
            drawMarkers();
        };
    });
