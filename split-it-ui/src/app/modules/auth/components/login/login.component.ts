import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-login',
  imports: [MATERIAL_IMPORTS, RouterModule, ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent {

  loginForm: FormGroup;

  constructor(
    private authService: AuthService,
    private fb: FormBuilder,
  ){
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, ]], //Validators.email
      password: ['', [Validators.required, ]] //Validators.minLength(6)
    });
  }

  login(event: Event) {
    event.preventDefault();
    if (this.loginForm.invalid) return;

    const { email, password } = this.loginForm.value;
    
    this.authService.login(email, password).subscribe({
      next: (response) => {
        console.log('Login successful', response);
      },
      error: (err) => {
        console.error('Login failed', err);
      }
    });
  }
}
