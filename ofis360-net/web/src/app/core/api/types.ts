/**
 * REST API sözleşmeleri (kaynak: src/Ofis360.Api/Contracts/Dto.cs ve Application servis record'ları).
 * Alan adları camelCase; kaynak Next.js uygulamasındaki Prisma dönüşleriyle aynıdır.
 */
import type { PlanElement } from '../../lib/plan';

export type Role = 'EMPLOYEE' | 'MANAGER' | 'ADMIN';

export interface SessionUser {
  id: string;
  email: string;
  fullName: string;
  title: string;
  role: Role;
  departmentId: string;
  departmentName: string;
  accessibilityApproved: boolean;
  preferredFloorId: string | null;
  prefersQuietZone: boolean;
  notificationsEnabled: boolean;
  grade: string | null;
  isSecretary: boolean;
}

export interface MeResponse {
  user: SessionUser;
  demoMode: boolean;
  defaultRoute: string;
  appName: string;
}

export interface DepartmentDto {
  id: string;
  code: string;
  name: string;
  color: string;
  active: boolean;
  userCount?: number | null;
}

export interface BuildingDto {
  id: string;
  code: string;
  name: string;
  address: string;
  order: number;
  active: boolean;
  isDemo: boolean;
  createdAt: string;
  updatedAt: string;
  floorCount?: number | null;
}

export interface FloorRefDto {
  id: string;
  number: number;
  name: string;
  reservable: boolean;
  active: boolean;
  buildingId: string;
  planWidth: number;
  planHeight: number;
}

export interface ZoneDto {
  id: string;
  floorId: string;
  code: string;
  name: string;
  kind: string;
  x: number;
  y: number;
  width: number;
  height: number;
  windowSide: boolean;
  quiet: boolean;
  reservable: boolean;
}

export interface FeatureDto {
  id: string;
  key: string;
  name: string;
  icon: string;
  description?: string | null;
}

export interface UserRefDto {
  id: string;
  fullName: string;
  email: string;
  title: string;
  role: Role;
  departmentId: string;
  departmentName: string | null;
}

export interface FloorDto {
  id: string;
  buildingId: string;
  number: number;
  name: string;
  description: string;
  reservable: boolean;
  active: boolean;
  planWidth: number;
  planHeight: number;
  createdAt: string;
  updatedAt: string;
  building: BuildingDto | null;
  zones: ZoneDto[] | null;
  deskCount: number | null;
  roomCount: number | null;
  label: string;
}

export interface DeskDto {
  id: string;
  code: string;
  floorId: string;
  zoneId: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  placed: boolean;
  active: boolean;
  accessibleOnly: boolean;
  popular: boolean;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  floor: FloorRefDto | null;
  zone: ZoneDto | null;
  features: FeatureDto[];
  reservationCount?: number | null;
}

export interface RoomDto {
  id: string;
  code: string;
  name: string;
  floorId: string;
  capacity: number;
  x: number;
  y: number;
  width: number;
  height: number;
  placed: boolean;
  active: boolean;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  floor: FloorRefDto | null;
  features: FeatureDto[];
  reservationCount?: number | null;
}

export interface DeskReservationDto {
  id: string;
  deskId: string;
  userId: string;
  date: string;
  status: string;
  note: string | null;
  source: string;
  checkedInAt: string | null;
  cancelledAt: string | null;
  cancelledById: string | null;
  cancelReason: string | null;
  createdAt: string;
  updatedAt: string;
  desk: DeskDto | null;
  user: UserRefDto | null;
}

export interface RoomReservationDto {
  id: string;
  roomId: string;
  userId: string;
  date: string;
  startAt: string;
  endAt: string;
  startMinutes: number;
  endMinutes: number;
  title: string;
  purpose: string;
  attendeeCount: number;
  status: string;
  source: string;
  startedAt?: string | null;
  cancelledAt: string | null;
  cancelledById: string | null;
  cancelReason: string | null;
  createdAt: string;
  updatedAt: string;
  room: RoomDto | null;
  user: UserRefDto | null;
}

