angular.module('MyBigBroApp.controllers', []).
    controller('mainController', function($scope, capturedImagesGeoMarkerService, webCamsService) {

        // OpenStreetMap + Leaflet default map setup (centered on Auckland by default)
        var map = L.map('map').setView([-36.8485, 174.7633], 12);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);

        setTimeout(function () {
            map.invalidateSize();
        }, 100);

        // Scope variables
        $scope.map = map;
        $scope.data = {};
        $scope.data.startdatetime = '';
        $scope.data.enddatetime = '';
        $scope.markers = [];
        $scope.data.markerdatetime = '';

        // Draw our markers, first run through
        drawMarkers();

        // Get querystring values
        function getParameterByName(name) {
            name = name.replace(/[\[]/, "\\[").replace(/[\]]/, "\\]");
            var regex = new RegExp("[\\?&]" + name + "=([^&#]*)"),
                results = regex.exec(location.search);
            return results === null ? "" : decodeURIComponent(results[1].replace(/\+/g, " "));
        }

        // Function to draw our geomarkers with images
        function drawMarkers() {
            // Clear existing markers
            for (var i = 0; i < $scope.markers.length; i++) {
                $scope.map.removeLayer($scope.markers[i]);
            }
            $scope.markers = [];

            // Get the device name
            if ($scope.data.devicename === undefined || $scope.data.devicename === "") {
                $scope.data.devicename = getParameterByName("deviceName");
            }
            var devicename = $scope.data.devicename;

            // Default count of markers to retrieve
            var markerCount = getParameterByName("markerCount") === "" ? 10 : getParameterByName("markerCount");

            // Create new markers
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
                // Create by date window (using ISO-8601 UTC format for API query)
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

        // Function to set up the image markers on the map
        function createMarkers(capturedImagesGeoMarker) {
            if (!capturedImagesGeoMarker || !capturedImagesGeoMarker.length) {
                return;
            }
            for (var i = 0; i < capturedImagesGeoMarker.length; i++) {
                createMarker(capturedImagesGeoMarker[i]);
            }
            fitBounds();
        }

        // Function to set up the camera markers on the map
        function createCameraMarkers(camerasGeoMarker) {
            if (!camerasGeoMarker || !camerasGeoMarker.length) {
                return;
            }
            for (var i = 0; i < camerasGeoMarker.length; i++) {
                createCameraMarker(camerasGeoMarker[i]);
            }
            fitBounds();
        }

        // Function to fit the map bounds to visible markers
        function fitBounds() {
            if ($scope.markers.length === 0) {
                return;
            }
            $scope.map.invalidateSize();
            var group = L.featureGroup($scope.markers);
            $scope.map.fitBounds(group.getBounds(), { padding: [40, 40], maxZoom: 15 });
        }

        // Function to create a geomarker on the OpenStreetMap layer
        function createMarker(info) {
            var title = "This image captured " + moment(info.GeoMarker.MarkerDateTime).format('MMMM Do YYYY, h:mm:ss a');
            var content = '<img width="100%" src="' + info.CapturedImage.Url + '" alt="Smile">';
            var marker = L.marker([info.GeoMarker.YCoord, info.GeoMarker.XCoord], {
                title: title
            }).addTo($scope.map);
            marker.title = title;
            marker.content = content;
            marker.on('click', function() {
                $("#mapModal #mapModalTitle").html(marker.title);
                $("#mapModal #mapModalBody").html(marker.content);
                $('#mapModal').modal("show");
            });
            $scope.markers.push(marker);
        }

        // Function to create a camera marker on the OpenStreetMap layer
        function createCameraMarker(info) {
            var cameraIcon = L.icon({
                iconUrl: 'content/images/favicon.ico',
                iconSize: [24, 24],
                iconAnchor: [12, 12]
            });
            var title = "Nearest known camera: " + info.Name;
            var content = '<img width="100%" src="' + info.Url + '" alt="Smile">';
            var marker = L.marker([info.YCoord, info.XCoord], {
                title: title,
                icon: cameraIcon
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

        // Event to enable a popup modal with the captured image
        $scope.openInfoWindow = function(e, selectedMarker) {
            e.preventDefault();
            selectedMarker.fire('click');
        };

        // Event to get start time DDL change
        $scope.onStartTimeSet = function (newDate, oldDate) {
            $scope.data.startdatetime = newDate;
            drawMarkers();
        };

        // Event to get end time DDL change
        $scope.onEndTimeSet = function (newDate, oldDate) {
            $scope.data.enddatetime = newDate;
            drawMarkers();
        };

        // Event to handle device name search
        $scope.onDeviceNameChange = function ($event) {
            if ($event) {
                $event.preventDefault();
            }
            drawMarkers();
        };

        // Event to reset date and device filters
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
