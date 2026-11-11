/* Sistem ayarlari: /Admin/Api/settings */
(function () {
  'use strict';
  var GROUPS = { GENERAL: 'Genel', OFFICE: 'Ofis ve raporlar' };

  angular.module('ofis360').controller('AdminSettingsCtrl', ['adminApi', 'toast', function (adminApi, toast) {
    var vm = this;

    function load() {
      vm.loading = true;
      return adminApi.get('settings').then(function (list) {
        var groups = {};
        list.forEach(function (s) {
          s.draft = s.value;
          if (s.type === 'NUMBER') s.draftNumber = s.value === '' ? null : +s.value;
          (groups[s.group] = groups[s.group] || { label: GROUPS[s.group] || s.group, items: [] }).items.push(s);
        });
        vm.groups = Object.keys(groups).map(function (k) { return groups[k]; });
        vm.error = null;
      }).catch(function (e) { vm.error = e.message; }).finally(function () { vm.loading = false; });
    }

    vm.save = function (s) {
      vm.busy = true;
      adminApi.put('settings/' + encodeURIComponent(s.key), { value: String(s.draft) })
        .then(function () { toast.ok('"' + s.label + '" kaydedildi.'); return load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };

    load();
  }]);
})();
