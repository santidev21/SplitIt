import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { ExpenseParticipant } from '../../../../models/expense.model';
import { PositiveNumberDirective } from '../../../../shared/directives/positive-number.directive';
import { PercentageDirective } from '../../../../shared/directives/percentage.directive';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-split-method-dialog',
  imports: [MATERIAL_IMPORTS, FormsModule, MatDialogModule, PositiveNumberDirective, PercentageDirective, TranslatePipe],
  templateUrl: './split-method-dialog.component.html',
  styleUrls: ['./split-method-dialog.component.scss']
})
export class SplitMethodDialogComponent {
  selectedTabIndex = 0;

  members: any[] = [];
  equalSplitSelection: { [key: string]: boolean } = {};
  amountSplit: { [key: string]: number } = {};
  percentageSplit: { [key: string]: number } = {};
  amount: number = 0;
  validationError: string = '';

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<SplitMethodDialogComponent>,
    private translate: TranslateService
  ) {
    this.members = data.members || [];
    this.members.forEach((m) => {
      this.equalSplitSelection[m.id] = true;
      this.amountSplit[m.id] = 0;
      this.percentageSplit[m.id] = 0;
    });
    this.amount = data.amount;
  }

  onTabChange(index: number): void {
    this.selectedTabIndex = index;
    this.validationError = '';
  }

  validateEqualSplit(): string {
    const selected = this.members.filter(m => this.equalSplitSelection[m.id]);
    if (selected.length === 0) return this.translate.instant('SPLIT.NO_MEMBERS');
    return '';
  }

  validateByAmount(): string {
    const entered = this.members.filter(m => m.amount != null && Number(m.amount) > 0);
    if (entered.length === 0) return this.translate.instant('SPLIT.NO_AMOUNTS');
    const sum = entered.reduce((s, m) => s + Number(m.amount), 0);
    if (Math.abs(sum - this.amount) > 0.01) {
      const diff = Math.abs(sum - this.amount);
      if (sum < this.amount) {
        return this.translate.instant('SPLIT.AMOUNTS_UNDER', { sum: sum.toFixed(2), diff: diff.toFixed(2), total: this.amount.toFixed(2) });
      }
      return this.translate.instant('SPLIT.AMOUNTS_OVER', { sum: sum.toFixed(2), diff: diff.toFixed(2), total: this.amount.toFixed(2) });
    }
    return '';
  }

  validateByPercentage(): string {
    const entered = this.members.filter(m => m.amount != null && Number(m.amount) !== 0);
    if (entered.length === 0) return this.translate.instant('SPLIT.NO_PERCENTAGES');
    for (const m of entered) {
      const pct = Number(m.amount);
      if (pct < 0 || pct > 100) return this.translate.instant('SPLIT.PERCENTAGE_INVALID', { name: m.name, pct });
    }
    const sumPct = entered.reduce((s, m) => s + Number(m.amount), 0);
    if (Math.abs(sumPct - 100) > 0.01) {
      return this.translate.instant('SPLIT.PERCENTAGE_INVALID_TOTAL', { sum: sumPct.toFixed(2) });
    }
    return '';
  }

  currentValidationError(): string {
    if (this.selectedTabIndex === 0) return this.validateEqualSplit();
    if (this.selectedTabIndex === 1) return this.validateByAmount();
    return this.validateByPercentage();
  }

  // Confirm and return the selected method + data
  confirmSplit(): void {
    const error = this.currentValidationError();
    if (error) {
      this.validationError = error;
      return;
    }
    this.validationError = '';

    let result: { method: string; expenseParticipant: ExpenseParticipant[] };

    if (this.selectedTabIndex === 0) {
      result = {
        method: 'SPLIT.METHOD_EQUAL',
        expenseParticipant: this.calculateEqualSplit()
      };
    } else if (this.selectedTabIndex === 1) {
      result = {
        method: 'SPLIT.METHOD_UNEQUAL',
        expenseParticipant: this.calculateSplitByAmount()
      };
    } else {
      result = {
        method: 'SPLIT.METHOD_PERCENTAGE',
        expenseParticipant: this.calculateSplitByPercentage()
      };
    }

    if (result.expenseParticipant.length > 0) this.dialogRef.close(result);
  }

    calculateEqualSplit(): ExpenseParticipant[] {
      const selectedMembers = this.members.filter(m => this.equalSplitSelection[m.id]);
      if (selectedMembers.length === 0) return [];
      // Monetary precision: distribute cents to avoid rounding error (e.g., 100/3 = 33.33,33.33,33.34)
      const count = selectedMembers.length;
      const perPersonRounded = Math.floor((this.amount / count) * 100) / 100;
      let remainderCents = Math.round((this.amount - perPersonRounded * count) * 100);
      return selectedMembers.map((m, idx) => {
        const extraCent = idx < remainderCents ? 0.01 : 0;
        const amountOwed = Math.round((perPersonRounded + extraCent) * 100) / 100;
        return { userId: m.id, amountOwed };
      });
    }

    calculateSplitByAmount() : ExpenseParticipant[] {
      const filtered = this.members.filter(m => m.amount != null && m.amount > 0);
      if (Math.abs(filtered.reduce((s, m) => s + Number(m.amount), 0) - this.amount) > 0.01) return [];
      return filtered.map((m) =>({
        userId: m.id,
        amountOwed: Math.round(Number(m.amount) * 100) / 100
      }));
    }

    calculateSplitByPercentage() : ExpenseParticipant[] {
      const filtered = this.members.filter(m => m.amount != null && m.amount > 0);
      if (Math.abs(filtered.reduce((s, m) => s + Number(m.amount), 0) - 100) > 0.01) return [];
      return filtered.map((m) =>({
        userId: m.id,
        amountOwed: Math.round((Number(m.amount) / 100) * this.amount * 100) / 100
      }));
    }

}
