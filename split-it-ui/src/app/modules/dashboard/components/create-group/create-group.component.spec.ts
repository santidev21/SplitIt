import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CreateGroupComponent } from './create-group.component';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { CurrencyService } from '../../services/currency.service';
import { UsersService } from '../../services/users.service';
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
    const usersSpy = jasmine.createSpyObj('UsersService', ['getUsers']);
    usersSpy.getUsers.and.returnValue(of([{ id: 2, name: 'Bob', email: 'bob@test.com' }]));
    const groupSpy = jasmine.createSpyObj('GroupService', ['createGroup']);
    groupSpy.createGroup.and.returnValue(of({ groupId: 99 }));

    await TestBed.configureTestingModule({
      imports: [CreateGroupComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: CurrencyService, useValue: currencySpy },
        { provide: UsersService, useValue: usersSpy },
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
      members: [2],
      allowToDeleteExpenses: false
    });
    expect(component.createGroupForm.valid).toBeTrue();
  });

  it('should enforce name required', () => {
    component.createGroupForm.patchValue({ name: '', description: 'Desc', currencyId: 1, members: [2] });
    expect(component.createGroupForm.get('name')?.hasError('required')).toBeTrue();
  });

  it('close should dismiss dialog', () => {
    component.close();
    expect(dialogRefSpy.close).toHaveBeenCalled();
  });
});
