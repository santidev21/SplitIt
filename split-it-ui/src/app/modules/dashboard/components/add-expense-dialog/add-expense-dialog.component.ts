import { Component, Inject, OnInit, ViewChild } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SplitMethodDialogComponent } from '../split-method-dialog/split-method-dialog.component';
import { MatDatepicker } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { GroupService } from '../../services/group.service';
import { GroupMember } from '../../../../models/group.model';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { PositiveNumberDirective } from '../../../../shared/directives/positive-number.directive';
import { ExpenseParticipant } from '../../../../models/expense.model';
import { ExpenseService } from '../../services/expense.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-add-expense-dialog',
  providers: [provideNativeDateAdapter()],
  imports: [MATERIAL_IMPORTS, MatDialogModule, LoadingSpinnerComponent, PositiveNumberDirective, TranslatePipe],
  templateUrl: './add-expense-dialog.component.html',
  styleUrls: ['./add-expense-dialog.component.scss']
})
export class AddExpenseDialogComponent implements OnInit {
  @ViewChild('picker') picker!: MatDatepicker<Date>;

  isLoading: boolean = true;
  isSaving: boolean = false;

  expenseForm!: FormGroup;
  selectedDate: Date = new Date();
  splitMethodLabel: string = 'EXPENSE.EQUALLY'

  members: GroupMember[] = [];
  groupId : number = 0;
  expenseParticipants: ExpenseParticipant[] = [];

  constructor(
    private fb: FormBuilder,
    private groupService: GroupService,
    private expenseService: ExpenseService,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<AddExpenseDialogComponent>,
    private notifications: NotificationService,
    @Inject(MAT_DIALOG_DATA) public data: { groupId: number },
    private translate: TranslateService
  ) {
    this.expenseForm = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(100)]],
      note: ['', Validators.maxLength(500)],
      amount: [null, [Validators.required, Validators.min(0.01), Validators.max(1000000)]],
      paidById: [null, Validators.required],
    });
    this.groupId = data.groupId;
  }

  ngOnInit(): void {
    this.groupService.getGroupMembers(this.groupId).subscribe({
      next: (resp : GroupMember[]) =>
      {
        this.members = resp;
        if (this.members.length > 0) {
          this.expenseForm.patchValue({
            paidById: this.members[0].id,
          });
        }
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notifications.toast(this.translate.instant('NOTIFICATIONS.COULD_NOT_LOAD_MEMBERS'), 'error');
      }
    });
  }

  openSplitMethod(){
    if (!this.expenseForm.value.amount || this.expenseForm.value.amount <= 0){
      this.notifications.toast(this.translate.instant('NOTIFICATIONS.AMOUNT_GT_ZERO_SPLIT'), 'warning');
      return;
    }
    const dialogRef = this.dialog.open(SplitMethodDialogComponent, {
      width: '450px',
      data: {
        members: this.members,
        amount: this.expenseForm.value.amount
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.expenseParticipants = result.expenseParticipant
        this.splitMethodLabel = result.method;
      }
    });
  }

  openDatepicker() {
    this.picker.open();
  }

  pickDate(){
    this.selectedDate = new Date();
  }

  onDateChange(event: any) {
    this.selectedDate = event.value;
  }

  saveExpense(){
    this.expenseForm.markAllAsTouched();
    if (this.expenseForm.valid) {
      if (this.expenseParticipants.length === 0) {
        this.notifications.toast(this.translate.instant('NOTIFICATIONS.OPEN_SPLIT_OPTIONS'), 'warning');
        return;
      }
      if (this.isSaving) return;
      this.isSaving = true;

      const result = {
        ...this.expenseForm.value,
        date: this.selectedDate,
        groupId: this.groupId,
        participants: this.expenseParticipants
      };

      this.expenseService.addExpense(result).subscribe({
        next: (resp) => {
          this.isSaving = false;
          if (resp){
            this.dialogRef.close('saved');
            this.notifications.success(this.translate.instant('NOTIFICATIONS.EXPENSE_ADDED'));
          }
        },
        error: () => { this.isSaving = false; }
      });
    }
  }
}
