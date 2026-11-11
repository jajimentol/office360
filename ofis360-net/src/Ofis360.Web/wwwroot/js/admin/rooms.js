/* Toplanti odalari: /rooms, /room-features, /room-priority-gaps, /rooms/{id}/policy; kisi arama /Admin/People */
(function () {
  'use strict';

  angular.module('ofis360').controller('AdminRoomsCtrl', ['$q', 'adminApi', 'toast', 'labels', function ($q, adminApi, toast, labels) {
    var vm = this;
    vm.floorFilter = null;
    vm.text = '';

    vm.featureNames = function (r) { return r.features.map(function (f) { return f.name; }).join(', ') || '–'; };
    vm.matches = function (r) {
      if (vm.floorFilter && r.floorId !== vm.floorFilter) return false;
      var t = (vm.text || '').toLocaleLowerCase('tr-TR');
      return !t || r.name.toLocaleLowerCase('tr-TR').indexOf(t) >= 0 || r.code.toLocaleLowerCase('tr-TR').indexOf(t) >= 0;
    };

    function load() {
      return $q.all([adminApi.get('rooms'), adminApi.get('room-priority-gaps')])
        .then(function (r) { vm.rooms = r[0]; vm.gaps = r[1]; vm.error = null; })
        .catch(function (e) { vm.error = e.message; });
    }

    vm.openForm = function (r) {
      vm.formError = null;
      vm.form = r ? {
        id: r.id, name: r.name, code: r.code, floorId: r.floorId, capacity: r.capacity, x: r.x, y: r.y, width: r.width, height: r.height,
        notes: r.notes || '', featureKeys: r.features.map(function (f) { return f.key; }), active: r.active
      } : { name: '', code: '', floorId: vm.floorFilter || (vm.floors[0] && vm.floors[0].id), capacity: 6, x: 0, y: 0, width: 200, height: 120, notes: '', featureKeys: [], active: true };
    };
    vm.toggleFeature = function (key) {
      var list = vm.form.featureKeys, i = list.indexOf(key);
      if (i >= 0) list.splice(i, 1); else list.push(key);
    };
    vm.save = function () {
      var f = vm.form;
      var payload = { name: f.name, code: f.code, floorId: f.floorId, capacity: +f.capacity || 1, x: +f.x || 0, y: +f.y || 0,
        width: +f.width || 200, height: +f.height || 120, notes: f.notes || null, featureKeys: f.featureKeys, active: !!f.active };
      vm.busy = true;
      (f.id ? adminApi.put('rooms/' + f.id, payload) : adminApi.post('rooms', payload))
        .then(function () { toast.ok(f.name + ' kaydedildi.'); vm.form = null; return load(); })
        .catch(function (e) { vm.formError = e.message; })
        .finally(function () { vm.busy = false; });
    };
    vm.toggle = function (r) {
      vm.busy = true;
      adminApi.put('rooms/' + r.id, { active: !r.active })
        .then(function () { toast.ok(r.name + (r.active ? ' pasifleştirildi.' : ' aktifleştirildi.')); return load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };
    vm.remove = function (r) {
      if (!window.confirm(r.name + ' silinsin mi? Geçmiş rezervasyonu olan oda silinemez; pasifleştirin.')) return;
      vm.busy = true;
      adminApi.del('rooms/' + r.id)
        .then(function () { toast.ok(r.name + ' silindi.'); return load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    // ---- Odaya ozel kurallar
    vm.openPolicy = function (room) {
      vm.policyError = null;
      vm.people = [];
      vm.personQuery = '';
      adminApi.get('rooms/' + room.id + '/policy').then(function (p) {
        p.rows = labels.grades.map(function (g) {
          var rule = p.gradeRules.filter(function (x) { return x.grade === g.key; })[0];
          return { grade: g.key, label: g.label, allowed: !!rule, min: rule ? rule.minAttendees : 1, bypass: rule ? rule.bypassPriority : false };
        });
        vm.policy = p;
      }).catch(function (e) { toast.error(e.message); });
    };
    vm.searchPeople = function () {
      if (!vm.personQuery || vm.personQuery.length < 2) { vm.people = []; return; }
      adminApi.people(vm.personQuery).then(function (list) {
        var ids = vm.policy.priorityUsers.map(function (p) { return p.id; });
        vm.people = list.filter(function (p) { return ids.indexOf(p.id) < 0; }).slice(0, 8);
      });
    };
    vm.addPerson = function (p) {
      vm.policy.priorityUsers.push({ id: p.id, fullName: p.fullName, title: p.title });
      vm.people = vm.people.filter(function (x) { return x.id !== p.id; });
    };
    vm.savePolicy = function () {
      var p = vm.policy;
      var days = p.priorityOpenDays === '' || p.priorityOpenDays === null || p.priorityOpenDays === undefined ? null : +p.priorityOpenDays;
      vm.busy = true;
      adminApi.put('rooms/' + p.roomId + '/policy', {
        secretaryOnly: !!p.secretaryOnly, priorityOpenDays: days,
        gradeRules: p.rows.filter(function (r) { return r.allowed; }).map(function (r) { return { grade: r.grade, minAttendees: +r.min || 1, bypassPriority: !!r.bypass }; }),
        priorityUserIds: p.priorityUsers.map(function (x) { return x.id; })
      })
        .then(function () { toast.ok(p.roomName + ' kuralları kaydedildi.'); vm.policy = null; return load(); })
        .catch(function (e) { vm.policyError = e.message; })
        .finally(function () { vm.busy = false; });
    };

    $q.all([adminApi.get('floors'), adminApi.get('room-features')]).then(function (r) {
      vm.floors = r[0].map(function (f) { return { id: f.id, label: (f.building ? f.building.name + ' · ' : '') + f.name }; });
      vm.features = r[1];
    });
    load();
  }]);
})();
