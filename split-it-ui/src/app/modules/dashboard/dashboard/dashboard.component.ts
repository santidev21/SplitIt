import { Component } from '@angular/core';
import { HeaderBarComponent } from '../components/header-bar/header-bar.component';
import { GroupCardComponent } from '../components/group-card/group-card.component';
import { RouterModule } from '@angular/router';
import { MATERIAL_IMPORTS } from '../../../../shared/material.imports';
import { MatDialog } from '@angular/material/dialog';
import { CreateGroupComponent } from '../components/create-group/create-group.component';

@Component({
  selector: 'app-dashboard',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, GroupCardComponent, RouterModule, ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent {
  userName: string | null = localStorage.getItem('userName');
  userHasGroups: boolean = false;

  constructor(
    private dialog: MatDialog
  ) {}

  openCreateGroupDialog() {
    this.dialog.open(CreateGroupComponent, {
      width: '600px'
    });
  }
  
}
