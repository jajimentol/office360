/* Denetim kayitlari: /audit (sayfali) */
(function () {
  'use strict';
  var FILTERS = [
    { value: '', label: 'Tümü' }, { value: 'DeskReservation', label: 'Masa rezervasyonu' }, { value: 'RoomReservation', label: 'Toplantı rezervasyonu' },
    { value: 'Desk', label: 'Masa' }, { value: 'MeetingRoom', label: 'Toplantı odası' }, { value: 'Floor', label: 'Kat' },
    { value: 'ReservationRule', label: 'Kural' }, { value: 'Closure', label: 'Kapatma' }, { value: 'User', label: 'Kullanıcı' },
    { value: 'SystemSetting', label: 'Ayar' }, { value: 'Report', label: 'Rapor' }
  ];

  angular.module('ofis360').controller('AdminAuditCtrl', ['adminApi', 'pager', function (adminApi, pager) {
    var vm = this;
    vm.filters = FILTERS;
    vm.entity = '';
    vm.pager = pager(40);

    vm.load = function () {
      vm.loading = true;
      return adminApi.get('audit', { entityType: vm.entity, take: vm.pager.size, skip: vm.pager.skip() })
        .then(function (p) { vm.page = p; vm.pager.total = p.total; vm.error = null; })
        .catch(function (e) { vm.error = e.message; })
        .finally(function () { vm.loading = false; });
    };
    vm.setEntity = function (v) { vm.entity = v; vm.pager.page = 1; vm.load(); };
    vm.load();
  }]);
})();
