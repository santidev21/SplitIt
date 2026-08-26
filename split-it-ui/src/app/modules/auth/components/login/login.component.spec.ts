import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoginComponent } from './login.component';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    authServiceSpy = jasmine.createSpyObj('AuthService', ['login']);
    authServiceSpy.login.and.returnValue(of({ token: 'fake', name: 'Test', id: 1 }));
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [LoginComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authServiceSpy },
        { provide: Router, useValue: routerSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
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
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('login should handle error', () => {
    authServiceSpy.login.and.returnValue(throwError(() => new Error('Invalid')));
    component.loginForm.patchValue({ email: 'test@test.com', password: 'wrong' });
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    spyOn(console, 'error');
    component.login(event);
    expect(authServiceSpy.login).toHaveBeenCalled();
    expect(component.isLoading).toBeFalse();
  });
});
