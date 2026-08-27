export interface Group {
    id: string;
    name: string;
    createdBy: string;  // User ID
    members: GroupMember[];
  }

  export interface GroupMember {
    id: number,
    name: string,
    role?: string,
    email?: string
  }

  export interface GroupDetails{
    name: string,
    description: string,
    allowToDeleteExpenses?: boolean,
    currencyId?: number
  }
