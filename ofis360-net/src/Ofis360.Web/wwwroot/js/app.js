/*
 * Ofis360 AngularJS 1.8.2 cekirdegi.
 * Kural: AngularJS yalnizca bu MVC uygulamasinin controller'larini cagirir (/Plan/..., /Presence/...).
 * API'ye MVC sunucu tarafinda gider; tarayici API'yi hic gormez.
 */
(function () {
  'use strict';

  var app = angular.module('ofis360', []);

  app.config(['$httpProvider', '$compileProvider', function ($httpProvider, $compileProvider) {
    // MVC, oturum dustugunde AJAX isteklerine yonlendirme yerine 401 doner.
    $httpProvider.defaults.headers.common['X-Requested-With'] = 'XMLHttpRequest';
    // Antiforgery: MVC XSRF-TOKEN cerezini yazar, $http X-XSRF-TOKEN basligini kendisi ekler (varsayilan ayar).
    $httpProvider.defaults.xsrfCookieName = 'XSRF-TOKEN';
    $httpProvider.defaults.xsrfHeaderName = 'X-XSRF-TOKEN';
    $httpProvider.interceptors.push(['$q', '$window', function ($q, $window) {
      return {
        responseError: function (response) {
          if (response.status === 401) {
            var here = $window.location.pathname + $window.location.search;
            $window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(here);
          }
          return $q.reject(response);
        }
      };
    }]);
    $compileProvider.debugInfoEnabled(false);
  }]);

  /** MVC JSON aksiyonlari icin ince sarmalayici; hata mesajini API zarfindan cikarir. */
  app.factory('api', ['$http', '$q', function ($http, $q) {
    function unwrap(response) { return response.data; }
    function fail(response) {
      var body = response && response.data;
      var message = (body && body.error && body.error.message) ||
        (response && response.status === 403 ? 'Bu işlem için yetkiniz yok.' :
          response && response.status === 503 ? 'Sunucuya şu anda ulaşılamıyor.' : 'İşlem tamamlanamadı. Lütfen yeniden deneyin.');
      return $q.reject({ status: response && response.status, message: message, code: body && body.error && body.error.code, details: body && body.error && body.error.details });
    }
    return {
      get: function (url, params) { return $http.get(url, { params: params }).then(unwrap, fail); },
      post: function (url, body, params) { return $http.post(url, body || {}, { params: params }).then(unwrap, fail); }
    };
  }]);

  /** Kisa bildirimler (sag alt kose). */
  app.factory('toast', ['$timeout', function ($timeout) {
    var items = [];
    function push(kind, text) {
      var item = { kind: kind, text: text };
      items.push(item);
      $timeout(function () { var i = items.indexOf(item); if (i >= 0) items.splice(i, 1); }, 4500);
    }
    return {
      items: items,
      ok: function (text) { push('ok', text); },
      error: function (text) { push('error', text); }
    };
  }]);

  app.directive('o3Toasts', ['toast', function (toast) {
    return {
      restrict: 'A',
      template: '<div class="toast" ng-repeat="t in toasts" ng-class="\'toast-\' + t.kind" role="status">{{ t.text }}</div>',
      link: function (scope) { scope.toasts = toast.items; }
    };
  }]);

  /** Gun anahtari (YYYY-MM-DD) yardimcilari. Ofis Turkiye saatinde; tarayici yerel tarihi kullanilir. */
  app.factory('days', function () {
    var WEEKDAYS = ['Paz', 'Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt'];
    var WEEKDAYS_LONG = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'];
    var MONTHS = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'];
    function pad(n) { return n < 10 ? '0' + n : '' + n; }
    function toKey(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
    function parse(key) { var p = key.split('-'); return new Date(+p[0], +p[1] - 1, +p[2]); }
    function isKey(v) { return typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v); }
    var api = {
      today: function () { return toKey(new Date()); },
      isKey: isKey,
      parse: parse,
      add: function (key, n) { var d = parse(key); d.setDate(d.getDate() + n); return toKey(d); },
      weekday: function (key, long) { return (long ? WEEKDAYS_LONG : WEEKDAYS)[parse(key).getDay()]; },
      isWeekend: function (key) { var w = parse(key).getDay(); return w === 0 || w === 6; },
      startOfWeek: function (key) { var d = parse(key); var w = (d.getDay() + 6) % 7; d.setDate(d.getDate() - w); return toKey(d); },
      long: function (key) { var d = parse(key); return d.getDate() + ' ' + MONTHS[d.getMonth()] + ' ' + d.getFullYear() + ', ' + WEEKDAYS_LONG[d.getDay()]; },
      short: function (key) { var d = parse(key); return d.getDate() + ' ' + MONTHS[d.getMonth()].slice(0, 3); },
      monthLabel: function (key) { var d = parse(key); return MONTHS[d.getMonth()] + ' ' + d.getFullYear(); }
    };
    return api;
  });

  app.filter('dayLong', ['days', function (days) { return function (k) { return days.isKey(k) ? days.long(k) : ''; }; }]);
  app.filter('dayShort', ['days', function (days) { return function (k) { return days.isKey(k) ? days.short(k) : ''; }; }]);
  app.filter('weekday', ['days', function (days) { return function (k) { return days.isKey(k) ? days.weekday(k, true) : ''; }; }]);
  app.filter('hm', function () {
    return function (m) {
      if (m === null || m === undefined || isNaN(m)) return '';
      var h = Math.floor(m / 60), mm = m % 60;
      return (h < 10 ? '0' + h : h) + ':' + (mm < 10 ? '0' + mm : mm);
    };
  });
  app.filter('initials', function () {
    return function (name) {
      if (!name) return '';
      var parts = String(name).trim().split(/\s+/);
      return ((parts[0] || '')[0] + ((parts.length > 1 ? parts[parts.length - 1] : '')[0] || '')).toLocaleUpperCase('tr-TR');
    };
  });

  /** URL sorgu parametreleri: sayfa yenilendiginde ya da paylasildiginda secili gun ve kat korunur. */
  app.factory('urlState', ['$window', function ($window) {
    return {
      get: function (name) { return new $window.URLSearchParams($window.location.search).get(name); },
      set: function (values) {
        var params = new $window.URLSearchParams($window.location.search);
        angular.forEach(values, function (v, k) { if (v === null || v === undefined || v === '') params.delete(k); else params.set(k, v); });
        var qs = params.toString();
        $window.history.replaceState(null, '', $window.location.pathname + (qs ? '?' + qs : ''));
      }
    };
  }]);

  /**
   * Tarih seridi: iki haftalik gorunum, haftalik ileri/geri, takvimden atlama.
   * min/max verilmezse sinir yoktur; allowPast false iken bugunden onceki gunler secilemez.
   */
  app.directive('o3DateStrip', ['days', function (days) {
    return {
      restrict: 'E',
      scope: { selected: '<', min: '<?', max: '<?', blocked: '<?', allowPast: '<?', onChange: '&' },
      template:
        '<div class="date-strip">' +
        '  <button type="button" class="ds-arrow" ng-click="shift(-7)" ng-disabled="!canBack()" aria-label="Önceki hafta">‹</button>' +
        '  <div class="ds-days" role="group" aria-label="Tarih seçimi">' +
        '    <button type="button" class="ds-day" ng-repeat="d in visible track by d.key" ng-click="pick(d)" ng-disabled="d.disabled"' +
        '            ng-class="{ selected: d.key === selected, today: d.isToday, weekend: d.weekend, \'week-start\': d.weekStart }" aria-pressed="{{ d.key === selected }}" aria-label="{{ d.label }}">' +
        '      <span class="ds-wd">{{ d.wd }}</span><span class="ds-num">{{ d.num }}</span><span class="ds-sub">{{ d.isToday ? \'Bugün\' : d.mon }}</span>' +
        '    </button>' +
        '  </div>' +
        '  <button type="button" class="ds-arrow" ng-click="shift(7)" ng-disabled="!canForward()" aria-label="Sonraki hafta">›</button>' +
        '  <label class="ds-cal"><span class="sr-only">Takvimden tarih seç</span>' +
        '    <input type="date" ng-model="calendar" ng-change="jump()" ng-attr-min="{{ minKey() }}" ng-attr-max="{{ max }}" />' +
        '  </label>' +
        '  <button type="button" class="btn btn-ghost btn-sm ds-today" ng-click="goToday()" ng-if="selected !== today">Bugün</button>' +
        '</div>',
      link: function (scope) {
        var COUNT = 14;
        scope.today = days.today();
        scope.start = null;

        scope.minKey = function () { return scope.allowPast ? (scope.min || '') : (scope.min && scope.min > scope.today ? scope.min : scope.today); };
        function disabled(key) {
          var min = scope.minKey();
          return (min && key < min) || (scope.max && key > scope.max) || (scope.blocked && scope.blocked.indexOf(key) >= 0);
        }
        function build() {
          if (!scope.start) return;
          var list = [];
          for (var i = 0; i < COUNT; i++) {
            var key = days.add(scope.start, i);
            var d = days.parse(key);
            list.push({
              key: key, wd: days.weekday(key), num: d.getDate(), mon: days.short(key).split(' ')[1],
              isToday: key === scope.today, weekend: days.isWeekend(key), weekStart: i > 0 && d.getDay() === 1,
              disabled: disabled(key), label: days.long(key)
            });
          }
          scope.visible = list;
        }
        function ensureVisible(key) {
          if (!days.isKey(key)) return;
          if (!scope.start || key < scope.start || key > days.add(scope.start, COUNT - 1)) scope.start = days.startOfWeek(key);
        }
        scope.shift = function (n) { scope.start = days.add(scope.start, n); build(); };
        scope.canBack = function () { var min = scope.minKey(); return !!scope.start && (!min || days.add(scope.start, -1) >= min); };
        scope.canForward = function () { return !!scope.start && (!scope.max || days.add(scope.start, COUNT) <= scope.max); };
        scope.pick = function (d) { if (!d.disabled && d.key !== scope.selected) scope.onChange({ day: d.key }); };
        scope.goToday = function () { scope.onChange({ day: scope.today }); };
        scope.jump = function () {
          if (!scope.calendar) return;
          var key = toKey(scope.calendar);
          if (!disabled(key)) scope.onChange({ day: key });
        };
        function toKey(date) {
          var m = date.getMonth() + 1, d = date.getDate();
          return date.getFullYear() + '-' + (m < 10 ? '0' + m : m) + '-' + (d < 10 ? '0' + d : d);
        }

        scope.$watchGroup(['selected', 'min', 'max', 'blocked', 'allowPast'], function () {
          ensureVisible(scope.selected);
          scope.calendar = days.isKey(scope.selected) ? days.parse(scope.selected) : null;
          build();
        });
      }
    };
  }]);
})();
