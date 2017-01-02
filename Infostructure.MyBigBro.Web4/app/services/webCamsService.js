angular.module('MyBigBroApp.services').
    factory('webCamsService', function ($http) {
        var webCamsApi = {};
        webCamsApi.getNearestWebCam = function (deviceName) {
            return $http({
                method: 'GET',
                url: 'http://' + document.location.host + '/api/webcams/nearest?deviceName=' + deviceName
            });
        };
        webCamsApi.getNearestWebCams = function (top, deviceName) {
            return $http({
                method: 'GET',
                url: 'http://' + document.location.host + '/api/webcams/nearestmany?top=' + top + '&deviceName=' + deviceName
            });
        };
        return webCamsApi;
    });