import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';

@Component({
  selector: 'app-split-method-dialog',
  imports: [MATERIAL_IMPORTS, FormsModule, MatDialogModule],
  templateUrl: './split-method-dialog.component.html',
  styleUrls: ['./split-method-dialog.component.scss']
})
export class SplitMethodDialogComponent {
  selectedTabIndex = 0;

  members: any[] = [];
  equalSplitSelection: { [key: string]: boolean } = {};
  amountSplit: { [key: string]: number } = {};
  percentageSplit: { [key: string]: number } = {};

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<SplitMethodDialogComponent>
  ) {
    this.members = data.members || [];
    this.members.forEach((m) => {
      this.equalSplitSelection[m.id] = true;
      this.amountSplit[m.id] = 0;
      this.percentageSplit[m.id] = 0;
    });
  }

  // Confirm and return the selected method + data
  confirmSplit(): void {
    let result;

    if (this.selectedTabIndex === 0) {
      result = {
        method: 'equal',
        members: Object.keys(this.equalSplitSelection).filter(
          (id) => this.equalSplitSelection[id]
        ),
      };
    } else if (this.selectedTabIndex === 1) {
      result = {
        method: 'amount',
        members: this.amountSplit,
      };
    } else {
      result = {
        method: 'percentage',
        members: this.percentageSplit,
      };
    }

    this.dialogRef.close(result);
  }
}
