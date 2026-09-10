import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoginComponent } from './login.component';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { environment } from '../../../../../environments/environment';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(async () => {
    // Provide a minimal mock of Google Identity Services so initGoogleButton()
    // doesn't enter the retry loop (which causes ExpressionChangedAfterItHasBeenCheckedError).
    (window as any).google = {
      accounts: {
        id: {
          initialize: jasmine.createSpy('initialize'),
          renderButton: jasmine.createSpy('renderButton'),
        },
      },
    };
    // Provide a fake googleClientId so initGoogleButton() doesn't set googleError
    // (which causes ExpressionChangedAfterItHasBeenCheckedError in tests).
    (environment as any).googleClientId = 'test-client-id';

    authServiceSpy = jasmine.createSpyObj('AuthService', ['login', 'isAuthenticated', 'loginWithGoogle']);
    authServiceSpy.isAuthenticated.and.returnValue(false);
    authServiceSpy.login.and.returnValue(of({ token: 'fake', userName: 'Test', userId: 1 }));
    authServiceSpy.loginWithGoogle.and.returnValue(of({ token: 'fake', userName: 'Test', userId: 1 }));

    await TestBed.configureTestingModule({
      imports: [LoginComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authServiceSpy },
        provideTranslateService({ lang: 'en', fallbackLang: 'en' }),
        provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    fixture.detectChanges();
  });

  afterEach(() => {
    delete (window as any).google;
    delete (environment as any).googleClientId;
  });

  it('should create', () => expect(component).toBeTruthy());

  it('login should not call service when form is invalid', () => {
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    component.login(event);
    expect(authServiceSpy.login).not.toHaveBeenCalled();
  });

  it('login should call service and navigate when form is valid', () => {
    component.loginForm.patchValue({ email: 'test@test.com', password: 'password123' });
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    component.login(event);
    expect(authServiceSpy.login).toHaveBeenCalledWith('test@test.com', 'password123');
    expect(component.isLoading).toBeFalse();
  });

  it('login should handle error', () => {
    authServiceSpy.login.and.returnValue(throwError(() => new Error('Invalid')));
    component.loginForm.patchValue({ email: 'test@test.com', password: 'wrongpass' });
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    component.login(event);
    expect(authServiceSpy.login).toHaveBeenCalled();
    expect(component.isLoading).toBeFalse();
  });

  it('login should not call service when email is malformed', () => {
    component.loginForm.patchValue({ email: 'not-an-email', password: 'password123' });
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    component.login(event);
    expect(authServiceSpy.login).not.toHaveBeenCalled();
  });

  it('google credential should call loginWithGoogle once', () => {
    component.handleGoogleCredential({ credential: 'cred-123' });
    expect(authServiceSpy.loginWithGoogle).toHaveBeenCalledTimes(1);
    expect(authServiceSpy.loginWithGoogle).toHaveBeenCalledWith('cred-123');
  });

  it('google duplicate callback while loading should be ignored', () => {
    component.isLoading = true;
    component.handleGoogleCredential({ credential: 'cred-123' });
    expect(authServiceSpy.loginWithGoogle).not.toHaveBeenCalled();
  });

  it('google callback without credential should not call service', () => {
    component.handleGoogleCredential({});
    expect(authServiceSpy.loginWithGoogle).not.toHaveBeenCalled();
    expect(component.googleError).toBeTruthy();
  });
});
