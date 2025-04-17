import { Directive, HostListener } from '@angular/core';

@Directive({
  selector: '[appPercentage]'
})
export class PercentageDirective {
  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent) {
    const allowedKeys = [
      'Backspace', 'Tab', 'ArrowLeft', 'ArrowRight', 'Delete'
    ];

    if (
      allowedKeys.includes(event.key) ||
      /^[0-9]$/.test(event.key)
    ) {
      return;
    }

    event.preventDefault();
  }

  @HostListener('input', ['$event'])
  onInput(event: Event) {
    const input = event.target as HTMLInputElement;
    const value = parseInt(input.value, 10);
    if (value > 100) {
      input.value = '100';
    } else if (value < 0 || isNaN(value)) {
      input.value = '0';
    }
  }

  @HostListener('paste', ['$event'])
  onPaste(event: ClipboardEvent) {
    const pasteData = event.clipboardData?.getData('text') ?? '';
    const numeric = parseInt(pasteData, 10);
    if (isNaN(numeric) || numeric < 0 || numeric > 100) {
      event.preventDefault();
    }
  }
}
