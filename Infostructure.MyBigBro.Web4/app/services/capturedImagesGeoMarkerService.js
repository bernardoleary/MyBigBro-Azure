angular.module('MyBigBroApp.services', []).
    factory('capturedImagesGeoMarkerService', function($http) {
        var capturedImagesGeoMarkerApi = {};
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerForDateTimeWindow = function(startDateTime, endDateTime) {
            return $http({
                method: 'GET',
                url: '/api/geomarkers/capturedimages?startDateTime=' + encodeURIComponent(startDateTime) + '&endDateTime=' + encodeURIComponent(endDateTime)
            });
        };
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerForDateTimeWindowAndDeviceName = function (startDateTime, endDateTime, deviceName) {
            return $http({
                method: 'GET',
                url: '/api/geomarkers/capturedimages?startDateTime=' + encodeURIComponent(startDateTime) + '&endDateTime=' + encodeURIComponent(endDateTime) + '&deviceName=' + encodeURIComponent(deviceName)
            });
        };
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerTop = function (top) {
            return $http({
                method: 'GET',
                url: '/api/geomarkers/capturedimages?top=' + encodeURIComponent(top)
            });
        };
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerTopAndDeviceName = function (top, deviceName) {
            return $http({
                method: 'GET',
                url: '/api/geomarkers/capturedimages?top=' + encodeURIComponent(top) + '&deviceName=' + encodeURIComponent(deviceName)
            });
        };
        return capturedImagesGeoMarkerApi;
    });
