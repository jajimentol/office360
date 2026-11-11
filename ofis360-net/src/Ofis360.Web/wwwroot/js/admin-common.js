/*
 * Yonetim ve analitik ekranlarinin ortak parcalari.
 * adminApi yalnizca MVC aksiyonlarini cagirir: /Admin/Api/... (→ /api/admin/...) ve /Manager/Api/... (→ /api/analytics/...).
 */
(function () {
  'use strict';

  var app = angular.module('ofis360');

  app.constant('labels', {
    roles: { EMPLOYEE: 'Çalışan', MANAGER: 'Yönetici', ADMIN: 'Admin' },
    grades: [
      { key: 'ASSISTANT_SPECIALIST', label: 'Uzman Yardımcısı' }, { key: 'SPECIALIST', label: 'Uzman' },
      { key: 'SENIOR_SPECIALIST', label: 'Kıdemli Uzman' }, { key: 'ASSISTANT_MANAGER', label: 'Müdür Yardımcısı' },
      { key: 'MANAGER', label: 'Müdür' }, { key: 'SENIOR_MANAGER', label: 'Kıdemli Müdür' }, { key: 'DIRECTOR', label: 'Direktör' },
      { key: 'DEPUTY_GM', label: 'Genel Müdür Yardımcısı' }, { key: 'GM', label: 'Genel Müdür' }
    ],
    closureScopes: [{ key: 'FLOOR', label: 'Kat' }, { key: 'DESK', label: 'Masa' }, { key: 'ROOM', label: 'Toplantı odası' }],
    closureReasons: [
      { key: 'MAINTENANCE', label: 'Bakım / onarım' }, { key: 'EVENT', label: 'Etkinlik' },
      { key: 'RESERVED_USE', label: 'Özel kullanım' }, { key: 'OTHER', label: 'Diğer' }
    ],
    deskStatus: { BOOKED: 'Rezerve', CHECKED_IN: 'Check-in yapıldı', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal edildi', NO_SHOW: 'Gelinmedi' },
    roomStatus: { BOOKED: 'Rezerve', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal edildi', NO_SHOW: 'Kullanılmadı' },
    zoneKinds: [
      { key: 'WORK', label: 'Çalışma alanı' }, { key: 'MEETING', label: 'Toplantı' }, { key: 'SOCIAL', label: 'Sosyal' },
      { key: 'CORRIDOR', label: 'Koridor' }, { key: 'CORE', label: 'Çekirdek' }, { key: 'RESTRICTED', label: 'Kapalı alan' }
    ]
  });

  app.factory('adminApi', ['api', function (api) {
    function url(path) { return '/Admin/Api/' + String(path).replace(/^\/+/, ''); }
    function clean(params) {
      var out = {};
      angular.forEach(params || {}, function (v, k) { if (v !== null && v !== undefined && v !== '') out[k] = v; });
      return out;
    }
    return {
      get: function (path, params) { return api.get(url(path), clean(params)); },
      post: function (path, body) { return api.post(url(path), body); },
      put: function (path, body) { return api.put(url(path), body); },
      del: function (path) { return api.del(url(path)); },
      people: function (q) { return api.get('/Admin/People', { q: q }); },
      exportUrl: function (path, params) {
        var qs = Object.keys(clean(params)).map(function (k) { return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]); }).join('&');
        return url(path) + (qs ? '?' + qs : '');
      }
    };
  }]);

  /** Basit sayfalama durumu: take/skip ve toplam. */
  app.factory('pager', function () {
    return function (size) {
      var p = { page: 1, size: size || 25, total: 0 };
      p.skip = function () { return (p.page - 1) * p.size; };
      p.pages = function () { return Math.max(1, Math.ceil(p.total / p.size)); };
      return p;
    };
  });

  app.directive('o3Pager', function () {
    return {
      restrict: 'E',
      scope: { pager: '<', onChange: '&' },
      template:
        '<div class="pager" ng-if="pager.total > pager.size">' +
        '  <button type="button" class="btn btn-ghost btn-sm" ng-disabled="pager.page <= 1" ng-click="go(-1)">‹ Önceki</button>' +
        '  <span>{{ pager.page }} / {{ pager.pages() }} · {{ pager.total }} kayıt</span>' +
        '  <button type="button" class="btn btn-ghost btn-sm" ng-disabled="pager.page >= pager.pages()" ng-click="go(1)">Sonraki ›</button>' +
        '</div>',
      link: function (scope) {
        scope.go = function (d) { scope.pager.page += d; scope.onChange(); };
      }
    };
  });


})();