export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  title: string;
  role: Role;
  roleLabel: string;
  departmentId: string;
  accessibilityApproved: boolean;
  isDemoAccount: boolean;
  active: boolean;
  preferredFloorId: string | null;
  prefersQuietZone: boolean;
  notificationsEnabled: boolean;
  createdAt: string;
  updatedAt: string;
  department: DepartmentDto | null;
  grade: string | null;
  gradeLabel: string | null;
  isSecretary: boolean;
  deskReservationCount?: number | null;
  roomReservationCount?: number | null;
}

export interface UserImportRow {
  line: number;
  email: string;
  fullName: string | null;
  action: 'CREATE' | 'UPDATE' | 'UNCHANGED' | 'ERROR';
  errors: string[];
  changes: string[];
}

export interface UserImportResult {
  applied: boolean;
  total: number;
  creates: number;
  updates: number;
  unchanged: number;
  errors: number;
  newDepartments: string[];
  fileError: string | null;
  rows: UserImportRow[];
}

export interface RuleDto {
  id: string;
  key: string;
  name: string;
  description: string;
  category: string;
  enabled: boolean;
  editableLimit: boolean;
  intValue: number | null;
  secondValue: number | null;
  unit: string | null;
  minValue: number | null;
  maxValue: number | null;
  systemRule: boolean;
  order: number;
  updatedAt: string;
  updatedById: string | null;
}

export interface SettingDto {
  id: string;
  key: string;
  value: string;
  label: string;
  description: string;
  type: string;
  group: string;
  order: number;
  updatedAt: string;
}

export interface ClosureDto {
  id: string;
  scope: string;
  scopeLabel: string;
  floorId: string | null;
  deskId: string | null;
  roomId: string | null;
  startDate: string;
  endDate: string;
  reason: string;
  reasonLabel: string;
  note: string;
  active: boolean;
  createdById: string;
  createdAt: string;
  updatedAt: string;
  floor: FloorRefDto | null;
  desk: DeskDto | null;
  room: RoomDto | null;
}

export interface NotificationDto {
  id: string;
  userId: string;
  type: string;
  title: string;
  body: string;
  link: string | null;
  severity: string;
  readAt: string | null;
  createdAt: string;
}

export interface AuditLogDto {
  id: string;
  actorId: string | null;
  actorName: string;
  actorRole: string;
  action: string;
  actionLabel: string;
  entityType: string;
  entityId: string | null;
  summary: string;
  metadata: string | null;
  createdAt: string;
}

export interface Paged<T> {
  items: T[];
  total: number;
  take: number;
  skip: number;
}

// --- Kural motoru ---

export interface RuleViolation {
  ruleKey: string;
  code: string;
  title: string;
  message: string;
  severity: string;
  hint: string | null;
}

export interface FairUseSummary {
  weeklyLimit: number;
  weeklyUsed: number;
  weeklyRemaining: number;
  advanceDays: number;
  popularWindowSize: number;
  popularMaxUsage: number;
  popularUsed: number;
  popularRestricted: boolean;
}

export interface TimeRange {
  startMinutes: number;
  endMinutes: number;
}

// --- Kat planı (availability.service) ---

export interface FeatureView {
  key: string;
  name: string;
  icon: string;
}

export interface DeskView {
  id: string;
  code: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  zoneId: string;
  zoneName: string;
  zoneQuiet: boolean;
  zoneWindowSide: boolean;
  floorId: string;
  floorNumber: number;
  popular: boolean;
  accessibleOnly: boolean;
  active: boolean;
  notes: string | null;
  features: FeatureView[];
  state: string;
  selectable: boolean;
  occupant: { fullName: string; departmentName: string } | null;
  violations: RuleViolation[];
  closureNote: string | null;
}

export interface ZoneView {
  id: string;
  code: string;
  name: string;
  kind: string;
  x: number;
  y: number;
  width: number;
  height: number;
  windowSide: boolean;
  quiet: boolean;
  reservable: boolean;
}

export interface RoomView {
  id: string;
  code: string;
  name: string;
  capacity: number;
  x: number;
  y: number;
  width: number;
  height: number;
  active: boolean;
  features: FeatureView[];
  closed: boolean;
}

