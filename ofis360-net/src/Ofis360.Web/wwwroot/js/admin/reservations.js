/* Rezervasyon yonetimi (admin + Sekreter): /desk-reservations, /room-reservations; iptal /Plan/Cancel* ile gerekceli. */
(function () {
  'use strict';

  function key(d) { return d ? d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2) : null; }

  angular.module('ofis360').controller('AdminReservationsCtrl', ['api', 'adminApi', 'toast', 'labels', 'pager', 'days', function (api, adminApi, toast, labels, pager, days) {
    var vm = this;
    vm.kind = 'desk';
    vm.today = days.today();
    vm.deskStatus = labels.deskStatus;
    vm.roomStatus = labels.roomStatus;
    var start = new Date(); start.setHours(0, 0, 0, 0);
    var end = new Date(start); end.setDate(end.getDate() + 7);
    vm.q = { from: start, to: end, floorId: null, status: null, search: '' };
    vm.pager = pager(30);

    function options(map) { return Object.keys(map).map(function (k) { return { key: k, label: map[k] }; }); }
    var statusOptions = { desk: options(vm.deskStatus), room: options(vm.roomStatus) };
    vm.statuses = function () { return statusOptions[vm.kind]; };

    vm.load = function () {
      var params = { from: key(vm.q.from), to: key(vm.q.to), status: vm.q.status, search: vm.q.search, take: vm.pager.size, skip: vm.pager.skip() };
      if (vm.kind === 'desk') params.floorId = vm.q.floorId;
      return adminApi.get(vm.kind === 'desk' ? 'desk-reservations' : 'room-reservations', params)
        .then(function (p) { vm.page = p; vm.pager.total = p.total; vm.error = null; })
        .catch(function (e) { vm.error = e.message; });
    };
    vm.search = function () { vm.pager.page = 1; vm.load(); };
    vm.setKind = function (k) { vm.kind = k; vm.q.status = null; vm.page = null; vm.search(); };

    vm.cancel = function (r) { vm.cancelling = r; vm.cancelReason = ''; };
    vm.confirmCancel = function () {
      var r = vm.cancelling;
      vm.busy = true;
      api.post(vm.kind === 'desk' ? '/Plan/CancelDesk' : '/Plan/CancelRoom', { reason: vm.cancelReason }, { id: r.id })
        .then(function () { toast.ok('Rezervasyon iptal edildi, sahibine bildirim gönderildi.'); vm.cancelling = null; return vm.load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    // Kat listesi: admin icin tum katlar; Sekreter icin calisan kat ozetleri.
    api.get('/Admin/FloorSummaries').then(function (floors) {
      vm.floors = floors.map(function (f) { return { id: f.id, label: f.buildingName + ' · ' + f.name }; });
    }).catch(function () { vm.floors = []; });
    vm.load();
  }]);
})();
