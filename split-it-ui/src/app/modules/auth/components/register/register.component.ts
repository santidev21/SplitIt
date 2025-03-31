import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-register',
  imports: [MATERIAL_IMPORTS, RouterModule, LoadingSpinnerComponent],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss']
})
export class RegisterComponent {
  registerForm: FormGroup;
  isLoading: boolean = false;


  constructor(
    private authService: AuthService,
    private fb: FormBuilder,
    private router: Router,
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
    this.isLoading = true;

    const { userName, email, password } = this.registerForm.value;    
    this.authService.register(userName, email, password).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Register failed', err);
      }
    });
  }
}
