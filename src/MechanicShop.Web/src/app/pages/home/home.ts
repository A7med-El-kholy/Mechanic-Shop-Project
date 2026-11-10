import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthStore } from '../../core/auth-store';

/**
 * Landing page — the public face of Mechanic Shop.
 * Creative hero with real car photography, AI assistant centerpiece,
 * service cards with images, testimonials, and booking CTAs.
 */
@Component({
  selector: 'app-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class HomePage {
  readonly auth = inject(AuthStore);

  readonly services = [
    {
      icon: '🔧',
      title: 'Engine Diagnostics',
      desc: 'Computerized scanning and full mechanical inspection to find the root cause fast.',
      price: '$89',
      priceUnit: '/hr',
      img: 'https://images.unsplash.com/photo-1583121274602-3e2820c69888?w=600&q=80',
      tag: 'Most Popular',
    },
    {
      icon: '🛞',
      title: 'Brakes & Tyres',
      desc: 'Brake pad replacement, rotor resurfacing, tyre fitting, balancing and alignment.',
      price: '$65',
      priceUnit: '/hr',
      img: 'https://images.unsplash.com/photo-1590646590354-62c92e9e18e7?w=600&q=80',
      tag: 'Express',
    },
    {
      icon: '⚡',
      title: 'Electrical & Battery',
      desc: 'Alternator, starter, wiring faults and battery testing with factory-grade tools.',
      price: '$75',
      priceUnit: '/hr',
      img: 'https://images.unsplash.com/photo-1618176772777-240831281130?w=600&q=80',
      tag: 'Same Day',
    },
    {
      icon: '🔍',
      title: 'Pre-Purchase Inspection',
      desc: '150-point safety and mechanical check before you buy. Same-day report with photos.',
      price: '$149',
      priceUnit: 'flat',
      img: 'https://images.unsplash.com/photo-1503376780353-7e6692767b70?w=600&q=80',
      tag: 'Recommended',
    },
  ];

  readonly stats = [
    { num: '15k+', lab: 'vehicles serviced' },
    { num: '12', lab: 'years in business' },
    { num: '5★', lab: 'average rating' },
    { num: '48h', lab: 'avg. turnaround' },
  ];

  readonly testimonials = [
    {
      quote: 'Exceptional service from start to finish. They diagnosed my brake issue in minutes and had me back on the road the same day. Honest pricing, no surprises.',
      name: 'Marcus T.',
      role: 'Ford Focus owner',
      avatar: 'https://images.unsplash.com/photo-1507003211169-0a1272a8e396?w=150&q=80',
    },
    {
      quote: 'Best workshop in town. The team knew exactly what was wrong with my BMW and the price was exactly as quoted. Will be back for my next service.',
      name: 'Sarah L.',
      role: 'BMW 3-Series owner',
      avatar: 'https://images.unsplash.com/photo-1438761681033-6461ffad8d80?w=150&q=80',
    },
    {
      quote: 'Pre-purchase inspection saved me from a lemon. Detailed report with photos, delivered same morning. Highly recommend.',
      name: 'James R.',
      role: 'First-time buyer',
      avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150&q=80',
    },
  ];

  readonly trustLogos = [
    { alt: 'ASE Certified', src: 'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=120&q=80' },
    { alt: 'IATN Member', src: 'https://images.unsplash.com/photo-1544367567-0f23b003804b?w=120&q=80' },
    { alt: 'AAA Approved', src: 'https://images.unsplash.com/photo-1556740758-97e0d1d3c41a?w=120&q=80' },
    { alt: 'Bosch Service', src: 'https://images.unsplash.com/photo-1522202176988-66273c2fd55f?w=120&q=80' },
  ];
}
