import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslatePipe } from '@ngx-translate/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { DebtSimplification } from '../../../../models/debts-summary';

export interface SimplifyDebtsDialogData {
  preview: DebtSimplification;
  /** Decimal places of the group's currency, used to format the amounts. */
  currencyDecimals: number;
}

/**
 * Shows the simplified transfer plan returned by the API and asks for confirmation.
 * Closes with `true` when the user confirms; the caller applies the simplification.
 */
@Component({
  selector: 'app-simplify-debts-dialog',
  imports: [MATERIAL_IMPORTS, MatDialogModule, TranslatePipe],
  templateUrl: './simplify-debts-dialog.component.html',
  styleUrls: ['./simplify-debts-dialog.component.scss'],
})
export class SimplifyDebtsDialogComponent {
  data = inject<SimplifyDebtsDialogData>(MAT_DIALOG_DATA);
  private dialogRef = inject<MatDialogRef<SimplifyDebtsDialogComponent, boolean>>(MatDialogRef);

  formatMoney(value: number): string {
    return value.toFixed(this.data.currencyDecimals);
  }

  cancel(): void {
    this.dialogRef.close(false);
  }

  confirm(): void {
    this.dialogRef.close(true);
  }
}
