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

@Component({
  selector: 'app-add-expense-dialog',
  providers: [provideNativeDateAdapter()],
  imports: [MATERIAL_IMPORTS, MatDialogModule, LoadingSpinnerComponent, PositiveNumberDirective],
  templateUrl: './add-expense-dialog.component.html',
  styleUrls: ['./add-expense-dialog.component.scss']
})
export class AddExpenseDialogComponent implements OnInit {
  @ViewChild('picker') picker!: MatDatepicker<Date>;

  isLoading: boolean = true;

  expenseForm!: FormGroup;
  selectedDate: Date = new Date();
  splitMethodLabel: string = "equally"

  members: GroupMember[] = [];
  groupId : number = 0;
  expenseParticipants: ExpenseParticipant[] = [];
  
  constructor(
    private fb: FormBuilder,
    private groupService: GroupService,
    private expenseService: ExpenseService,
    private dialog: MatDialog,
    private dialogRef: MatDialogRef<AddExpenseDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { groupId: number }
  ) {
    this.expenseForm = this.fb.group({
      title: ['', Validators.required],
      note: [''],
      amount: [null, Validators.required],
      paidById: [null, Validators.required],
    });
    this.groupId = data.groupId;
  }

  // TODO:
  // At least 1 member select (equally).
  // ByAmount (Sum of amounts = total amount)
  // Write Amount First.


  ngOnInit(): void {
    this.groupService.getGroupMembers(this.groupId).subscribe((resp : GroupMember[]) =>
    {
      this.members = resp;
      if (this.members.length > 0) {
        this.expenseForm.patchValue({
          paidById: this.members[0].id,
        });
      }
      this.isLoading = false; 
    }, error => {
      this.isLoading = false;
      console.error('Error fetching group members', error);
    });
  }

  openSplitMethod(){
    if (this.expenseForm.value.amount > 0){
      const dialogRef = this.dialog.open(SplitMethodDialogComponent, {
        width: '400px',
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
        date: this.selectedDate,
        groupId: this.groupId,
        participants: this.expenseParticipants
      };

      this.expenseService.addExpense(result).subscribe(resp =>{
        if (resp){
          this.dialogRef.close(result);
        }
      });
    }
  }
}
