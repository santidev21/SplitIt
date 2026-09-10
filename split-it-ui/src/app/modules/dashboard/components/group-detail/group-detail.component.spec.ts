import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GroupDetailComponent } from './group-detail.component';
import { ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { of } from 'rxjs';
import { ExpenseService } from '../../services/expense.service';
import { GroupService } from '../../services/group.service';
import { NotificationService } from '../../../../shared/services/notification.service';

describe('GroupDetailComponent debt state', () => {
  let component: GroupDetailComponent;
  let fixture: ComponentFixture<GroupDetailComponent>;
  let expenseSpy: jasmine.SpyObj<ExpenseService>;

  function configure(debtsOwedByUser: any[], debtsOwedToUser: any[]) {
    TestBed.resetTestingModule();
    expenseSpy = jasmine.createSpyObj('ExpenseService', [
      'getGroupExpenses',
      'getFullDebtSummary',
      'settleExpenseWithUser',
    ]);
    expenseSpy.getGroupExpenses.and.returnValue(of([]));
    expenseSpy.getFullDebtSummary.and.returnValue(of({ debtsOwedByUser, debtsOwedToUser }));
    const groupSpy = jasmine.createSpyObj('GroupService', [
      'getGroupDetails',
      'getUserGroupRole',
      'deleteGroup',
    ]);
    groupSpy.getGroupDetails.and.returnValue(of({ name: 'G', description: 'D' }));
    groupSpy.getUserGroupRole.and.returnValue(of({ role: 'creator' }));

    TestBed.configureTestingModule({
      imports: [GroupDetailComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '1' } } } },
        { provide: MatDialog, useValue: jasmine.createSpyObj('MatDialog', ['open']) },
        { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) },
        {
          provide: NotificationService,
          useValue: jasmine.createSpyObj('NotificationService', ['toast', 'confirm', 'success']),
        },
        { provide: ExpenseService, useValue: expenseSpy },
        { provide: GroupService, useValue: groupSpy },
        provideTranslateService({ lang: 'en', fallbackLang: 'en' }),
        provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(GroupDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('should create', () => {
    configure([], []);
    expect(component).toBeTruthy();
  });

  it('settled when nothing is owed either way', () => {
    configure([], []);
    expect(component.debtState).toBe('settled');
    expect(component.debtAmount).toBe(0);
  });

  it('owe when the user owes more than they are owed', () => {
    configure(
      [{ creditorUserId: 2, creditorUserName: 'Bob', totalAmountOwed: 50.4 }],
      [{ debtorUserId: 3, debtorUserName: 'Charlie', totalAmountOwed: 20 }]
    );
    expect(component.debtState).toBe('owe');
    expect(component.debtAmount).toBe(30);
  });

  it('owed when the user is owed more than they owe', () => {
    configure(
      [],
      [{ debtorUserId: 3, debtorUserName: 'Charlie', totalAmountOwed: 42.6 }]
    );
    expect(component.debtState).toBe('owed');
    expect(component.debtAmount).toBe(43);
  });
});
