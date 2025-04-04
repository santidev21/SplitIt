import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { CurrencyService } from '../../services/currency.service';
import { UsersService } from '../../services/users.service';
import { User } from '../../../../models/user.model';
import { Currency } from '../../../../models/currency.model';
import { GroupService } from '../../services/group.service';

@Component({
  selector: 'app-create-group',
  imports: [MATERIAL_IMPORTS, RouterModule],
  templateUrl: './create-group.component.html',
  styleUrls: ['./create-group.component.scss']
})
export class CreateGroupComponent implements OnInit{
  createGroupForm: FormGroup;
  users: User[] = [];
  currencies: Currency[] = [];
  selectedCurrency!: number;
  selectedUsers: number[] = [];

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<CreateGroupComponent>,
    private currencyService: CurrencyService,
    private usersService: UsersService,
    private groupService: GroupService,
    private router: Router
  ) {
    this.createGroupForm = this.fb.group({
      name: ['', Validators.required],
      description: ['', Validators.required],
      currencyId: [null, Validators.required],
      members: [[], Validators.required],
      allowToDeleteExpenses: [false]
    });
  }

  ngOnInit(): void {
    this.currencyService.getCurrencies().subscribe(currencies =>{
      this.currencies = currencies;
    });
    this.usersService.getUsers().subscribe(users =>{
      this.users = users;
    });
  }

  onSubmit(): void {
    if (this.createGroupForm.valid) {
      const groupData = this.createGroupForm.value;      
      this.groupService.createGroup(groupData).subscribe(resp =>{
        this.dialogRef.close();
        this.router.navigate(['/dashboard/group', resp.groupId]);
      });
    }
  }

  close() {
    this.dialogRef.close();
  }
}
