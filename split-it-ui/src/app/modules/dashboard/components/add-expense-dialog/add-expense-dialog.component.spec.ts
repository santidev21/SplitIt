import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AddExpenseDialogComponent } from './add-expense-dialog.component';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialog } from '@angular/material/dialog';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { of } from 'rxjs';
import { GroupService } from '../../services/group.service';
import { ExpenseService } from '../../services/expense.service';
import { CurrencyService } from '../../services/currency.service';
import { AuthService } from '../../../auth/services/auth.service';

describe('AddExpenseDialogComponent', () => {
  let component: AddExpenseDialogComponent;
  let fixture: ComponentFixture<AddExpenseDialogComponent>;
  let dialogSpy: jasmine.SpyObj<MatDialog>;
  let groupSpy: jasmine.SpyObj<GroupService>;

  beforeEach(async () => {
    groupSpy = jasmine.createSpyObj('GroupService', ['getGroupMembers', 'getGroupDetails']);
    groupSpy.getGroupMembers.and.returnValue(of([{ id: 1, name: 'Alice' }, { id: 2, name: 'Bob' }]));
    groupSpy.getGroupDetails.and.returnValue(of({ name: 'G', description: 'D', currencyId: 1 }));
    const expenseSpy = jasmine.createSpyObj('ExpenseService', ['addExpense']);
    expenseSpy.addExpense.and.returnValue(of({ id: 1 }));
    const currencySpy = jasmine.createSpyObj('CurrencyService', ['getCurrencies']);
    currencySpy.getCurrencies.and.returnValue(of([
      { id: 1, name: 'Dólar', symbol: 'USD', decimalPlaces: 2 },
      { id: 2, name: 'Peso Colombiano', symbol: 'COP', decimalPlaces: 0 }
    ]));
    dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [AddExpenseDialogComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MatDialog, useValue: dialogSpy },
        { provide: MatDialogRef, useValue: jasmine.createSpyObj('MatDialogRef', ['close']) },
        { provide: MAT_DIALOG_DATA, useValue: { groupId: 1 } },
        { provide: GroupService, useValue: groupSpy },
        { provide: ExpenseService, useValue: expenseSpy },
        { provide: CurrencyService, useValue: currencySpy },
        { provide: AuthService, useValue: { getCurrentUserId: () => 1 } },
        provideTranslateService({ lang: 'en', fallbackLang: 'en' }),
        provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AddExpenseDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('form should be invalid when empty', () => {
    expect(component.expenseForm.valid).toBeFalse();
  });

  it('form should be valid with title, amount, paidBy', () => {
    component.expenseForm.patchValue({ title: 'Dinner', amount: 100, paidById: 1 });
    expect(component.expenseForm.valid).toBeTrue();
  });

  it('should load members on init', (done) => {
    setTimeout(() => {
      expect(component.members.length).toBe(2);
      done();
    }, 100);
  });

  it('passes the currency decimal places to the split dialog', () => {
    component.expenseForm.patchValue({ amount: 100 });
    component.openSplitMethod();
    const args = dialogSpy.open.calls.argsFor(0);
    const data = args.length > 1 ? (args[1] as any).data : undefined;
    expect(data?.decimalPlaces).toBe(2);
  });

  describe('currency decimal validation', () => {
    it('resolves USD cents (2 decimals) from the group currency', (done) => {
      setTimeout(() => {
        expect(component.decimalPlaces).toBe(2);
        component.expenseForm.patchValue({ title: 'T', amount: 100.25, paidById: 1 });
        expect(component.expenseForm.valid).toBeTrue();
        done();
      }, 50);
    });

    it('rejects expenses with more decimals than the currency allows (COP whole units)', (done) => {
      // Change the resolved currency to COP (decimalPlaces 0).
      groupSpy.getGroupDetails.and.returnValue(of({ name: 'G', description: 'D', currencyId: 2 }));
      component.ngOnInit();
      setTimeout(() => {
        expect(component.decimalPlaces).toBe(0);
        component.expenseForm.patchValue({ title: 'T', amount: 100.5, paidById: 1 });
        expect(component.expenseForm.valid).toBeFalse();
        component.expenseForm.patchValue({ amount: 100 });
        expect(component.expenseForm.valid).toBeTrue();
        done();
      }, 50);
    });
  });
});
