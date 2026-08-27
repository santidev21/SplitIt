import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { HeaderBarComponent } from '../header-bar/header-bar.component';
import { FormsModule } from '@angular/forms';
import { FriendService, Friend, FriendRequest, FriendRequestsResponse, SearchUser } from '../../services/friend.service';
import { NotificationService } from '../../../../shared/services/notification.service';

@Component({
  selector: 'app-friends-page',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, FormsModule],
  templateUrl: './friends-page.component.html',
  styleUrls: ['./friends-page.component.scss']
})
export class FriendsPageComponent implements OnInit {
  selectedTab = 0;
  isLoading = true;

  friends: Friend[] = [];
  incoming: FriendRequest[] = [];
  sent: FriendRequest[] = [];

  searchTerm = '';
  searchResults: SearchUser[] = [];
  isSearching = false;
  addByEmail = '';
  pendingSearchUserId: number | null = null;

  constructor(
    private friendService: FriendService,
    private notifications: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading = true;
    this.friendService.getFriends().subscribe({
      next: (friends) => {
        this.friends = friends;
        this.loadRequests(false);
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  loadRequests(trackLoading = true): void {
    if (trackLoading) this.isLoading = true;
    this.friendService.getRequests().subscribe({
      next: (resp: FriendRequestsResponse) => {
        this.incoming = resp.incoming;
        this.sent = resp.sent;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  search(): void {
    const term = this.searchTerm.trim();
    if (term.length < 2) {
      this.notifications.toast('Type at least 2 characters to search.', 'warning');
      return;
    }
    this.isSearching = true;
    this.friendService.search(term).subscribe({
      next: (results) => {
        this.searchResults = results;
        this.isSearching = false;
        if (results.length === 0) {
          this.notifications.toast('No users found.', 'info');
        }
      },
      error: () => {
        this.isSearching = false;
      }
    });
  }

  addUserToFriends(user: SearchUser): void {
    this.pendingSearchUserId = user.id;
    this.friendService.sendRequest({ userId: user.id }).subscribe({
      next: () => {
        this.pendingSearchUserId = null;
        this.notifications.toast(`Friend request sent to ${user.name}.`, 'success');
        this.searchResults = this.searchResults.filter(r => r.id !== user.id);
        this.loadRequests(false);
      },
      error: () => {
        this.pendingSearchUserId = null;
      }
    });
  }

  sendByEmail(): void {
    const email = this.addByEmail.trim();
    if (!email) {
      this.notifications.toast('Enter an email address.', 'warning');
      return;
    }
    this.friendService.sendRequest({ email }).subscribe({
      next: () => {
        this.addByEmail = '';
        this.notifications.toast('Friend request sent.', 'success');
        this.loadRequests(false);
      },
      error: () => {}
    });
  }

  acceptRequest(request: FriendRequest): void {
    this.friendService.respond(request.friendshipId, true).subscribe({
      next: () => {
        this.notifications.toast(`${request.name} is now your friend.`, 'success');
        this.loadData();
      },
      error: () => {}
    });
  }

  rejectRequest(request: FriendRequest): void {
    this.friendService.respond(request.friendshipId, false).subscribe({
      next: () => {
        this.incoming = this.incoming.filter(r => r.friendshipId !== request.friendshipId);
        this.notifications.toast('Request rejected.', 'info');
      },
      error: () => {}
    });
  }

  removeFriend(friend: Friend): void {
    this.notifications.confirm(
      'Remove friend',
      `Remove ${friend.name} from your friends? You will not be removed from shared groups.`,
      'Yes, remove'
    ).then(result => {
      if (result.isConfirmed) {
        this.friendService.removeFriend(friend.id).subscribe({
          next: () => {
            this.friends = this.friends.filter(f => f.id !== friend.id);
            this.notifications.toast('Friend removed.', 'success');
          },
          error: () => {}
        });
      }
    });
  }
}
