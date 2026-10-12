import { TestBed } from '@angular/core/testing';
import { HeaderBarComponent } from './header-bar.component';
import { AuthService } from '../../../auth/services/auth.service';
import { LanguageService } from '../../../../shared/services/language.service';

describe('HeaderBarComponent', () => {
  let authService: any;
  let languageService: any;

  function build() {
    authService = { isAdminRole: () => false, logout: jasmine.createSpy('logout') };
    languageService = { currentLang: 'en', toggleLanguage: jasmine.createSpy('toggleLanguage') };
    // The component uses inject(), so it must be created inside an injection
    // context with the fakes provided through DI.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
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
});
