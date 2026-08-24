import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';

function b64url(obj: any): string {
  return btoa(JSON.stringify(obj)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
}
function makeToken(payload: any): string {
  const header = { alg: 'HS256', typ: 'JWT' };
  const now = Math.floor(Date.now() / 1000);
  const body = { exp: now + 3600, ...payload };
  return `${b64url(header)}.${b64url(body)}.sig`;
}
function expiredToken(): string {
  return makeToken({ exp: Math.floor(Date.now() / 1000) - 3600 });
}

describe('authGuard', () => {
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(() => {
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({
      providers: [{ provide: Router, useValue: routerSpy }]
    });
    localStorage.clear();
  });

  afterEach(() => localStorage.clear());

  it('should allow activation when valid token present', () => {
    localStorage.setItem('token', makeToken({ sub: '1' }));
    localStorage.setItem('userName', 'Alice');
    localStorage.setItem('userId', '1');
    const result = TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard/home' } as any));
    expect(result).toBe(true);
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });

  it('should deny and redirect when no token', () => {
    const result = TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard/home' } as any));
    expect(result).toBe(false);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login'], jasmine.objectContaining({ queryParams: { returnUrl: '/dashboard/home' } }));
  });

  it('should deny when token expired and clear storage', () => {
    localStorage.setItem('token', expiredToken());
    localStorage.setItem('userName', 'Bob');
    localStorage.setItem('userId', '1');
    const result = TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard/group/1' } as any));
    expect(result).toBe(false);
    expect(localStorage.getItem('token')).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalled();
  });

  it('should deny when token malformed', () => {
    localStorage.setItem('token', 'not-a-jwt');
    const result = TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard/home' } as any));
    expect(result).toBe(false);
  });
});
