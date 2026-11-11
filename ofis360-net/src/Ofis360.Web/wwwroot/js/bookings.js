/* Rezervasyonlarim: /Bookings/List ile yuklenir; iptal ve check-in /Plan/* aksiyonlarini kullanir. */
(function () {
  'use strict';

  var STATUS = { BOOKED: 'Rezerve', CHECKED_IN: 'Check-in yapıldı', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal edildi', NO_SHOW: 'Gelinmedi' };
  var ROOM_STATUS = { BOOKED: 'Rezerve', COMPLETED: 'Tamamlandı', CANCELLED: 'İptal edildi', NO_SHOW: 'Kullanılmadı' };

  angular.module('ofis360').controller('BookingsCtrl', ['api', 'days', 'toast', function (api, days, toast) {
    var vm = this;
    vm.tab = 'upcoming';
    vm.today = days.today();
    vm.statusLabel = function (s) { return STATUS[s] || s; };
    vm.roomStatusLabel = function (s) { return ROOM_STATUS[s] || s; };
    vm.desks = function () { return vm.data ? (vm.tab === 'upcoming' ? vm.data.upcomingDesks : vm.data.pastDesks) : []; };
    vm.rooms = function () { return vm.data ? (vm.tab === 'upcoming' ? vm.data.upcomingRooms : vm.data.pastRooms) : []; };
    vm.canCheckIn = function (r) { return vm.tab === 'upcoming' && r.status === 'BOOKED' && r.date === vm.today; };

    function load() {
      vm.loading = true;
      return api.get('/Bookings/List')
        .then(function (data) { vm.data = data; vm.error = null; })
        .catch(function (err) { vm.error = err.message; })
        .finally(function () { vm.loading = false; });
    }

    function act(url, id, message) {
      vm.busy = true;
      return api.post(url, {}, { id: id })
        .then(function () { toast.ok(message); return load(); })
        .catch(function (err) { toast.error(err.message); })
        .finally(function () { vm.busy = false; });
    }

    vm.cancelDesk = function (r) {
      if (window.confirm(days.long(r.date) + ' tarihli ' + r.desk.code + ' rezervasyonu iptal edilsin mi?')) act('/Plan/CancelDesk', r.id, 'Masa rezervasyonu iptal edildi.');
    };
    vm.cancelRoom = function (r) {
      if (window.confirm('"' + r.title + '" toplantısı iptal edilsin mi?')) act('/Plan/CancelRoom', r.id, 'Toplantı iptal edildi.');
    };
    vm.checkIn = function (r) { act('/Plan/CheckIn', r.id, 'Check-in yapıldı.'); };

    load();
  }]);
})();
