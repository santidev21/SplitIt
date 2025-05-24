export interface DebtOwedByUserDto {
    creditorUserId: number;
    creditorUserName: string;
    totalAmountOwed: number;
}

export interface DebtOwedToUserDto {
    debtorUserId: number;
    debtorUserName: string;
    totalAmountOwed: number;
}

export interface FullDebtSummaryDto {
    debtsOwedByUser: DebtOwedByUserDto[];
    debtsOwedToUser: DebtOwedToUserDto[];
}

export interface DebtDetails{
    userId: number;
    name: string; 
    amount: number;
}