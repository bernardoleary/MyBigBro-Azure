angular.module('MyBigBroApp.controllers', []).
    controller('mainController', function ($scope, $interval, $timeout, $sce, commuteService) {

        var map = L.map('map').setView([-36.8485, 174.7633], 12);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        $scope.map = map;
        $scope.corridorMapMarkers = [];
        $scope.viewMode = 'strip';

        var savedStarred = [];
        try {
            savedStarred = JSON.parse(localStorage.getItem('mybigbro_starred_cams') || '[]');
        } catch (e) {
            savedStarred = [];
        }

        $scope.commute = {
            city: localStorage.getItem('mybigbro_city') || 'auckland',
            layoutMode: localStorage.getItem('mybigbro_layout') || 'deck',
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
            londonVideoMode: true,
            starredIds: savedStarred,
            cacheBust: Date.now(),
            dragOffsetY: 0,
            isDragging: false
        };

        $scope.trustUrl = function (url) {
            return url ? $sce.trustAsResourceUrl(url) : '';
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

        function calcDistanceKm(lat1, lon1, lat2, lon2) {
            var R = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                Math.cos(lat1 * Math.PI / 180.0) * Math.cos(lat2 * Math.PI / 180.0) *
                Math.sin(dLon / 2) * Math.sin(dLon / 2);
            var c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
            return R * c * 1.18;
        }

        function detectHighwayBadge(cam) {
            var name = (cam.Name || '').toUpperCase();
            if (name.indexOf('SH16') !== -1) return { code: 'SH16', bg: '#b91c1c' };
            if (name.indexOf('SH18') !== -1) return { code: 'SH18', bg: '#047857' };
            if (name.indexOf('SH20A') !== -1) return { code: 'SH20A', bg: '#b45309' };
            if (name.indexOf('SH20') !== -1) return { code: 'SH20', bg: '#6d28d9' };
            if (name.indexOf('SH1') !== -1) return { code: 'SH1', bg: '#be123c' };
            if (name.indexOf('A406') !== -1 || name.indexOf('HANGER') !== -1) return { code: 'A406', bg: '#15803d' };
            if (name.indexOf('A13') !== -1 || name.indexOf('LIMEHOUSE') !== -1 || name.indexOf('A12') !== -1) return { code: 'A13', bg: '#0369a1' };
            if (name.indexOf('A40') !== -1) return { code: 'A40', bg: '#15803d' };
            if (name.indexOf('A4 ') !== -1 || name.indexOf('CHISWICK') !== -1) return { code: 'A4', bg: '#1d4ed8' };
            return { code: cam.City === 'london' ? 'TfL' : 'NZTA', bg: '#0284c7' };
        }

        $scope.loadCommuteCorridors = function (preserveCursor) {
            commuteService.getCorridors().success(function (corridors) {
                $scope.commute.allCorridors = corridors || [];
                updateCityCorridors(preserveCursor);
                if ($scope.viewMode === 'map') {
                    drawCorridorCamerasOnMap();
                }
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
                ShortName: 'Saved',
                Name: 'Saved Commute Cameras',
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
                    if (distFromPrev > 40) distFromPrev = 2.5;
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
                enriched.push(cam);
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

        $scope.getCurrentJourneyCamera = function () {
            if (!$scope.commute.displayedCameras || !$scope.commute.displayedCameras.length) return null;
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

        $scope.goToCameraIndex = function (idx) {
            if (!$scope.commute.displayedCameras || idx < 0 || idx >= $scope.commute.displayedCameras.length) return;
            $scope.commute.activeCamIndex = idx;
        };

        $scope.nextCameraInJourney = function () {
            if ($scope.commute.activeCamIndex < $scope.commute.displayedCameras.length - 1) {
                $scope.commute.activeCamIndex++;
            }
        };

        $scope.prevCameraInJourney = function () {
            if ($scope.commute.activeCamIndex > 0) {
                $scope.commute.activeCamIndex--;
            }
        };

        // Drag Up/Down, Scroll Wheel & Arrow Key Gestures
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
            if (dy > 90) dy = 90;
            if (dy < -90) dy = -90;
            $scope.commute.dragOffsetY = dy;
        };

        $scope.onStageDragEnd = function () {
            if (dragStartY === null || !$scope.commute.isDragging) return;
            var dy = $scope.commute.dragOffsetY;
            dragStartY = null;
            $scope.commute.isDragging = false;
            $scope.commute.dragOffsetY = 0;

            if (dy < -40) {
                $scope.nextCameraInJourney();
            } else if (dy > 40) {
                $scope.prevCameraInJourney();
            }
        };

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
                    if (e.target && (e.target.tagName === 'BUTTON' || e.target.tagName === 'INPUT' || e.target.tagName === 'VIDEO')) return;
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
                    if (now - lastWheelTime < 300) {
                        e.preventDefault();
                        return;
                    }
                    if (Math.abs(e.deltaY) > 20) {
                        e.preventDefault();
                        lastWheelTime = now;
                        $scope.$applyAsync(function () {
                            if (e.deltaY > 0) $scope.nextCameraInJourney();
                            else $scope.prevCameraInJourney();
                        });
                    }
                }, { passive: false });
            }

            window.addEventListener('keydown', function (e) {
                if ($scope.viewMode !== 'strip' || $scope.commute.layoutMode !== 'deck') return;
                if (e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA')) return;
                if (e.key === 'ArrowDown' || e.key === 'j') {
                    e.preventDefault();
                    $scope.$applyAsync(function () { $scope.nextCameraInJourney(); });
                } else if (e.key === 'ArrowUp' || e.key === 'k') {
                    e.preventDefault();
                    $scope.$applyAsync(function () { $scope.prevCameraInJourney(); });
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

        $scope.isStarred = function (camId) {
            return $scope.commute.starredIds.indexOf(camId) !== -1;
        };

        $scope.toggleStar = function (camId, $event) {
            if ($event) $event.stopPropagation();
            var idx = $scope.commute.starredIds.indexOf(camId);
            if (idx === -1) $scope.commute.starredIds.push(camId);
            else $scope.commute.starredIds.splice(idx, 1);
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
                if (idx >= cam.Frames.length) idx = cam.Frames.length - 1;
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
            return 'Live';
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

        var playbackPromise = $interval(function () {
            if ($scope.viewMode === 'strip' && $scope.commute.isPlaying && $scope.commute.maxFrameCount > 1) {
                $scope.commute.frameStep = ($scope.commute.frameStep + 1) % $scope.commute.maxFrameCount;
            }
        }, $scope.commute.speedMs);

        var refreshCorridorsPromise = $interval(function () {
            $scope.loadCommuteCorridors(true);
        }, 30000);

        $scope.$on('$destroy', function () {
            if (playbackPromise) $interval.cancel(playbackPromise);
            if (refreshCorridorsPromise) $interval.cancel(refreshCorridorsPromise);
        });

        function drawCorridorCamerasOnMap() {
            for (var i = 0; i < $scope.corridorMapMarkers.length; i++) {
                $scope.map.removeLayer($scope.corridorMapMarkers[i]);
            }
            $scope.corridorMapMarkers = [];

            if (!$scope.commute.displayedCameras || !$scope.commute.displayedCameras.length) return;

            for (var k = 0; k < $scope.commute.displayedCameras.length; k++) {
                var cam = $scope.commute.displayedCameras[k];
                var camIcon = L.divIcon({
                    className: 'mbb-cam-map-pin',
                    html: '<div style="background:#0284c7;color:#fff;border:2px solid #fff;border-radius:50%;width:26px;height:26px;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;box-shadow:0 2px 6px rgba(0,0,0,0.4);">' + (k + 1) + '</div>',
                    iconSize: [26, 26],
                    iconAnchor: [13, 13]
                });

                var popupHtml = '<div style="min-width:240px;"><strong>' + cam.Name + '</strong><br/><small>' + (cam.Subtitle || '') + '</small><div style="margin-top:6px;">' +
                    (cam.City === 'london' && cam.VideoUrl
                        ? '<video autoplay loop muted playsinline style="width:100%;border-radius:6px;" src="' + cam.VideoUrl + '"></video>'
                        : '<img style="width:100%;border-radius:6px;" src="' + $scope.getActiveFrameUrl(cam) + '" />') +
                    '</div></div>';

                var m = L.marker([cam.Lat, cam.Lng], { icon: camIcon })
                    .bindPopup(popupHtml)
                    .addTo($scope.map);
                $scope.corridorMapMarkers.push(m);
            }

            if ($scope.corridorMapMarkers.length > 0) {
                var group = L.featureGroup($scope.corridorMapMarkers);
                $scope.map.fitBounds(group.getBounds(), { padding: [40, 40], maxZoom: 14 });
            }
        }

        $scope.loadCommuteCorridors(false);
    });
