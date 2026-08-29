import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly STORAGE_KEY = 'lang';
  private readonly SUPPORTED_LANGS = ['en', 'es'];
  private readonly DEFAULT_LANG = 'en';

  constructor(private translate: TranslateService) {
    this.translate.addLangs(this.SUPPORTED_LANGS);
    this.translate.setFallbackLang(this.DEFAULT_LANG);

    const saved = this.getSavedLang();
    this.translate.use(saved);
    this.setDocumentLang(saved);
  }

  get currentLang(): string {
    return this.translate.getCurrentLang() || this.DEFAULT_LANG;
  }

  toggleLanguage(): void {
    const next = this.currentLang === 'en' ? 'es' : 'en';
    this.setLang(next);
  }

  setLang(lang: string): void {
    if (!this.SUPPORTED_LANGS.includes(lang)) return;
    this.translate.use(lang);
    localStorage.setItem(this.STORAGE_KEY, lang);
    this.setDocumentLang(lang);
  }

  private getSavedLang(): string {
    if (typeof localStorage === 'undefined') return this.DEFAULT_LANG;
    const saved = localStorage.getItem(this.STORAGE_KEY);
    if (saved && this.SUPPORTED_LANGS.includes(saved)) return saved;
    return this.DEFAULT_LANG;
  }

  private setDocumentLang(lang: string): void {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = lang;
    }
  }
}
