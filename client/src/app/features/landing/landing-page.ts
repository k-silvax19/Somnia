import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideCompass, LucideStar, LucideUserRound } from '@lucide/angular';
import { Brand } from '../../shared/brand';

@Component({
  selector: 'app-landing-page',
  imports: [Brand, RouterLink, LucideCompass, LucideStar, LucideUserRound],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.scss',
})
export class LandingPage {}
