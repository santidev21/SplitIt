import { Component, Inject } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { PositiveNumberDirective } from '../../../../shared/directives/positive-number.directive';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

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
  imports: [MATERIAL_IMPORTS, MatDialogModule, PositiveNumberDirective, TranslatePipe],
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
    private dialogRef: MatDialogRef<SettleDebtDialogComponent>,
    private translate: TranslateService
  ) {
    this.maxAmount = Math.max(0, Math.round(data.remainingDebt * 100) / 100);
    this.descriptionText = data.theyPayMe
      ? this.translate.instant('SETTLE.PAYS_YOU', { name: data.otherUserName })
      : this.translate.instant('SETTLE.YOU_PAY', { name: data.otherUserName });
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
