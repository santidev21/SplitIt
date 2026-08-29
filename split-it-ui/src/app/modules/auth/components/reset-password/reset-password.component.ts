import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-reset-password',
  imports: [MATERIAL_IMPORTS, RouterModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './reset-password.component.html',
  styleUrls: ['../../auth.styles.scss']
})
export class ResetPasswordComponent {
  resetForm: FormGroup;
  isLoading = false;
  submitted = false;
  success = false;
  token = '';

  constructor(
    private authService: AuthService,
    private fb: FormBuilder,
    private router: Router,
    private route: ActivatedRoute,
  ) {
    this.token = this.route.snapshot.queryParamMap.get('token') || '';
    this.resetForm = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]]
    });
  }

  submit() {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      return;
    }
    if (this.resetForm.value.newPassword !== this.resetForm.value.confirmPassword) {
      this.resetForm.get('confirmPassword')?.setErrors({ mismatch: true });
      return;
    }
    this.isLoading = true;
    this.authService.resetPassword(this.token, this.resetForm.value.newPassword).subscribe({
      next: () => {
        this.isLoading = false;
        this.success = true;
        this.submitted = true;
      },
      error: () => {
        this.isLoading = false;
        this.submitted = true;
      }
    });
  }
}
