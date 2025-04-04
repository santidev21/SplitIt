import { Component, Input } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { UserGroup } from '../../../../models/user.model';

@Component({
  selector: 'app-group-card',
  imports: [MATERIAL_IMPORTS],
  templateUrl: './group-card.component.html',
  styleUrls: ['./group-card.component.scss']
})
export class GroupCardComponent {
  @Input() group!: UserGroup;
}
 