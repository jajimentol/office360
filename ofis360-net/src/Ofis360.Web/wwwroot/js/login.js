/* Giris ekrani: demo hesaplar MVC uzerinden listelenir; secilen hesapla MVC, API'de oturum acar. */
(function () {
  'use strict';

  var ROLE_GROUPS = [
    { role: 'EMPLOYEE', label: 'Çalışan' },
    { role: 'MANAGER', label: 'Yönetici' },
    { role: 'ADMIN', label: 'Admin' }
  ];

  angular.module('ofis360').controller('LoginCtrl', ['api', '$window', function (api, $window) {
    var vm = this;
    vm.loading = true;

    api.get('/Account/Options').then(function (o) {
      vm.mode = o.mode;
      vm.status = o.status;
      vm.intro = o.intro;
      var accounts = o.accounts || [];
      vm.groups = ROLE_GROUPS.map(function (g) {
        return { label: g.label, accounts: accounts.filter(function (a) { return a.role === g.role; }) };
      }).filter(function (g) { return g.accounts.length; });
    }).catch(function (err) { vm.error = err.message; })
      .finally(function () { vm.loading = false; });

    vm.login = function (account) {
      vm.busy = true;
      vm.error = null;
      api.post('/Account/Login', { userId: account.id }, { returnUrl: vm.returnUrl })
        .then(function (res) { $window.location.href = res.redirect || '/Plan'; })
        .catch(function (err) { vm.error = err.message; vm.busy = false; });
    };
  }]);
})();
