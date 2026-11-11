/**
 * Merkezi uygulama yapilandirmasi.
 * Marka adi, sloganlar ve varsayilan operasyonel degerler tek noktadan yonetilir.
 * Uygulama adini degistirmek icin yalnizca APP_CONFIG.name alanini guncellemek yeterlidir.
 */
export const APP_CONFIG = {
  name: 'Ofis360',
  shortName: 'O360',
  tagline: 'Hibrit Ofis ve Ortak Alan Rezervasyon Sistemi',
  companyName: 'Ofis360 Kurumsal',
  supportEmail: 'ofis-destek@ofis360.local',
  locale: 'tr-TR',
  timeZoneLabel: 'Türkiye Saati (GMT+3)',
  version: '1.0.0',
} as const;

/** Varsayilan calisma saatleri (dakika cinsinden, gun basindan itibaren). */
export const DEFAULT_WORKING_HOURS = {
  startMinutes: 8 * 60,
  endMinutes: 19 * 60,
  slotMinutes: 30,
} as const;

/** Kat plani gorsel olcekleme sinirlari. */
export const FLOOR_PLAN = {
  deskWidth: 46,
  deskHeight: 34,
  minZoom: 0.6,
  maxZoom: 1.8,
} as const;

/** Sistem ayarlari icin anahtar sabitleri. */
export const SETTING_KEYS = {
  appName: 'app.name',
  workdayStart: 'office.workday_start',
  workdayEnd: 'office.workday_end',
  officeDays: 'office.working_days',
  announcement: 'office.announcement',
  contactEmail: 'office.contact_email',
} as const;
