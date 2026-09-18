import { of, throwError } from 'rxjs';
import { LegalPageComponent } from './legal-page.component';

describe('LegalPageComponent', () => {
  function build(doc: string, html = '<h1>Doc</h1>') {
    const route = { snapshot: { data: { doc } } } as any;
    const http = { get: jasmine.createSpy('get').and.returnValue(of(html)) } as any;
    const translate = { getCurrentLang: () => 'en', onLangChange: of({}) } as any;
    const location = { back: jasmine.createSpy('back') } as any;
    const component = new LegalPageComponent(route, http, translate, location);
    return { component, http, location };
  }

  it('loads the privacy document by default', () => {
    const { component, http } = build('privacy', '<h1>Privacy</h1>');
    component.ngOnInit();
    expect(component.titleKey).toBe('LEGAL.PRIVACY_TITLE');
    expect(http.get).toHaveBeenCalledWith('assets/legal/privacy.en.html', { responseType: 'text' });
    expect(component.content).toContain('Privacy');
    expect(component.loadError).toBeFalse();
  });

  it('loads the terms document when routed with doc=terms', () => {
    const { component, http } = build('terms', '<h1>Terms</h1>');
    component.ngOnInit();
    expect(component.titleKey).toBe('LEGAL.TERMS_TITLE');
    expect(http.get).toHaveBeenCalledWith('assets/legal/terms.en.html', { responseType: 'text' });
    expect(component.content).toContain('Terms');
  });

  it('sets an error flag when loading fails', () => {
    const route = { snapshot: { data: { doc: 'privacy' } } } as any;
    const http = { get: () => throwError(() => new Error('network')) } as any;
    const translate = { getCurrentLang: () => 'en', onLangChange: of({}) } as any;
    const location = { back: () => {} } as any;
    const component = new LegalPageComponent(route, http, translate, location);

    component.ngOnInit();
    expect(component.loadError).toBeTrue();
    expect(component.content).toBeNull();
  });

  it('goBack calls location.back', () => {
    const { component, location } = build('privacy');
    component.goBack();
    expect(location.back).toHaveBeenCalled();
  });

  it('unsubscribes on destroy', () => {
    const { component } = build('privacy');
    component.ngOnInit();
    expect(() => component.ngOnDestroy()).not.toThrow();
  });
});
