/**
 * SQLite enum destegi olmadigindan sabitler burada tanimlanir.
 * PostgreSQL'e geciste bu birlesim tipleri dogrudan Prisma enum'larina donusturulebilir.
 */

export const GRADES = ['ASSISTANT_SPECIALIST', 'SPECIALIST', 'SENIOR_SPECIALIST', 'ASSISTANT_MANAGER', 'MANAGER', 'SENIOR_MANAGER', 'DIRECTOR', 'DEPUTY_GM', 'GM'] as const;
export type Grade = (typeof GRADES)[number];

export const GRADE_LABELS: Record<Grade, string> = {
  ASSISTANT_SPECIALIST: 'Uzman Yardımcısı',
  SPECIALIST: 'Uzman',
  SENIOR_SPECIALIST: 'Kıdemli Uzman',
  ASSISTANT_MANAGER: 'Müdür Yardımcısı',
  MANAGER: 'Müdür',
  SENIOR_MANAGER: 'Kıdemli Müdür',
  DIRECTOR: 'Direktör',
  DEPUTY_GM: 'Genel Müdür Yardımcısı',
  GM: 'Genel Müdür',
};

export const ROLES = ['EMPLOYEE', 'MANAGER', 'ADMIN'] as const;
export type Role = (typeof ROLES)[number];

export const ROLE_LABELS: Record<Role, string> = {
  EMPLOYEE: 'Çalışan',
  MANAGER: 'Yönetici',
  ADMIN: 'Yönetim (Admin)',
};

export const DESK_RESERVATION_STATUSES = [
  'BOOKED',
  'CHECKED_IN',
  'COMPLETED',
  'CANCELLED',
  'NO_SHOW',
] as const;
export type DeskReservationStatus = (typeof DESK_RESERVATION_STATUSES)[number];

export const ROOM_RESERVATION_STATUSES = ['BOOKED', 'COMPLETED', 'CANCELLED', 'NO_SHOW'] as const;
export type RoomReservationStatus = (typeof ROOM_RESERVATION_STATUSES)[number];

/** Masayi dolu sayan durumlar. */
export const OCCUPYING_DESK_STATUSES: DeskReservationStatus[] = [
  'BOOKED',
  'CHECKED_IN',
  'COMPLETED',
  'NO_SHOW',
];

/** Odayi dolu sayan durumlar. */
export const OCCUPYING_ROOM_STATUSES: RoomReservationStatus[] = ['BOOKED', 'COMPLETED', 'NO_SHOW'];

export const RESERVATION_STATUS_LABELS: Record<string, string> = {
  BOOKED: 'Rezerve',
  CHECKED_IN: 'Giriş yapıldı',
  COMPLETED: 'Tamamlandı',
  CANCELLED: 'İptal edildi',
  NO_SHOW: 'Kullanılmadı',
};

export const ZONE_KINDS = ['WORK', 'MEETING', 'SOCIAL', 'CORRIDOR', 'CORE', 'RESTRICTED'] as const;
export type ZoneKind = (typeof ZONE_KINDS)[number];

export const ZONE_KIND_LABELS: Record<ZoneKind, string> = {
  WORK: 'Çalışma alanı',
  MEETING: 'Toplantı odası',
  SOCIAL: 'Mutfak / sosyal alan',
  CORRIDOR: 'Koridor',
  CORE: 'Asansör ve merdiven',
  RESTRICTED: 'Rezervasyona kapalı alan',
};

export const CLOSURE_SCOPES = ['FLOOR', 'DESK', 'ROOM'] as const;
export type ClosureScope = (typeof CLOSURE_SCOPES)[number];

export const CLOSURE_SCOPE_LABELS: Record<ClosureScope, string> = {
  FLOOR: 'Kat',
  DESK: 'Masa',
  ROOM: 'Toplantı odası',
};

export const CLOSURE_REASONS = ['MAINTENANCE', 'EVENT', 'RESERVED_USE', 'OTHER'] as const;
export type ClosureReason = (typeof CLOSURE_REASONS)[number];

export const CLOSURE_REASON_LABELS: Record<ClosureReason, string> = {
  MAINTENANCE: 'Bakım / onarım',
  EVENT: 'Etkinlik',
  RESERVED_USE: 'Özel kullanım',
  OTHER: 'Diğer',
};

/** Masa gorsel durumlari (kat planinda kullanilir). */
export const DESK_VIEW_STATES = [
  'AVAILABLE',
  'SELECTED',
  'RESERVED',
  'CLOSED',
  'ACCESSIBLE',
  'MINE',
  'RESTRICTED_BY_RULE',
] as const;
export type DeskViewState = (typeof DESK_VIEW_STATES)[number];

export const NOTIFICATION_TYPES = {
  deskReservationCreated: 'DESK_RESERVATION_CREATED',
  deskReservationCancelled: 'DESK_RESERVATION_CANCELLED',
  deskReservationCancelledByAdmin: 'DESK_RESERVATION_CANCELLED_BY_ADMIN',
  roomReservationCreated: 'ROOM_RESERVATION_CREATED',
  roomReservationCancelled: 'ROOM_RESERVATION_CANCELLED',
  roomReservationCancelledByAdmin: 'ROOM_RESERVATION_CANCELLED_BY_ADMIN',
  upcomingReservationReminder: 'UPCOMING_RESERVATION_REMINDER',
  resourceClosed: 'RESOURCE_CLOSED',
  alternativeDeskSuggestion: 'ALTERNATIVE_DESK_SUGGESTION',
  announcement: 'ANNOUNCEMENT',
} as const;

