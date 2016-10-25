app.directive("requiredfield", function () {
    return {
        restrict: 'a',
        require: 'ngmodel',
        scope: {
            ngmodel: '=',
            monitor: '='
        },
        link: function (scope, element, attrs, ngmodelctrl) {
            scope.$watch('monitor', function (newvalue, oldvalue) {
                if (newvalue) {
                    element.after("<span class=\"z-decorator\"><div></div><span class=\"invalid\">'" + attrs.name + "' is required</span></span>");
                }
            });
            scope.$watch('ngmodel', function (newvalue, oldvalue) {
                if (newvalue != '' && newvalue) {

                    if (ngmodelctrl.$modelvalue) {
                        var zdecorator = null;
                        if (attrs.fieldtype == 'date' || attrs.fieldtype == 'date')
                            zdecorator = angular.element(element).next().next();
                        else
                            zdecorator = angular.element(element).next();

                        if (zdecorator[0]) {
                            if (zdecorator[0].classlist.contains('z-decorator')) {
                                angular.element(zdecorator).remove();
                            }
                        }
                    }

                    scope.monitor = false;
                }
            });
        }
    }
});