import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GroupSettingsDialogComponent } from './group-settings-dialog.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { GroupService } from '../../services/group.service';
import { FriendService } from '../../services/friend.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { AuthService } from '../../../auth/services/auth.service';
import { of } from 'rxjs';

describe('GroupSettingsDialogComponent', () => {
  let component: GroupSettingsDialogComponent;
  let fixture: ComponentFixture<GroupSettingsDialogComponent>;

  function configure(isCreator: boolean, isAdminOrCreator: boolean) {
    TestBed.resetTestingModule();

    const groupSpy = jasmine.createSpyObj('GroupService', [
      'getGroupDetails', 'getGroupMembers', 'updateGroup', 'inviteMember',
      'updateMemberRole', 'removeMember',
    ]);
    groupSpy.getGroupDetails.and.returnValue(of({ name: 'Test', description: 'Desc' }));
    groupSpy.getGroupMembers.and.returnValue(of([]));
    groupSpy.updateGroup.and.returnValue(of({}));

    const friendSpy = jasmine.createSpyObj('FriendService', ['getFriends']);
    friendSpy.getFriends.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [GroupSettingsDialogComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: { groupId: 1, isCreator, isAdminOrCreator } },
        { provide: MatDialogRef, useValue: jasmine.createSpyObj('MatDialogRef', ['close']) },
        { provide: GroupService, useValue: groupSpy },
        { provide: FriendService, useValue: friendSpy },
        { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['toast', 'confirm', 'success']) },
        { provide: AuthService, useValue: { getCurrentUserId: () => 1 } },
        provideTranslateService({ lang: 'en', fallbackLang: 'en' }),
        provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(GroupSettingsDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('should create', () => {
    configure(true, true);
    expect(component).toBeTruthy();
  });

  it('displayName does not return raw name for current user', () => {
    configure(true, true);
    const result = component.displayName({ id: 1, name: 'Alice', role: 'creator', email: 'a@b.com' } as any);
    expect(result).not.toBe('Alice');
  });

  it('displayName returns real name for other user', () => {
    configure(true, true);
    expect(component.displayName({ id: 2, name: 'Bob', role: 'member', email: 'b@b.com' } as any)).toBe('Bob');
  });

  it('canManageMember returns false for self', () => {
    configure(true, true);
    expect(component.canManageMember({ id: 1, name: 'Alice', role: 'creator' } as any)).toBeFalse();
  });

  it('canManageMember returns false for creator', () => {
    configure(true, true);
    expect(component.canManageMember({ id: 2, name: 'Bob', role: 'creator' } as any)).toBeFalse();
  });

  it('canManageMember returns true for member when admin', () => {
    configure(true, true);
    expect(component.canManageMember({ id: 2, name: 'Bob', role: 'member' } as any)).toBeTrue();
  });

  it('inviteableFriends filters out existing members', () => {
    configure(true, true);
    component.friends = [{ id: 1, name: 'A' } as any, { id: 3, name: 'C' } as any];
    component.members = [{ id: 1, name: 'A', role: 'member', email: '' } as any];
    expect(component.inviteableFriends().length).toBe(1);
    expect(component.inviteableFriends()[0].id).toBe(3);
  });
});