export type NotificationType = (typeof NOTIFICATION_TYPES)[keyof typeof NOTIFICATION_TYPES];

export const AUDIT_ACTIONS = {
  login: 'AUTH_LOGIN',
  logout: 'AUTH_LOGOUT',
  deskReservationCreate: 'DESK_RESERVATION_CREATE',
  deskReservationCancel: 'DESK_RESERVATION_CANCEL',
  roomReservationCreate: 'ROOM_RESERVATION_CREATE',
  roomReservationCancel: 'ROOM_RESERVATION_CANCEL',
  buildingCreate: 'BUILDING_CREATE',
  buildingUpdate: 'BUILDING_UPDATE',
  floorCreate: 'FLOOR_CREATE',
  floorUpdate: 'FLOOR_UPDATE',
  zoneUpdate: 'ZONE_UPDATE',
  deskCreate: 'DESK_CREATE',
  deskUpdate: 'DESK_UPDATE',
  deskDelete: 'DESK_DELETE',
  roomCreate: 'ROOM_CREATE',
  roomUpdate: 'ROOM_UPDATE',
  roomDelete: 'ROOM_DELETE',
  ruleUpdate: 'RULE_UPDATE',
  userCreate: 'USER_CREATE',
  userUpdate: 'USER_UPDATE',
  userImport: 'USER_IMPORT',
  closureCreate: 'CLOSURE_CREATE',
  closureUpdate: 'CLOSURE_UPDATE',
  settingUpdate: 'SETTING_UPDATE',
  floorPlanSave: 'FLOOR_PLAN_SAVE',
  reportExport: 'REPORT_EXPORT',
} as const;

export const AUDIT_ACTION_LABELS: Record<string, string> = {
  AUTH_LOGIN: 'Oturum açıldı',
  AUTH_LOGOUT: 'Oturum kapatıldı',
  DESK_RESERVATION_CREATE: 'Masa rezervasyonu oluşturuldu',
  DESK_RESERVATION_CANCEL: 'Masa rezervasyonu iptal edildi',
  ROOM_RESERVATION_CREATE: 'Toplantı odası rezervasyonu oluşturuldu',
  ROOM_RESERVATION_CANCEL: 'Toplantı odası rezervasyonu iptal edildi',
  BUILDING_CREATE: 'Bina eklendi',
  BUILDING_UPDATE: 'Bina güncellendi',
  FLOOR_CREATE: 'Kat eklendi',
  FLOOR_UPDATE: 'Kat güncellendi',
  ZONE_UPDATE: 'Bölge güncellendi',
  DESK_CREATE: 'Masa eklendi',
  DESK_UPDATE: 'Masa güncellendi',
  DESK_DELETE: 'Masa silindi',
  ROOM_CREATE: 'Toplantı odası eklendi',
  ROOM_UPDATE: 'Toplantı odası güncellendi',
  ROOM_DELETE: 'Toplantı odası silindi',
  ROOM_POLICY_UPDATE: 'Oda kuralları güncellendi',
  NO_SHOW_RUN: 'Otomatik no-show işlemi',
  RULE_UPDATE: 'Rezervasyon kuralı güncellendi',
  USER_CREATE: 'Kullanıcı eklendi',
  USER_UPDATE: 'Kullanıcı güncellendi',
  USER_IMPORT: 'Kullanıcılar toplu yüklendi',
  RULE_MATRIX_UPDATE: 'Kademe bazlı kurallar güncellendi',
  HOLIDAY_CREATE: 'Resmi tatil eklendi',
  HOLIDAY_DELETE: 'Resmi tatil silindi',
  CLOSURE_CREATE: 'Kapatma kaydı oluşturuldu',
  CLOSURE_UPDATE: 'Kapatma kaydı güncellendi',
  SETTING_UPDATE: 'Sistem ayarı güncellendi',
  FLOOR_PLAN_SAVE: 'Kat krokisi kaydedildi',
  REPORT_EXPORT: 'Rapor dışa aktarıldı',
};

export const RULE_KEYS = {
  oneDeskPerDay: 'ONE_DESK_PER_DAY',
  advanceBookingDays: 'ADVANCE_BOOKING_DAYS',
  weeklyDeskLimit: 'WEEKLY_DESK_LIMIT',
  popularDeskStreakLimit: 'POPULAR_DESK_STREAK_LIMIT',
  popularShareLimit: 'POPULAR_SHARE_LIMIT',
  accessibleDeskRestriction: 'ACCESSIBLE_DESK_RESTRICTION',
  noPastDate: 'NO_PAST_DATE',
  roomWorkingHours: 'ROOM_WORKING_HOURS',
  roomCapacityCheck: 'ROOM_CAPACITY_CHECK',
  roomMaxDurationMinutes: 'ROOM_MAX_DURATION_MINUTES',
  noShowRelease: 'NO_SHOW_RELEASE',
} as const;

export type RuleKey = (typeof RULE_KEYS)[keyof typeof RULE_KEYS];
