/* Ofiste kim var: secilen gun masa rezervasyonu olan kisiler. Gecmis gunler de secilebilir. */
(function () {
  'use strict';

  angular.module('ofis360').controller('PresenceCtrl', ['api', 'days', 'toast', 'urlState', function (api, days, toast, urlState) {
    var vm = this;
    vm.date = days.isKey(urlState.get('date')) ? urlState.get('date') : days.today();
    vm.filter = { q: '', floorId: null, departmentId: null };
    var seq = 0;

    vm.load = function () {
      var mine = ++seq;
      urlState.set({ date: vm.date });
      return api.get('/Presence/Data', { date: vm.date, floorId: vm.filter.floorId || null, departmentId: vm.filter.departmentId || null, q: vm.filter.q || null })
        .then(function (data) { if (mine === seq) { vm.data = data; vm.error = null; } })
        .catch(function (err) { if (mine === seq) vm.error = err.message; });
    };

    vm.changeDate = function (day) { vm.date = day; vm.load(); };

    vm.setVisibility = function (managersOnly) {
      if (vm.data && vm.data.managersOnly === managersOnly) return;
      vm.busy = true;
      api.post('/Presence/Visibility', { managersOnly: managersOnly })
        .then(function () {
          toast.ok(managersOnly ? 'Sayfa artık yalnızca yöneticilere açık.' : 'Sayfa artık tüm çalışanlara açık.');
          return vm.load();
        })
        .catch(function (err) { toast.error(err.message); })
        .finally(function () { vm.busy = false; });
    };

    vm.load();
  }]);
})();
