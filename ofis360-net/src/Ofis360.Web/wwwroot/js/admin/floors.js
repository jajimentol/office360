/* Kat ve alan yonetimi: /buildings, /floors, /floors/capacity, /floors/{id}, /zones/{id} */
(function () {
  'use strict';

  angular.module('ofis360').controller('AdminFloorsCtrl', ['$q', 'adminApi', 'toast', 'labels', function ($q, adminApi, toast, labels) {
    var vm = this;
    vm.zoneKinds = labels.zoneKinds;
    var capacity = {};

    vm.cap = function (f) { return capacity[f.id] || {}; };

    function load() {
      return $q.all([adminApi.get('buildings', { includeInactive: true }), adminApi.get('floors'), adminApi.get('floors/capacity')])
        .then(function (r) {
          vm.buildings = r[0];
          vm.floors = r[1];
          capacity = {};
          r[2].forEach(function (c) { capacity[c.floorId] = c; });
          vm.error = null;
        }).catch(function (e) { vm.error = e.message; });
    }

    function run(promise, message, close) {
      vm.busy = true;
      vm.formError = null;
      return promise
        .then(function () { toast.ok(message); close(); return load(); })
        .catch(function (e) { vm.formError = e.message; toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    }

    vm.openBuilding = function (b) {
      vm.formError = null;
      vm.building = b ? { id: b.id, code: b.code, name: b.name, address: b.address, order: b.order, active: b.active }
        : { code: '', name: '', address: '', order: (vm.buildings || []).length + 1, active: true };
    };
    vm.saveBuilding = function () {
      var b = vm.building, body = { code: b.code, name: b.name, address: b.address || '', order: +b.order || 0, active: !!b.active };
      run(b.id ? adminApi.put('buildings/' + b.id, body) : adminApi.post('buildings', body), b.name + ' kaydedildi.', function () { vm.building = null; });
    };

    vm.openFloor = function (f) {
      vm.formError = null;
      vm.floor = f ? { id: f.id, buildingId: f.buildingId, number: f.number, name: f.name, description: f.description, reservable: f.reservable, active: f.active, planWidth: f.planWidth, planHeight: f.planHeight }
        : { buildingId: vm.buildings && vm.buildings[0] && vm.buildings[0].id, number: 1, name: '', description: '', reservable: true, active: true, planWidth: 1500, planHeight: 1000 };
    };
    vm.saveFloor = function () {
      var f = vm.floor;
      var body = { buildingId: f.buildingId, number: +f.number, name: f.name, description: f.description || '', reservable: !!f.reservable, active: !!f.active,
        planWidth: +f.planWidth || null, planHeight: +f.planHeight || null };
      run(f.id ? adminApi.put('floors/' + f.id, body) : adminApi.post('floors', body), f.name + ' kaydedildi.', function () { vm.floor = null; });
    };

    vm.openZones = function (f) {
      adminApi.get('floors/' + f.id).then(function (d) {
        vm.zonesFloor = f;
        vm.zones = ((d.floor && d.floor.zones) || []).slice().sort(function (a, b) { return a.code < b.code ? -1 : 1; });
      }).catch(function (e) { toast.error(e.message); });
    };
    vm.saveZone = function (z) {
      vm.busy = true;
      adminApi.put('zones/' + z.id, { name: z.name, kind: z.kind, x: z.x, y: z.y, width: z.width, height: z.height, windowSide: !!z.windowSide, quiet: !!z.quiet, reservable: !!z.reservable })
        .then(function () { toast.ok(z.name + ' kaydedildi.'); return load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    load();
  }]);
})();
