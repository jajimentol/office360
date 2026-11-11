/* Katilim raporu: /attendance-report ve CSV icin /attendance-report/export (MVC uzerinden indirilir). */
(function () {
  'use strict';
  function key(d) { return d ? d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2) : null; }

  angular.module('ofis360').controller('AdminAttendanceCtrl', ['adminApi', function (adminApi) {
    var vm = this;
    var to = new Date(); to.setHours(0, 0, 0, 0);
    var from = new Date(to); from.setDate(from.getDate() - 29);
    vm.q = { from: from, to: to, departmentId: null };

    function params() { return { from: key(vm.q.from), to: key(vm.q.to), departmentId: vm.q.departmentId }; }
    vm.exportUrl = function (type) { return adminApi.exportUrl('attendance-report/export', angular.extend({ type: type }, params())); };
    vm.load = function () {
      return adminApi.get('attendance-report', params())
        .then(function (r) { vm.r = r; vm.error = null; })
        .catch(function (e) { vm.error = e.message; });
    };

    adminApi.get('rules/attendance-feed').then(function (r) { vm.feedConfigured = r.configured; });
    adminApi.get('departments').then(function (d) { vm.departments = d; });
    vm.load();
  }]);
})();
