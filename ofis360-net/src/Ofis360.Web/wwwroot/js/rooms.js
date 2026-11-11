/* Toplanti odasi rezervasyonu: /Rooms/Availability + /Plan/ReserveRoom. Takvim (oda x saat) ve liste gorunumu. */
(function () {
  'use strict';

  var PURPOSES = ['Ekip içi koordinasyon', 'Müşteri görüşmesi', 'Proje çalışması', 'Değerlendirme toplantısı', 'Eğitim ve gelişim', 'İşe alım süreci'];
  function hm(m) { var h = Math.floor(m / 60), mm = m % 60; return (h < 10 ? '0' : '') + h + ':' + (mm < 10 ? '0' : '') + mm; }

  angular.module('ofis360').controller('RoomsCtrl', ['$q', 'api', 'days', 'toast', 'urlState', 'currentUser', function ($q, api, days, toast, urlState, currentUser) {
    var vm = this;
    vm.today = days.today();
    vm.date = days.isKey(urlState.get('date')) ? urlState.get('date') : vm.today;
    vm.view = urlState.get('view') === 'list' ? 'list' : 'calendar';
    vm.purposes = PURPOSES;
    vm.canActForOthers = currentUser.canActForOthers;
    vm.f = { start: null, end: null, capacity: null, floorId: null, features: [] };
    vm.min = Math.min;
    var mine = [];

    vm.toggleFeature = function (key) {
      var i = vm.f.features.indexOf(key);
      if (i >= 0) vm.f.features.splice(i, 1); else vm.f.features.push(key);
      load();
    };
    vm.apply = function () { load(); };
    vm.reset = function () { vm.f = { start: null, end: null, capacity: null, floorId: null, features: [] }; load(); };
    vm.changeDate = function (day) { vm.date = day; load(); };
    vm.openCount = function () { return (vm.rooms || []).filter(function (r) { return !r.closed; }).length; };
    vm.isMine = function (b) { return mine.indexOf(b.id) >= 0; };

    // Takvim ekseni: mesai saatleri yuzde olarak.
    vm.pos = function (m) { return vm.work ? Math.max(0, Math.min(100, (m - vm.work.start) / (vm.work.end - vm.work.start) * 100)) : 0; };
    vm.nowPos = function () {
      if (vm.date !== vm.today || !vm.work) return 0;
      var n = new Date(); return vm.pos(n.getHours() * 60 + n.getMinutes());
    };
    vm.trackClick = function (event, room) {
      if (room.closed || room.accessReason) return;
      var rect = event.currentTarget.getBoundingClientRect();
      var m = vm.work.start + (event.clientX - rect.left) / rect.width * (vm.work.end - vm.work.start);
      var start = Math.floor(m / vm.work.step) * vm.work.step;
      var free = room.freeSlots.filter(function (s) { return s.startMinutes <= start && s.endMinutes > start; })[0];
      if (!free) { toast.error('Seçilen saat dolu ya da geçmişte.'); return; }
      vm.openBooking(room, start, Math.min(start + 60, free.endMinutes));
    };

    function buildTimes() {
      vm.times = [];
      for (var m = vm.work.start; m <= vm.work.end; m += vm.work.step) vm.times.push({ m: m, label: hm(m) });
      vm.endTimes = vm.times.slice(1);
      vm.hourMarks = [];
      for (var h = Math.ceil(vm.work.start / 60) * 60; h <= vm.work.end; h += 60) vm.hourMarks.push(h);
    }

    function load() {
      vm.loading = true;
      vm.error = null;
      urlState.set({ date: vm.date, view: vm.view === 'list' ? 'list' : null });
      var timeOk = vm.f.start !== null && vm.f.end !== null && vm.f.end > vm.f.start;
      vm.timeFilter = timeOk ? { start: vm.f.start, end: vm.f.end } : null;
      return $q.all([
        api.get('/Rooms/Availability', {
          date: vm.date, start: timeOk ? vm.f.start : null, end: timeOk ? vm.f.end : null, minCapacity: vm.f.capacity || null,
          floorId: vm.f.floorId || null, features: vm.f.features.join(',') || null
        }),
        api.get('/Bookings/List')
      ]).then(function (r) {
        var av = r[0];
        vm.av = av;
        vm.work = { start: av.workStartMinutes, end: av.workEndMinutes, step: av.slotMinutes || 15 };
        buildTimes();
        mine = (r[1].upcomingRooms || []).concat(r[1].pastRooms || []).map(function (x) { return x.id; });
        // Saat filtresi verildiyse yalnizca o aralikta alinabilen odalar listelenir.
        vm.rooms = timeOk ? av.rooms.filter(function (x) { return x.availableForRequest; }) : av.rooms;
      }).catch(function (e) { vm.error = e.message; })
        .finally(function () { vm.loading = false; });
    }

    vm.openBooking = function (room, start, end) {
      vm.violations = []; vm.suggestions = null; vm.formError = null;
      var first = room.freeSlots[0];
      start = start !== undefined ? start : (vm.timeFilter ? vm.timeFilter.start : first ? first.startMinutes : vm.work.start);
      end = end !== undefined ? end : (vm.timeFilter ? vm.timeFilter.end : first ? Math.min(first.startMinutes + 60, first.endMinutes) : start + 60);
      vm.form = { target: vm.form ? vm.form.target : null, title: vm.form && vm.room ? vm.form.title : '', start: start, end: end,
        attendees: Math.min(Math.max(room.minAttendees || 1, vm.f.capacity || 2), room.capacity), purpose: PURPOSES[0] };
      vm.room = room;
    };
    vm.pickSlot = function (start, end) { vm.form.start = start; vm.form.end = end; };
    vm.switchRoom = function (id) {
      var r = (vm.av.rooms || []).filter(function (x) { return x.id === id; })[0];
      if (r) { var keep = vm.form; vm.room = r; vm.violations = []; vm.suggestions = null; vm.form = keep; }
    };

    vm.submit = function () {
      var f = vm.form;
      if (!f.title || f.title.trim().length < 3) { vm.formError = 'Toplantı başlığı en az 3 karakter olmalıdır.'; vm.violations = []; return; }
      if (!(f.end > f.start)) { vm.formError = 'Bitiş saati başlangıç saatinden sonra olmalıdır.'; vm.violations = []; return; }
      vm.busy = true; vm.formError = null; vm.violations = []; vm.suggestions = null;
      api.post('/Plan/ReserveRoom', {
        roomId: vm.room.id, date: vm.date, startMinutes: f.start, endMinutes: f.end, title: f.title.trim(), purpose: f.purpose,
        attendeeCount: +f.attendees || 1, targetUserId: f.target ? f.target.id : null
      }).then(function (res) {
        toast.ok(res.roomName + ' ' + hm(f.start) + '–' + hm(f.end) + ' için ayrıldı' + (f.target ? ' (' + f.target.fullName + ' adına).' : '.'));
        vm.room = null; vm.form = null;
        return load();
      }).catch(function (e) {
        vm.formError = e.message;
        vm.violations = e.violations || [];
        vm.suggestions = e.details && (e.details.alternativeSlots || e.details.alternativeRooms) ? e.details : null;
      }).finally(function () { vm.busy = false; });
    };

    api.get('/Plan/Context').then(function (ctx) {
      vm.maxDay = ctx.latestBookableDayKey;
      vm.blockedDays = ctx.blockedDayKeys || [];
      vm.features = ctx.roomFeatures;
      vm.roomFloors = ctx.floors.filter(function (f) { return f.hasRooms; }).map(function (f) { return { id: f.id, label: f.buildingName + ' · ' + f.name }; });
      if (vm.date < vm.today) vm.date = vm.today;
      if (vm.maxDay && vm.date > vm.maxDay) vm.date = vm.maxDay;
      return load();
    }).catch(function (e) { vm.error = e.message; });
  }]);
})();
