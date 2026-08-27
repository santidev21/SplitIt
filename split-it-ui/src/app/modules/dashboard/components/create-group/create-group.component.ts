import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, FormsModule, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { CurrencyService } from '../../services/currency.service';
import { FriendService, Friend, SearchUser } from '../../services/friend.service';
import { Currency } from '../../../../models/currency.model';
import { GroupService } from '../../services/group.service';
import { NotificationService } from '../../../../shared/services/notification.service';

@Component({
  selector: 'app-create-group',
  imports: [MATERIAL_IMPORTS, RouterModule, FormsModule],
  templateUrl: './create-group.component.html',
  styleUrls: ['./create-group.component.scss']
})
export class CreateGroupComponent implements OnInit{
  createGroupForm: FormGroup;
  friends: Friend[] = [];
  selectedFriendIds: Set<number> = new Set<number>();
  currencies: Currency[] = [];
  isSaving = false;

  searchTerm = '';
  searchResults: SearchUser[] = [];
  isSearching = false;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<CreateGroupComponent>,
    private currencyService: CurrencyService,
    private friendService: FriendService,
    private groupService: GroupService,
    private router: Router,
    private notifications: NotificationService
  ) {
    this.createGroupForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(500)]],
      currencyId: [null, Validators.required],
      allowToDeleteExpenses: [false]
    });
  }

  ngOnInit(): void {
    this.currencyService.getCurrencies().subscribe({
      next: (currencies) => { this.currencies = currencies; },
      error: () => this.notifications.toast('Could not load currencies.', 'error')
    });
    this.loadFriends();
  }

  loadFriends(): void {
    this.friendService.getFriends().subscribe({
      next: (friends) => { this.friends = friends; },
      error: () => this.notifications.toast('Could not load your friends.', 'error')
    });
  }

  toggleFriend(friendId: number): void {
    if (this.selectedFriendIds.has(friendId)) {
      this.selectedFriendIds.delete(friendId);
    } else {
      this.selectedFriendIds.add(friendId);
    }
  }

  searchUsers(): void {
    const term = this.searchTerm.trim();
    if (term.length < 2) return;
    this.isSearching = true;
    this.friendService.search(term).subscribe({
      next: (results) => {
        this.searchResults = results;
        this.isSearching = false;
      },
      error: () => { this.isSearching = false; }
    });
  }

  sendFriendRequest(user: SearchUser): void {
    this.friendService.sendRequest({ userId: user.id }).subscribe({
      next: () => {
        this.notifications.toast(`Friend request sent to ${user.name}. They need to accept it before you can add them.`, 'success');
        this.searchResults = this.searchResults.filter(r => r.id !== user.id);
      },
      error: () => {}
    });
  }

  onSubmit(): void {
    this.createGroupForm.markAllAsTouched();
    if (this.createGroupForm.invalid) {
      return;
    }
    this.isSaving = true;

    const groupData = {
      ...this.createGroupForm.value,
      members: Array.from(this.selectedFriendIds)
    };

    this.groupService.createGroup(groupData).subscribe({
      next: (resp) => {
        this.isSaving = false;
        this.dialogRef.close();
        this.notifications.success('Group created successfully.');
        this.router.navigate(['/dashboard/group', resp.groupId]);
      },
      error: () => { this.isSaving = false; }
    });
  }

  close() {
    this.dialogRef.close();
  }
}
