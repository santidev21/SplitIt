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
  ) {}

  ngOnInit(): void {
    this.groupId = Number(this.route.snapshot.paramMap.get('id'));
    this.getGroupDetails();
    this.getGroupExpenses();
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
        this.getGroupExpenses();
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

  getUserGroupRole(){
    this.groupService.getUserGroupRole(this.groupId).subscribe((resp : any) =>{
      const userRole = resp.role;
      this.isAdminOrCreator = userRole === UserGroupRole.Creator || userRole === UserGroupRole.Admin;
    })
  }

  onShowAllExpenses(){
    this.getGroupExpenses();
  }

  
  balance = {
    total: 45,
    details: [
      { name: 'Pedro', amount: 25 },
      { name: 'Laura', amount: 20 },
      { name: 'Carlos', amount: 0 }
    ]
  };
  

  
}
 