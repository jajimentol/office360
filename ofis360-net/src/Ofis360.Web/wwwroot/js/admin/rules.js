/* Rezervasyon kurallari: varsayilanlar (/rules), kademe matrisi (/rules/matrix), resmi tatiller (/holidays). */
(function () {
  'use strict';
  var CATEGORY = { DESK: 'Masa kuralları', ROOM: 'Toplantı odası kuralları', GENERAL: 'Genel kurallar' };
  var WORKING_HOURS = 'ROOM_WORKING_HOURS', NO_SHOW = 'NO_SHOW_RELEASE', POPULAR_SHARE = 'POPULAR_SHARE_LIMIT';

  function toTime(m) { if (m === null || m === undefined) return ''; var h = Math.floor(m / 60), mm = m % 60; return (h < 10 ? '0' : '') + h + ':' + (mm < 10 ? '0' : '') + mm; }
  function fromTime(t) { if (!t) return null; var p = String(t).split(':'); return (+p[0]) * 60 + (+p[1]); }

  angular.module('ofis360').controller('AdminRulesCtrl', ['adminApi', 'toast', 'labels', function (adminApi, toast, labels) {
    var vm = this;
    vm.grades = labels.grades;
    vm.matrixBuildingId = null;
    vm.holiday = { date: '', name: '' };

    // ---- Varsayilan kurallar
    vm.isTime = function (r, which) { return r.key === WORKING_HOURS || (which === 2 && r.key === NO_SHOW); };
    vm.firstLabel = function (r) { return r.key === WORKING_HOURS ? 'Başlangıç' : r.key === POPULAR_SHARE ? 'Pencere (N)' : r.key === NO_SHOW ? 'Tolerans (dk)' : 'Değer'; };
    vm.secondLabel = function (r) { return r.key === WORKING_HOURS ? 'Bitiş' : r.key === NO_SHOW ? 'Başlangıç saati' : 'Üst sınır (M)'; };
    function draftOf(r) {
      return {
        enabled: r.enabled,
        int: r.intValue === null || r.intValue === undefined ? null : (vm.isTime(r, 1) ? toTime(r.intValue) : r.intValue),
        second: r.secondValue === null || r.secondValue === undefined ? null : (vm.isTime(r, 2) ? toTime(r.secondValue) : r.secondValue)
      };
    }
    function asDate(t) { if (!t) return null; var p = t.split(':'); return new Date(1970, 0, 1, +p[0], +p[1]); }
    vm.resetRule = function (r) {
      r.draft = draftOf(r);
      // input[type=time] Date nesnesi bekler.
      if (vm.isTime(r, 1) && r.draft.int !== null) r.draft.int = asDate(r.draft.int);
      if (vm.isTime(r, 2) && r.draft.second !== null) r.draft.second = asDate(r.draft.second);
    };
    function value(r, which) {
      var v = which === 1 ? r.draft.int : r.draft.second;
      if (v === null || v === undefined || v === '') return null;
      if (v instanceof Date) return v.getHours() * 60 + v.getMinutes();
      return vm.isTime(r, which) ? fromTime(v) : +v;
    }
    vm.ruleDirty = function (r) {
      if (!r.draft) return false;
      return r.draft.enabled !== r.enabled ||
        (r.intValue !== null && r.intValue !== undefined && value(r, 1) !== r.intValue) ||
        (r.secondValue !== null && r.secondValue !== undefined && value(r, 2) !== r.secondValue);
    };
    vm.saveRule = function (r) {
      var payload = { enabled: r.draft.enabled };
      if (r.intValue !== null && r.intValue !== undefined) payload.intValue = value(r, 1);
      if (r.secondValue !== null && r.secondValue !== undefined) payload.secondValue = value(r, 2);
      vm.busy = true;
      adminApi.put('rules/' + r.key, payload)
        .then(function () { toast.ok(r.name + ' kaydedildi ve hemen geçerli oldu.'); return loadRules(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };
    function loadRules() {
      return adminApi.get('rules').then(function (rules) {
        var groups = {};
        rules.forEach(function (r) {
          vm.resetRule(r);
          (groups[r.category] = groups[r.category] || { label: CATEGORY[r.category] || r.category, rules: [] }).rules.push(r);
        });
        vm.groups = Object.keys(groups).map(function (k) { return groups[k]; });
      }).catch(function (e) { vm.error = e.message; });
    }

    // ---- Kademe matrisi
    vm.baseLabel = function (r) { return r.kind === 'toggle' ? (r.baseEnabled ? 'Açık' : 'Kapalı') : (r.baseValue === null || r.baseValue === undefined ? '–' : r.baseValue); };
    vm.loadMatrix = function () {
      return adminApi.get('rules/matrix', { buildingId: vm.matrixBuildingId }).then(function (m) {
        var values = {};
        m.cells.forEach(function (c) { values[c.ruleKey + '|' + c.grade] = c.intValue !== null && c.intValue !== undefined ? String(c.intValue) : String(c.enabled); });
        m.rules.forEach(function (r) { vm.grades.forEach(function (g) { var id = r.key + '|' + g.key; if (!(id in values)) values[id] = ''; }); });
        vm.original = values;
        vm.draft = angular.copy(values);
        vm.matrix = m;
      }).catch(function (e) { vm.error = e.message; });
    };
    vm.resetMatrix = function () { vm.draft = angular.copy(vm.original); };
    function display(r, v) { if (!v) return 'Varsayılan (' + vm.baseLabel(r) + ')'; return r.kind === 'toggle' ? (v === 'true' ? 'Açık' : 'Kapalı') : v; }
    vm.matrixChanges = function () {
      var out = [];
      if (!vm.matrix) return out;
      vm.matrix.rules.forEach(function (r) {
        vm.grades.forEach(function (g) {
          var id = r.key + '|' + g.key, a = vm.original[id] || '', b = (vm.draft[id] === null || vm.draft[id] === undefined) ? '' : String(vm.draft[id]);
          if (a !== b) out.push({ rule: r.name, grade: g.label, from: display(r, a), to: display(r, b) });
        });
      });
      return out;
    };
    vm.saveMatrix = function () {
      var cells = [];
      vm.matrix.rules.forEach(function (r) {
        vm.grades.forEach(function (g) {
          var v = vm.draft[r.key + '|' + g.key];
          if (v === null || v === undefined || v === '') return;
          cells.push(r.kind === 'toggle' ? { ruleKey: r.key, grade: g.key, enabled: v === 'true' } : { ruleKey: r.key, grade: g.key, intValue: +v });
        });
      });
      vm.busy = true;
      adminApi.put('rules/matrix', { buildingId: vm.matrixBuildingId || null, cells: cells })
        .then(function () { toast.ok('Kademe kuralları güncellendi.'); vm.confirmMatrix = false; return vm.loadMatrix(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    // ---- Resmi tatiller
    function loadHolidays() { return adminApi.get('holidays').then(function (h) { vm.holidays = h; }); }
    vm.addHoliday = function () {
      if (!vm.holiday.date || !vm.holiday.name) return;
      var d = vm.holiday.date, key = d instanceof Date ? d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2) : d;
      vm.busy = true;
      adminApi.post('holidays', { date: key, name: vm.holiday.name.trim() })
        .then(function () { toast.ok('Tatil eklendi.'); vm.holiday = { date: '', name: '' }; return loadHolidays(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };
    vm.removeHoliday = function (h) {
      if (!window.confirm(h.name + ' silinsin mi?')) return;
      vm.busy = true;
      adminApi.del('holidays/' + h.id)
        .then(function () { toast.ok('Tatil silindi.'); return loadHolidays(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    adminApi.get('rules/attendance-feed').then(function (r) { vm.feedConfigured = r.configured; });
    loadRules();
    vm.loadMatrix();
    loadHolidays();
  }]);
})();