export interface FloorPlanFloorInfo {
  id: string;
  number: number;
  name: string;
  description: string;
  reservable: boolean;
  planWidth: number;
  planHeight: number;
  buildingId: string;
  buildingName: string;
  buildingCode: string;
  buildingActive: boolean;
}

export interface FloorPlanSummary {
  total: number;
  available: number;
  reserved: number;
  closed: number;
  restricted: number;
  occupancyRate: number;
}

export interface FloorPlanView {
  floor: FloorPlanFloorInfo;
  zones: ZoneView[];
  elements: PlanElement[];
  desks: DeskView[];
  rooms: RoomView[];
  summary: FloorPlanSummary;
}

export interface FloorSummary {
  id: string;
  number: number;
  name: string;
  description: string;
  reservable: boolean;
  buildingId: string;
  buildingName: string;
  buildingCode: string;
  totalDesks: number;
  reservedDesks: number;
  availableDesks: number;
  roomCount: number;
  occupancyRate: number;
}

// --- Çalışan ana sayfa / kişisel özet ---

export interface HomeResponse {
  today: string;
  todayDesk: DeskReservationDto | null;
  nextDesk: DeskReservationDto | null;
  todayRooms: RoomReservationDto[];
  nextRoom: RoomReservationDto | null;
  fairUse: FairUseSummary;
  announcement: string | null;
  advanceDays: number;
  floors: FloorSummary[];
}

export interface PersonalSummary {
  totals: {
    deskReservations: number;
    roomReservations: number;
    cancelled: number;
    noShow: number;
    completed: number;
    last30Days: number;
    noShowRate: number;
    popularShareRate: number;
  };
  weeklyTrend: Array<{ label: string; weekStart: string; days: number }>;
  weekdayDistribution: Array<{ label: string; count: number }>;
  floorPreference: Array<{ name: string; count: number }>;
  favoriteDesks: Array<{ code: string; zoneName: string; count: number; popular: boolean }>;
  fairUse: FairUseSummary;
  upcomingCount: number;
}

export interface MyReservations {
  upcomingDesks: DeskReservationDto[];
  pastDesks: DeskReservationDto[];
  upcomingRooms: RoomReservationDto[];
  pastRooms: RoomReservationDto[];
}

export interface NotificationsResponse {
  items: NotificationDto[];
  unread: number;
  channels: Array<{ key: string; enabled: boolean }>;
}

// --- Toplantı odası ---

export interface RoomBusySlot {
  id: string;
  title: string;
  startMinutes: number;
  endMinutes: number;
  /** Başkasının toplantısında boş gelir; başlık "Dolu" olur. */
  ownerName: string | null;
  attendeeCount: number;
}

export interface RoomAvailabilityView {
  id: string;
  code: string;
  name: string;
  capacity: number;
  floorId: string;
  floorName: string;
  floorNumber: number;
  active: boolean;
  closed: boolean;
  closureNote: string | null;
  notes: string | null;
  features: FeatureView[];
  busySlots: RoomBusySlot[];
  freeSlots: TimeRange[];
  availableForRequest: boolean | null;
  usageRate: number;
  accessReason?: string | null;
  minAttendees: number;
}

export interface RoomAvailabilityResult {
  rooms: RoomAvailabilityView[];
  workStartMinutes: number;
  workEndMinutes: number;
  slotMinutes: number;
}

export interface RoomSuggestion {
  alternativeSlots?: TimeRange[];
  alternativeRooms?: Array<{ id: string; code: string; name: string; capacity: number; floorName: string }>;
}

// --- Yönetici analitiği ---

