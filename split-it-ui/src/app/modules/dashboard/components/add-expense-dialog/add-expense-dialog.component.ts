import { Component, Inject, ViewChild } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SplitMethodDialogComponent } from '../split-method-dialog/split-method-dialog.component';
import { MatDatepicker } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';

@Component({
  selector: 'app-add-expense-dialog',
  providers: [provideNativeDateAdapter()],
  imports: [MATERIAL_IMPORTS, MatDialogModule],
  templateUrl: './add-expense-dialog.component.html',
  styleUrls: ['./add-expense-dialog.component.scss']
})
export class AddExpenseDialogComponent {
  @ViewChild('picker') picker!: MatDatepicker<Date>;

  expenseForm: FormGroup;
  selectedDate: Date = new Date();
  splitMethodLabel: string = "equally"

  splitMethod: {
    method: 'equally' | 'unequally' | 'percentage',
    data: any
  } | null = null;

  members = [
    { id: '1', name: 'You' },
    { id: '2', name: 'Alice' },
    { id: '3', name: 'Bob' },
  ];

  constructor(
    private fb: FormBuilder,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<AddExpenseDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {
    this.expenseForm = this.fb.group({
      title: ['', Validators.required],
      notes: [''],
      amount: [null, Validators.required],
      paidBy: [this.members[0].id, Validators.required],
    });
  }

  openSplitMethod(){
    const dialogRef = this.dialog.open(SplitMethodDialogComponent, {
      width: '400px',
      data: {
        members: this.members
      }
    });
  
    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.splitMethod = result;
        this.splitMethodLabel = this.getSplitLabel(result.method);
      }
    });
  }

  getSplitLabel(method: string): string {
    switch (method) {
      case 'equally':
        return 'equally';
      case 'unequally':
        return 'unequally';
      case 'percentage':
        return 'by percentage';
      default:
        return 'equally';
    }
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
    if (this.expenseForm.valid) {
      const result = {
        ...this.expenseForm.value,
        date: this.selectedDate
      };
      this.dialogRef.close(result);
    }
  }
}
