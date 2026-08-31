import { Component, OnInit, AfterViewInit, NgZone } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { TranslatePipe } from '@ngx-translate/core';
import { environment } from '../../../../../environments/environment';

declare var google: any;

@Component({
  selector: 'app-login',
  imports: [MATERIAL_IMPORTS, RouterModule, LoadingSpinnerComponent, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit, AfterViewInit {

  loginForm: FormGroup;
  isLoading: boolean = false;
  submitAttempted = false;
  googleReady = false;

  constructor(
    private authService: AuthService,
    private fb: FormBuilder,
    private router: Router,
    private ngZone: NgZone,
  ){
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/dashboard/home']);
    }
  }

  ngAfterViewInit(): void {
    this.loadGoogleScript();
  }

  private loadGoogleScript(): void {
    if (typeof document === 'undefined') return;
    if (document.getElementById('google-gsi-script')) {
      this.initGoogleButton();
      return;
    }
    const script = document.createElement('script');
    script.id = 'google-gsi-script';
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = () => this.initGoogleButton();
    document.head.appendChild(script);
  }

  private initGoogleButton(): void {
    if (typeof google === 'undefined' || !google.accounts?.id) {
      setTimeout(() => this.initGoogleButton(), 200);
      return;
    }
    google.accounts.id.initialize({
      client_id: (environment as any).googleClientId || '',
      callback: (response: any) => this.handleGoogleCredential(response),
    });
    google.accounts.id.renderButton(
      document.getElementById('google-signin-button'),
      { theme: 'outline', size: 'large', width: '100%', text: 'signin_with' }
    );
    this.googleReady = true;
  }

  handleGoogleCredential(response: any): void {
    this.ngZone.run(() => {
      this.isLoading = true;
      this.authService.loginWithGoogle(response.credential).subscribe({
        next: () => {
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        }
      });
    });
  }

  login(event: Event) {
    event.preventDefault();
    this.submitAttempted = true;
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    this.isLoading = true;

    const { email, password } = this.loginForm.value;
    this.authService.login(email, password).subscribe({
      next: () => {
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }
}
