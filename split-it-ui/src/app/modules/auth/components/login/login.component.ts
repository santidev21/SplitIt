import { Component } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-login',
  imports: [MATERIAL_IMPORTS, RouterModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {

}
