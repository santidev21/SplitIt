export interface Group {
    id: string;
    name: string;
    createdBy: string;  // User ID
    members: GroupMember[];
  }
  
  export interface GroupMember {
    id: number,
    name: string
  }

  export interface GroupDetails{
    name: string,
    description: string
  }
  