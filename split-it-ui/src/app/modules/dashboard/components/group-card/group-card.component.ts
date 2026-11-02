import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { UserGroup } from '../../../../models/user.model';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-group-card',
  imports: [MATERIAL_IMPORTS, RouterModule, TranslatePipe],
  templateUrl: './group-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./group-card.component.scss'],
})
export class GroupCardComponent {
  @Input() group!: UserGroup;
}
