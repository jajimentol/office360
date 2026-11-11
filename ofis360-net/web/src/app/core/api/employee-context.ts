import { Injectable, inject } from '@angular/core';
import { ApiClient } from './api-client';

/** GET /api/me/context — çalışan ekranlarının ortak bağlamı. */
export interface RuleSettings {
  oneDeskPerDay: { enabled: boolean };
  advanceBookingDays: { enabled: boolean; days: number };
  weeklyDeskLimit: { enabled: boolean; limit: number };
  popularDeskStreakLimit: { enabled: boolean; limit: number };
  popularShareLimit: { enabled: boolean; windowSize: number; maxPopular: number };
  accessibleDeskRestriction: { enabled: boolean };
  noPastDate: { enabled: boolean };
  roomWorkingHours: { enabled: boolean; startMinutes: number; endMinutes: number };
  roomCapacityCheck: { enabled: boolean };
  roomMaxDurationMinutes: { enabled: boolean; minutes: number };
  workingDaysOnly: { enabled: boolean };
  roomSlotMinutes: { enabled: boolean; minutes: number };
}

export interface ContextFloor {
  id: string;
  number: number;
  name: string;
  reservable: boolean;
  buildingId: string;
  buildingName: string;
  buildingCode: string;
  hasRooms: boolean;
}

export interface EmployeeContext {
  settings: RuleSettings;
  latestBookableDayKey: string;
  blockedDayKeys: string[];
  floors: ContextFloor[];
  roomFeatures: Array<{ key: string; name: string }>;
  deskReservationCount: number;
  roomReservationCount: number;
}

@Injectable({ providedIn: 'root' })
export class EmployeeContextService {
  private readonly api = inject(ApiClient);

  load(): Promise<EmployeeContext> {
    return this.api.get<EmployeeContext>('me/context');
  }
}
