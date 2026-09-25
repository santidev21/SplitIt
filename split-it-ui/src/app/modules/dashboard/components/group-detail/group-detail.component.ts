import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { HeaderBarComponent } from '../header-bar/header-bar.component';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { AddExpenseDialogComponent } from '../add-expense-dialog/add-expense-dialog.component';
import { SettleDebtDialogComponent } from '../settle-debt-dialog/settle-debt-dialog.component';
import { GroupSettingsDialogComponent } from '../group-settings-dialog/group-settings-dialog.component';
import { Expense } from '../../../../models/expense.model';
import { ExpenseService } from '../../services/expense.service';
import { GroupDetails } from '../../../../models/group.model';
import { GroupService } from '../../services/group.service';
import { CurrencyService } from '../../services/currency.service';
import { UserGroupRole } from '../../../../models/enums/user-group-role.enum';
import { DebtDetails, DebtOwedByUserDto, DebtOwedToUserDto, FullDebtSummaryDto } from '../../../../models/debts-summary';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NotificationService } from '../../../../shared/services/notification.service';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-group-detail',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, RouterModule, FormsModule, TranslatePipe],
  templateUrl: './group-detail.component.html',
  styleUrls: ['./group-detail.component.scss']
})
export class GroupDetailComponent implements OnInit{
  groupId!: number;
  showAllExpenses = false;
  isAdminOrCreator = true;
  isCreator = false;

  debtsOwedByUser: DebtOwedByUserDto[] = [];
  debtsOwedToUser: DebtOwedToUserDto[] = [];
  debtDetails: DebtDetails[] = [];
  // Structured debt state; rendered via the translate pipe so it reacts to
  // language changes (never freeze a translated string with instant() here).
  debtState: 'owe' | 'owed' | 'settled' = 'settled';
  debtAmount = 0;
  debtAmountLabel = '0.00';
  totalOwedByUser = 0;
  totalOwedToUser = 0;
  /** Decimal places of the group's currency (fixed at creation): 2 for USD, 0 for COP. */
  currencyDecimals = 2;

