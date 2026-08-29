import { Injectable } from '@angular/core';
import Swal, { SweetAlertOptions, SweetAlertResult } from 'sweetalert2';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  success(title: string, text?: string): Promise<SweetAlertResult> {
    return Swal.fire({
      icon: 'success',
      title,
      text,
      timer: 2200,
      showConfirmButton: false,
      timerProgressBar: true
    } as SweetAlertOptions);
  }

  error(title: string, text?: string): Promise<SweetAlertResult> {
    return Swal.fire({
      icon: 'error',
      title,
      text,
      confirmButtonText: 'OK',
      confirmButtonColor: '#005cbb'
    } as SweetAlertOptions);
  }

  info(title: string, text?: string): Promise<SweetAlertResult> {
    return Swal.fire({
      icon: 'info',
      title,
      text,
      confirmButtonText: 'OK',
      confirmButtonColor: '#005cbb'
    } as SweetAlertOptions);
  }

  confirm(title: string, text?: string, confirmText = 'Yes, continue'): Promise<SweetAlertResult> {
    return Swal.fire({
      icon: 'warning',
      title,
      text,
      showCancelButton: true,
      confirmButtonText: confirmText,
      confirmButtonColor: '#d33',
      cancelButtonText: 'Cancel',
      cancelButtonColor: '#005cbb'
    } as SweetAlertOptions);
  }

  toast(message: string, icon: 'success' | 'error' | 'info' | 'warning' = 'success'): void {
    Swal.fire({
      toast: true,
      position: 'center',
      icon,
      title: message,
      showConfirmButton: true,
      confirmButtonText: 'OK',
      confirmButtonColor: '#005cbb',
      timer: undefined,
      timerProgressBar: false,
      customClass: {
        popup: 'centered-toast'
      }
    } as SweetAlertOptions);
  }
}
