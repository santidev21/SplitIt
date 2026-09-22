import { Component, OnInit, AfterViewInit, NgZone } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LoadingSpinnerComponent } from '../../../../shared/components/loading-spinner/loading-spinner.component';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { environment } from '../../../../../environments/environment';
import { MatSnackBar } from '@angular/material/snack-bar';

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
  googleError: string | null = null;
  private googleInitAttempts = 0;
  private readonly maxGoogleInitAttempts = 25;

  constructor(
    private authService: AuthService,
    private fb: FormBuilder,
    private router: Router,
    private ngZone: NgZone,
    private snackBar: MatSnackBar,
    private translate: TranslateService,
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

  retryGoogle(): void {
    this.googleError = null;
    this.googleInitAttempts = 0;
    this.initGoogleButton();
  }

  private initGoogleButton(): void {
    if (typeof google === 'undefined' || !google.accounts?.id) {
      this.googleInitAttempts++;
      if (this.googleInitAttempts >= this.maxGoogleInitAttempts) {
        this.ngZone.run(() => {
          this.googleError = this.translate.instant('AUTH.GOOGLE_UNAVAILABLE');
        });
        return;
      }
      setTimeout(() => this.initGoogleButton(), 200);
      return;
    }
    if (!(environment as any).googleClientId) {
      console.warn('Google Client ID not configured');
      this.googleError = this.translate.instant('AUTH.GOOGLE_UNAVAILABLE');
      return;
    }
    google.accounts.id.initialize({
      client_id: (environment as any).googleClientId,
      context: 'signin',
      itp_support: true,
      auto_select: false,
      callback: (response: any) => this.handleGoogleCredential(response),
      error_callback: () => this.ngZone.run(() => {
        this.googleError = this.translate.instant('AUTH.GOOGLE_UNAVAILABLE');
      }),
    });
    google.accounts.id.renderButton(
      document.getElementById('google-signin-button'),
      { theme: 'outline', size: 'large', width: '100%', text: 'signin_with' }
    );
    this.googleReady = true;
  }

  handleGoogleCredential(response: any): void {
    this.ngZone.run(() => {
      // Ignore duplicate callbacks (double-tap / retried GIS responses on
      // slow mobile connections) to avoid parallel sign-up requests.
      if (this.isLoading) return;
      if (!response?.credential) {
        this.googleError = this.translate.instant('AUTH.GOOGLE_FAILED');
        return;
      }
      // Consent is captured implicitly by the notice rendered above the Google
      // button; the backend still records the consent version + IP on first sign-up.
      this.isLoading = true;
      this.authService.loginWithGoogle(response.credential, true).subscribe({
        next: () => {
          this.isLoading = false;
        },
        error: (err) => {
          this.isLoading = false;
          const backendMsg = err?.error?.message;
          const msg = backendMsg
            ? `${this.translate.instant('AUTH.GOOGLE_FAILED')} (${backendMsg})`
            : this.translate.instant('AUTH.GOOGLE_FAILED');
          this.snackBar.open(msg, 'OK', { duration: 5000 });
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
