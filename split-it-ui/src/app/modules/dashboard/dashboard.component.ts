import { Component, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { HeaderBarComponent } from './components/header-bar/header-bar.component';
import { GroupCardComponent } from './components/group-card/group-card.component';
import { MATERIAL_IMPORTS } from '../../../shared/material.imports';
import { CreateGroupComponent } from './components/create-group/create-group.component';
import { GroupService } from './services/group.service';
import { FriendService } from './services/friend.service';
import { NotificationService } from '../../shared/services/notification.service';
import { UserGroup } from '../../models/user.model';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../auth/services/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, GroupCardComponent, RouterModule, TranslatePipe],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit{
  userName: string | null = null;
  userHasGroups: boolean = false;
  userGroups: UserGroup[] = [];

  constructor(
    private dialog: MatDialog,
    private groupService: GroupService,
    private friendService: FriendService,
    private notifications: NotificationService,
    private translate: TranslateService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.userName = this.authService.getUserName();
    const userId = this.authService.getCurrentUserId();
    if (userId) {
      this.groupService.getUserGroups(userId).subscribe((resp : UserGroup[]) => {
        if (resp && resp.length)
        {
          this.userHasGroups = true;
          this.userGroups = resp;
        }
      })
      this.checkPendingFriendRequests();
    }
  }

  checkPendingFriendRequests(): void {
    this.friendService.getRequests().subscribe({
      next: (resp) => {
        if (resp.incoming && resp.incoming.length > 0) {
          const names = resp.incoming.map((r: any) => r.name).join(', ');
          const msg = resp.incoming.length === 1
            ? this.translate.instant('DASHBOARD.FRIEND_REQUEST_SINGLE', { name: names })
            : this.translate.instant('DASHBOARD.FRIEND_REQUEST_PLURAL', { count: resp.incoming.length, names });
          setTimeout(() => {
            this.notifications.info(this.translate.instant('DASHBOARD.FRIEND_REQUESTS'), msg);
          }, 1500);
        }
      }
    });
  }

  openCreateGroupDialog() {
    this.dialog.open(CreateGroupComponent, {
      width: '600px'
    });
  }

}
