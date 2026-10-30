export interface ExpenseParticipant {
  userId: number;
  amountOwed: number;
}

export interface ExpenseParticipantDetail {
  name: string;
  amount: number;
}

export interface Expense {
  id: number;
  title: string;
  amount: number;
  paidBy: string;
  date: string;
  note: string | null;
  isPayment?: boolean;
  /** Consolidated debt created by "simplify debts"; paidBy is the creditor, the participant the debtor. */
  isSimplification?: boolean;
  participants: ExpenseParticipantDetail[];
}
