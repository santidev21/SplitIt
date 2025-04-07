import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { HeaderBarComponent } from '../header-bar/header-bar.component';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { AddExpenseDialogComponent } from '../add-expense-dialog/add-expense-dialog.component';

@Component({
  selector: 'app-group-detail',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, RouterModule, FormsModule],
  templateUrl: './group-detail.component.html',
  styleUrls: ['./group-detail.component.scss']
})
export class GroupDetailComponent implements OnInit{
  groupId!: number;

  constructor(
    private route: ActivatedRoute,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.groupId = Number(this.route.snapshot.paramMap.get('id'));
  }

  onEdit(exp: any){

  }

  onDelete(exp: any){

  }

  onAddExpense(){
    this.dialog.open(AddExpenseDialogComponent, {
          width: '600px'
        });
  }

  onEditGroup(){

  }

  onDeleteGroup(){

  }

  group = {
    name: 'Trip to Mendoza',
    description: 'Expenses for the trip with friends in March 2025.'
  };
  
  balance = {
    total: 45,
    details: [
      { name: 'Pedro', amount: 25 },
      { name: 'Laura', amount: 20 },
      { name: 'Carlos', amount: 0 }
    ]
  };
  
  showAllExpenses = false;
  isAdminOrCreator = true;
  
  filteredExpenses = [
    {
      title: 'Lunch at La Barra',
      amount: 60,
      paidBy: 'You',
      date: new Date('2025-03-10'),
      notes: 'Shared entrecote, wine and tip',
      participants: [
        { name: 'You', amount: 10 },
        { name: 'Laura', amount: 50 }
      ]
    },
    {
      title: 'Groceries',
      amount: 120,
      paidBy: 'Laura',
      date: new Date('2025-03-12'),
      notes: 'Weekly groceries at Carrefour',
      participants: [
        { name: 'Laura', amount: 60 },
        { name: 'You', amount: 60 }
      ]
    },
    {
      title: 'Pizza night',
      amount: 80,
      paidBy: 'Carlos',
      date: new Date('2025-03-15'),
      notes: 'Domino’s + beers 🍕🍺',
      participants: [
        { name: 'Carlos', amount: 40 },
        { name: 'You', amount: 40 }
      ]
    },
    {
      title: 'Streaming Subscription',
      amount: 50,
      paidBy: 'You',
      date: new Date('2025-03-20'),
      notes: 'Netflix for the month',
      participants: [
        { name: 'You', amount: 25 },
        { name: 'Laura', amount: 25 }
      ]
    },
    {
      title: 'Taxi to airport',
      amount: 70,
      paidBy: 'Laura',
      date: new Date('2025-03-22'),
      notes: 'Split ride with Carlos and you',
      participants: [
        { name: 'Laura', amount: 30 },
        { name: 'Carlos', amount: 20 },
        { name: 'You', amount: 20 }
      ]
    }
  ];
  
}
 