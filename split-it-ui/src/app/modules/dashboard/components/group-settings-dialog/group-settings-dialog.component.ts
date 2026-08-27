import { Component, Inject, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { GroupService } from '../../services/group.service';
import { FriendService, Friend } from '../../services/friend.service';
import { GroupMember } from '../../../../models/group.model';
import { NotificationService } from '../../../../shared/services/notification.service';

export interface GroupSettingsDialogData {
  groupId: number;
  isCreator: boolean;
  isAdminOrCreator: boolean;
}

@Component({
  selector: 'app-group-settings-dialog',
  imports: [MATERIAL_IMPORTS, MatDialogModule, FormsModule],
  templateUrl: './group-settings-dialog.component.html',
  styleUrls: ['./group-settings-dialog.component.scss']
})
export class GroupSettingsDialogComponent implements OnInit {
  groupForm: FormGroup;
  members: GroupMember[] = [];
  friends: Friend[] = [];
  selectedTabIndex = 0;
  currentUserId: number;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: GroupSettingsDialogData,
    private fb: FormBuilder,
    private groupService: GroupService,
    private friendService: FriendService,
    private dialogRef: MatDialogRef<GroupSettingsDialogComponent>,
    private notifications: NotificationService
  ) {
    this.currentUserId = Number(localStorage.getItem('userId'));
    this.groupForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(500)]],
      allowToDeleteExpenses: [false]
    });
  }

  ngOnInit(): void {
    this.loadGroupDetails();
    this.loadMembers();
    this.loadFriends();
  }

  loadGroupDetails(): void {
    this.groupService.getGroupDetails(this.data.groupId).subscribe({
      next: (details) => {
        this.groupForm.patchValue({
          name: details.name,
          description: details.description,
          allowToDeleteExpenses: details.allowToDeleteExpenses ?? false
        });
      },
      error: () => {}
    });
  }

  loadMembers(): void {
    this.groupService.getGroupMembers(this.data.groupId).subscribe({
      next: (members) => { this.members = members; },
      error: () => {}
    });
  }

  loadFriends(): void {
    this.friendService.getFriends().subscribe({
      next: (friends) => { this.friends = friends; },
      error: () => {}
    });
  }

  get memberIds(): number[] {
    return this.members.map(m => m.id);
  }

  inviteableFriends(): Friend[] {
    return this.friends.filter(f => !this.memberIds.includes(f.id));
  }

  canManageMember(member: GroupMember): boolean {
    if (member.id === this.currentUserId) return false;
    if (member.role === 'creator') return false;
    if (member.role === 'admin') return this.data.isCreator;
    return this.data.isAdminOrCreator;
  }

  save(): void {
    if (this.groupForm.invalid) {
      this.groupForm.markAllAsTouched();
      return;
    }
    this.groupService.updateGroup(this.data.groupId, this.groupForm.value).subscribe({
      next: () => {
        this.notifications.success('Group updated.');
        this.dialogRef.close('saved');
      },
      error: (err: any) => {
        const msg = err.error?.message || 'Failed to update group.';
        this.notifications.toast(msg, 'error');
      }
    });
  }

  invite(friend: Friend): void {
    this.groupService.inviteMember(this.data.groupId, friend.id).subscribe({
      next: () => {
        this.notifications.toast(`${friend.name} added to the group.`, 'success');
        this.loadMembers();
      },
      error: () => {}
    });
  }

  promote(member: GroupMember): void {
    this.groupService.updateMemberRole(this.data.groupId, member.id, 'admin').subscribe({
      next: () => {
        this.notifications.toast(`${member.name} promoted to admin.`, 'success');
        this.loadMembers();
      },
      error: () => {}
    });
  }

  demote(member: GroupMember): void {
    this.groupService.updateMemberRole(this.data.groupId, member.id, 'member').subscribe({
      next: () => {
        this.notifications.toast(`${member.name} is now a member.`, 'success');
        this.loadMembers();
      },
      error: () => {}
    });
  }

  remove(member: GroupMember): void {
    this.notifications.confirm(
      'Remove member',
      `Remove ${member.name} from the group?`,
      'Yes, remove'
    ).then(result => {
      if (result.isConfirmed) {
        this.groupService.removeMember(this.data.groupId, member.id).subscribe({
          next: () => {
            this.notifications.toast('Member removed.', 'success');
            this.loadMembers();
          },
          error: () => {}
        });
      }
    });
  }

  close(): void {
    this.dialogRef.close();
  }
}
