angular.module('MyBigBroApp.services')
    .factory('commuteService', ['$http', function ($http) {
        var commuteFactory = {};

        commuteFactory.getCorridors = function (city) {
            var q = city ? '?city=' + encodeURIComponent(city) : '';
            return $http.get('/api/commute/corridors' + q);
        };

        commuteFactory.getCorridor = function (id) {
            return $http.get('/api/commute/corridors/' + encodeURIComponent(id));
        };

        commuteFactory.triggerPoll = function (corridorId) {
            var q = corridorId ? '?corridorId=' + encodeURIComponent(corridorId) : '';
            return $http.post('/api/commute/poll' + q, {});
        };

        return commuteFactory;
    }]);
