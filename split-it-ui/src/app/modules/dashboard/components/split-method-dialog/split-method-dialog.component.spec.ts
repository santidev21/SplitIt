import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SplitMethodDialogComponent } from './split-method-dialog.component';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

describe('SplitMethodDialogComponent', () => {
  let component: SplitMethodDialogComponent;
  let fixture: ComponentFixture<SplitMethodDialogComponent>;
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<SplitMethodDialogComponent>>;

  const members = [
    { id: 1, name: 'Alice' },
    { id: 2, name: 'Bob' },
    { id: 3, name: 'Charlie' }
  ];

  function createComponent(data: { members: any[]; amount: number }) {
    TestBed.resetTestingModule();
    dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);
    TestBed.configureTestingModule({
      imports: [SplitMethodDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SplitMethodDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  describe('equal split', () => {
    beforeEach(() => createComponent({ members, amount: 100 }));

    it('should create', () => expect(component).toBeTruthy());

    it('distributes 100 among 3 giving the leftover cent to the first participants', () => {
      component.selectedTabIndex = 0;
      component.confirmSplit();

      expect(dialogRefSpy.close).toHaveBeenCalled();
      const result = dialogRefSpy.close.calls.mostRecent().args[0];
      expect(result.method).toBe('equally');
      expect(result.expenseParticipant.map((p: any) => p.amountOwed)).toEqual([33.34, 33.33, 33.33]);
    });

    it('splits 100 among 2 exactly', () => {
      component.equalSplitSelection[3] = false;
      component.confirmSplit();

      const result = dialogRefSpy.close.calls.mostRecent().args[0];
      expect(result.expenseParticipant.map((p: any) => p.amountOwed)).toEqual([50, 50]);
    });

    it('blocks confirmation when no member is selected and shows the reason', () => {
      component.equalSplitSelection[1] = false;
      component.equalSplitSelection[2] = false;
      component.equalSplitSelection[3] = false;
      component.confirmSplit();

      expect(dialogRefSpy.close).not.toHaveBeenCalled();
      expect(component.validationError).toContain('at least one member');
    });
  });

  describe('equal split with small amount', () => {
    beforeEach(() => createComponent({ members, amount: 10 }));

    it('sums exactly to the total for awkward amounts', () => {
      component.confirmSplit();

      const result = dialogRefSpy.close.calls.mostRecent().args[0];
      const sum = result.expenseParticipant.reduce((s: number, p: any) => s + p.amountOwed, 0);
      expect(sum).toBe(10);
    });
  });

  describe('split by amount', () => {
    beforeEach(() => {
      createComponent({ members, amount: 100 });
      component.selectedTabIndex = 1;
    });

    it('accepts amounts that add up to the total', () => {
      component.members[0].amount = 50;
      component.members[1].amount = 30;
      component.members[2].amount = 20;
      component.confirmSplit();

      const result = dialogRefSpy.close.calls.mostRecent().args[0];
      expect(result.method).toBe('unequally');
      expect(result.expenseParticipant.map((p: any) => p.amountOwed)).toEqual([50, 30, 20]);
    });

    it('rejects amounts that do not add up and shows how much is missing', () => {
      component.members[0].amount = 50;
      component.members[1].amount = 30;
      component.members[2].amount = 10;
      component.confirmSplit();

      expect(dialogRefSpy.close).not.toHaveBeenCalled();
      expect(component.validationError).toContain('missing $10.00');
    });

    it('rejects amounts that exceed the total', () => {
      component.members[0].amount = 60;
      component.members[1].amount = 30;
      component.members[2].amount = 20;
      component.confirmSplit();

      expect(dialogRefSpy.close).not.toHaveBeenCalled();
      expect(component.validationError).toContain('over the total');
    });
  });

  describe('split by percentage', () => {
    beforeEach(() => {
      createComponent({ members, amount: 100 });
      component.selectedTabIndex = 2;
    });

    it('accepts percentages that sum to 100', () => {
      component.members[0].amount = 50;
      component.members[1].amount = 30;
      component.members[2].amount = 20;
      component.confirmSplit();

      const result = dialogRefSpy.close.calls.mostRecent().args[0];
      expect(result.method).toBe('percentage');
      expect(result.expenseParticipant.map((p: any) => p.amountOwed)).toEqual([50, 30, 20]);
    });

    it('rejects percentages that do not sum to 100', () => {
      component.members[0].amount = 50;
      component.members[1].amount = 30;
      component.members[2].amount = 10;
      component.confirmSplit();

      expect(dialogRefSpy.close).not.toHaveBeenCalled();
      expect(component.validationError).toContain('must add up to 100%');
    });

    it('rejects negative percentages', () => {
      component.members[0].amount = 110;
      component.members[1].amount = -10;
      component.confirmSplit();

      expect(dialogRefSpy.close).not.toHaveBeenCalled();
      expect(component.validationError).toContain('between 0 and 100');
    });
  });

  describe('split by percentage on non-round total', () => {
    beforeEach(() => {
      createComponent({ members, amount: 90 });
      component.selectedTabIndex = 2;
    });

    it('maps percentages proportionally on non-round totals', () => {
      component.members[0].amount = 30;
      component.members[1].amount = 70;
      component.confirmSplit();

      const result = dialogRefSpy.close.calls.mostRecent().args[0];
      expect(result.expenseParticipant.map((p: any) => p.amountOwed)).toEqual([27, 63]);
    });
  });
});
