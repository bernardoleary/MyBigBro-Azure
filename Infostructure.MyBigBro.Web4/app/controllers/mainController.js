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

        // Top-level View Mode: 'strip' (Commute Infographic), 'map' (Interactive Map), 'drive' (PWA Wake-Lock GPS Tracker)
        var initialMode = getParameterByName("view");
        var initialDevice = getParameterByName("deviceName");
        $scope.viewMode = initialDevice ? 'map' : (initialMode || 'strip');

        // =========================================================================
        // COMMUTE STRIP & INFOGRAPHIC JOURNEY DECK STATE
        // =========================================================================
        var savedStarred = [];
        try {
            savedStarred = JSON.parse(localStorage.getItem('mybigbro_starred_cams') || '[]');
        } catch (e) {
            savedStarred = [];
        }

        $scope.commute = {
            city: localStorage.getItem('mybigbro_city') || 'auckland',
            layoutMode: localStorage.getItem('mybigbro_layout') || 'deck', // 'deck' (Drag Up/Down 1-by-1) or 'spine' (Full Infographic Spine)
            allCorridors: [],
            cityCorridors: [],
            selectedCorridorId: localStorage.getItem('mybigbro_corridor') || 'ak-sh16',
            activeCorridor: null,
            displayedCameras: [],
            activeCamIndex: 0,
            totalRouteKm: 0,
            totalRouteMins: 0,
            isReversed: localStorage.getItem('mybigbro_reversed') === 'true',
            isPlaying: true,
            speedMs: 650,
            frameStep: 0,
            maxFrameCount: 1,
            isPolling: false,
            lastUpdatedText: 'Just now',
            londonVideoMode: true,
            starredIds: savedStarred,
            cacheBust: Date.now(),
            dragOffsetY: 0,
            isDragging: false,
            transitionDir: ''
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

        $scope.setLayoutMode = function (mode) {
            $scope.commute.layoutMode = mode;
            localStorage.setItem('mybigbro_layout', mode);
        };

        // Haversine distance in km between two lat/lng points
        function calcDistanceKm(lat1, lon1, lat2, lon2) {
            var R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                Math.cos(lat1 * Math.PI / 180.0) * Math.cos(lat2 * Math.PI / 180.0) *
                Math.sin(dLon / 2) * Math.sin(dLon / 2);
            var c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
            // Multiply by 1.18 road winding factor for realistic motorway road distance
            return R * c * 1.18;
        }

        function detectHighwayBadge(cam) {
            var name = (cam.Name || '').toUpperCase();
            if (name.indexOf('SH16') !== -1) return { code: 'SH16', color: '#dc2626', bg: '#991b1b' };
            if (name.indexOf('SH18') !== -1) return { code: 'SH18', color: '#059669', bg: '#065f46' };
            if (name.indexOf('SH20A') !== -1) return { code: 'SH20A', color: '#d97706', bg: '#92400e' };
            if (name.indexOf('SH20') !== -1) return { code: 'SH20', color: '#7c3aed', bg: '#5b21b6' };
            if (name.indexOf('SH1') !== -1) return { code: 'SH1', color: '#e11d48', bg: '#9f1239' };
            if (name.indexOf('A406') !== -1 || name.indexOf('HANGER') !== -1) return { code: 'A406', color: '#16a34a', bg: '#14532d' };
            if (name.indexOf('A13') !== -1 || name.indexOf('LIMEHOUSE') !== -1 || name.indexOf('A12') !== -1) return { code: 'A13/A12', color: '#0284c7', bg: '#0c4a6e' };
            if (name.indexOf('A40') !== -1) return { code: 'A40', color: '#16a34a', bg: '#14532d' };
            if (name.indexOf('A4 ') !== -1 || name.indexOf('CHISWICK') !== -1) return { code: 'A4/M4', color: '#2563eb', bg: '#1e3a8a' };
            if (cam.City === 'london') return { code: 'TfL', color: '#0284c7', bg: '#0c4a6e' };
            return { code: 'MWY', color: '#0284c7', bg: '#0c4a6e' };
        }

        function detectLandmark(cam) {
            var text = ((cam.Name || '') + ' ' + (cam.Subtitle || '')).toLowerCase();
            if (text.indexOf('causeway') !== -1 || text.indexOf('whau') !== -1) return '🌊 Waitemata Harbour Causeway';
            if (text.indexOf('waterview') !== -1) return '🚇 Waterview Tunnel Interchange';
            if (text.indexOf('onewa') !== -1) return '🌉 Auckland Harbour Bridge North';
            if (text.indexOf('central mwy') !== -1) return '🔀 Central Motorway Junction (Spaghetti Jct)';
            if (text.indexOf('upper harbour bridge') !== -1) return '🌉 Upper Harbour Bridge Crossing';
            if (text.indexOf('airport') !== -1 || text.indexOf('george bolt') !== -1) return '✈️ Auckland International Airport Link';
            if (text.indexOf('onehunga') !== -1 || text.indexOf('coronation') !== -1) return '🌉 Manukau Harbour / Mangere Bridge';
            if (text.indexOf('hanger lane') !== -1) return '🔄 Hanger Lane Gyratory & A40 Interchange';
            if (text.indexOf('ace cafe') !== -1) return '🏍️ Iconic Ace Cafe North Circular';
            if (text.indexOf('limehouse') !== -1) return '🚇 Limehouse Link Tunnel';
            if (text.indexOf('blackwall') !== -1) return '🚇 Blackwall River Thames Tunnel';
            if (text.indexOf('heathrow') !== -1) return '✈️ Heathrow Airport Spur & M4';
            if (text.indexOf('bridge') !== -1 && cam.City === 'london') return '🌉 River Thames Crossing';
            return '';
        }

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
                $scope.commute.activeCamIndex = 0;
                return;
            }

            var rawList = $scope.commute.activeCorridor.Cameras.slice();
            if ($scope.commute.isReversed) {
                rawList.reverse();
            }

            var totalKm = 0;
            var totalMins = 0;
            var enriched = [];
            var avgSpeedKmh = $scope.commute.city === 'london' ? 48 : 82;

            for (var i = 0; i < rawList.length; i++) {
                var cam = angular.copy(rawList[i]);
                var distFromPrev = 0;
                var minsFromPrev = 0;
                if (i > 0) {
                    distFromPrev = calcDistanceKm(rawList[i - 1].Lat, rawList[i - 1].Lng, cam.Lat, cam.Lng);
                    if (distFromPrev > 40) distFromPrev = 2.5; // cap if starred across cities
                    minsFromPrev = Math.max(1, Math.round((distFromPrev / avgSpeedKmh) * 60));
                }
                totalKm += distFromPrev;
                totalMins += minsFromPrev;

                cam.SeqNumber = i + 1;
                cam.DistFromPrevKm = distFromPrev.toFixed(1);
                cam.MinsFromPrev = minsFromPrev;
                cam.CumulativeKm = totalKm.toFixed(1);
                cam.CumulativeMins = totalMins;
                cam.Badge = detectHighwayBadge(cam);
                cam.Landmark = detectLandmark(cam);
                enriched.push(cam);
            }

            for (var k = 0; k < enriched.length; k++) {
                enriched[k].ProgressPct = enriched.length > 1
                    ? Math.round((k / (enriched.length - 1)) * 100)
                    : 100;
            }

            $scope.commute.displayedCameras = enriched;
            $scope.commute.totalRouteKm = totalKm.toFixed(1);
            $scope.commute.totalRouteMins = Math.max(1, totalMins);

            if (!preserveCursor || $scope.commute.activeCamIndex >= enriched.length) {
                $scope.commute.activeCamIndex = 0;
            }

            var maxF = 1;
            for (var m = 0; m < enriched.length; m++) {
                if (enriched[m].Frames && enriched[m].Frames.length > maxF) {
                    maxF = enriched[m].Frames.length;
                }
            }
            $scope.commute.maxFrameCount = maxF;
            if (!preserveCursor || $scope.commute.frameStep >= maxF) {
                $scope.commute.frameStep = maxF - 1;
            }
        }

        // =========================================================================
        // JOURNEY DECK NAVIGATION (Drag Up/Down, Wheel, Keyboard & Rail Click)
        // =========================================================================
        $scope.getCurrentJourneyCamera = function () {
            if (!$scope.commute.displayedCameras || !$scope.commute.displayedCameras.length) {
                return null;
            }
            return $scope.commute.displayedCameras[$scope.commute.activeCamIndex] || $scope.commute.displayedCameras[0];
        };

        $scope.getPrevJourneyCamera = function () {
            if ($scope.commute.activeCamIndex <= 0) return null;
            return $scope.commute.displayedCameras[$scope.commute.activeCamIndex - 1];
        };

        $scope.getNextJourneyCamera = function () {
            if (!$scope.commute.displayedCameras) return null;
            if ($scope.commute.activeCamIndex >= $scope.commute.displayedCameras.length - 1) return null;
            return $scope.commute.displayedCameras[$scope.commute.activeCamIndex + 1];
        };

        $scope.goToCameraIndex = function (idx, dirHint) {
            if (!$scope.commute.displayedCameras || idx < 0 || idx >= $scope.commute.displayedCameras.length) {
                return;
            }
            if (idx === $scope.commute.activeCamIndex) return;

            $scope.commute.transitionDir = dirHint || (idx > $scope.commute.activeCamIndex ? 'next' : 'prev');
            $scope.commute.activeCamIndex = idx;

            $timeout(function () {
                $scope.commute.transitionDir = '';
            }, 260);

            // If in spine mode, also smooth-scroll to that node
            if ($scope.commute.layoutMode === 'spine') {
                var el = document.getElementById('spine-cam-' + idx);
                if (el && el.scrollIntoView) {
                    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }
        };

        $scope.nextCameraInJourney = function () {
            if ($scope.commute.activeCamIndex < $scope.commute.displayedCameras.length - 1) {
                $scope.goToCameraIndex($scope.commute.activeCamIndex + 1, 'next');
            }
        };

        $scope.prevCameraInJourney = function () {
            if ($scope.commute.activeCamIndex > 0) {
                $scope.goToCameraIndex($scope.commute.activeCamIndex - 1, 'prev');
            }
        };

        // Pointer / Touch Drag Up & Down Gesture on the Journey Stage
        var dragStartY = null;
        var lastWheelTime = 0;

        $scope.onStageDragStart = function (clientY) {
            dragStartY = clientY;
            $scope.commute.isDragging = true;
            $scope.commute.dragOffsetY = 0;
        };

        $scope.onStageDragMove = function (clientY) {
            if (dragStartY === null || !$scope.commute.isDragging) return;
            var dy = clientY - dragStartY;
            // Clamp visual drag displacement to [-110, 110] px
            if (dy > 110) dy = 110;
            if (dy < -110) dy = -110;
            $scope.commute.dragOffsetY = dy;
        };

        $scope.onStageDragEnd = function () {
            if (dragStartY === null || !$scope.commute.isDragging) return;
            var dy = $scope.commute.dragOffsetY;
            dragStartY = null;
            $scope.commute.isDragging = false;
            $scope.commute.dragOffsetY = 0;

            // Dragging UP (negative dy) advances forward to the NEXT camera down the highway
            // Dragging DOWN (positive dy) moves back to the PREVIOUS camera
            if (dy < -42) {
                $scope.nextCameraInJourney();
            } else if (dy > 42) {
                $scope.prevCameraInJourney();
            }
        };

        // Bind touch, mouse drag, wheel, and arrow keys to the Journey Stage
        $timeout(function () {
            var stage = document.getElementById('journey-drag-stage');
            if (stage) {
                stage.addEventListener('touchstart', function (e) {
                    if (e.touches && e.touches.length === 1) {
                        $scope.$applyAsync(function () {
                            $scope.onStageDragStart(e.touches[0].clientY);
                        });
                    }
                }, { passive: true });

                stage.addEventListener('touchmove', function (e) {
                    if ($scope.commute.isDragging && e.touches && e.touches.length === 1) {
                        if (Math.abs(e.touches[0].clientY - (dragStartY || 0)) > 12) {
                            e.preventDefault();
                        }
                        $scope.$applyAsync(function () {
                            $scope.onStageDragMove(e.touches[0].clientY);
                        });
                    }
                }, { passive: false });

                stage.addEventListener('touchend', function () {
                    $scope.$applyAsync(function () {
                        $scope.onStageDragEnd();
                    });
                });

                stage.addEventListener('mousedown', function (e) {
                    if (e.target && (e.target.tagName === 'BUTTON' || e.target.tagName === 'INPUT' || e.target.tagName === 'VIDEO')) {
                        return;
                    }
                    $scope.$applyAsync(function () {
                        $scope.onStageDragStart(e.clientY);
                    });
                });

                window.addEventListener('mousemove', function (e) {
                    if ($scope.commute.isDragging) {
                        $scope.$applyAsync(function () {
                            $scope.onStageDragMove(e.clientY);
                        });
                    }
                });

                window.addEventListener('mouseup', function () {
                    if ($scope.commute.isDragging) {
                        $scope.$applyAsync(function () {
                            $scope.onStageDragEnd();
                        });
                    }
                });

                stage.addEventListener('wheel', function (e) {
                    if ($scope.viewMode !== 'strip' || $scope.commute.layoutMode !== 'deck') return;
                    var now = Date.now();
                    if (now - lastWheelTime < 320) {
                        e.preventDefault();
                        return;
                    }
                    if (Math.abs(e.deltaY) > 22) {
                        e.preventDefault();
                        lastWheelTime = now;
                        $scope.$applyAsync(function () {
                            if (e.deltaY > 0) {
                                $scope.nextCameraInJourney();
                            } else {
                                $scope.prevCameraInJourney();
                            }
                        });
                    }
                }, { passive: false });
            }

            window.addEventListener('keydown', function (e) {
                if ($scope.viewMode !== 'strip' || $scope.commute.layoutMode !== 'deck') return;
                if (e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA')) return;
                if (e.key === 'ArrowDown' || e.key === 'j' || e.key === 'PageDown') {
                    e.preventDefault();
                    $scope.$applyAsync(function () {
                        $scope.nextCameraInJourney();
                    });
                } else if (e.key === 'ArrowUp' || e.key === 'k' || e.key === 'PageUp') {
                    e.preventDefault();
                    $scope.$applyAsync(function () {
                        $scope.prevCameraInJourney();
                    });
                }
            });
        }, 250);

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
            rebuildDisplayedCameras(false);
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
                $scope.loadCommuteCorridors(true);
                $scope.commute.isPolling = false;
            }).error(function () {
                $scope.commute.isPolling = false;
            });
        };

        $scope.focusCameraOnMap = function (cam) {
            if (!cam) return;
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
