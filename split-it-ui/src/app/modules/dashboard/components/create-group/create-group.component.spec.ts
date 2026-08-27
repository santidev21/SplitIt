import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CreateGroupComponent } from './create-group.component';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { CurrencyService } from '../../services/currency.service';
import { FriendService } from '../../services/friend.service';
import { GroupService } from '../../services/group.service';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('CreateGroupComponent', () => {
  let component: CreateGroupComponent;
  let fixture: ComponentFixture<CreateGroupComponent>;
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<CreateGroupComponent>>;

  beforeEach(async () => {
    dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);
    const currencySpy = jasmine.createSpyObj('CurrencyService', ['getCurrencies']);
    currencySpy.getCurrencies.and.returnValue(of([{ id: 1, name: 'USD', symbol: '$' }]));
    const friendSpy = jasmine.createSpyObj('FriendService', ['getFriends', 'search', 'sendRequest']);
    friendSpy.getFriends.and.returnValue(of([
      { id: 2, name: 'Bob', email: 'bob@test.com' },
      { id: 3, name: 'Alice', email: 'alice@test.com' }
    ]));
    const groupSpy = jasmine.createSpyObj('GroupService', ['createGroup']);
    groupSpy.createGroup.and.returnValue(of({ groupId: 99 }));

    await TestBed.configureTestingModule({
      imports: [CreateGroupComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: CurrencyService, useValue: currencySpy },
        { provide: FriendService, useValue: friendSpy },
        { provide: GroupService, useValue: groupSpy },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CreateGroupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('should be invalid when empty', () => {
    expect(component.createGroupForm.valid).toBeFalse();
  });

  it('should be valid when required fields filled', () => {
    component.createGroupForm.patchValue({
      name: 'Trip',
      description: 'Desc',
      currencyId: 1,
      allowToDeleteExpenses: false
    });
    expect(component.createGroupForm.valid).toBeTrue();
  });

  it('should load friends on init', () => {
    expect(component.friends.length).toBe(2);
  });

  it('should toggle friend selection', () => {
    component.toggleFriend(2);
    expect(component.selectedFriendIds.has(2)).toBeTrue();
    component.toggleFriend(2);
    expect(component.selectedFriendIds.has(2)).toBeFalse();
  });

  it('should submit selected friends as members', () => {
    const groupSpy = TestBed.inject(GroupService) as jasmine.SpyObj<GroupService>;
    const router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    component.createGroupForm.patchValue({
      name: 'Trip',
      description: 'Desc',
      currencyId: 1,
      allowToDeleteExpenses: false
    });
    component.toggleFriend(2);
    component.toggleFriend(3);
    component.onSubmit();

    expect(groupSpy.createGroup).toHaveBeenCalledWith(jasmine.objectContaining({
      members: [2, 3]
    }));
    expect(dialogRefSpy.close).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard/group', 99]);
  });

  it('close should dismiss dialog', () => {
    component.close();
    expect(dialogRefSpy.close).toHaveBeenCalled();
  });

  it('onSubmit should not call service when form is invalid', () => {
    const groupSpy = TestBed.inject(GroupService) as jasmine.SpyObj<GroupService>;
    component.onSubmit();
    expect(groupSpy.createGroup).not.toHaveBeenCalled();
  });
});
