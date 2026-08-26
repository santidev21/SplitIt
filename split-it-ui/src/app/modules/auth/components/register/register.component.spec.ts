import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RegisterComponent } from './register.component';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    authServiceSpy = jasmine.createSpyObj('AuthService', ['register']);
    authServiceSpy.register.and.returnValue(of({ token: 'fake', name: 'Test', id: 1 }));
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [RegisterComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authServiceSpy },
        { provide: Router, useValue: routerSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('register should not call service when form is invalid', () => {
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    component.register(event);
    expect(authServiceSpy.register).not.toHaveBeenCalled();
  });

  it('register should call service and navigate when form is valid', () => {
    component.registerForm.patchValue({ userName: 'Test', email: 'test@test.com', password: 'password123' });
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    component.register(event);
    expect(authServiceSpy.register).toHaveBeenCalledWith('Test', 'test@test.com', 'password123');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('register should handle error', () => {
    authServiceSpy.register.and.returnValue(throwError(() => new Error('Duplicate')));
    component.registerForm.patchValue({ userName: 'Test', email: 'test@test.com', password: 'pass' });
    const event = new Event('submit');
    spyOn(event, 'preventDefault');
    spyOn(console, 'error');
    component.register(event);
    expect(component.isLoading).toBeFalse();
  });
});
