/* Kullanicilar: /users, /users/stats, /departments, /users/import */
(function () {
  'use strict';
  var app = angular.module('ofis360');

  /** Secilen dosyanin metnini modele yazar (CSV iceri aktarma). */
  app.directive('o3FileText', ['$parse', function ($parse) {
    return {
      restrict: 'A',
      link: function (scope, el, attrs) {
        var setter = $parse(attrs.o3FileText).assign;
        el.on('change', function () {
          var file = el[0].files && el[0].files[0];
          if (!file) return;
          var reader = new FileReader();
          reader.onload = function () { scope.$apply(function () { setter(scope, reader.result); }); };
          reader.readAsText(file, 'utf-8');
        });
      }
    };
  }]);

  app.controller('AdminUsersCtrl', ['$q', 'adminApi', 'toast', 'labels', 'pager', function ($q, adminApi, toast, labels, pager) {
    var vm = this;
    vm.grades = labels.grades;
    vm.q = { search: '', role: '', departmentId: null, grade: null, status: '' };
    vm.pager = pager(25);
    vm.roleLabel = function (r) { return labels.roles[r] || r; };

    vm.load = function () {
      return adminApi.get('users', angular.extend({ take: vm.pager.size, skip: vm.pager.skip() }, vm.q))
        .then(function (p) { vm.page = p; vm.pager.total = p.total; vm.error = null; })
        .catch(function (e) { vm.error = e.message; });
    };
    vm.search = function () { vm.pager.page = 1; vm.load(); };
    function loadStats() { return adminApi.get('users/stats').then(function (s) { vm.stats = s; }); }

    vm.openForm = function (u) {
      vm.formError = null;
      vm.form = u ? {
        id: u.id, fullName: u.fullName, email: u.email, title: u.title, role: u.role, departmentId: u.departmentId,
        grade: u.grade || null, isSecretary: u.isSecretary, accessibilityApproved: u.accessibilityApproved, active: u.active
      } : { fullName: '', email: '', title: '', role: 'EMPLOYEE', departmentId: vm.departments.length ? vm.departments[0].id : null, grade: null, isSecretary: false, accessibilityApproved: false, active: true };
    };

    vm.save = function () {
      var f = vm.form;
      var payload = { fullName: f.fullName, email: f.email, title: f.title || '', role: f.role, departmentId: f.departmentId, grade: f.grade || null,
        isSecretary: !!f.isSecretary, accessibilityApproved: !!f.accessibilityApproved, active: !!f.active };
      vm.busy = true;
      (f.id ? adminApi.put('users/' + f.id, payload) : adminApi.post('users', payload))
        .then(function () { toast.ok('Kullanıcı kaydedildi.'); vm.form = null; return $q.all([vm.load(), loadStats()]); })
        .catch(function (e) { vm.formError = e.message; })
        .finally(function () { vm.busy = false; });
    };

    vm.openImport = function () { vm.importOpen = true; vm.importCsv = ''; vm.importResult = null; vm.importError = null; };
    vm.runImport = function (apply) {
      if (apply && !window.confirm(vm.importResult.creates + ' yeni kullanıcı eklenecek, ' + vm.importResult.updates + ' kullanıcı güncellenecek. Devam edilsin mi?')) return;
      vm.busy = true;
      vm.importError = null;
      adminApi.post('users/import', { csv: vm.importCsv, apply: apply })
        .then(function (r) {
          vm.importResult = r;
          if (apply && r.applied) { toast.ok('Personel listesi uygulandı.'); return $q.all([vm.load(), loadStats()]); }
        })
        .catch(function (e) { vm.importError = e.message; })
        .finally(function () { vm.busy = false; });
    };

    adminApi.get('departments').then(function (d) { vm.departments = d; });
    vm.load();
    loadStats();
  }]);
})();
