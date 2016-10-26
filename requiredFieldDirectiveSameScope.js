app.directive("requiredField", ['$parse', function ($parse) {
        return {
            restrict: 'A',
            require: 'ngModel',
            link: function (scope, element, attrs, ngModelCtrl) {
                scope.safeApply = function (fn) {
                    var phase = this.$root.$$phase;
                    if (phase == '$apply' || phase == '$digest') {
                        if (fn && (typeof (fn) === 'function')) {
                            fn();
                        }
                    } else {
                        this.$apply(fn);
                    }
                };

                function getNextZDecoratorElement(type) {
                    var zDecorator = null;
                    if (type == 'date' || type == 'Date')
                        zDecorator = angular.element(element).next().next();
                    else
                        zDecorator = angular.element(element).next();

                    if (zDecorator[0]) {
                        if (zDecorator[0].classList.contains('z-decorator'))
                            return zDecorator;
                        else
                            return null;
                    }
                    return null;
                }

                scope.$watch(attrs.monitor, function (newValue, oldValue) {
                    if (newValue) {
                        var zDecorator = getNextZDecoratorElement(attrs.fieldType);
                        if (!zDecorator || !zDecorator[0])
                            element.after("<span class=\"z-decorator\"><div></div><span class=\"invalid\">'" + attrs.name + "' is required</span></span>");
                    }
                });
                scope.$watch(attrs.ngModel, function (newValue, oldValue) {
                    if (newValue != '' && newValue) {

                        if (newValue) {
                            var zDecorator = getNextZDecoratorElement(attrs.fieldType);

                            if (zDecorator && zDecorator[0])
                                angular.element(zDecorator).remove();
                        }

                        scope.safeApply(function () {
                            $parse(attrs.monitor).assign(scope, false)
                        });
                    }
                });
            }
        }
    }
    ]);
