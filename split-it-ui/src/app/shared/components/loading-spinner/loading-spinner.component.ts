import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [MatProgressSpinnerModule],
  templateUrl: './loading-spinner.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./loading-spinner.component.scss'],
})
export class LoadingSpinnerComponent {
  @Input() isLoading = false;
}
