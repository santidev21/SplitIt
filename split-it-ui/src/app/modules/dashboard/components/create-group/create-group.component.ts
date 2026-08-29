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
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-create-group',
  imports: [MATERIAL_IMPORTS, RouterModule, FormsModule, TranslatePipe],
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
    private notifications: NotificationService,
    private translate: TranslateService
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
      error: () => this.notifications.toast(this.translate.instant('NOTIFICATIONS.COULD_NOT_LOAD_CURRENCIES'), 'error')
    });
    this.loadFriends();
  }

  loadFriends(): void {
    this.friendService.getFriends().subscribe({
      next: (friends) => { this.friends = friends; },
      error: () => this.notifications.toast(this.translate.instant('NOTIFICATIONS.COULD_NOT_LOAD_FRIENDS'), 'error')
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
        this.notifications.toast(this.translate.instant('NOTIFICATIONS.FRIEND_REQUEST_SENT_TO', { name: user.name }), 'success');
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
        this.notifications.success(this.translate.instant('NOTIFICATIONS.GROUP_CREATED'));
        this.router.navigate(['/dashboard/group', resp.groupId]);
      },
      error: () => { this.isSaving = false; }
    });
  }

  close() {
    this.dialogRef.close();
  }
}
