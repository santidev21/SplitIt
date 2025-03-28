import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  imports: [MATERIAL_IMPORTS, RouterModule, ReactiveFormsModule],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss']
})
export class RegisterComponent {
  registerForm: FormGroup;

  constructor(
    private authService: AuthService,
    private fb: FormBuilder,
  ){
    this.registerForm = this.fb.group({
      userName: ['', [Validators.required, ]],
      email: ['', [Validators.required, ]], //Validators.email
      password: ['', [Validators.required, ]] //Validators.minLength(6)
    });
  }

  register(event: Event) {
    event.preventDefault();
    if (this.registerForm.invalid) return;

    const { userName, email, password } = this.registerForm.value;
    
    console.log(this.registerForm);
    
    this.authService.register(userName, email, password).subscribe({
      next: (response) => {
        console.log('Register successful', response);
      },
      error: (err) => {
        console.error('Register failed', err);
      }
    });
  }
}
