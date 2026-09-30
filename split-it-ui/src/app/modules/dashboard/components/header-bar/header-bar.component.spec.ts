import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import Swal from 'sweetalert2';
import { HeaderBarComponent } from './header-bar.component';
import { AuthService } from '../../../auth/services/auth.service';
import { AccountService } from '../../services/account.service';
import { LanguageService } from '../../../../shared/services/language.service';

describe('HeaderBarComponent', () => {
  let authService: any;
  let accountService: any;
  let translate: any;
  let languageService: any;
  let router: any;

  function build() {
    authService = { isAdminRole: () => false, logout: jasmine.createSpy('logout') };
    accountService = {
      exportData: jasmine.createSpy('exportData').and.returnValue(of({ id: 1 })),
      deleteAccount: jasmine.createSpy('deleteAccount').and.returnValue(of({ message: 'ok' })),
    };
    translate = { instant: (key: string) => key };
    languageService = { currentLang: 'en', toggleLanguage: jasmine.createSpy('toggleLanguage') };
    router = { navigate: jasmine.createSpy('navigate') };
    // The component uses inject(), so it must be created inside an injection
    // context with the fakes provided through DI.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authService },
        { provide: AccountService, useValue: accountService },
        { provide: TranslateService, useValue: translate },
        { provide: LanguageService, useValue: languageService },
      ],
    });
    return TestBed.runInInjectionContext(() => new HeaderBarComponent());
  }

  it('creates with admin flag from AuthService', () => {
    const component = build();
    expect(component).toBeTruthy();
    expect(component.isAdmin).toBeFalse();
  });

  it('toggleAccount toggles the dropdown', () => {
    const component = build();
    component.toggleAccount();
    expect(component.accountOpen).toBeTrue();
    component.toggleAccount();
    expect(component.accountOpen).toBeFalse();
  });

  it('toggleLanguage delegates to LanguageService', () => {
    const component = build();
    component.toggleLanguage();
    expect(languageService.toggleLanguage).toHaveBeenCalled();
  });

  it('toggleTheme flips isDark and persists the preference', () => {
    const component = build();
    component.isDark = false;
    component.toggleTheme();
    expect(component.isDark).toBeTrue();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    component.toggleTheme();
    expect(component.isDark).toBeFalse();
  });

  it('logout delegates to AuthService', () => {
    const component = build();
    component.logout();
    expect(authService.logout).toHaveBeenCalled();
  });

  it('exportData calls the service', () => {
    const component = build();
    component.exportData();
    expect(accountService.exportData).toHaveBeenCalled();
    expect(component.accountOpen).toBeFalse();
  });

  it('deleteAccount does nothing when the user cancels', async () => {
    const component = build();
    spyOn(Swal, 'fire').and.returnValue(Promise.resolve({ isConfirmed: false } as any));
    await component.deleteAccount();
    expect(accountService.deleteAccount).not.toHaveBeenCalled();
  });

  it('deleteAccount deletes and logs out when confirmed with a password', async () => {
    const component = build();
    spyOn(Swal, 'fire').and.returnValue(
      Promise.resolve({ isConfirmed: true, value: 'Pass123!' } as any),
    );
    await component.deleteAccount();
    expect(accountService.deleteAccount).toHaveBeenCalledWith('Pass123!');
    expect(authService.logout).toHaveBeenCalled();
  });
});
