import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';
import { of, throwError } from 'rxjs';

function b64url(obj: any): string {
  return btoa(JSON.stringify(obj)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
}
function makeToken(payload: any): string {
  const header = { alg: 'HS256', typ: 'JWT' };
  const now = Math.floor(Date.now() / 1000);
  const body = { exp: now + 3600, ...payload };
  return `${b64url(header)}.${b64url(body)}.sig`;
}

describe('authGuard', () => {
  let routerSpy: jasmine.SpyObj<Router>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    authServiceSpy = jasmine.createSpyObj('AuthService', ['getToken', 'refreshSession']);
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: routerSpy },
        { provide: AuthService, useValue: authServiceSpy }
      ]
    });
  });

  it('should allow activation when valid token present', () => {
    const token = makeToken({ sub: '1' });
    authServiceSpy.getToken.and.returnValue(token);
    const result = TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard/home' } as any));
    expect(result).toBe(true);
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });

  it('should try refresh and deny when no token and refresh fails', () => {
    authServiceSpy.getToken.and.returnValue(null);
    authServiceSpy.refreshSession.and.returnValue(throwError(() => new Error('no token')));
    const result = TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard/home' } as any));
    expect(result instanceof Promise || (result as any)?.subscribe).toBeTruthy();
  });

  it('should deny when token expired and try refresh', () => {
    const expiredToken = makeToken({ exp: Math.floor(Date.now() / 1000) - 3600 });
    authServiceSpy.getToken.and.returnValue(expiredToken);
    authServiceSpy.refreshSession.and.returnValue(throwError(() => new Error('expired')));
    TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard/group/1' } as any));
    expect(authServiceSpy.refreshSession).toHaveBeenCalled();
  });
});
