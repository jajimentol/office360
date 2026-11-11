/* Kat plani ekrani: tarih seridi + kroki + masa / toplanti odasi rezervasyonu. Tum istekler /Plan/* MVC aksiyonlarina gider. */
(function () {
  'use strict';

  var PURPOSES = ['Ekip içi koordinasyon', 'Müşteri görüşmesi', 'Proje çalışması', 'Değerlendirme toplantısı', 'Eğitim ve gelişim', 'İşe alım süreci'];
  var DESK_STATE_LABELS = {
    AVAILABLE: 'Uygun', MINE: 'Sizin masanız', RESERVED: 'Dolu', CLOSED: 'Kullanıma kapalı',
    ACCESSIBLE: 'Erişilebilirlik için ayrılmış', RESTRICTED_BY_RULE: 'Kural gereği seçilemez'
  };
  var STATUS_LABELS = { BOOKED: 'Rezerve', CHECKED_IN: 'Check-in yapıldı', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal edildi', NO_SHOW: 'Gelinmedi' };

  angular.module('ofis360').controller('PlanCtrl', ['$q', 'api', 'days', 'toast', 'urlState',
    function ($q, api, days, toast, urlState) {
      var vm = this;
      vm.today = days.today();
      vm.date = days.isKey(urlState.get('date')) ? urlState.get('date') : vm.today;
      vm.floorId = urlState.get('floor');
      vm.zoom = 1;
      vm.purposes = PURPOSES;
      vm.pick = { start: null, end: null };
      vm.roomForm = { title: '', purpose: PURPOSES[0], attendeeCount: 2 };
      var pendingRoomId = urlState.get('room');
      var mine = { upcomingDesks: [], upcomingRooms: [] };
      var roomAvailability = null;

      vm.deskStateLabel = function (s) { return DESK_STATE_LABELS[s] || s; };
      vm.statusLabel = function (s) { return STATUS_LABELS[s] || s; };
      vm.roomStateLabel = function (room) {
        if (room.closed) return 'Kullanıma kapalı';
        return room.dayState === 'FULL' ? 'Bu gün tamamen dolu' : room.dayState === 'PARTIAL' ? 'Bu gün kısmen dolu' : 'Bu gün boş';
      };
      vm.hasFeature = function (name) { return !!(vm.desk && vm.desk.features.some(function (f) { return f.name === name; })); };
      vm.canCheckIn = function (r) { return r && r.status === 'BOOKED' && r.date === vm.today; };
      vm.zoomBy = function (d) { vm.zoom = Math.min(2.5, Math.max(0.6, Math.round((vm.zoom + d) * 10) / 10)); };

      function fail(err) { vm.error = err.message; }

      function init() {
        vm.loading = true;
        api.get('/Plan/Context').then(function (ctx) {
          vm.floors = ctx.floors.filter(function (f) { return f.reservable; });
          if (!vm.floors.length) vm.floors = ctx.floors;
          vm.maxDay = ctx.latestBookableDayKey;
          vm.blockedDays = ctx.blockedDayKeys || [];
          if (vm.date < vm.today) vm.date = vm.today;
          if (vm.maxDay && vm.date > vm.maxDay) vm.date = vm.maxDay;
          if (!vm.floors.some(function (f) { return f.id === vm.floorId; })) vm.floorId = vm.floors.length ? vm.floors[0].id : null;
          return load();
        }).catch(fail).finally(function () { vm.loading = false; });
      }

      /** Plan, odalarin gun ici dolulugu ve kullanicinin kayitlari birlikte yuklenir. */
      function load(keepSelection) {
        if (!vm.floorId) return $q.resolve();
        vm.loading = true;
        vm.error = null;
        urlState.set({ date: vm.date, floor: vm.floorId });
        var params = { floorId: vm.floorId, date: vm.date };
        return $q.all([api.get('/Plan/Data', params), api.get('/Plan/Rooms', params), api.get('/Bookings/List')])
          .then(function (res) {
            vm.plan = res[0];
            roomAvailability = res[1];
            mine = res[2];
            refreshMine();
            if (keepSelection && vm.desk) vm.selectDesk(vm.desk.id);
            else if (keepSelection && vm.room) vm.selectRoom(vm.room.id);
            else vm.clearSelection();
            if (pendingRoomId) { vm.selectRoom(pendingRoomId); pendingRoomId = null; }
          })
          .catch(fail)
          .finally(function () { vm.loading = false; });
      }

      function refreshMine() {
        vm.myDesk = (mine.upcomingDesks || []).filter(function (r) {
          return r.date === vm.date && (r.status === 'BOOKED' || r.status === 'CHECKED_IN');
        })[0] || null;
        vm.myRoomsToday = (mine.upcomingRooms || []).filter(function (r) { return r.date === vm.date && r.status === 'BOOKED'; });
      }

      vm.changeDate = function (day) { vm.date = day; load(); };
      vm.changeFloor = function () { load(); };

      vm.clearSelection = function () {
        vm.desk = null; vm.room = null; vm.roomAvail = null;
        vm.selectedDeskId = null; vm.selectedRoomId = null;
        vm.pick = { start: null, end: null };
      };

      vm.selectDesk = function (id) {
        var desk = vm.plan && vm.plan.desks.filter(function (d) { return d.id === id; })[0];
        vm.clearSelection();
        if (!desk) return;
        vm.desk = desk;
        vm.selectedDeskId = id;
        vm.deskNote = '';
      };

      vm.selectRoom = function (id) {
        var room = vm.plan && vm.plan.rooms.filter(function (r) { return r.id === id; })[0];
        vm.clearSelection();
        if (!room) return;
        vm.room = room;
        vm.selectedRoomId = id;
        vm.roomAvail = roomAvailability && roomAvailability.rooms.filter(function (r) { return r.id === id; })[0];
        vm.roomForm.attendeeCount = Math.min(Math.max(2, (vm.roomAvail && vm.roomAvail.minAttendees) || 1), room.capacity);
        buildSlots();
      };

      vm.myRoomBooking = function (busyId) {
        return (mine.upcomingRooms || []).filter(function (r) { return r.id === busyId && r.status === 'BOOKED'; })[0] || null;
      };

      function buildSlots() {
        vm.slots = [];
        if (!roomAvailability || !vm.roomAvail) return;
        var start = roomAvailability.workStartMinutes, end = roomAvailability.workEndMinutes, step = roomAvailability.slotMinutes || 15;
        vm.roomWork = { start: start, end: end, step: step };
        var now = new Date();
        var nowMinutes = vm.date === vm.today ? now.getHours() * 60 + now.getMinutes() : -1;
        for (var m = start; m + step <= end; m += step) {
          var hit = vm.roomAvail.busySlots.filter(function (b) { return b.startMinutes < m + step && b.endMinutes > m; })[0];
          vm.slots.push({
            start: m, end: m + step, busy: !!hit, mine: !!(hit && vm.myRoomBooking(hit.id)),
            label: hit ? hit.title : '', past: nowMinutes >= 0 && m < nowMinutes
          });
        }
      }

      vm.isPicked = function (s) { return vm.pick.start !== null && s.start >= vm.pick.start && s.end <= vm.pick.end; };

      /** Ilk tik baslangici, ikinci tik bitisi secer; arada dolu dilim varsa secim bastan baslar. */
      vm.pickSlot = function (s) {
        if (s.busy || s.past) return;
        var p = vm.pick;
        if (p.start === null || p.end - p.start > vm.roomWork.step || s.start < p.start) {
          vm.pick = { start: s.start, end: s.end };
          return;
        }
        if (s.start === p.start) { vm.pick = { start: null, end: null }; return; }
        var blocked = vm.slots.some(function (x) { return x.start >= p.start && x.end <= s.end && (x.busy || x.past); });
        vm.pick = blocked ? { start: s.start, end: s.end } : { start: p.start, end: s.end };
      };

      vm.reserveDesk = function () {
        vm.busy = true;
        api.post('/Plan/ReserveDesk', { deskId: vm.desk.id, date: vm.date, note: vm.deskNote || null })
          .then(function (res) { toast.ok(res.deskCode + ' masası ' + days.long(vm.date) + ' için ayrıldı.'); return load(); })
          .catch(function (err) { toast.error(err.message); })
          .finally(function () { vm.busy = false; });
      };

      vm.cancelDesk = function (r) {
        if (!window.confirm(r.desk.code + ' masasındaki rezervasyonunuz iptal edilsin mi?')) return;
        vm.busy = true;
        api.post('/Plan/CancelDesk', {}, { id: r.id })
          .then(function () { toast.ok('Masa rezervasyonu iptal edildi.'); return load(); })
          .catch(function (err) { toast.error(err.message); })
          .finally(function () { vm.busy = false; });
      };

      vm.checkIn = function (r) {
        vm.busy = true;
        api.post('/Plan/CheckIn', {}, { id: r.id })
          .then(function () { toast.ok('Check-in yapıldı. İyi çalışmalar!'); return load(true); })
          .catch(function (err) { toast.error(err.message); })
          .finally(function () { vm.busy = false; });
      };

      vm.reserveRoom = function () {
        if (!vm.roomForm.title || vm.roomForm.title.trim().length < 3) { toast.error('Toplantı başlığı en az 3 karakter olmalıdır.'); return; }
        vm.busy = true;
        api.post('/Plan/ReserveRoom', {
          roomId: vm.room.id, date: vm.date, startMinutes: vm.pick.start, endMinutes: vm.pick.end,
          title: vm.roomForm.title.trim(), purpose: vm.roomForm.purpose, attendeeCount: vm.roomForm.attendeeCount || 1
        })
          .then(function (res) {
            toast.ok(res.roomName + ' ayrıldı.');
            vm.roomForm.title = '';
            return load(true);
          })
          .catch(function (err) { toast.error(err.message); })
          .finally(function () { vm.busy = false; });
      };

      vm.cancelRoom = function (r) {
        if (!window.confirm('"' + r.title + '" toplantısı iptal edilsin mi?')) return;
        vm.busy = true;
        api.post('/Plan/CancelRoom', {}, { id: r.id })
          .then(function () { toast.ok('Toplantı iptal edildi.'); return load(true); })
          .catch(function (err) { toast.error(err.message); })
          .finally(function () { vm.busy = false; });
      };

      init();
    }]);
})();
