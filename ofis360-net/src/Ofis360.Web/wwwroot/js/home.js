/* Calisan ana sayfasi: /Home/Data (→ /api/panel/home). */
(function () {
  'use strict';
  var STATUS = { BOOKED: 'Rezerve', CHECKED_IN: 'Check-in yapıldı', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal edildi', NO_SHOW: 'Gelinmedi' };

  angular.module('ofis360').controller('HomeCtrl', ['api', 'toast', function (api, toast) {
    var vm = this;
    vm.statusLabel = function (s) { return STATUS[s] || s; };
    vm.pct = function (a, b) { return b ? Math.min(100, a / b * 100) : 0; };

    function load() {
      return api.get('/Home/Data').then(function (d) { vm.d = d; vm.error = null; }).catch(function (e) { vm.error = e.message; });
    }
    vm.checkIn = function () {
      vm.busy = true;
      api.post('/Plan/CheckIn', {}, { id: vm.d.todayDesk.id })
        .then(function () { toast.ok('Check-in yapıldı. İyi çalışmalar!'); return load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };
    load();
  }]);
})();
