import { Component, Inject } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PositiveNumberDirective } from '../../../../shared/directives/positive-number.directive';

export interface SettleDebtDialogData {
  groupId: number;
  otherUserId: number;
  otherUserName: string;
  remainingDebt: number;
  /** true = the other user pays the current user; false = current user pays */
  theyPayMe: boolean;
}

@Component({
  selector: 'app-settle-debt-dialog',
  imports: [MATERIAL_IMPORTS, MatDialogModule, PositiveNumberDirective],
  templateUrl: './settle-debt-dialog.component.html',
  styleUrls: ['./settle-debt-dialog.component.scss']
})
export class SettleDebtDialogComponent {
  settleForm: FormGroup;
  maxAmount: number;
  descriptionText: string;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: SettleDebtDialogData,
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<SettleDebtDialogComponent>
  ) {
    this.maxAmount = Math.max(0, Math.round(data.remainingDebt * 100) / 100);
    this.descriptionText = data.theyPayMe
      ? `${data.otherUserName} pays you in this group.`
      : `You pay ${data.otherUserName} in this group.`;
    this.settleForm = this.fb.group({
      amount: [this.maxAmount, [
        Validators.required,
        Validators.min(0.01),
        Validators.max(this.maxAmount)
      ]]
    });
  }

  useFullAmount(): void {
    this.settleForm.patchValue({ amount: this.maxAmount });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  confirm(): void {
    if (this.settleForm.invalid) {
      this.settleForm.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ amount: this.settleForm.value.amount });
  }
}
