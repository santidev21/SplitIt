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
    /** Currency-aware, pre-formatted absolute value for display (e.g. "30.40" for USD, "30" for COP). */
    amountLabel?: string;
}