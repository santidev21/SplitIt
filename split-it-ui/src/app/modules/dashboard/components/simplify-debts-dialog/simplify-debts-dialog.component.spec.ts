import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideTranslateService } from '@ngx-translate/core';
import { SimplifyDebtsDialogComponent } from './simplify-debts-dialog.component';

describe('SimplifyDebtsDialogComponent', () => {
  let fixture: ComponentFixture<SimplifyDebtsDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<SimplifyDebtsDialogComponent, boolean>>;

  beforeEach(async () => {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    await TestBed.configureTestingModule({
      imports: [SimplifyDebtsDialogComponent, NoopAnimationsModule],
      providers: [
        provideTranslateService({ lang: 'en', fallbackLang: 'en' }),
        { provide: MatDialogRef, useValue: dialogRef },
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            currencyDecimals: 0,
            preview: {
              currentTransferCount: 3,
              simplifiedTransferCount: 2,
              canSimplify: true,
              applied: false,
              transfers: [
                {
                  fromUserId: 1,
                  fromUserName: 'Ana',
                  toUserId: 2,
                  toUserName: 'Luis',
                  amount: 1500,
                },
                {
                  fromUserId: 3,
                  fromUserName: 'Eva',
                  toUserId: 2,
                  toUserName: 'Luis',
                  amount: 500,
                },
              ],
            },
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(SimplifyDebtsDialogComponent);
    fixture.detectChanges();
  });

  it('lists one row per transfer', () => {
    expect(fixture.nativeElement.querySelectorAll('.simplify-transfers li').length).toBe(2);
  });

  it('formats amounts with the currency precision', () => {
    expect(fixture.componentInstance.formatMoney(1500)).toBe('1500');
  });

  it('closes with true on confirm and false on cancel', () => {
    fixture.componentInstance.confirm();
    expect(dialogRef.close).toHaveBeenCalledWith(true);
    fixture.componentInstance.cancel();
    expect(dialogRef.close).toHaveBeenCalledWith(false);
  });
});
