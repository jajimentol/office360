import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService, defaultRouteForRole } from './auth.service';

describe('AuthService (kaynak: server/session.ts + auth.actions.ts)', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  it('rol → varsayılan rota eşlemesi kaynakla aynı', () => {
    expect(defaultRouteForRole('ADMIN')).toBe('/admin');
    expect(defaultRouteForRole('MANAGER')).toBe('/yonetim');
    expect(defaultRouteForRole('EMPLOYEE')).toBe('/panel');
  });

  it('401 oturum yok demektir: kullanıcı null, loaded true, hata fırlatılmaz', async () => {
    const promise = service.load();
    http.expectOne('/api/auth/me').flush({ error: { code: 'UNAUTHORIZED', message: 'Oturum gerekli.' } }, { status: 401, statusText: 'Unauthorized' });
    expect(await promise).toBeNull();
    expect(service.loaded()).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('başarılı yanıt kullanıcıyı ve uygulama adını yazar; eş zamanlı çağrılar tek istek yapar', async () => {
    const first = service.load();
    const second = service.load();
    http.expectOne('/api/auth/me').flush({
      user: { id: 'u1', email: 'a@b', fullName: 'Zeynep Kaya', title: 'Admin', role: 'ADMIN', departmentId: 'd', departmentName: 'İK', accessibilityApproved: false, preferredFloorId: null, prefersQuietZone: false, notificationsEnabled: true },
      defaultRoute: '/admin',
      appName: 'Ofis360 Test',
    });
    await Promise.all([first, second]);
    http.verify();
    expect(service.user()?.fullName).toBe('Zeynep Kaya');
    expect(service.appName()).toBe('Ofis360 Test');
    expect(service.hasRole('ADMIN')).toBe(true);
    expect(service.hasRole('EMPLOYEE')).toBe(false);
  });
});
