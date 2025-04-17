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
      if (selectedMembers.length > 0){
        const perPersonAmount = this.amount / selectedMembers.length;
    
        return selectedMembers.map((m) => ({
          userId: m.id,
          amountOwed: perPersonAmount
        }));
      }
      return [];
    }

    calculateSpitByAmount() : ExpenseParticipant[] {
      return this.members.
        filter(m => m.amount > 0)
        .map((m) =>({          
          userId: m.id,
          amountOwed: m.amount
        }));
    }
  
    calculateSplyByPercentage() : ExpenseParticipant[] {
      return this.members.
        filter(m => m.amount > 0)
        .map((m) =>({          
          userId: m.id,
          amountOwed: (m.amount / 100) * this.amount
        }));
    }

}
