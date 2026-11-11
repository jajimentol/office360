/* Kisisel sayfalar: kullanim ozetim, bildirim merkezi, profil ve tercihler (/Me/*). */
(function () {
  'use strict';
  var app = angular.module('ofis360');
  var BRAND = '#1f5493';
  var CHANNELS = { IN_APP: 'Uygulama içi', EMAIL: 'E-posta', TEAMS: 'Teams' };

  function barChart(labels, data, label) {
    return {
      type: 'bar', data: { labels: labels, datasets: [{ label: label, data: data, backgroundColor: BRAND }] },
      options: { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
    };
  }

  app.controller('UsageCtrl', ['api', function (api) {
    var vm = this;
    vm.charts = {};
    api.get('/Me/UsageData').then(function (u) {
      vm.u = u;
      vm.charts = {
        weekly: barChart(u.weeklyTrend.map(function (w) { return w.label; }), u.weeklyTrend.map(function (w) { return w.days; }), 'Ofis günü'),
        weekday: barChart(u.weekdayDistribution.map(function (w) { return w.label; }), u.weekdayDistribution.map(function (w) { return w.count; }), 'Rezervasyon')
      };
    }).catch(function (e) { vm.error = e.message; });
  }]);

  app.controller('NotificationsCtrl', ['api', 'toast', function (api, toast) {
    var vm = this;
    vm.onlyUnread = false;
    vm.channelLabel = function (k) { return CHANNELS[k] || k; };
    vm.visible = function () { return vm.n ? (vm.onlyUnread ? vm.unreadItems : vm.n.items) : []; };
    // Eski uygulamanin yollari yeni sayfalara cevrilir.
    vm.link = function (l) {
      if (!l) return null;
      if (l.indexOf('/panel/rezervasyonlarim') === 0) return '/Bookings';
      if (l.indexOf('/panel/toplanti') === 0) return '/Rooms';
      if (l.indexOf('/panel/masa') === 0) return '/Plan' + (l.indexOf('?') >= 0 ? l.slice(l.indexOf('?')) : '');
      if (l.indexOf('/panel') === 0) return '/Home';
      return l;
    };
    function load() {
      return api.get('/Me/NotificationList', { take: 100 }).then(function (n) {
        vm.n = n;
        vm.unreadItems = n.items.filter(function (x) { return !x.readAt; });
      }).catch(function (e) { vm.error = e.message; });
    }
    vm.read = function (x) {
      vm.busy = true;
      api.post('/Me/MarkRead', {}, { id: x.id }).then(load).catch(function (e) { toast.error(e.message); }).finally(function () { vm.busy = false; });
    };
    vm.readAll = function () {
      vm.busy = true;
      api.post('/Me/MarkAllRead').then(function () { toast.ok('Tüm bildirimler okundu.'); return load(); })
        .catch(function (e) { toast.error(e.message); }).finally(function () { vm.busy = false; });
    };
    load();
  }]);

  app.controller('ProfileCtrl', ['$q', 'api', 'toast', 'labels', function ($q, api, toast, labels) {
    var vm = this;
    vm.roleLabel = function (r) { return labels.roles[r] || r; };
    vm.gradeLabel = function (g) { var x = labels.grades.filter(function (i) { return i.key === g; })[0]; return x ? x.label : 'Atanmamış'; };
    function load() {
      return $q.all([api.get('/Me/ProfileData'), api.get('/Me/Context')]).then(function (r) {
        vm.user = r[0].user;
        vm.ctx = r[1];
        vm.prefs = { preferredFloorId: vm.user.preferredFloorId || null, prefersQuietZone: vm.user.prefersQuietZone, notificationsEnabled: vm.user.notificationsEnabled };
      }).catch(function (e) { vm.error = e.message; });
    }
    vm.save = function () {
      vm.busy = true;
      api.post('/Me/Preferences', {
        preferredFloorId: vm.prefs.preferredFloorId || null, clearPreferredFloor: !vm.prefs.preferredFloorId,
        prefersQuietZone: !!vm.prefs.prefersQuietZone, notificationsEnabled: !!vm.prefs.notificationsEnabled
      }).then(function () { toast.ok('Tercihleriniz kaydedildi.'); return load(); })
        .catch(function (e) { toast.error(e.message); })
        .finally(function () { vm.busy = false; });
    };
    load();
  }]);
})();
