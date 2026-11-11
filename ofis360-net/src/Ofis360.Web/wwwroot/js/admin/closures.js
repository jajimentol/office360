/* Bakim ve kapatmalar: /closures, hedef listeleri /floors, /desks, /rooms */
(function () {
  'use strict';

  function dayKey(d) {
    if (!d) return null;
    if (typeof d === 'string') return d;
    return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
  }

  angular.module('ofis360').controller('AdminClosuresCtrl', ['$q', 'adminApi', 'toast', 'labels', function ($q, adminApi, toast, labels) {
    var vm = this;
    vm.scopes = labels.closureScopes;
    vm.reasons = labels.closureReasons;
    vm.onlyActive = true;
    var targets = { FLOOR: [], DESK: [], ROOM: [] };

    vm.targets = function () { return vm.form ? targets[vm.form.scope] : []; };
    vm.activeCount = function () { return (vm.closures || []).filter(function (c) { return c.active; }).length; };
    vm.filtered = function () { return (vm.closures || []).filter(function (c) { return !vm.onlyActive || c.active; }); };
    vm.targetLabel = function (c) {
      if (c.desk) return c.desk.code + (c.floor ? ' · ' + c.floor.name : '');
      if (c.room) return c.room.name + ' (' + c.room.code + ')';
      if (c.floor) return c.floor.name;
      return '–';
    };

    function load() {
      return $q.all([adminApi.get('closures'), adminApi.get('floors'), adminApi.get('desks', { placement: 'placed', take: 500 }), adminApi.get('rooms')])
        .then(function (r) {
          vm.closures = r[0];
          targets.FLOOR = r[1].map(function (f) { return { id: f.id, label: (f.building ? f.building.name + ' · ' : '') + f.name }; });
          targets.DESK = r[2].items.map(function (d) { return { id: d.id, label: d.code + ' · ' + (d.floor ? d.floor.name : '') }; });
          targets.ROOM = r[3].map(function (m) { return { id: m.id, label: m.name + ' (' + m.code + ') · ' + (m.floor ? m.floor.name : '') }; });
          vm.error = null;
        }).catch(function (e) { vm.error = e.message; });
    }

    vm.openForm = function () {
      var today = new Date(); today.setHours(0, 0, 0, 0);
      vm.formError = null;
      vm.form = { scope: 'DESK', targetId: targets.DESK.length ? targets.DESK[0].id : null, start: today, end: today, reason: 'MAINTENANCE', note: '' };
    };

    vm.save = function () {
      var f = vm.form;
      if (!f.targetId || !f.start || !f.end) { vm.formError = 'Alan ve tarih aralığı seçin.'; return; }
      vm.busy = true;
      adminApi.post('closures', { scope: f.scope, targetId: f.targetId, startDayKey: dayKey(f.start), endDayKey: dayKey(f.end), reason: f.reason, note: f.note || '' })
        .then(function (res) {
          toast.ok('Kapatma oluşturuldu' + (res.cancelledReservations ? ', ' + res.cancelledReservations + ' rezervasyon iptal edildi.' : '.'));
          vm.form = null;
          return load();
        })
        .catch(function (e) { vm.formError = e.message; })
        .finally(function () { vm.busy = false; });
    };

    vm.deactivate = function (c) {
      if (!window.confirm('Bu kapatma kaldırılsın mı? İptal edilen rezervasyonlar geri gelmez.')) return;
      vm.busy = true;
      adminApi.put('closures/' + c.id, { active: false })
        .then(function () { toast.ok('Kapatma kaldırıldı.'); return load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    load();
  }]);
})();
