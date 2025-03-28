import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-register',
  imports: [MATERIAL_IMPORTS, RouterModule],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {

}
