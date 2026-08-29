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

describe('AddExpenseDialogComponent', () => {
  let component: AddExpenseDialogComponent;
  let fixture: ComponentFixture<AddExpenseDialogComponent>;

  beforeEach(async () => {
    const groupSpy = jasmine.createSpyObj('GroupService', ['getGroupMembers']);
    groupSpy.getGroupMembers.and.returnValue(of([{ id: 1, name: 'You' }, { id: 2, name: 'Bob' }]));
    const expenseSpy = jasmine.createSpyObj('ExpenseService', ['addExpense']);
    expenseSpy.addExpense.and.returnValue(of({ id: 1 }));

    await TestBed.configureTestingModule({
      imports: [AddExpenseDialogComponent, NoopAnimationsModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MatDialog, useValue: jasmine.createSpyObj('MatDialog', ['open']) },
        { provide: MatDialogRef, useValue: jasmine.createSpyObj('MatDialogRef', ['close']) },
        { provide: MAT_DIALOG_DATA, useValue: { groupId: 1 } },
        { provide: GroupService, useValue: groupSpy },
        { provide: ExpenseService, useValue: expenseSpy },
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
});
