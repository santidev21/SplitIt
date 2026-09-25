import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { ExpenseParticipant } from '../../../../models/expense.model';
import { PositiveNumberDirective } from '../../../../shared/directives/positive-number.directive';
import { PercentageDirective } from '../../../../shared/directives/percentage.directive';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../auth/services/auth.service';

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
  currentUserId = 0;
  /** Currency precision (2 for USD cents, 0 for whole Colombian pesos); resolved from the group's currency. */
  decimalPlaces = 2;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<SplitMethodDialogComponent>,
    private translate: TranslateService,
    authService: AuthService
  ) {
    this.currentUserId = authService.getCurrentUserId();
    this.members = data.members || [];
    this.members.forEach((m) => {
      this.equalSplitSelection[m.id] = true;
      this.amountSplit[m.id] = 0;
      this.percentageSplit[m.id] = 0;
    });
    this.amount = data.amount;
    this.decimalPlaces = data.decimalPlaces !== undefined && data.decimalPlaces !== null ? data.decimalPlaces : 2;
  }

  /** Size of one minor unit of the currency: 0.01 for 2 decimals, 1 for 0. */
  scaleUnit(): number {
    return Math.pow(10, this.decimalPlaces);
  }

  /** Rounds a raw number to the currency's minor unit (e.g. 30.005 -> 30.01 for USD, 30.5 -> 31 for COP). */
  toUnit(value: number): number {
    const unit = this.scaleUnit();
    const rounded = Math.round(value * unit) / unit;
    // Avoid -0 artifacts in label rendering.
    return Object.is(rounded, -0) ? 0 : rounded;
  }

  /** True when the value has more decimals than the currency allows. */
  hasExcessPrecision(value: number): boolean {
    const unit = this.scaleUnit();
    return Math.abs(value * unit - Math.round(value * unit)) > 1e-9;
  }

  /**
   * Guarantees the parts sum exactly to the total at the currency's precision.
   * Any rounding drift (e.g. percentage splits of awkward totals) is absorbed by
   * the last participant so no cent is silently created or dropped.
   */
  driftCorrect(parts: ExpenseParticipant[], total: number): ExpenseParticipant[] {
    if (parts.length === 0) return parts;
    const sum = parts.reduce((s, p) => s + p.amountOwed, 0);
    const drift = this.toUnit(total - sum);
    if (drift !== 0) {
      const last = parts[parts.length - 1];
      const corrected = this.toUnit(last.amountOwed + drift);
      if (corrected > 0) {
        parts[parts.length - 1] = { ...last, amountOwed: corrected };
      }
    }
    return parts;
  }

  onTabChange(index: number): void {
    this.selectedTabIndex = index;
    this.validationError = '';
  }

  /** Localized label for a member: "You"/"Tú" for the current user, real name otherwise. */
  displayName(member: any): string {
    return member?.id === this.currentUserId
      ? this.translate.instant('COMMON.YOU')
      : member?.name;
  }

  validateEqualSplit(): string {
    if (this.hasExcessPrecision(this.amount)) return this.translate.instant('SPLIT.AMOUNT_DECIMALS', { decimals: this.decimalPlaces });
    const selected = this.members.filter(m => this.equalSplitSelection[m.id]);
    if (selected.length === 0) return this.translate.instant('SPLIT.NO_MEMBERS');
    // Whole-unit floor of an equal split: each participant must receive at least one minor unit.
    const unit = this.scaleUnit();
    const perPerson = Math.floor((this.amount / selected.length) * unit) / unit;
    const remainder = Math.round((this.amount - perPerson * selected.length) * unit);
    if (perPerson <= 0 && remainder < selected.length) return this.translate.instant('SPLIT.AMOUNT_TOO_SMALL');
    return '';
  }

  validateByAmount(): string {
    if (this.hasExcessPrecision(this.amount)) return this.translate.instant('SPLIT.AMOUNT_DECIMALS', { decimals: this.decimalPlaces });
    const entered = this.members.filter(m => m.amount != null && Number(m.amount) > 0);
    if (entered.length === 0) return this.translate.instant('SPLIT.NO_AMOUNTS');
    for (const m of entered) {
      if (this.hasExcessPrecision(Number(m.amount))) {
        return this.translate.instant('SPLIT.AMOUNT_DECIMALS', { decimals: this.decimalPlaces });
      }
    }
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
    if (this.hasExcessPrecision(this.amount)) return this.translate.instant('SPLIT.AMOUNT_DECIMALS', { decimals: this.decimalPlaces });
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
      // Split at the currency's precision: floor the base amount and distribute the
      // leftover minor units to the first participants (e.g. 100/3 => 33.34,33.33,33.33
      // for USD; 100/3 => 34,33,33 for COP). The result always sums exactly to the total.
      const unit = this.scaleUnit();
      const count = selectedMembers.length;
      const perPerson = Math.floor((this.amount / count) * unit) / unit;
      const remainder = Math.round((this.amount - perPerson * count) * unit);
      return selectedMembers.map((m, idx) => {
        const extra = idx < remainder ? 1 / unit : 0;
        const amountOwed = this.toUnit(perPerson + extra);
        return { userId: m.id, amountOwed };
      });
    }

    calculateSplitByAmount() : ExpenseParticipant[] {
      const filtered = this.members.filter(m => m.amount != null && m.amount > 0);
      if (Math.abs(filtered.reduce((s, m) => s + Number(m.amount), 0) - this.amount) > 0.01) return [];
      const parts = filtered.map((m) =>({
        userId: m.id,
        amountOwed: this.toUnit(Number(m.amount))
      }));
      return this.driftCorrect(parts, this.amount);
    }

    calculateSplitByPercentage() : ExpenseParticipant[] {
      const filtered = this.members.filter(m => m.amount != null && m.amount > 0);
      if (Math.abs(filtered.reduce((s, m) => s + Number(m.amount), 0) - 100) > 0.01) return [];
      // Round each percentage share to the currency's precision, then absorb any
      // rounding drift into the last participant so the total is conserved exactly.
      const parts = filtered.map((m) =>({
        userId: m.id,
        amountOwed: this.toUnit((Number(m.amount) / 100) * this.amount)
      }));
      return this.driftCorrect(parts, this.amount);
    }

}
