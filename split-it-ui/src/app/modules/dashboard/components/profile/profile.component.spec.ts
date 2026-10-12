import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import Swal from 'sweetalert2';
import { ProfileComponent } from './profile.component';
import { AuthService } from '../../../auth/services/auth.service';
import { AccountService } from '../../services/account.service';
import { NotificationService } from '../../../../shared/services/notification.service';

describe('ProfileComponent', () => {
  let authService: any;
  let accountService: any;
  let notifications: any;
  let translate: any;

  function build() {
    authService = { getUserName: () => 'Ana', logout: jasmine.createSpy('logout') };
    accountService = {
      exportData: jasmine.createSpy('exportData').and.returnValue(of({ id: 1 })),
      deleteAccount: jasmine.createSpy('deleteAccount').and.returnValue(of({ message: 'ok' })),
    };
    notifications = {
      toast: jasmine.createSpy('toast'),
      success: jasmine.createSpy('success'),
      error: jasmine.createSpy('error'),
    };
    translate = { instant: (key: string) => key };
    // The component uses inject(), so it must be created inside an injection
    // context with the fakes provided through DI.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: AccountService, useValue: accountService },
        { provide: NotificationService, useValue: notifications },
        { provide: TranslateService, useValue: translate },
      ],
    });
    return TestBed.runInInjectionContext(() => new ProfileComponent());
  }

  it('loads the user name on init', () => {
    const component = build();
    component.ngOnInit();
    expect(component.userName).toBe('Ana');
  });

  it('exportData downloads the data through the service', () => {
    const component = build();
    component.exportData();
    expect(accountService.exportData).toHaveBeenCalled();
    expect(notifications.toast).toHaveBeenCalled();
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
