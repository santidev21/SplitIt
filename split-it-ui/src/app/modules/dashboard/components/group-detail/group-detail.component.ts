import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { HeaderBarComponent } from '../header-bar/header-bar.component';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { AddExpenseDialogComponent } from '../add-expense-dialog/add-expense-dialog.component';
import { Expense } from '../../../../models/expense.model';
import { ExpenseService } from '../../services/expense.service';
import { GroupDetails } from '../../../../models/group.model';
import { GroupService } from '../../services/group.service';
import { UserGroupRole } from '../../../../models/enums/user-group-role.enum';
import { DebtDetails, DebtOwedByUserDto, DebtOwedToUserDto, FullDebtSummaryDto } from '../../../../models/debts-summary';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-group-detail',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, RouterModule, FormsModule],
  templateUrl: './group-detail.component.html',
  styleUrls: ['./group-detail.component.scss']
})
export class GroupDetailComponent implements OnInit{
  groupId!: number;
  showAllExpenses = false;
  isAdminOrCreator = true;

  debtsOwedByUser: DebtOwedByUserDto[] = [];
  debtsOwedToUser: DebtOwedToUserDto[] = [];
  debtDetails: DebtDetails[] = [];
  debtMessage: string = '';
  totalOwedByUser = 0;
  totalOwedToUser = 0;
  
  filteredExpenses: Expense[] = [];
  group : GroupDetails = {
    name: 'Trip to Mendoza',
    description: 'Expenses for the trip with friends in March 2025.'
  };

  constructor(
    private route: ActivatedRoute,
    private dialog: MatDialog,
    private expenseService: ExpenseService,
    private groupService: GroupService,
    private snackbar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.groupId = Number(this.route.snapshot.paramMap.get('id'));
    this.getGroupDetails();
    this.getGroupExpenses();
    this.getDebtsSummary();
    this.getUserGroupRole();
  }

  onEdit(exp: any){

  }

  onDelete(exp: any){

  }

  onAddExpense(){
    const dialogRef = this.dialog.open(AddExpenseDialogComponent, {
          width: '600px',
          data: { groupId : this.groupId }
        });

    dialogRef.afterClosed().subscribe(result => {
      if (result === 'saved') {
        this.refreshPage();
      }
    });
  }

  onEditGroup(){

  }

  onDeleteGroup(){

  }

  getGroupDetails(){
    this.groupService.getGroupDetails(this.groupId).subscribe((resp) =>{
      if (resp){
        this.group = resp
      }      
    })
  }

  getGroupExpenses(){
    this.expenseService.getGroupExpenses(this.groupId, this.showAllExpenses).subscribe((resp) =>{
      if (resp && resp.length > 0){
        this.filteredExpenses = resp
      }
      
    })
  }

  getDebtsSummary(){
    this.expenseService.getFullDebtSummary(this.groupId).subscribe((resp: FullDebtSummaryDto) =>{
      this.totalOwedByUser = resp.debtsOwedByUser.reduce((sum, d) => sum + d.totalAmountOwed, 0);
      this.totalOwedToUser = resp.debtsOwedToUser.reduce((sum, d) => sum + d.totalAmountOwed, 0);

      if (this.totalOwedByUser > this.totalOwedToUser) {
        const amount = Math.round(this.totalOwedByUser - this.totalOwedToUser);
        this.debtMessage = `You owe: $${amount}`;
      } else if (this.totalOwedToUser > this.totalOwedByUser) {
        const amount = Math.round(this.totalOwedToUser - this.totalOwedByUser);
        this.debtMessage = `You are owed: $${amount}`;
      } else {
        this.debtMessage = 'You are all settled up!';
      }

      // Combine debts into a single list with signed values
    const debtsToUser = resp.debtsOwedToUser.map(d => ({
      userId: d.debtorUserId,
      name: d.debtorUserName,
      amount: Math.round(d.totalAmountOwed)
    }));

    const debtsByUser = resp.debtsOwedByUser.map(d => ({
      userId: d.creditorUserId,
      name: d.creditorUserName,
      amount: -Math.round(d.totalAmountOwed)
    }));

    // Order first debtsToUser
    this.debtDetails = [...debtsToUser, ...debtsByUser].filter(d => d.amount !== 0);
    });
  }

  getUserGroupRole(){
    this.groupService.getUserGroupRole(this.groupId).subscribe((resp : any) =>{
      const userRole = resp.role;
      this.isAdminOrCreator = userRole === UserGroupRole.Creator || userRole === UserGroupRole.Admin;
    })
  }

  onShowAllExpenses(){
    this.getGroupExpenses();
  }

  settleDebt(debt: DebtDetails){
    const httpBody = {
      payerUserId: debt.userId,
      groupId: this.groupId,
      amount: debt.amount
    }

    this.expenseService.settleExpenseWithUser(httpBody).subscribe((resp) =>{
      if(resp){
        this.refreshPage();
        this.snackbar.open(`${resp.settledCount} debts settled successfully!`, 'OK', { duration: 3000 });
      }
    })
  }
  
  refreshPage(){
    this.getGroupDetails();
    this.getGroupExpenses();
    this.getDebtsSummary();
  }
}
 