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

export interface DebtDetails {
  userId: number;
  name: string;
  amount: number;
  /** Currency-aware, pre-formatted absolute value for display (e.g. "30.40" for USD, "30" for COP). */
  amountLabel?: string;
}

export interface SimplifiedTransfer {
  fromUserId: number;
  fromUserName: string;
  toUserId: number;
  toUserName: string;
  amount: number;
}

/** Simplified transfer plan for a group (`GET simplify-preview` / `POST simplify`). */
export interface DebtSimplification {
  currentTransferCount: number;
  simplifiedTransferCount: number;
  canSimplify: boolean;
  applied: boolean;
  transfers: SimplifiedTransfer[];
}
