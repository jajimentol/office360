/* Yonetici analitigi: /Manager/Api (→ /api/analytics). Grafikler Chart.js 4 ile (yerel kopya, derleme adimi yok). */
(function () {
  'use strict';

  var BRAND = '#1f5493', BRAND_LIGHT = 'rgba(31, 84, 147, .15)', ACCENT = '#0d9488', RED = '#FF0025';
  var HEAT = ['#eef4fb', '#cfe0f3', '#9ec1e6', '#5f97d2', '#2f6db5', '#1a4580'];
  var EXPORTS = [
    { key: 'ozet', label: 'Özet' }, { key: 'doluluk', label: 'Doluluk' }, { key: 'kat', label: 'Kat' }, { key: 'masa', label: 'Masa' },
    { key: 'oda', label: 'Oda' }, { key: 'departman', label: 'Departman' }, { key: 'adil-kullanim', label: 'Adil kullanım' }
  ];

  function key(d) { return d ? d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2) : null; }
  function pct(v) { return Math.round(v) + '%'; }

  var app = angular.module('ofis360');

  function bar(labels, datasets, opts) {
    opts = opts || {};
    return {
      type: 'bar',
      data: { labels: labels, datasets: datasets },
      options: {
        indexAxis: opts.horizontal ? 'y' : 'x', maintainAspectRatio: false, responsive: true,
        plugins: { legend: { display: datasets.length > 1 }, tooltip: { callbacks: opts.percent ? { label: function (c) { return c.dataset.label + ': ' + pct(c.raw); } } : {} } },
        scales: (function () {
          var value = { beginAtZero: true, ticks: opts.percent ? { callback: function (v) { return v + '%'; } } : {} };
          if (opts.percent) value.max = 100;
          return opts.horizontal ? { x: value } : { y: value };
        })()
      }
    };
  }

  function line(labels, datasets, opts) {
    opts = opts || {};
    return {
      type: 'line',
      data: { labels: labels, datasets: datasets },
      options: {
        maintainAspectRatio: false, responsive: true, interaction: { mode: 'index', intersect: false },
        plugins: { legend: { display: datasets.length > 1 } },
        scales: { y: { beginAtZero: true, max: opts.percent ? 100 : undefined, ticks: opts.percent ? { callback: function (v) { return v + '%'; } } : {} } }
      }
    };
  }

  app.controller('AnalyticsCtrl', ['$q', 'api', function ($q, api) {
    var vm = this;
    var to = new Date(); to.setHours(0, 0, 0, 0);
    var from = new Date(to); from.setDate(from.getDate() - 29);
    vm.q = { from: from, to: to, floor: null, department: null, resource: 'ALL' };
    vm.exports = EXPORTS;
    vm.heatColors = HEAT;
    vm.charts = {};

    function params() { return { from: key(vm.q.from), to: key(vm.q.to), floor: vm.q.floor || null, department: vm.q.department || null, resource: vm.q.resource }; }
    vm.exportUrl = function (type) {
      var p = angular.extend({ type: type }, params());
      return '/Manager/Api/export?' + Object.keys(p).filter(function (k) { return p[k]; }).map(function (k) { return k + '=' + encodeURIComponent(p[k]); }).join('&');
    };

    function buildCharts(a) {
      vm.charts = {
        trend: line(a.occupancyTrend.map(function (r) { return r.label; }),
          [{ label: 'Doluluk oranı', data: a.occupancyTrend.map(function (r) { return Math.round(r.occupancyRate * 10) / 10; }), borderColor: BRAND, backgroundColor: BRAND_LIGHT, fill: true, tension: .3 }], { percent: true }),
        floors: bar(a.floorComparison.map(function (r) { return r.name; }),
          [{ label: 'Doluluk oranı', data: a.floorComparison.map(function (r) { return r.occupancyRate; }), backgroundColor: BRAND }], { horizontal: true, percent: true }),
        hourly: bar(a.hourlyDensity.map(function (r) { return r.hour; }), [
          { label: 'Toplantı (adet)', data: a.hourlyDensity.map(function (r) { return r.meetings; }), backgroundColor: BRAND },
          { label: 'Ofis girişi (adet)', data: a.hourlyDensity.map(function (r) { return r.arrivals; }), backgroundColor: ACCENT }]),
        departments: bar(a.departmentDistribution.map(function (r) { return r.name; }),
          [{ label: 'Ofise geliş oranı', data: a.departmentDistribution.map(function (r) { return r.attendanceRate; }), backgroundColor: a.departmentDistribution.map(function (r) { return r.color || BRAND; }) }], { horizontal: true, percent: true }),
        bookedUsed: line(a.bookedVsUsed.map(function (r) { return r.label; }), [
          { label: 'Rezervasyon', data: a.bookedVsUsed.map(function (r) { return r.booked; }), borderColor: BRAND, tension: .3 },
          { label: 'Gerçekleşen kullanım', data: a.bookedVsUsed.map(function (r) { return r.used; }), borderColor: ACCENT, tension: .3 },
          { label: 'Gelinmedi', data: a.bookedVsUsed.map(function (r) { return r.noShow; }), borderColor: RED, borderDash: [4, 4], tension: .3 }]),
        rooms: bar(a.roomUsage.map(function (r) { return r.name; }),
          [{ label: 'Kullanım oranı', data: a.roomUsage.map(function (r) { return r.usageRate; }), backgroundColor: '#6B8BF5' }], { horizontal: true, percent: true }),
        distribution: bar(a.fairness.distribution.map(function (r) { return r.bucket; }),
          [{ label: 'Çalışan sayısı', data: a.fairness.distribution.map(function (r) { return r.employees; }), backgroundColor: BRAND }]),
        popular: bar(a.fairness.popularShare.map(function (r) { return r.bucket; }),
          [{ label: 'Çalışan sayısı', data: a.fairness.popularShare.map(function (r) { return r.employees; }), backgroundColor: ACCENT }])
      };
    }

    /** Isi haritasi: krokideki masalar rezervasyon sayisina gore 6 tonla boyanir. */
    vm.buildHeatmap = function () {
      var floor = (vm.ctx.planFloors || []).filter(function (f) { return f.id === vm.heatFloorId; })[0];
      if (!floor || !vm.a) { vm.heatPlan = null; return; }
      var counts = {};
      vm.a.deskHeatmap.forEach(function (d) { counts[d.deskId] = d; });
      var max = Math.max.apply(null, floor.desks.map(function (d) { return counts[d.id] ? counts[d.id].reservations : 0; }).concat([1]));
      var fill = {};
      floor.desks.forEach(function (d) {
        var c = counts[d.id];
        if (!c || !c.reservations) return;
        var i = Math.min(HEAT.length - 1, Math.ceil(c.reservations / max * (HEAT.length - 1)));
        fill[d.id] = { color: HEAT[i], dark: i >= 3, label: c.reservations + ' rezervasyon' };
      });
      vm.heatFill = fill;
      vm.heatPlan = { floor: { planWidth: floor.planWidth, planHeight: floor.planHeight }, elements: floor.elements, zones: [], rooms: [], desks: floor.desks };
    };

    vm.load = function () {
      vm.error = null;
      return api.get('/Manager/Api', params()).then(function (a) {
        vm.a = a;
        vm.leastUsed = a.deskHeatmap.slice().reverse().slice(0, 6);
        buildCharts(a);
        vm.buildHeatmap();
      }).catch(function (e) { vm.error = e.message; });
    };

    api.get('/Manager/Api/context').then(function (ctx) {
      vm.ctx = ctx;
      vm.heatFloorId = ctx.planFloors.length ? ctx.planFloors[0].id : null;
      return vm.load();
    }).catch(function (e) { vm.error = e.message; });
  }]);
})();
