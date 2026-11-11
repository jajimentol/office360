/**
 * Tarih yardimcilari.
 *
 * Tum takvim gunleri UTC gece yarisi olarak saklanir; boylece sunucu saat dilimi
 * ne olursa olsun "2026-08-21" her zaman ayni kayda karsilik gelir.
 * Kullaniciya gosterim de ayni gun anahtari uzerinden yapilir.
 */

export type DayKey = string; // YYYY-MM-DD

const TR_MONTHS = [
  'Ocak',
  'Şubat',
  'Mart',
  'Nisan',
  'Mayıs',
  'Haziran',
  'Temmuz',
  'Ağustos',
  'Eylül',
  'Ekim',
  'Kasım',
  'Aralık',
];

const TR_DAYS = ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'];
const TR_DAYS_SHORT = ['Paz', 'Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt'];

const pad = (value: number) => String(value).padStart(2, '0');

/** Verilen gun anahtarindan UTC gece yarisi Date uretir. */
export function dayKeyToDate(dayKey: DayKey): Date {
  const [year, month, day] = dayKey.split('-').map(Number);
  return new Date(Date.UTC(year, (month ?? 1) - 1, day ?? 1));
}

/** Date nesnesini YYYY-MM-DD gun anahtarina cevirir (UTC bazli). */
export function dateToDayKey(date: Date): DayKey {
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
}

/** Sunucunun yerel saatine gore bugunun gun anahtari. */
export function todayKey(now: Date = new Date()): DayKey {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** Bugunun UTC gece yarisi karsiligi. */
export function todayDate(now: Date = new Date()): Date {
  return dayKeyToDate(todayKey(now));
}

export function addDaysToKey(dayKey: DayKey, days: number): DayKey {
  const date = dayKeyToDate(dayKey);
  date.setUTCDate(date.getUTCDate() + days);
  return dateToDayKey(date);
}

export function diffInDays(fromKey: DayKey, toKey: DayKey): number {
  const from = dayKeyToDate(fromKey).getTime();
  const to = dayKeyToDate(toKey).getTime();
  return Math.round((to - from) / 86_400_000);
}

export function isValidDayKey(value: unknown): value is DayKey {
  if (typeof value !== 'string') return false;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = dayKeyToDate(value);
  return dateToDayKey(date) === value;
}

/** 0 = Pazar ... 6 = Cumartesi */
export function weekdayIndex(dayKey: DayKey): number {
  return dayKeyToDate(dayKey).getUTCDay();
}

export function isWeekend(dayKey: DayKey): boolean {
  const index = weekdayIndex(dayKey);
  return index === 0 || index === 6;
}

/** Haftanin ilk gunu (Pazartesi) anahtarini dondurur. */
export function startOfWeekKey(dayKey: DayKey): DayKey {
  const index = weekdayIndex(dayKey);
  const offset = index === 0 ? -6 : 1 - index;
  return addDaysToKey(dayKey, offset);
}

export function endOfWeekKey(dayKey: DayKey): DayKey {
  return addDaysToKey(startOfWeekKey(dayKey), 6);
}

export function rangeOfDays(startKey: DayKey, endKey: DayKey): DayKey[] {
  const result: DayKey[] = [];
  const total = diffInDays(startKey, endKey);
  for (let i = 0; i <= total; i += 1) {
    result.push(addDaysToKey(startKey, i));
  }
  return result;
}

/** Gun anahtarini "21 Agustos 2026 Cuma" bicimine cevirir. */
export function formatLongDate(dayKey: DayKey, options?: { withWeekday?: boolean }): string {
  const date = dayKeyToDate(dayKey);
  const base = `${date.getUTCDate()} ${TR_MONTHS[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
  if (options?.withWeekday === false) return base;
  return `${base} ${TR_DAYS[date.getUTCDay()]}`;
}

/** "21 Agu" kisa bicimi. */
export function formatShortDate(dayKey: DayKey): string {
  const date = dayKeyToDate(dayKey);
  return `${date.getUTCDate()} ${TR_MONTHS[date.getUTCMonth()].slice(0, 3)}`;
}

export function weekdayLabel(dayKey: DayKey, short = false): string {
  const index = weekdayIndex(dayKey);
  return short ? TR_DAYS_SHORT[index] : TR_DAYS[index];
}

export function monthLabel(monthIndex: number): string {
  return TR_MONTHS[monthIndex] ?? '';
}

/** Dakika degerini "09:30" bicimine cevirir. */
export function minutesToTime(minutes: number): string {
  return `${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`;
}

/** "09:30" degerini dakikaya cevirir. */
export function timeToMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number);
  return (hours ?? 0) * 60 + (minutes ?? 0);
}

export function isValidTime(value: unknown): value is string {
  if (typeof value !== 'string') return false;
  if (!/^([01]\d|2[0-3]):([0-5]\d)$/.test(value)) return false;
  return true;
}

/** Gun anahtari + dakika degerinden UTC tabanli tam zaman uretir. */
export function dayKeyWithMinutes(dayKey: DayKey, minutes: number): Date {
  const date = dayKeyToDate(dayKey);
  date.setUTCMinutes(minutes);
  return date;
}

/** Belirli gun icin su anki dakika degeri; gun gecmisteyse buyuk, gelecekteyse kucuk deger doner. */
export function currentMinutesForDay(dayKey: DayKey, now: Date = new Date()): number {
  const today = todayKey(now);
  if (dayKey > today) return -1;
  if (dayKey < today) return 24 * 60 + 1;
  return now.getHours() * 60 + now.getMinutes();
}

const ISTANBUL_PARTS = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Europe/Istanbul',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** Kayıt zamanını (UTC) Türkiye saatiyle "9 Kasım 2026 00:30" biçiminde gösterir. */
export function formatDateTimeLabel(date: Date): string {
  const part = (type: Intl.DateTimeFormatPartTypes) => ISTANBUL_PARTS.formatToParts(date).find((item) => item.type === type)?.value ?? '';
  const dayKey = `${part('year')}-${part('month')}-${part('day')}`;
  const minutes = Number(part('hour')) * 60 + Number(part('minute'));
  return `${formatLongDate(dayKey, { withWeekday: false })} ${minutesToTime(minutes)}`;
}

/** Bildirim listelerinde kullanilan "3 saat once" bicimi. */
export function relativeTimeLabel(date: Date, now: Date = new Date()): string {
  const diffMs = now.getTime() - date.getTime();
  const minutes = Math.round(diffMs / 60_000);
  if (minutes < 1) return 'Az önce';
  if (minutes < 60) return `${minutes} dakika önce`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} saat önce`;
  const days = Math.round(hours / 24);
  if (days < 30) return `${days} gün önce`;
  const months = Math.round(days / 30);
  return `${months} ay önce`;
}
