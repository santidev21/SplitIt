import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';

@Component({
  selector: 'app-create-group',
  imports: [MATERIAL_IMPORTS, RouterModule],
  templateUrl: './create-group.component.html',
  styleUrls: ['./create-group.component.scss']
})
export class CreateGroupComponent {
  createGroupForm: FormGroup;
  
  //TODO: Add endpoint for those selects.
  currencies = ['USD', 'EUR', 'COP', 'MXN'];
  users = [
    { id: 1, name: 'Alice Johnson' },
    { id: 2, name: 'Bob Smith' },
    { id: 3, name: 'Charlie Brown' },
    { id: 4, name: 'Diana White' },
    { id: 5, name: 'Ethan Green' },
  ];

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<CreateGroupComponent>
  ) {
    this.createGroupForm = this.fb.group({
      name: ['', Validators.required],
      description: ['', Validators.required],
      currency: ['', Validators.required],
      members: [[]],
      allowAllToDeleteExpenses: [false]
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
