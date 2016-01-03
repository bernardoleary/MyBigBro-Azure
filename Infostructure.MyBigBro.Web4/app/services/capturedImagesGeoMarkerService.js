angular.module('MyBigBroApp.services', []).
    factory('capturedImagesGeoMarkerService', function($http) {
        var capturedImagesGeoMarkerApi = {};
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerForDateTimeWindow = function(startDateTime, endDateTime) {
            return $http({
                method: 'GET',
                url: 'http://' + document.location.host + '/api/geomarkers/capturedimages?startDateTime=' + startDateTime + '&endDateTime=' + endDateTime
            });
        };
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerForDateTimeWindowAndDeviceName = function (startDateTime, endDateTime, deviceName) {
            return $http({
                method: 'GET',
                url: 'http://' + document.location.host + '/api/geomarkers/capturedimages?startDateTime=' + startDateTime + '&endDateTime=' + endDateTime + '&deviceName=' + deviceName
            });
        };
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerTop = function (top) {
            return $http({
                method: 'GET',
                url: 'http://' + document.location.host + '/api/geomarkers/capturedimages?top=' + top
            });
        };
        capturedImagesGeoMarkerApi.getCapturedImagesGeoMarkerTopAndDeviceName = function (top, deviceName) {
            return $http({
                method: 'GET',
                url: 'http://' + document.location.host + '/api/geomarkers/capturedimages?top=' + top + '&deviceName=' + deviceName
            });
        };
        return capturedImagesGeoMarkerApi;
    });