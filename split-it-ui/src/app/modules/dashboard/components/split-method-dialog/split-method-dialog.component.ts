import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { ExpenseParticipant } from '../../../../models/expense.model';
import { PositiveNumberDirective } from '../../../../shared/directives/positive-number.directive';
import { PercentageDirective } from '../../../../shared/directives/percentage.directive';

@Component({
  selector: 'app-split-method-dialog',
  imports: [MATERIAL_IMPORTS, FormsModule, MatDialogModule, PositiveNumberDirective, PercentageDirective],
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

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<SplitMethodDialogComponent>
  ) {
    this.members = data.members || [];
    this.members.forEach((m) => {
      this.equalSplitSelection[m.id] = true;
      this.amountSplit[m.id] = 0;
      this.percentageSplit[m.id] = 0;
    });
    this.amount = data.amount;
  }

  // Confirm and return the selected method + data
  confirmSplit(): void {
    let result: { method: string; expenseParticipant: ExpenseParticipant[] };

    if (this.selectedTabIndex === 0) {
      result = {
        method: 'equally',
        expenseParticipant: this.calculateEqualSplit()
      };
    } else if (this.selectedTabIndex === 1) {
      result = {
        method: 'unequally',
        expenseParticipant: this.calculateSpitByAmount()
      };
    } else {
      result = {
        method: 'percentage',
        expenseParticipant: this.calculateSplyByPercentage()
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

    calculateSpitByAmount() : ExpenseParticipant[] {
      const filtered = this.members.filter(m => m.amount != null && m.amount > 0);
      // Validation: sum must equal amount ±0.01
      const sum = filtered.reduce((s, m) => s + Number(m.amount), 0);
      if (Math.abs(sum - this.amount) > 0.01) {
        // Return empty to indicate invalid; caller will handle
        return [];
      }
      return filtered.map((m) =>({
        userId: m.id,
        amountOwed: Math.round(Number(m.amount) * 100) / 100
      }));
    }

    calculateSplyByPercentage() : ExpenseParticipant[] {
      const filtered = this.members.filter(m => m.amount != null && m.amount > 0);
      const sumPct = filtered.reduce((s, m) => s + Number(m.amount), 0);
      if (Math.abs(sumPct - 100) > 0.01) {
        return [];
      }
      // Check each percentage 0-100
      for (const m of filtered) {
        const pct = Number(m.amount);
        if (pct < 0 || pct > 100) return [];
      }
      return filtered.map((m) =>({
        userId: m.id,
        amountOwed: Math.round((Number(m.amount) / 100) * this.amount * 100) / 100
      }));
    }

}
