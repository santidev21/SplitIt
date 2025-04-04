import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { CurrencyService } from '../../services/currency.service';
import { UsersService } from '../../services/users.service';
import { User } from '../../../../models/user.model';
import { Currency } from '../../../../models/currency.model';

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
    private usersService: UsersService
  ) {
    this.createGroupForm = this.fb.group({
      name: ['', Validators.required],
      description: ['', Validators.required],
      currency: [null, Validators.required],
      members: [[], Validators.required],
      allowAllToDeleteExpenses: [false]
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
      console.log(this.createGroupForm.value);
    }
  }

  close() {
    this.dialogRef.close();
  }
}
