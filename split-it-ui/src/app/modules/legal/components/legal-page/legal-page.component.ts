import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { Subscription } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-legal-page',
  imports: [RouterModule, MatCardModule, TranslatePipe],
  templateUrl: './legal-page.component.html',
  styleUrls: ['./legal-page.component.scss'],
})
export class LegalPageComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private translate = inject(TranslateService);
  private location = inject(Location);

  content: string | null = null;
  titleKey = 'LEGAL.PRIVACY_TITLE';
  loadError = false;

  private doc = 'privacy';
  private langSub?: Subscription;

  goBack(): void {
    this.location.back();
  }

  ngOnInit(): void {
    this.doc = this.route.snapshot.data['doc'] === 'terms' ? 'terms' : 'privacy';
    this.titleKey = this.doc === 'terms' ? 'LEGAL.TERMS_TITLE' : 'LEGAL.PRIVACY_TITLE';
    this.load();
    this.langSub = this.translate.onLangChange.subscribe(() => this.load());
  }

  ngOnDestroy(): void {
    this.langSub?.unsubscribe();
  }

  private load(): void {
    const lang = this.translate.getCurrentLang() || 'en';
    // Static, trusted, app-owned content; Angular still sanitizes the binding.
    this.http.get(`assets/legal/${this.doc}.${lang}.html`, { responseType: 'text' }).subscribe({
      next: (html) => {
        this.content = html;
        this.loadError = false;
      },
      error: () => {
        this.content = null;
        this.loadError = true;
      },
    });
  }
}
