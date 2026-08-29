import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [MATERIAL_IMPORTS, RouterModule, ReactiveFormsModule],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['../../auth.styles.scss']
})
export class ForgotPasswordComponent {
  step = 1;
  emailForm: FormGroup;
  codeForm: FormGroup;
  isLoading = false;
  errorMessage = '';

  constructor(
    private authService: AuthService,
    private fb: FormBuilder,
    private router: Router,
  ) {
    this.emailForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
    this.codeForm = this.fb.group({
      code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    });
  }

  submitEmail() {
    if (this.emailForm.invalid) {
      this.emailForm.markAllAsTouched();
      return;
    }
    this.isLoading = true;
    this.errorMessage = '';
    this.authService.forgotPassword(this.emailForm.value.email).subscribe({
      next: () => {
        this.isLoading = false;
        this.step = 2;
      },
      error: () => {
        this.isLoading = false;
        this.step = 2;
      }
    });
  }

  submitCode() {
    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }
    if (this.codeForm.value.newPassword !== this.codeForm.value.confirmPassword) {
      this.codeForm.get('confirmPassword')?.setErrors({ mismatch: true });
      return;
    }
    this.isLoading = true;
    this.errorMessage = '';
    this.authService.verifyResetCode(
      this.emailForm.value.email,
      this.codeForm.value.code,
      this.codeForm.value.newPassword
    ).subscribe({
      next: () => {
        this.isLoading = false;
        this.step = 3;
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err.error?.message || 'Invalid or expired code.';
      }
    });
  }
}
