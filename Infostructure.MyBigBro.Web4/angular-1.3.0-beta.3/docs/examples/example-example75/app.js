  var myApp = angular.module('spicyApp1', []);

  myApp.controller('SpicyCtrl', ['$scope', function($scope){
      $scope.spice = 'very';

      $scope.chiliSpicy = function() {
          $scope.spice = 'chili';
      };

      $scope.jalapenoSpicy = function() {
          $scope.spice = 'jalapeño';
      };
  }]);