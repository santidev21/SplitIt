import { Directive, HostListener } from '@angular/core';

@Directive({
  selector: '[appPositiveNumber]'
})
export class PositiveNumberDirective {
  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent) {
    const allowedKeys = [
      'Backspace', 'Tab', 'ArrowLeft', 'ArrowRight', 'Delete'
    ];

    // Allow digits and allowed control keys
    if (
      allowedKeys.includes(event.key) ||
      /^[0-9]$/.test(event.key)
    ) {
      return;
    }

    // Block everything else
    event.preventDefault();
  }
  

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent) {
    const pasteData = event.clipboardData?.getData('text') ?? '';
    if (!/^\d+$/.test(pasteData)) {
      event.preventDefault();
    }
  }
}
