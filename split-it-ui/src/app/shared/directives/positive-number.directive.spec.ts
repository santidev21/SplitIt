import { PositiveNumberDirective } from './positive-number.directive';

describe('PositiveNumberDirective', () => {
  let directive: PositiveNumberDirective;

  beforeEach(() => {
    directive = new PositiveNumberDirective();
  });

  it('should create', () => {
    expect(directive).toBeTruthy();
  });

  it('should allow digit keys', () => {
    const event = new KeyboardEvent('keydown', { key: '5' });
    spyOn(event, 'preventDefault');
    directive.onKeyDown(event);
    expect(event.preventDefault).not.toHaveBeenCalled();
  });

  it('should allow control keys', () => {
    const event = new KeyboardEvent('keydown', { key: 'Backspace' });
    spyOn(event, 'preventDefault');
    directive.onKeyDown(event);
    expect(event.preventDefault).not.toHaveBeenCalled();
  });

  it('should block non-digit keys', () => {
    const event = new KeyboardEvent('keydown', { key: 'a' });
    spyOn(event, 'preventDefault');
    directive.onKeyDown(event);
    expect(event.preventDefault).toHaveBeenCalled();
  });

  it('should allow valid numeric paste', () => {
    const dt = new DataTransfer();
    dt.setData('text', '123');
    const event = new ClipboardEvent('paste', { clipboardData: dt });
    spyOn(event, 'preventDefault');
    directive.onPaste(event);
    expect(event.preventDefault).not.toHaveBeenCalled();
  });

  it('should block non-numeric paste', () => {
    const dt = new DataTransfer();
    dt.setData('text', 'abc');
    const event = new ClipboardEvent('paste', { clipboardData: dt });
    spyOn(event, 'preventDefault');
    directive.onPaste(event);
    expect(event.preventDefault).toHaveBeenCalled();
  });
});
