angular.module('MyBigBroApp.services').
    factory('webCamsService', function ($http) {
        var webCamsApi = {};
        webCamsApi.getNearestWebCam = function (deviceName) {
            return $http({
                method: 'GET',
                url: '/api/webcams/nearest?deviceName=' + encodeURIComponent(deviceName)
            });
        };
        webCamsApi.getNearestWebCams = function (top, deviceName) {
            return $http({
                method: 'GET',
                url: '/api/webcams/nearestmany?top=' + encodeURIComponent(top) + '&deviceName=' + encodeURIComponent(deviceName)
            });
        };
        return webCamsApi;
    });