export interface AnalyticsResult {
  filters: {
    fromDayKey: string;
    toDayKey: string;
    floorId: string | null;
    departmentId: string | null;
    resourceType: string;
    dayCount: number;
    workdayCount: number;
  };
  kpis: {
    todayReservations: number;
    todayOccupancyRate: number;
    weeklyAverageOccupancy: number;
    noShowRate: number;
    roomUsageRate: number;
    cancellationRate: number;
    totalDeskReservations: number;
    totalRoomReservations: number;
    activeDeskCount: number;
    totalEmployees: number;
    activeEmployees: number;
    inOfficeToday: number;
    availableDesksToday: number;
    totalUsableDesks: number;
    unmetRequests: number;
    unmetRequestDays: number;
    noShowLostSeats: number;
    peakDay: { dayKey: string; label: string; weekday: string; occupancyRate: number } | null;
  };
  floorPressure: Array<{
    floorId: string;
    floorNumber: number;
    name: string;
    capacity: number;
    peakOccupancy: number;
    averageOccupancy: number;
    saturatedDays: number;
  }>;
  occupancyTrend: Array<{ dayKey: string; label: string; weekday: string; reservations: number; occupancyRate: number }>;
  floorComparison: Array<{ floorId: string; floorNumber: number; name: string; reservations: number; capacity: number; occupancyRate: number }>;
  hourlyDensity: Array<{ hour: string; meetings: number; arrivals: number }>;
  departmentDistribution: Array<{
    departmentId: string;
    name: string;
    color: string;
    reservations: number;
    employeeCount: number;
    averageOfficeDays: number;
    attendanceRate: number;
  }>;
  deskHeatmap: Array<{
    deskId: string;
    code: string;
    floorNumber: number;
    zoneName: string;
    popular: boolean;
    reservations: number;
    usageRate: number;
  }>;
  roomUsage: Array<{
    roomId: string;
    code: string;
    name: string;
    floorNumber: number;
    capacity: number;
    reservations: number;
    bookedMinutes: number;
    usageRate: number;
    averageAttendees: number;
  }>;
  bookedVsUsed: Array<{ dayKey: string; label: string; booked: number; used: number; noShow: number }>;
  fairness: {
    distribution: Array<{ bucket: string; employees: number }>;
    popularShare: Array<{ bucket: string; employees: number }>;
    giniIndex: number;
    topDeskShare: number;
    activeEmployees: number;
    averageReservationsPerEmployee: number;
  };
  statusBreakdown: Array<{ status: string; count: number }>;
}

// --- Admin ---

export interface FloorCapacityStats {
  floorId: string;
  floorNumber: number;
  floorName: string;
  buildingId: string;
  buildingName: string;
  buildingActive: boolean;
  reservable: boolean;
  totalDesks: number;
  placedDesks: number;
  unplacedDesks: number;
  activeDesks: number;
  closedDesks: number;
  totalRooms: number;
  placedRooms: number;
  unplacedRooms: number;
}

export interface UserStats {
  total: number;
  active: number;
  passive: number;
  employees: number;
  managers: number;
  admins: number;
  accessibility: number;
  withoutGrade: number;
  secretaries: number;
}

// --- Kroki editörü (floor-plan.service) ---

export interface SnapshotFloor {
  id: string;
  name: string;
  number: number;
  planWidth: number;
  planHeight: number;
  reservable: boolean;
}

export interface SnapshotDesk {
  id: string;
  code: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  zoneId: string;
  zoneName: string;
  floorName: string;
  active: boolean;
  popular: boolean;
  accessibleOnly: boolean;
  reservationCount: number;
}

export interface SnapshotRoom {
  id: string;
  code: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  capacity: number;
  active: boolean;
  reservationCount: number;
}

export interface SnapshotZone {
  id: string;
  name: string;
  reservable: boolean;
}

export interface UnplacedDesk {
  id: string;
  code: string;
  width: number;
  height: number;
  zoneId: string;
  zoneName: string;
  active: boolean;
  popular: boolean;
  accessibleOnly: boolean;
}

export interface UnplacedRoom {
  id: string;
  code: string;
  name: string;
  width: number;
  height: number;
  capacity: number;
  active: boolean;
}

export interface FloorPlanSnapshot {
  floor: SnapshotFloor;
  elements: PlanElement[];
  desks: SnapshotDesk[];
  rooms: SnapshotRoom[];
  zones: SnapshotZone[];
  unplacedDesks: UnplacedDesk[];
  unplacedRooms: UnplacedRoom[];
}

export interface SavePlanResult {
  elements: PlanElement[];
  createdCount: number;
  updatedCount: number;
  deletedCount: number;
  movedDeskCount: number;
  movedRoomCount: number;
}

export interface AutoPlaceResult {
  placedDesks: number;
  remainingDesks: number;
  placedRooms: number;
  remainingRooms: number;
}
