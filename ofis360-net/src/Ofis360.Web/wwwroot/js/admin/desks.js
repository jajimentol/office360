/* Masa yonetimi: /desks, /floors, /desk-features */
(function () {
  'use strict';

  angular.module('ofis360').controller('AdminDesksCtrl', ['$q', 'adminApi', 'toast', 'pager', function ($q, adminApi, toast, pager) {
    var vm = this;
    vm.q = { floorId: null, placement: '', search: '', onlyInactive: false };
    vm.pager = pager(30);
    var zonesByFloor = {};

    vm.zonesOf = function (floorId) { return zonesByFloor[floorId] || []; };
    vm.featureNames = function (d) { return d.features.map(function (f) { return f.name; }).join(', ') || '–'; };

    vm.load = function () {
      return adminApi.get('desks', angular.extend({ take: vm.pager.size, skip: vm.pager.skip() }, vm.q, { onlyInactive: vm.q.onlyInactive || null }))
        .then(function (p) { vm.page = p; vm.pager.total = p.total; vm.error = null; })
        .catch(function (e) { vm.error = e.message; });
    };
    vm.search = function () { vm.pager.page = 1; vm.load(); };

    function loadFloorZones(floorId) {
      if (!floorId || zonesByFloor[floorId]) return $q.resolve();
      return adminApi.get('floors/' + floorId).then(function (d) {
        zonesByFloor[floorId] = ((d.floor && d.floor.zones) || []).slice().sort(function (a, b) { return a.code < b.code ? -1 : 1; });
      });
    }

    vm.openForm = function (d) {
      vm.formError = null;
      var floorId = d ? d.floorId : (vm.q.floorId || (vm.floors[0] && vm.floors[0].id));
      loadFloorZones(floorId).then(function () {
        vm.form = d ? {
          id: d.id, code: d.code, floorId: d.floorId, zoneId: d.zoneId, x: d.x, y: d.y, notes: d.notes || '',
          featureKeys: d.features.map(function (f) { return f.key; }), popular: d.popular, accessibleOnly: d.accessibleOnly, active: d.active
        } : { code: '', floorId: floorId, zoneId: (vm.zonesOf(floorId)[0] || {}).id, x: 0, y: 0, notes: '', featureKeys: [], popular: false, accessibleOnly: false, active: true };
      });
    };

    vm.toggleFeature = function (key) {
      var list = vm.form.featureKeys, i = list.indexOf(key);
      if (i >= 0) list.splice(i, 1); else list.push(key);
    };

    vm.save = function () {
      var f = vm.form;
      var payload = { code: f.code, floorId: f.floorId, zoneId: f.zoneId, x: +f.x || 0, y: +f.y || 0, notes: f.notes || null,
        featureKeys: f.featureKeys, popular: !!f.popular, accessibleOnly: !!f.accessibleOnly, active: !!f.active };
      vm.busy = true;
      (f.id ? adminApi.put('desks/' + f.id, payload) : adminApi.post('desks', payload))
        .then(function () { toast.ok(f.code + ' kaydedildi.'); vm.form = null; return vm.load(); })
        .catch(function (e) { vm.formError = e.message; })
        .finally(function () { vm.busy = false; });
    };

    vm.toggle = function (d) {
      vm.busy = true;
      adminApi.put('desks/' + d.id, { active: !d.active })
        .then(function () { toast.ok(d.code + (d.active ? ' pasifleştirildi.' : ' aktifleştirildi.')); return vm.load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    vm.remove = function (d) {
      if (!window.confirm(d.code + ' masası silinsin mi? Geçmiş rezervasyonu olan masa silinemez; pasifleştirin.')) return;
      vm.busy = true;
      adminApi.del('desks/' + d.id)
        .then(function () { toast.ok(d.code + ' silindi.'); return vm.load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    $q.all([adminApi.get('floors'), adminApi.get('desk-features')]).then(function (r) {
      vm.floors = r[0].map(function (f) { return { id: f.id, label: (f.building ? f.building.name + ' · ' : '') + f.name }; });
      vm.features = r[1];
    });
    vm.load();
  }]);
})();
