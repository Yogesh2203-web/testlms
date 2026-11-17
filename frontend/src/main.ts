
import 'zone.js';

import { bootstrapApplication } from '@angular/platform-browser';
import {
  provideRouter,
  Routes,
  withHashLocation,
  RouterOutlet
} from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import {
  DomSanitizer,
  SafeResourceUrl
} from '@angular/platform-browser';

@Component({
  selector: 'app-page-host',
  standalone: true,
  template: `
    <main class="frame-shell">
      <iframe
        title="Institute LMS original interface"
        [src]="pageUrl"
        style="width:100%; height:100vh; border:0; display:block;">
      </iframe>
    </main>
  `
})
class PageHostComponent {

  pageUrl!: SafeResourceUrl;

  private readonly pages: Record<string, string> = {
    '/': '/original/index.html',
    '/login': '/original/pages/login.html',
    '/change-password': '/original/pages/change-password.html',
    '/courses': '/original/pages/courses.html',
    '/about': '/original/pages/about.html',
    '/career': '/original/pages/career.html',
    '/contact': '/original/pages/contact.html',
    '/social-media': '/original/pages/social-media.html',

    '/admission/enquiry': '/original/admission/enquiry.html',
    '/admission/student-admission': '/original/admission/student-admission.html',
    '/admission/fee-receipt': '/original/admission/fee-receipt.html',
    '/admission/admission-receipt': '/original/admission/admission-receipt.html',
    '/admission/excel-import': '/original/admission/excel-import.html',

    '/dashboard/business-head': '/original/dashboard/business-head.html',
    '/dashboard/manager': '/original/dashboard/manager.html',
    '/dashboard/trainer': '/original/dashboard/trainer.html',
    '/dashboard/student': '/original/dashboard/student.html',

    '/dashboard/branches': '/original/dashboard/branches.html',
    '/dashboard/users': '/original/dashboard/users.html',
    '/dashboard/students': '/original/dashboard/students.html',
    '/dashboard/trainers': '/original/dashboard/trainers.html',
    '/dashboard/courses': '/original/dashboard/courses.html',
    '/dashboard/attendance': '/original/dashboard/attendance.html',
    '/dashboard/fees': '/original/dashboard/fees.html',
    '/dashboard/reports': '/original/dashboard/reports.html',
    '/dashboard/settings': '/original/dashboard/settings.html',

    '/dashboard/student-courses': '/original/dashboard/student-courses.html',
    '/dashboard/student-attendance': '/original/dashboard/student-attendance.html',
    '/dashboard/student-assignments': '/original/dashboard/student-assignments.html',
    '/dashboard/student-exams': '/original/dashboard/student-exams.html',
    '/dashboard/student-fees': '/original/dashboard/student-fees.html',
    '/dashboard/student-learning': '/original/dashboard/student-learning.html',
    '/dashboard/student-profile': '/original/dashboard/student-profile.html',
    '/dashboard/student-results': '/original/dashboard/student-results.html',

    '/dashboard/trainer-assignments': '/original/dashboard/trainer-assignments.html',
    '/dashboard/trainer-attendance': '/original/dashboard/trainer-attendance.html',
    '/dashboard/trainer-exams': '/original/dashboard/trainer-exams.html',
    '/dashboard/trainer-settings': '/original/dashboard/trainer-settings.html',
    '/dashboard/trainer-students': '/original/dashboard/trainer-students.html'
  };

  constructor(private sanitizer: DomSanitizer) {
    const hashPath = window.location.hash
      .replace(/^#/, '')
      .split('?')[0];

    const path = hashPath || '/';

    const target = this.pages[path] || '/original/index.html';

    this.pageUrl =
      this.sanitizer.bypassSecurityTrustResourceUrl(target);
  }
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet></router-outlet>`
})
class AppComponent {}

const routes: Routes = [
  {
    path: '**',
    component: PageHostComponent
  }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes, withHashLocation()),
    provideHttpClient()
  ]
}).catch(console.error);