  allExpenses: Expense[] = [];
  filteredExpenses: Expense[] = [];
  paymentsHistory: Expense[] = [];
  group : GroupDetails = {
    name: 'Trip to Mendoza',
    description: 'Expenses for the trip with friends in March 2025.'
  };

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private dialog: MatDialog,
    private expenseService: ExpenseService,
    private groupService: GroupService,
    private snackbar: MatSnackBar,
    private notifications: NotificationService,
    private translate: TranslateService,
    private currencyService: CurrencyService
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
    if (!this.isAdminOrCreator){
      this.notifications.toast(this.translate.instant('GROUP_DETAIL.EDIT_ONLY_CREATOR'), 'warning');
      return;
    }
    const dialogRef = this.dialog.open(GroupSettingsDialogComponent, {
      width: '560px',
      data: {
        groupId: this.groupId,
        isCreator: this.isCreator,
        isAdminOrCreator: this.isAdminOrCreator
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result === 'saved') {
        this.refreshPage();
      }
    });
  }

  onDeleteGroup(){
    this.notifications.confirm(
      this.translate.instant('GROUP_DETAIL.DELETE_TITLE'),
      this.translate.instant('GROUP_DETAIL.DELETE_TEXT'),
      this.translate.instant('COMMON.YES_DELETE')
    ).then(result => {
      if (result.isConfirmed) {
        this.groupService.deleteGroup(this.groupId).subscribe({
          next: () => {
            this.notifications.success(this.translate.instant('GROUP_DETAIL.DELETE_SUCCESS'));
            this.router.navigate(['/dashboard/home']);
          },
          error: () => {}
        });
      }
    });
  }

  getGroupDetails(){
    this.groupService.getGroupDetails(this.groupId).subscribe((resp) =>{
      if (resp){
        this.group = resp;
        // Resolve the currency's decimal precision for money formatting.
        if (resp.currencyId) {
          this.currencyService.getCurrencies().subscribe({
            next: (currencies) => {
              const currency = currencies.find(c => Number(c.id) === resp.currencyId);
              if (currency?.decimalPlaces !== undefined && currency.decimalPlaces !== null) {
                this.currencyDecimals = currency.decimalPlaces;
              }
            },
            error: () => { /* keep the 2-decimal default */ }
          });
        }
      }
    })
  }

  getGroupExpenses(){
    this.expenseService.getGroupExpenses(this.groupId, this.showAllExpenses).subscribe((resp) =>{
      if (resp && resp.length > 0){
        this.allExpenses = resp;
        this.filteredExpenses = resp.filter((e: Expense) => !e.isPayment);
        this.paymentsHistory = resp.filter((e: Expense) => e.isPayment);
      } else {
        this.allExpenses = [];
        this.filteredExpenses = [];
        this.paymentsHistory = [];
      }
    })
  }

  getDebtsSummary(){
    this.expenseService.getFullDebtSummary(this.groupId).subscribe((resp: FullDebtSummaryDto) =>{
      this.totalOwedByUser = resp.debtsOwedByUser.reduce((sum, d) => sum + d.totalAmountOwed, 0);
      this.totalOwedToUser = resp.debtsOwedToUser.reduce((sum, d) => sum + d.totalAmountOwed, 0);

      if (this.totalOwedByUser > this.totalOwedToUser) {
        this.debtState = 'owe';
        this.debtAmount = this.totalOwedByUser - this.totalOwedToUser;
      } else if (this.totalOwedToUser > this.totalOwedByUser) {
        this.debtState = 'owed';
        this.debtAmount = this.totalOwedToUser - this.totalOwedByUser;
      } else {
        this.debtState = 'settled';
        this.debtAmount = 0;
      }
      // Currency-aware: do not round whole currency units (e.g. a $42.60 USD balance
      // must not be displayed as $43), and COP balances must not show cents.
      this.debtAmountLabel = this.debtAmount.toFixed(this.currencyDecimals);

      // Combine debts into a single list with signed values
    const debtsToUser = resp.debtsOwedToUser.map(d => ({
      userId: d.debtorUserId,
      name: d.debtorUserName,
      amount: Math.round(d.totalAmountOwed * 100) / 100
    }));

    const debtsByUser = resp.debtsOwedByUser.map(d => ({
      userId: d.creditorUserId,
      name: d.creditorUserName,
      amount: -Math.round(d.totalAmountOwed * 100) / 100
    }));

    // Order first debtsToUser
    this.debtDetails = [...debtsToUser, ...debtsByUser]
      .filter(d => d.amount !== 0)
      .map(d => ({ ...d, amountLabel: Math.abs(d.amount).toFixed(this.currencyDecimals) }));
    });
  }

  /** Formats a money value with the group currency's precision. */
  formatMoney(value: number): string {
    return Number.isFinite(value) ? value.toFixed(this.currencyDecimals) : '0';
  }

  getUserGroupRole(){
    this.groupService.getUserGroupRole(this.groupId).subscribe({
      next: (resp : any) => {
        const userRole = resp.role;
        this.isCreator = userRole === UserGroupRole.Creator;
        this.isAdminOrCreator = this.isCreator || userRole === UserGroupRole.Admin;
      },
      error: () => {
        this.isAdminOrCreator = false;
        this.isCreator = false;
      }
    })
  }

  onShowAllExpenses(){
    this.getGroupExpenses();
  }

  settleDebt(debt: DebtDetails){
    // debt.amount is signed: negative = current user owes creditor, positive = debtor owes current user
    const theyPayMe = debt.amount > 0;
    const dialogRef = this.dialog.open(SettleDebtDialogComponent, {
      width: '420px',
      data: {
        groupId: this.groupId,
        otherUserId: debt.userId,
        otherUserName: debt.name,
        remainingDebt: Math.abs(debt.amount),
        theyPayMe
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result && result.amount > 0) {
        this.registerPayment(debt, result.amount);
      }
    });
  }

  private registerPayment(debt: DebtDetails, amount: number){
    const httpBody = {
      payerUserId: debt.userId,
      groupId: this.groupId,
      amount: amount
    };

    this.expenseService.settleExpenseWithUser(httpBody).subscribe({
      next: (resp: any) =>{
        this.refreshPage();
        const remainingText = resp.remainingDebt !== undefined && resp.remainingDebt > 0
          ? this.translate.instant('GROUP_DETAIL.REMAINING_DEBT', { amount: resp.remainingDebt })
          : '';
        this.notifications.toast(this.translate.instant('GROUP_DETAIL.PAYMENT_REGISTERED', { amount, remaining: remainingText }), 'success');
      },
      error: () => {
        // Error feedback is handled globally by errorInterceptor
      }
    })
  }

  refreshPage(){
    this.getGroupDetails();
    this.getGroupExpenses();
    this.getDebtsSummary();
  }
}
