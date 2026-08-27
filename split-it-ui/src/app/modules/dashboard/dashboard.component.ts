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

@Component({
  selector: 'app-dashboard',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, GroupCardComponent, RouterModule, ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit{
  userName: string | null = localStorage.getItem('userName');
  userHasGroups: boolean = false;
  userGroups: UserGroup[] = [];

  constructor(
    private dialog: MatDialog,
    private groupService: GroupService,
    private friendService: FriendService,
    private notifications: NotificationService
  ) {}

  ngOnInit(): void {
    let userId : string | null = localStorage.getItem('userId');
    if (userId){
      this.groupService.getUserGroups(parseInt(userId)).subscribe((resp : UserGroup[]) =>{
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
            ? `${names} sent you a friend request.`
            : `${resp.incoming.length} pending friend requests from: ${names}`;
          setTimeout(() => {
            this.notifications.info('Friend Requests', msg);
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
