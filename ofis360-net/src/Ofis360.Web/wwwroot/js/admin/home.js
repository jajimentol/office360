/* Admin panosu: kat ozetleri, sayaclar, aktif kapatmalar, son islemler, gecerli kurallar. */
(function () {
  'use strict';

  angular.module('ofis360').controller('AdminHomeCtrl', ['$q', 'api', 'adminApi', 'days', function ($q, api, adminApi, days) {
    var vm = this;
    vm.today = days.today();

    function sum(field) { return (vm.d ? vm.d.floors : []).reduce(function (t, f) { return t + f[field]; }, 0); }
    vm.todayReserved = function () { return sum('reservedDesks'); };
    vm.todayTotal = function () { return sum('totalDesks'); };
    vm.todayOccupancy = function () { var t = vm.todayTotal(); return t ? vm.todayReserved() / t * 100 : 0; };

    $q.all({
      floors: api.get('/Admin/FloorSummaries', { date: vm.today }),
      desks: adminApi.get('desks', { take: 1 }),
      inactive: adminApi.get('desks', { take: 1, onlyInactive: true }),
      rooms: adminApi.get('rooms'),
      users: adminApi.get('users/stats'),
      closures: adminApi.get('closures', { active: true }),
      audit: adminApi.get('audit', { take: 8 }),
      rules: adminApi.get('rules/settings')
    }).then(function (d) {
      d.closures = d.closures.filter(function (c) { return c.active; });
      vm.d = d;
    }).catch(function (e) { vm.error = e.message; });
  }]);
})();
