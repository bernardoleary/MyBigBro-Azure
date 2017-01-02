angular.module('MyBigBroApp.controllers').
    controller('loginController', function($scope) {
        $scope.driversList = [
            {
                Driver: {
                    givenName: 'Bernard',
                    familyName: 'O\'Leary'
                },
                points: 322,
                nationality: "NZ\'er",
                Constructors: [
                    { name: "Red Bull" }
                ]
            }
        ];
    });