import { Component, inject } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-register',
  imports: [MATERIAL_IMPORTS, RouterModule, LoadingSpinnerComponent, TranslatePipe],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
})
export class RegisterComponent {
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private router = inject(Router);

  registerForm: FormGroup;
  isLoading = false;
  submitAttempted = false;

  constructor() {
    this.registerForm = this.fb.group({
      userName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(100)]],
      acceptTerms: [false, [Validators.requiredTrue]],
    });
  }

  register(event: Event) {
    event.preventDefault();
    this.submitAttempted = true;
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }
    this.isLoading = true;

    const { userName, email, password, acceptTerms } = this.registerForm.value;
    this.authService.register(userName, email, password, acceptTerms === true).subscribe({
      next: () => {
        this.isLoading = false;
        this.router.navigate(['/dashboard']);
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }
}
